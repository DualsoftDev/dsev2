namespace DSPLCServer.Core

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open DSPLCServer.Database


/// Data logging statistics
type DataLoggerStatistics = {
    BufferSize: int
    MaxBufferSize: int
    TotalPointsLogged: int64
    TotalBatchesProcessed: int64
    TotalPointsFlushed: int64
    LastFlushTime: DateTime
    FlushInterval: TimeSpan
    BatchSize: int
    BufferUtilization: float
    AverageFlushTime: TimeSpan
    ErrorCount: int64
    IsRunning: bool
}

/// Data logger service - Handles logging of scan results to database with buffering and batching
type DataLogger(repository: IDataRepository, logger: ILogger<DataLogger>) =
    
    let dataBuffer = ConcurrentQueue<LoggedDataPoint>()
    let maxBufferSize = 10000
    let batchSize = 1000
    let flushInterval = TimeSpan.FromSeconds(5.0)
    
    let mutable totalPointsLogged = 0L
    let mutable totalBatchesProcessed = 0L
    let mutable totalPointsFlushed = 0L
    let mutable totalFlushTime = TimeSpan.Zero
    let mutable errorCount = 0L
    let mutable lastFlushTime = DateTime.UtcNow
    let mutable isRunning = false
    let mutable cancellationTokenSource = new CancellationTokenSource()
    let mutable backgroundTask: Task option = None
    
    // Events
    let dataPointLoggedEvent = Event<LoggedDataPoint>()
    let batchFlushedEvent = Event<int * TimeSpan>()
    let bufferFullEvent = Event<int>()
    let flushErrorEvent = Event<exn>()
    
    /// Log a single scan result
    member this.LogScanResult(result: ScanResult) =
        try
            let dataPoint = LoggedDataPoint.FromScanResult(result)
            this.LogDataPoint(dataPoint)
            logger.LogTrace("Logged scan result for tag {TagId} from PLC {PlcId}", result.TagId, result.PlcId)
        with
        | ex ->
            Interlocked.Increment(&errorCount) |> ignore
            logger.LogError(ex, "Error logging scan result for tag {TagId}", result.TagId)
    
    /// Log a scan batch
    member this.LogScanBatch(batch: ScanBatch) =
        try
            let dataPoints = batch.Results |> List.map LoggedDataPoint.FromScanResult
            this.LogDataPoints(dataPoints)
            
            logger.LogDebug("Logged scan batch with {Count} results from PLC {PlcId}", 
                batch.Results.Length, batch.PlcId)
        with
        | ex ->
            Interlocked.Increment(&errorCount) |> ignore
            logger.LogError(ex, "Error logging scan batch for PLC {PlcId}", batch.PlcId)
    
    /// Log multiple scan batches
    member this.LogScanBatches(batches: ScanBatch list) =
        for batch in batches do
            this.LogScanBatch(batch)
    
    /// Add a data point to the buffer
    member this.LogDataPoint(dataPoint: LoggedDataPoint) =
        if dataBuffer.Count < maxBufferSize then
            dataBuffer.Enqueue(dataPoint)
            Interlocked.Increment(&totalPointsLogged) |> ignore
            dataPointLoggedEvent.Trigger(dataPoint)
        else
            Interlocked.Increment(&errorCount) |> ignore
            bufferFullEvent.Trigger(dataBuffer.Count)
            logger.LogWarning("Data buffer full ({BufferSize}), dropping data point for tag {TagId}", 
                maxBufferSize, dataPoint.TagId)
    
    /// Add multiple data points to the buffer
    member this.LogDataPoints(dataPoints: LoggedDataPoint list) =
        for dataPoint in dataPoints do
            this.LogDataPoint(dataPoint)
    
    /// Flush buffer to database (single batch)
    member private this.FlushBuffer() =
        task {
            if dataBuffer.IsEmpty then 
                return 0
            else
                let pointsToFlush = ResizeArray<LoggedDataPoint>()
                let mutable count = 0
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                
                // Extract batch-sized data
                while count < batchSize && not dataBuffer.IsEmpty do
                    match dataBuffer.TryDequeue() with
                    | (true, dataPoint) -> 
                        pointsToFlush.Add(dataPoint)
                        count <- count + 1
                    | _ -> count <- batchSize // Exit loop
                
                if pointsToFlush.Count > 0 then
                    try
                        // Insert into database
                        do! repository.InsertDataPointsAsync(pointsToFlush |> List.ofSeq)
                        
                        stopwatch.Stop()
                        let flushTime = stopwatch.Elapsed
                        
                        // Update statistics
                        Interlocked.Increment(&totalBatchesProcessed) |> ignore
                        Interlocked.Add(&totalPointsFlushed, pointsToFlush.Count) |> ignore
                        totalFlushTime <- totalFlushTime.Add(flushTime)
                        lastFlushTime <- DateTime.UtcNow
                        
                        batchFlushedEvent.Trigger(pointsToFlush.Count, flushTime)
                        logger.LogDebug("Flushed {Count} data points to database in {Duration}ms", 
                            pointsToFlush.Count, flushTime.TotalMilliseconds)
                        
                        return pointsToFlush.Count
                        
                    with
                    | ex ->
                        Interlocked.Increment(&errorCount) |> ignore
                        flushErrorEvent.Trigger(ex)
                        
                        // Re-queue failed data points (add back to front of queue)
                        for i = pointsToFlush.Count - 1 downto 0 do
                            dataBuffer.Enqueue(pointsToFlush.[i])
                            
                        logger.LogError(ex, "Failed to flush {Count} data points to database", pointsToFlush.Count)
                        return 0
                else
                    return 0
        }
    
    /// Force immediate flush of all buffered data
    member this.FlushAllAsync() =
        task {
            if not isRunning then
                logger.LogWarning("Cannot flush: DataLogger is not running")
                return 0
            else
                let mutable totalFlushed = 0
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                
                logger.LogInformation("Starting immediate flush of all buffered data ({BufferSize} points)", dataBuffer.Count)
                
                let mutable shouldContinue = true
                while shouldContinue && not dataBuffer.IsEmpty do
                    let! flushed = this.FlushBuffer()
                    totalFlushed <- totalFlushed + flushed
                    
                    if flushed = 0 then
                        // Prevent infinite loop if flush consistently fails
                        shouldContinue <- false
                
                stopwatch.Stop()
                logger.LogInformation("Immediate flush completed: {Count} points in {Duration}ms", 
                    totalFlushed, stopwatch.Elapsed.TotalMilliseconds)
                
                return totalFlushed
        }
    
    /// Background flush task
    member private this.FlushTask() =
        task {
            logger.LogDebug("DataLogger flush task started")
            
            try
                while not cancellationTokenSource.Token.IsCancellationRequested do
                    try
                        let! flushedCount = this.FlushBuffer()
                        
                        if flushedCount > 0 then
                            logger.LogTrace("Background flush processed {Count} points", flushedCount)
                        
                        // Wait for next flush interval
                        do! Task.Delay(flushInterval, cancellationTokenSource.Token)
                        
                    with
                    | :? OperationCanceledException -> ()
                    | ex ->
                        Interlocked.Increment(&errorCount) |> ignore
                        logger.LogError(ex, "Error in background flush task")
                        do! Task.Delay(TimeSpan.FromSeconds(1.0), cancellationTokenSource.Token)
            with
            | :? OperationCanceledException -> ()
            
            logger.LogDebug("DataLogger flush task stopped")
        }
    
    /// Get current statistics
    member this.GetStatistics() : DataLoggerStatistics = {
        BufferSize = dataBuffer.Count
        MaxBufferSize = maxBufferSize
        TotalPointsLogged = totalPointsLogged
        TotalBatchesProcessed = totalBatchesProcessed
        TotalPointsFlushed = totalPointsFlushed
        LastFlushTime = lastFlushTime
        FlushInterval = flushInterval
        BatchSize = batchSize
        BufferUtilization = (float dataBuffer.Count / float maxBufferSize) * 100.0
        AverageFlushTime = 
            if totalBatchesProcessed > 0L then
                TimeSpan.FromTicks(totalFlushTime.Ticks / totalBatchesProcessed)
            else
                TimeSpan.Zero
        ErrorCount = errorCount
        IsRunning = isRunning
    }
    
    /// Clean up old data from database
    member this.CleanupOldDataAsync(retentionDays: int) =
        task {
            let cutoffDate = DateTime.UtcNow.AddDays(-float retentionDays)
            
            logger.LogInformation("Starting data cleanup, deleting data older than {CutoffDate}", cutoffDate)
            
            try
                let! deletedCount = repository.DeleteDataPointsBeforeAsync(cutoffDate)
                logger.LogInformation("Data cleanup completed, deleted {Count} records", deletedCount)
                return deletedCount
            with
            | ex ->
                Interlocked.Increment(&errorCount) |> ignore
                logger.LogError(ex, "Data cleanup failed")
                return 0L
        }
    
    /// Export data for a time range
    member this.ExportDataAsync(startTime: DateTime, endTime: DateTime, plcIds: string list option) =
        task {
            try
                logger.LogInformation("Exporting data from {StartTime} to {EndTime}", startTime, endTime)
                
                let! dataPoints = 
                    match plcIds with
                    | Some ids -> repository.GetDataPointsAsync(startTime, endTime, ids)
                    | None -> repository.GetDataPointsAsync(startTime, endTime, [])
                
                logger.LogInformation("Exported {Count} data points", dataPoints.Length)
                return Ok dataPoints
            with
            | ex ->
                Interlocked.Increment(&errorCount) |> ignore
                logger.LogError(ex, "Data export failed")
                return Error ex.Message
        }
    
    /// Start the data logger
    member this.Start() =
        task {
            if not isRunning then
                try
                    isRunning <- true
                    cancellationTokenSource <- new CancellationTokenSource()
                    
                    logger.LogInformation("Starting data logger with buffer size {BufferSize}, batch size {BatchSize}", 
                        maxBufferSize, batchSize)
                    
                    // Start background flush task
                    let task = this.FlushTask()
                    backgroundTask <- Some task
                    
                    logger.LogInformation("Data logger started")
                with
                | ex ->
                    logger.LogError(ex, "Error starting data logger")
                    isRunning <- false
        }
    
    /// Stop the data logger
    member this.Stop() =
        task {
            if isRunning then
                try
                    logger.LogInformation("Stopping data logger...")
                    isRunning <- false
                    
                    // Cancel background task
                    cancellationTokenSource.Cancel()
                    
                    // Wait for background task to finish
                    match backgroundTask with
                    | Some task ->
                        try
                            do! task
                        with
                        | :? OperationCanceledException -> ()
                    | None -> ()
                    
                    // Flush remaining data
                    logger.LogInformation("Flushing remaining data on shutdown ({BufferSize} points)", dataBuffer.Count)
                    let! flushedCount = this.FlushAllAsync()
                    
                    if flushedCount > 0 then
                        logger.LogInformation("Flushed {Count} remaining data points on shutdown", flushedCount)
                    
                    if not dataBuffer.IsEmpty then
                        logger.LogWarning("Data buffer still contains {Count} unflushed data points", dataBuffer.Count)
                    
                    logger.LogInformation("Data logger stopped")
                with
                | ex ->
                    logger.LogError(ex, "Error stopping data logger")
        }
    
    // Events
    [<CLIEvent>]
    member this.DataPointLogged = dataPointLoggedEvent.Publish
    
    [<CLIEvent>]
    member this.BatchFlushed = batchFlushedEvent.Publish
    
    [<CLIEvent>]
    member this.BufferFull = bufferFullEvent.Publish
    
    [<CLIEvent>]
    member this.FlushError = flushErrorEvent.Publish
    
    // IDisposable implementation
    interface IDisposable with
        member this.Dispose() =
            this.Stop() |> ignore
            cancellationTokenSource.Dispose()

/// Hosted service wrapper for DataLogger
type DataLoggerService(repository: IDataRepository, logger: ILogger<DataLogger>) =
    
    let dataLogger = new DataLogger(repository, logger)
    
    /// Access to the underlying data logger
    member this.DataLogger = dataLogger
    
    interface IHostedService with
        member this.StartAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Starting DataLogger hosted service")
                do! dataLogger.Start()
                return ()
            }
        
        member this.StopAsync(cancellationToken: CancellationToken) =
            task {
                logger.LogInformation("Stopping DataLogger hosted service")
                do! dataLogger.Stop()
                return ()
            }
    
    interface IDisposable with
        member this.Dispose() =
            (dataLogger :> IDisposable).Dispose()