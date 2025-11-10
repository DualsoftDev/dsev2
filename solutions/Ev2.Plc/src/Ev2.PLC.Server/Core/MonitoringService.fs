namespace DSPLCServer.Core

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open DSPLCServer.Common
open DSPLCServer.Database
open DSPLCServer.PLC

/// System health status levels
type HealthLevel =
    | Healthy = 0
    | Warning = 1
    | Critical = 2
    | Down = 3

/// System health check result
type HealthCheckResult = {
    Component: string
    Level: HealthLevel
    Message: string
    ResponseTime: TimeSpan
    Timestamp: DateTime
    Details: Map<string, obj> option
}

/// Overall system health status
type SystemHealthStatus = {
    OverallLevel: HealthLevel
    TotalComponents: int
    HealthyComponents: int
    WarningComponents: int
    CriticalComponents: int
    DownComponents: int
    LastCheck: DateTime
    Components: HealthCheckResult list
    SystemUptime: TimeSpan
}

/// Performance monitoring data
type PerformanceSnapshot = {
    Timestamp: DateTime
    CpuUsage: float option
    MemoryUsage: int64 option
    MemoryTotal: int64 option
    ActiveConnections: int
    TotalScansPerSecond: float
    AverageResponseTime: TimeSpan
    ErrorRate: float
    DataThroughput: float
}

/// Monitoring and diagnostics service for the PLC server
type MonitoringService(
    scanScheduler: ScanScheduler,
    dataLogger: DataLogger,
    repository: IDataRepository,
    logger: ILogger<MonitoringService>) =
    
    let healthCheckResults = ConcurrentDictionary<string, HealthCheckResult>()
    let performanceHistory = ConcurrentQueue<PerformanceSnapshot>()
    let maxPerformanceHistory = 1000
    let serviceStartTime = DateTime.UtcNow
    
    let mutable isRunning = false
    let mutable cancellationTokenSource = new CancellationTokenSource()
    let mutable monitoringTask: Task option = None
    
    // Events
    let healthStatusChangedEvent = Event<SystemHealthStatus>()
    let componentHealthChangedEvent = Event<HealthCheckResult>()
    let performanceSnapshotEvent = Event<PerformanceSnapshot>()
    let alertTriggeredEvent = Event<string * HealthLevel * string>()
    
    /// Perform health check on all system components
    member this.PerformHealthCheckAsync() =
        task {
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            let results = ResizeArray<HealthCheckResult>()
            
            try
                // Check scan scheduler health
                let! schedulerHealth = this.CheckScanSchedulerHealth()
                results.Add(schedulerHealth)
                
                // Check data logger health
                let! dataLoggerHealth = this.CheckDataLoggerHealth()
                results.Add(dataLoggerHealth)
                
                // Check database health
                let! databaseHealth = this.CheckDatabaseHealth()
                results.Add(databaseHealth)
                
                // Check PLC connections
                let! plcHealth = this.CheckPlcConnectionsHealth()
                results.AddRange(plcHealth)
                
                // Update stored results
                for result in results do
                    healthCheckResults.AddOrUpdate(result.Component, result, fun _ _ -> result) |> ignore
                    componentHealthChangedEvent.Trigger(result)
                
                // Determine overall status
                let systemHealth = this.CalculateSystemHealth(results |> List.ofSeq)
                healthStatusChangedEvent.Trigger(systemHealth)
                
                logger.LogDebug("Health check completed in {Duration}ms: {Level}", 
                    stopwatch.Elapsed.TotalMilliseconds, systemHealth.OverallLevel)
                
                return systemHealth
            with
            | ex ->
                logger.LogError(ex, "Error during health check")
                let errorResult = {
                    Component = "MonitoringService"
                    Level = HealthLevel.Critical
                    Message = $"Health check failed: {ex.Message}"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = None
                }
                return {
                    OverallLevel = HealthLevel.Critical
                    TotalComponents = 1
                    HealthyComponents = 0
                    WarningComponents = 0
                    CriticalComponents = 1
                    DownComponents = 0
                    LastCheck = DateTime.UtcNow
                    Components = [errorResult]
                    SystemUptime = DateTime.UtcNow - serviceStartTime
                }
        }
    
    /// Check scan scheduler health
    member private this.CheckScanSchedulerHealth() =
        task {
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            
            try
                let activeManagers = scanScheduler.GetActiveManagers()
                let managerCount = activeManagers.Length
                
                let connectedCount = 
                    activeManagers 
                    |> List.filter (fun m -> m.ConnectionStatus.IsOperational)
                    |> List.length
                
                let level = 
                    if managerCount = 0 then HealthLevel.Warning
                    elif connectedCount = managerCount then HealthLevel.Healthy
                    elif connectedCount > managerCount / 2 then HealthLevel.Warning
                    else HealthLevel.Critical
                
                let details = Map.ofList [
                    ("TotalManagers", box managerCount)
                    ("ConnectedManagers", box connectedCount)
                    ("ConnectionRatio", box (float connectedCount / float managerCount))
                ]
                
                stopwatch.Stop()
                
                return {
                    Component = "ScanScheduler"
                    Level = level
                    Message = $"{connectedCount}/{managerCount} PLCs connected"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = Some details
                }
            with
            | ex ->
                stopwatch.Stop()
                logger.LogError(ex, "Error checking scan scheduler health")
                return {
                    Component = "ScanScheduler"
                    Level = HealthLevel.Critical
                    Message = $"Health check failed: {ex.Message}"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = None
                }
        }
    
    /// Check data logger health
    member private this.CheckDataLoggerHealth() =
        task {
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            
            try
                let stats = dataLogger.GetStatistics()
                
                let level = 
                    if not stats.IsRunning then HealthLevel.Critical
                    elif stats.BufferUtilization > 90.0 then HealthLevel.Critical
                    elif stats.BufferUtilization > 75.0 then HealthLevel.Warning
                    elif stats.ErrorCount > 100L then HealthLevel.Warning
                    else HealthLevel.Healthy
                
                let details = Map.ofList [
                    ("IsRunning", box stats.IsRunning)
                    ("BufferUtilization", box stats.BufferUtilization)
                    ("TotalPointsLogged", box stats.TotalPointsLogged)
                    ("ErrorCount", box stats.ErrorCount)
                    ("AverageFlushTime", box stats.AverageFlushTime.TotalMilliseconds)
                ]
                
                stopwatch.Stop()
                
                return {
                    Component = "DataLogger"
                    Level = level
                    Message = $"Buffer: {stats.BufferUtilization:F1}%, Errors: {stats.ErrorCount}"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = Some details
                }
            with
            | ex ->
                stopwatch.Stop()
                logger.LogError(ex, "Error checking data logger health")
                return {
                    Component = "DataLogger"
                    Level = HealthLevel.Critical
                    Message = $"Health check failed: {ex.Message}"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = None
                }
        }
    
    /// Check database health
    member private this.CheckDatabaseHealth() =
        task {
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            
            try
                let! isHealthy = repository.HealthCheckAsync()
                
                let level = if isHealthy then HealthLevel.Healthy else HealthLevel.Critical
                let message = if isHealthy then "Database accessible" else "Database connection failed"
                
                stopwatch.Stop()
                
                return {
                    Component = "Database"
                    Level = level
                    Message = message
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = None
                }
            with
            | ex ->
                stopwatch.Stop()
                logger.LogError(ex, "Error checking database health")
                return {
                    Component = "Database"
                    Level = HealthLevel.Critical
                    Message = $"Database check failed: {ex.Message}"
                    ResponseTime = stopwatch.Elapsed
                    Timestamp = DateTime.UtcNow
                    Details = None
                }
        }
    
    /// Check individual PLC connection health
    member private this.CheckPlcConnectionsHealth() =
        task {
            let managers = scanScheduler.GetActiveManagers()
            let results = ResizeArray<HealthCheckResult>()
            
            for manager in managers do
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                
                try
                    let! isHealthy = manager.HealthCheckAsync()
                    
                    let level = 
                        if isHealthy && manager.ConnectionStatus.IsOperational then HealthLevel.Healthy
                        elif isHealthy then HealthLevel.Warning
                        else HealthLevel.Critical
                    
                    let connectionInfo = manager.ConnectionInfo
                    let diagnostics = manager.Diagnostics
                    
                    let details = Map.ofList [
                        ("IsHealthy", box isHealthy)
                        ("IsConnected", box manager.ConnectionStatus.IsOperational)
                        ("ConnectionStatus", box (manager.ConnectionStatus.ToString()))
                        ("Uptime", box manager.Uptime.TotalHours)
                        ("LastSuccessfulScan", box diagnostics.LastSuccessfulScan)
                        ("TotalScans", box diagnostics.TotalScans)
                        ("FailedScans", box diagnostics.FailedScans)
                    ]
                    
                    stopwatch.Stop()
                    
                    let result = {
                        Component = $"PLC-{manager.Config.PlcId}"
                        Level = level
                        Message = $"{manager.Config.Vendor} - {manager.ConnectionStatus}"
                        ResponseTime = stopwatch.Elapsed
                        Timestamp = DateTime.UtcNow
                        Details = Some details
                    }
                    
                    results.Add(result)
                    
                with
                | ex ->
                    stopwatch.Stop()
                    logger.LogError(ex, "Error checking health for PLC {PlcId}", manager.Config.PlcId)
                    
                    let result = {
                        Component = $"PLC-{manager.Config.PlcId}"
                        Level = HealthLevel.Critical
                        Message = $"Health check failed: {ex.Message}"
                        ResponseTime = stopwatch.Elapsed
                        Timestamp = DateTime.UtcNow
                        Details = None
                    }
                    
                    results.Add(result)
            
            return results |> List.ofSeq
        }
    
    /// Calculate overall system health from component results
    member private this.CalculateSystemHealth(results: HealthCheckResult list) =
        let totalComponents = results.Length
        let healthyComponents = results |> List.filter (fun r -> r.Level = HealthLevel.Healthy) |> List.length
        let warningComponents = results |> List.filter (fun r -> r.Level = HealthLevel.Warning) |> List.length
        let criticalComponents = results |> List.filter (fun r -> r.Level = HealthLevel.Critical) |> List.length
        let downComponents = results |> List.filter (fun r -> r.Level = HealthLevel.Down) |> List.length
        
        let overallLevel = 
            if downComponents > 0 || criticalComponents > totalComponents / 2 then HealthLevel.Critical
            elif criticalComponents > 0 || warningComponents > totalComponents / 2 then HealthLevel.Warning
            else HealthLevel.Healthy
        
        {
            OverallLevel = overallLevel
            TotalComponents = totalComponents
            HealthyComponents = healthyComponents
            WarningComponents = warningComponents
            CriticalComponents = criticalComponents
            DownComponents = downComponents
            LastCheck = DateTime.UtcNow
            Components = results
            SystemUptime = DateTime.UtcNow - serviceStartTime
        }
    
    /// Collect performance snapshot
    member this.CollectPerformanceSnapshot() =
        try
            let managers = scanScheduler.GetActiveManagers()
            let dataLoggerStats = dataLogger.GetStatistics()
            
            // Calculate active connections
            let activeConnections = 
                managers 
                |> List.filter (fun m -> m.ConnectionStatus.IsOperational)
                |> List.length
            
            // Calculate scan rate (scans per second over last minute)
            let totalScans = managers |> List.sumBy (fun m -> m.Diagnostics.TotalScans)
            let scansPerSecond = float totalScans / 60.0  // Simplified calculation
            
            // Calculate average response time
            let avgResponseTime = 
                let responseTimes = managers |> List.choose (fun m -> 
                    if m.Diagnostics.AverageResponseTime.TotalMilliseconds > 0.0 then
                        Some m.Diagnostics.AverageResponseTime
                    else None)
                
                if responseTimes.IsEmpty then TimeSpan.Zero
                else
                    let totalMs = responseTimes |> List.sumBy (_.TotalMilliseconds)
                    TimeSpan.FromMilliseconds(totalMs / float responseTimes.Length)
            
            // Calculate error rate
            let totalOperations = managers |> List.sumBy (fun m -> m.Diagnostics.TotalScans)
            let totalErrors = managers |> List.sumBy (fun m -> m.Diagnostics.FailedScans)
            let errorRate = 
                if totalOperations > 0 then
                    (float totalErrors / float totalOperations) * 100.0
                else 0.0
            
            // Calculate data throughput (points per second)
            let dataThroughput = float dataLoggerStats.TotalPointsLogged / (DateTime.UtcNow - serviceStartTime).TotalSeconds
            
            let snapshot = {
                Timestamp = DateTime.UtcNow
                CpuUsage = None  // Would need system monitoring integration
                MemoryUsage = None  // Would need system monitoring integration
                MemoryTotal = None
                ActiveConnections = activeConnections
                TotalScansPerSecond = scansPerSecond
                AverageResponseTime = avgResponseTime
                ErrorRate = errorRate
                DataThroughput = dataThroughput
            }
            
            // Add to history (keep last N snapshots)
            performanceHistory.Enqueue(snapshot)
            while performanceHistory.Count > maxPerformanceHistory do
                performanceHistory.TryDequeue() |> ignore
            
            performanceSnapshotEvent.Trigger(snapshot)
            
            logger.LogTrace("Performance snapshot collected: {ActiveConnections} connections, {ErrorRate:F2}% error rate", 
                activeConnections, errorRate)
            
            snapshot
        with
        | ex ->
            logger.LogError(ex, "Error collecting performance snapshot")
            {
                Timestamp = DateTime.UtcNow
                CpuUsage = None
                MemoryUsage = None
                MemoryTotal = None
                ActiveConnections = 0
                TotalScansPerSecond = 0.0
                AverageResponseTime = TimeSpan.Zero
                ErrorRate = 100.0
                DataThroughput = 0.0
            }
    
    /// Get current system health status
    member this.GetCurrentHealthStatus() =
        let results = healthCheckResults.Values |> List.ofSeq
        if results.IsEmpty then
            {
                OverallLevel = HealthLevel.Warning
                TotalComponents = 0
                HealthyComponents = 0
                WarningComponents = 0
                CriticalComponents = 0
                DownComponents = 0
                LastCheck = DateTime.MinValue
                Components = []
                SystemUptime = DateTime.UtcNow - serviceStartTime
            }
        else
            this.CalculateSystemHealth(results)
    
    /// Get recent performance history
    member this.GetPerformanceHistory(count: int option) =
        let snapshots = performanceHistory.ToArray() |> Array.toList
        match count with
        | Some n -> snapshots |> List.rev |> List.take (min n snapshots.Length)
        | None -> snapshots |> List.rev
    
    /// Get performance statistics for time range
    member this.GetPerformanceStatistics(startTime: DateTime, endTime: DateTime) =
        let snapshots = 
            performanceHistory.ToArray()
            |> Array.filter (fun s -> s.Timestamp >= startTime && s.Timestamp <= endTime)
            |> Array.toList
        
        if snapshots.IsEmpty then
            Map.empty
        else
            let avgConnections = snapshots |> List.averageBy (fun s -> float s.ActiveConnections)
            let avgScansPerSec = snapshots |> List.averageBy (_.TotalScansPerSecond)
            let avgResponseTime = snapshots |> List.averageBy (fun s -> s.AverageResponseTime.TotalMilliseconds)
            let avgErrorRate = snapshots |> List.averageBy (_.ErrorRate)
            let avgThroughput = snapshots |> List.averageBy (_.DataThroughput)
            
            Map.ofList [
                ("AverageConnections", box avgConnections)
                ("AverageScansPerSecond", box avgScansPerSec)
                ("AverageResponseTime", box avgResponseTime)
                ("AverageErrorRate", box avgErrorRate)
                ("AverageDataThroughput", box avgThroughput)
                ("SampleCount", box snapshots.Length)
                ("StartTime", box startTime)
                ("EndTime", box endTime)
            ]
    
    /// Background monitoring task
    member private this.MonitoringTask() =
        task {
            logger.LogInformation("Monitoring service task started")
            
            try
                while not cancellationTokenSource.Token.IsCancellationRequested do
                    try
                        // Perform health check every 30 seconds
                        let! _ = this.PerformHealthCheckAsync()
                        
                        // Collect performance snapshot every 10 seconds
                        this.CollectPerformanceSnapshot() |> ignore
                        
                        // Wait before next check
                        do! Task.Delay(TimeSpan.FromSeconds(30.0), cancellationTokenSource.Token)
                        
                    with
                    | :? OperationCanceledException -> ()
                    | ex ->
                        logger.LogError(ex, "Error in monitoring loop")
                        do! Task.Delay(TimeSpan.FromSeconds(5.0), cancellationTokenSource.Token)
            with
            | :? OperationCanceledException -> ()
            
            logger.LogInformation("Monitoring service task stopped")
        }
    
    /// Start monitoring service
    member this.Start() =
        task {
            if not isRunning then
                try
                    isRunning <- true
                    cancellationTokenSource <- new CancellationTokenSource()
                    
                    logger.LogInformation("Starting monitoring service")
                    
                    // Perform initial health check
                    let! _ = this.PerformHealthCheckAsync()
                    
                    // Start background monitoring
                    let task = this.MonitoringTask()
                    monitoringTask <- Some task
                    
                    logger.LogInformation("Monitoring service started")
                with
                | ex ->
                    logger.LogError(ex, "Error starting monitoring service")
                    isRunning <- false
        }
    
    /// Stop monitoring service
    member this.Stop() =
        task {
            if isRunning then
                try
                    logger.LogInformation("Stopping monitoring service")
                    isRunning <- false
                    
                    // Cancel background task
                    cancellationTokenSource.Cancel()
                    
                    // Wait for task completion
                    match monitoringTask with
                    | Some task ->
                        try
                            do! task
                        with
                        | :? OperationCanceledException -> ()
                    | None -> ()
                    
                    logger.LogInformation("Monitoring service stopped")
                with
                | ex ->
                    logger.LogError(ex, "Error stopping monitoring service")
        }
    
    // Events
    [<CLIEvent>]
    member this.HealthStatusChanged = healthStatusChangedEvent.Publish
    
    [<CLIEvent>]
    member this.ComponentHealthChanged = componentHealthChangedEvent.Publish
    
    [<CLIEvent>]
    member this.PerformanceSnapshotTaken = performanceSnapshotEvent.Publish
    
    [<CLIEvent>]
    member this.AlertTriggered = alertTriggeredEvent.Publish
    
    // IDisposable
    interface IDisposable with
        member this.Dispose() =
            this.Stop() |> ignore
            cancellationTokenSource.Dispose()

/// Hosted service wrapper for MonitoringService
type MonitoringHostedService(
    scanScheduler: ScanScheduler,
    dataLogger: DataLogger,
    repository: IDataRepository,
    logger: ILogger<MonitoringService>) =
    
    let monitoringService = new MonitoringService(scanScheduler, dataLogger, repository, logger)
    
    /// Access to the underlying monitoring service
    member this.MonitoringService = monitoringService
    
    interface IHostedService with
        member this.StartAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Starting MonitoringService hosted service")
                do! monitoringService.Start()
                return ()
            }
        
        member this.StopAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Stopping MonitoringService hosted service")
                do! monitoringService.Stop()
                return ()
            }
    
    interface IDisposable with
        member this.Dispose() =
            monitoringService.Dispose()