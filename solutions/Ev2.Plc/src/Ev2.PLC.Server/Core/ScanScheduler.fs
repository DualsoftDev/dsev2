namespace DSPLCServer.Core

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open DSPLCServer.PLC


/// Scan scheduler service - Manages periodic scanning of PLCs using the universal driver interface
type ScanScheduler(
    serviceProvider: IServiceProvider, 
    managerFactory: PlcManagerFactory,
    logger: ILogger<ScanScheduler>) =
    
    let activeManagers = ConcurrentDictionary<string, ServerPlcManager>()
    let scanJobs = ConcurrentDictionary<string, ScanJob>()
    let jobTimers = ConcurrentDictionary<string, Timer>()
    let executionSemaphore = new SemaphoreSlim(10, 10) // Max 10 concurrent scans
    let mutable isRunning = false
    let mutable cancellationTokenSource = new CancellationTokenSource()
    
    // Events
    let jobCompletedEvent = Event<ScanJobResult>()
    let jobFailedEvent = Event<ScanJobResult>()
    let managerConnectedEvent = Event<string>()
    let managerDisconnectedEvent = Event<string * string>()
    
    /// Add PLC manager to scheduler
    member this.AddManager(config: PlcServerConfig) =
        task {
            try
                logger.LogInformation("Adding PLC manager {PlcId} ({Vendor})", config.PlcId, config.Vendor)
                
                match managerFactory.CreateManager(config) with
                | Ok manager ->
                    // Subscribe to manager events
                    manager.ConnectionStateChanged.Add(fun (plcId, status) ->
                        if status.IsOperational then
                            managerConnectedEvent.Trigger(plcId)
                        else
                            managerDisconnectedEvent.Trigger(plcId, status.ToString()))
                    
                    manager.ScanCompleted.Add(fun (plcId, stats) ->
                        logger.LogDebug("Scan completed for PLC {PlcId}: {SuccessfulTags}/{TotalTags}", 
                            plcId, stats.SuccessfulTags, stats.TotalTags))
                    
                    manager.ErrorOccurred.Add(fun (plcId, error) ->
                        logger.LogError("Error in PLC {PlcId}: {Error}", plcId, error))
                    
                    if activeManagers.TryAdd(config.PlcId, manager) then
                        logger.LogInformation("Successfully added PLC manager {PlcId}", config.PlcId)
                        
                        // Create scan job
                        let scanJob = {
                            JobId = Guid.NewGuid().ToString()
                            PlcId = config.PlcId
                            Priority = ScanJobPriority.Normal
                            Tags = config.Tags
                            Interval = config.ScanInterval
                            MaxRetries = config.MaxRetries
                            CurrentRetries = 0
                            NextRun = DateTime.UtcNow.Add(config.ScanInterval)
                            LastRun = None
                            IsEnabled = config.IsEnabled
                        }
                        
                        if scanJobs.TryAdd(scanJob.JobId, scanJob) then
                            if isRunning && config.IsEnabled then
                                this.ScheduleJob(scanJob)
                            return Ok ()
                        else
                            return Error "Failed to add scan job"
                    else
                        (manager :> IDisposable).Dispose()
                        return Error "Manager with this PlcId already exists"
                
                | Error error ->
                    return Error $"Failed to create manager: {error}"
            with
            | ex ->
                logger.LogError(ex, "Exception adding manager for PLC {PlcId}", config.PlcId)
                return Error ex.Message
        }
    
    /// Remove PLC manager from scheduler
    member this.RemoveManager(plcId: string) =
        task {
            try
                logger.LogInformation("Removing PLC manager {PlcId}", plcId)
                
                // Remove and dispose timers
                let jobsToRemove = scanJobs.Values |> Seq.filter (fun j -> j.PlcId = plcId) |> Seq.toList
                for job in jobsToRemove do
                    match jobTimers.TryRemove(job.JobId) with
                    | (true, timer) -> timer.Dispose()
                    | _ -> ()
                    scanJobs.TryRemove(job.JobId) |> ignore
                
                // Remove and dispose manager
                match activeManagers.TryRemove(plcId) with
                | (true, manager) ->
                    do! manager.DisconnectAsync()
                    (manager :> IDisposable).Dispose()
                    logger.LogInformation("Successfully removed PLC manager {PlcId}", plcId)
                    return Ok ()
                | _ ->
                    return Error "Manager not found"
            with
            | ex ->
                logger.LogError(ex, "Exception removing manager for PLC {PlcId}", plcId)
                return Error ex.Message
        }
    
    /// Schedule a scan job
    member private this.ScheduleJob(job: ScanJob) =
        if job.IsEnabled then
            let delay = max TimeSpan.Zero (job.NextRun - DateTime.UtcNow)
            let timer = new Timer(TimerCallback(this.ExecuteJobAsync), job.JobId, delay, job.Interval)
            jobTimers.TryAdd(job.JobId, timer) |> ignore
            logger.LogDebug("Scheduled scan job {JobId} for PLC {PlcId} (next run: {NextRun})", 
                job.JobId, job.PlcId, job.NextRun)
    
    /// Execute scan job asynchronously
    member private this.ExecuteJobAsync(state: obj) =
        let jobId = state :?> string
        
        Task.Run(fun () ->
            task {
                match scanJobs.TryGetValue(jobId) with
                | (true, job) when job.IsEnabled ->
                    let! _ = executionSemaphore.WaitAsync(cancellationTokenSource.Token)
                    try
                        do! this.ExecuteScanJob(job)
                    finally
                        executionSemaphore.Release() |> ignore
                | _ -> ()
            } |> ignore) |> ignore
    
    /// Execute a single scan job
    member private this.ExecuteScanJob(job: ScanJob) =
        task {
            let startTime = DateTime.UtcNow
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            
            try
                logger.LogTrace("Executing scan job {JobId} for PLC {PlcId}", job.JobId, job.PlcId)
                
                match activeManagers.TryGetValue(job.PlcId) with
                | (true, manager) ->
                    // Check connection
                    if not manager.PlcConnectionStatus.IsOperational then
                        logger.LogDebug("Connecting to PLC {PlcId} for scan", job.PlcId)
                        let! connected = manager.ConnectAsync()
                        if not connected then
                            let error = "Failed to connect to PLC"
                            let result = {
                                JobId = job.JobId
                                PlcId = job.PlcId
                                Success = false
                                Statistics = None
                                Error = Some error
                                ExecutionTime = stopwatch.Elapsed
                                Timestamp = startTime
                            }
                            jobFailedEvent.Trigger(result)
                            this.HandleJobFailure(job)
                            return ()
                    
                    // Execute scan
                    if job.Tags.Length > 0 then
                        match! manager.ReadTagsAsync(job.Tags) with
                        | Some batch ->
                            let stats = ServerScanStatistics.FromScanBatch(batch)
                            let result = {
                                JobId = job.JobId
                                PlcId = job.PlcId
                                Success = true
                                Statistics = Some stats
                                Error = None
                                ExecutionTime = stopwatch.Elapsed
                                Timestamp = startTime
                            }
                            jobCompletedEvent.Trigger(result)
                            this.HandleJobSuccess(job)
                            
                            logger.LogTrace("Scan job {JobId} completed: {SuccessfulTags}/{TotalTags} tags", 
                                job.JobId, stats.SuccessfulTags, stats.TotalTags)
                        | None ->
                            let error = "Failed to read tags from PLC"
                            let result = {
                                JobId = job.JobId
                                PlcId = job.PlcId
                                Success = false
                                Statistics = None
                                Error = Some error
                                ExecutionTime = stopwatch.Elapsed
                                Timestamp = startTime
                            }
                            jobFailedEvent.Trigger(result)
                            this.HandleJobFailure(job)
                    else
                        logger.LogDebug("No tags configured for scan job {JobId}", job.JobId)
                        
                | _ ->
                    let error = "PLC manager not found"
                    let result = {
                        JobId = job.JobId
                        PlcId = job.PlcId
                        Success = false
                        Statistics = None
                        Error = Some error
                        ExecutionTime = stopwatch.Elapsed
                        Timestamp = startTime
                    }
                    jobFailedEvent.Trigger(result)
                    logger.LogWarning("PLC manager {PlcId} not found for scan job {JobId}", job.PlcId, job.JobId)
            with
            | ex ->
                logger.LogError(ex, "Exception executing scan job {JobId} for PLC {PlcId}", job.JobId, job.PlcId)
                let result = {
                    JobId = job.JobId
                    PlcId = job.PlcId
                    Success = false
                    Statistics = None
                    Error = Some ex.Message
                    ExecutionTime = stopwatch.Elapsed
                    Timestamp = startTime
                }
                jobFailedEvent.Trigger(result)
                this.HandleJobFailure(job)
        }
    
    /// Handle successful job execution
    member private this.HandleJobSuccess(job: ScanJob) =
        let updatedJob = { job with CurrentRetries = 0; LastRun = Some DateTime.UtcNow }
        scanJobs.TryUpdate(job.JobId, updatedJob, job) |> ignore
    
    /// Handle failed job execution
    member private this.HandleJobFailure(job: ScanJob) =
        let updatedJob = { job with CurrentRetries = job.CurrentRetries + 1; LastRun = Some DateTime.UtcNow }
        scanJobs.TryUpdate(job.JobId, updatedJob, job) |> ignore
        
        if updatedJob.CurrentRetries >= updatedJob.MaxRetries then
            logger.LogError("Scan job {JobId} for PLC {PlcId} failed {Retries} times, disabling", 
                job.JobId, job.PlcId, updatedJob.CurrentRetries)
            
            let disabledJob = { updatedJob with IsEnabled = false }
            scanJobs.TryUpdate(job.JobId, disabledJob, updatedJob) |> ignore
            
            // Remove timer
            match jobTimers.TryRemove(job.JobId) with
            | (true, timer) -> timer.Dispose()
            | _ -> ()
    
    /// Get all active managers
    member this.GetActiveManagers() =
        activeManagers.Values |> Seq.toList
    
    /// Get manager by PLC ID
    member this.GetManager(plcId: string) =
        match activeManagers.TryGetValue(plcId) with
        | (true, manager) -> Some manager
        | _ -> None
    
    /// Get scan job by ID
    member this.GetScanJob(jobId: string) =
        match scanJobs.TryGetValue(jobId) with
        | (true, job) -> Some job
        | _ -> None
    
    /// Get all scan jobs for a PLC
    member this.GetScanJobsByPlc(plcId: string) =
        scanJobs.Values |> Seq.filter (fun j -> j.PlcId = plcId) |> Seq.toList
    
    /// Enable/disable scan job
    member this.SetJobEnabled(jobId: string, enabled: bool) =
        match scanJobs.TryGetValue(jobId) with
        | (true, job) ->
            let updatedJob = { job with IsEnabled = enabled }
            if scanJobs.TryUpdate(jobId, updatedJob, job) then
                if enabled && isRunning then
                    this.ScheduleJob(updatedJob)
                elif not enabled then
                    match jobTimers.TryRemove(jobId) with
                    | (true, timer) -> timer.Dispose()
                    | _ -> ()
                Ok ()
            else
                Error "Failed to update job"
        | _ ->
            Error "Job not found"
    
    /// Perform immediate scan of a PLC
    member this.ScanPLCImmediate(plcId: string) =
        task {
            match activeManagers.TryGetValue(plcId) with
            | (true, manager) ->
                logger.LogInformation("Performing immediate scan of PLC {PlcId}", plcId)
                
                let jobs = this.GetScanJobsByPlc(plcId)
                let activeTags = jobs |> List.collect (fun j -> j.Tags) |> List.distinct
                
                if activeTags.Length > 0 then
                    match! manager.ReadTagsAsync(activeTags) with
                    | Some batch ->
                        let stats = ServerScanStatistics.FromScanBatch(batch)
                        logger.LogInformation("Immediate scan completed for PLC {PlcId}: {SuccessfulTags}/{TotalTags}", 
                            plcId, stats.SuccessfulTags, stats.TotalTags)
                        return Ok stats
                    | None ->
                        return Error "Failed to read tags"
                else
                    return Error "No tags configured for PLC"
            | _ ->
                return Error "PLC manager not found"
        }
    
    /// Start the scheduler
    member this.Start() =
        task {
            if not isRunning then
                try
                    isRunning <- true
                    cancellationTokenSource <- new CancellationTokenSource()
                    
                    logger.LogInformation("Starting scan scheduler")
                    
                    // Schedule all enabled jobs
                    for job in scanJobs.Values do
                        if job.IsEnabled then
                            this.ScheduleJob(job)
                    
                    logger.LogInformation("Scan scheduler started with {JobCount} jobs", scanJobs.Count)
                with
                | ex ->
                    logger.LogError(ex, "Error starting scan scheduler")
                    isRunning <- false
        }
    
    /// Stop the scheduler
    member this.Stop() =
        task {
            if isRunning then
                try
                    logger.LogInformation("Stopping scan scheduler")
                    isRunning <- false
                    
                    // Cancel all operations
                    cancellationTokenSource.Cancel()
                    
                    // Dispose all timers
                    for timer in jobTimers.Values do
                        timer.Dispose()
                    jobTimers.Clear()
                    
                    // Disconnect all managers
                    for manager in activeManagers.Values do
                        do! manager.DisconnectAsync()
                        (manager :> IDisposable).Dispose()
                    activeManagers.Clear()
                    scanJobs.Clear()
                    
                    logger.LogInformation("Scan scheduler stopped")
                with
                | ex ->
                    logger.LogError(ex, "Error stopping scan scheduler")
        }
    
    // Events
    [<CLIEvent>]
    member this.JobCompleted = jobCompletedEvent.Publish
    
    [<CLIEvent>]
    member this.JobFailed = jobFailedEvent.Publish
    
    [<CLIEvent>]
    member this.ManagerConnected = managerConnectedEvent.Publish
    
    [<CLIEvent>]
    member this.ManagerDisconnected = managerDisconnectedEvent.Publish
    
    // IDisposable implementation
    interface IDisposable with
        member this.Dispose() =
            this.Stop() |> ignore
            executionSemaphore.Dispose()
            cancellationTokenSource.Dispose()

/// Hosted service wrapper for ScanScheduler
type ScanSchedulerService(
    serviceProvider: IServiceProvider,
    managerFactory: PlcManagerFactory,
    logger: ILogger<ScanScheduler>) =
    
    let scheduler = new ScanScheduler(serviceProvider, managerFactory, logger)
    
    /// Access to the underlying scheduler
    member this.Scheduler = scheduler
    
    interface IHostedService with
        member this.StartAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Starting ScanScheduler hosted service")
                do! scheduler.Start()
                return ()
            }
        
        member this.StopAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Stopping ScanScheduler hosted service")
                do! scheduler.Stop()
                return ()
            }
    
    interface IDisposable with
        member this.Dispose() =
            (scheduler :> IDisposable).Dispose()