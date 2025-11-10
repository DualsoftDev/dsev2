namespace Ev2.PLC.Common.Types

open System

// ===================================
// Scan Operations Types - Universal scanning and data collection for PLC operations
// ===================================

/// Scan priority levels
type ScanPriority =
    | Critical
    | High
    | Normal
    | Low
    | Background

    member this.Level =
        match this with
        | Critical -> 0
        | High -> 1
        | Normal -> 2
        | Low -> 3
        | Background -> 4

    member this.Weight =
        match this with
        | Critical -> 1000
        | High -> 100
        | Normal -> 10
        | Low -> 1
        | Background -> 0

/// Scan operation types
type ScanOperation =
    | Read
    | Write of value: PlcValue
    | ReadWrite of value: PlcValue


    member this.WriteValue =
        match this with
        | Write value | ReadWrite value -> Some value
        | Read -> None

/// Scan request
type ScanRequest = {
    RequestId: string
    PlcId: string
    TagIds: string list
    Operation: ScanOperation
    Priority: ScanPriority
    Timeout: TimeSpan option
    RetryCount: int
    MaxRetryDelay: TimeSpan option
    Metadata: Map<string, string>
    RequestedAt: DateTime
    RequestedBy: string option
} with
    static member Create(requestId: string, plcId: string, tagIds: string list, operation: ScanOperation) = {
        RequestId = requestId
        PlcId = plcId
        TagIds = tagIds
        Operation = operation
        Priority = Normal
        Timeout = None
        RetryCount = 3
        MaxRetryDelay = None
        Metadata = Map.empty
        RequestedAt = DateTime.UtcNow
        RequestedBy = None
    }

    member this.TagCount = this.TagIds.Length
    member this.IsMultiTag = this.TagCount > 1
    member this.HasTimeout = this.Timeout.IsSome

/// Individual scan result for a single tag
type ScanResult = {
    TagId: string
    PlcId: string
    Operation: ScanOperation
    Value: PlcValue
    Quality: DataQuality
    Status: DataStatus
    Timestamp: DateTime
    ResponseTime: TimeSpan option
    RetryCount: int
    ErrorMessage: string option
    Metadata: Map<string, string>
} with
    static member Create(tagId: string, plcId: string, operation: ScanOperation, value: PlcValue, quality: DataQuality) = {
        TagId = tagId
        PlcId = plcId
        Operation = operation
        Value = value
        Quality = quality
        Status = DataStatus.CreateGood()
        Timestamp = DateTime.UtcNow
        ResponseTime = None
        RetryCount = 0
        ErrorMessage = None
        Metadata = Map.empty
    }

    member this.IsSuccessful = this.Quality.IsGood && this.ErrorMessage.IsNone
    member this.HasError = this.ErrorMessage.IsSome
    member this.Age = DateTime.UtcNow - this.Timestamp

    member this.WithError(error: string) =
        { this with ErrorMessage = Some error; Quality = Bad error }

    member this.WithResponseTime(responseTime: TimeSpan) =
        { this with ResponseTime = Some responseTime }

    member this.WithRetry(retryCount: int) =
        { this with RetryCount = retryCount }

/// Batch scan results
type ScanBatch = {
    BatchId: string
    PlcId: string
    RequestId: string
    Results: ScanResult list
    StartTime: DateTime
    EndTime: DateTime option
    TotalResponseTime: TimeSpan option
    SuccessCount: int
    FailureCount: int
    Statistics: ScanStatistics option
} with
    static member Create(batchId: string, plcId: string, requestId: string, results: ScanResult list) = {
        BatchId = batchId
        PlcId = plcId
        RequestId = requestId
        Results = results
        StartTime = DateTime.UtcNow
        EndTime = None
        TotalResponseTime = None
        SuccessCount = results |> List.filter (_.IsSuccessful) |> List.length
        FailureCount = results |> List.filter (_.HasError) |> List.length
        Statistics = None
    }

    member this.TotalCount = this.Results.Length
    member this.SuccessRate = 
        if this.TotalCount = 0 then 0.0
        else (float this.SuccessCount) / (float this.TotalCount) * 100.0

    member this.IsCompleted = this.EndTime.IsSome
    member this.Duration = 
        match this.EndTime with
        | Some endTime -> Some (endTime - this.StartTime)
        | None -> None

    member this.Complete() =
        let now = DateTime.UtcNow
        { this with 
            EndTime = Some now
            TotalResponseTime = Some (now - this.StartTime) }

/// Scan statistics
and ScanStatistics = {
    PlcId: string
    TimeWindow: TimeSpan
    WindowStart: DateTime
    WindowEnd: DateTime
    
    // Request statistics
    TotalRequests: int64
    SuccessfulRequests: int64
    FailedRequests: int64
    
    // Timing statistics
    AverageResponseTime: TimeSpan
    MaxResponseTime: TimeSpan
    MinResponseTime: TimeSpan
    MedianResponseTime: TimeSpan
    
    // Throughput statistics
    RequestsPerSecond: float
    TagsPerSecond: float
    SuccessfulTagsPerSecond: float
    
    // Quality statistics
    QualityDistribution: Map<DataQuality, int64>
    AverageQualityScore: float
    
    LastUpdated: DateTime
} with
    static member Empty(plcId: string) = {
        PlcId = plcId
        TimeWindow = TimeSpan.FromHours(1.0)
        WindowStart = DateTime.UtcNow
        WindowEnd = DateTime.UtcNow
        TotalRequests = 0L
        SuccessfulRequests = 0L
        FailedRequests = 0L
        AverageResponseTime = TimeSpan.Zero
        MaxResponseTime = TimeSpan.Zero
        MinResponseTime = TimeSpan.MaxValue
        MedianResponseTime = TimeSpan.Zero
        RequestsPerSecond = 0.0
        TagsPerSecond = 0.0
        SuccessfulTagsPerSecond = 0.0
        QualityDistribution = Map.empty
        AverageQualityScore = 0.0
        LastUpdated = DateTime.UtcNow
    }

    member this.SuccessRate =
        if this.TotalRequests = 0L then 0.0
        else (float this.SuccessfulRequests) / (float this.TotalRequests) * 100.0

    member this.FailureRate = 100.0 - this.SuccessRate

/// Scan scheduler configuration
type ScanSchedule = {
    ScheduleId: string
    PlcId: string
    TagGroups: string list
    ScanInterval: TimeSpan
    Priority: ScanPriority
    IsEnabled: bool
    MaxConcurrentScans: int
    TimeoutPerScan: TimeSpan
    RetryPolicy: RetryPolicy
    QualityThreshold: int
    LastScanTime: DateTime option
    NextScanTime: DateTime option
    ScanCount: int64
    ErrorCount: int64
    CreatedAt: DateTime
} with
    static member Create(scheduleId: string, plcId: string, scanInterval: TimeSpan) = {
        ScheduleId = scheduleId
        PlcId = plcId
        TagGroups = []
        ScanInterval = scanInterval
        Priority = Normal
        IsEnabled = true
        MaxConcurrentScans = 10
        TimeoutPerScan = TimeSpan.FromSeconds(30.0)
        RetryPolicy = RetryPolicy.Default
        QualityThreshold = 70
        LastScanTime = None
        NextScanTime = Some (DateTime.UtcNow.Add(scanInterval))
        ScanCount = 0L
        ErrorCount = 0L
        CreatedAt = DateTime.UtcNow
    }

    member this.IsReadyToScan() =
        this.IsEnabled && 
        match this.NextScanTime with
        | Some nextTime -> DateTime.UtcNow >= nextTime
        | None -> true

    member this.CalculateNextScanTime() =
        DateTime.UtcNow.Add(this.ScanInterval)

    member this.RecordScanCompletion(wasSuccessful: bool) =
        let now = DateTime.UtcNow
        { this with
            LastScanTime = Some now
            NextScanTime = Some (this.CalculateNextScanTime())
            ScanCount = this.ScanCount + 1L
            ErrorCount = if wasSuccessful then this.ErrorCount else this.ErrorCount + 1L }

/// Retry policy for failed operations
and RetryPolicy = {
    MaxRetries: int
    InitialDelay: TimeSpan
    MaxDelay: TimeSpan
    BackoffMultiplier: float
    RetryOnTimeout: bool
    RetryOnQualityFailure: bool
} with
    static member Default = {
        MaxRetries = 3
        InitialDelay = TimeSpan.FromMilliseconds(100.0)
        MaxDelay = TimeSpan.FromSeconds(10.0)
        BackoffMultiplier = 2.0
        RetryOnTimeout = true
        RetryOnQualityFailure = false
    }

    static member NoRetry = {
        MaxRetries = 0
        InitialDelay = TimeSpan.Zero
        MaxDelay = TimeSpan.Zero
        BackoffMultiplier = 1.0
        RetryOnTimeout = false
        RetryOnQualityFailure = false
    }

    member this.CalculateDelay(retryAttempt: int) =
        let delay = float this.InitialDelay.TotalMilliseconds * (this.BackoffMultiplier ** float retryAttempt)
        let clampedDelay = min delay this.MaxDelay.TotalMilliseconds
        TimeSpan.FromMilliseconds(clampedDelay)

    member this.ShouldRetry(retryCount: int, error: string option, quality: DataQuality) =
        if retryCount >= this.MaxRetries then false
        else
            match error, quality with
            | Some _, _ -> true
            | None, Bad _ -> this.RetryOnQualityFailure
            | None, Uncertain _ -> this.RetryOnQualityFailure
            | None, Good -> false


/// Module for working with scan operations
module Scan =

    /// Generate unique request ID
    let generateRequestId (plcId: string) =
        let now = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff")
        let guid = System.Guid.NewGuid().ToString("N").[..7]
        $"{plcId}_req_{now}_{guid}"

    /// Generate unique batch ID
    let generateBatchId (plcId: string) =
        let now = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff")
        let guid = System.Guid.NewGuid().ToString("N").[..7]
        $"{plcId}_batch_{now}_{guid}"

    /// Create read request
    let createReadRequest (plcId: string) (tagIds: string list) =
        let requestId = generateRequestId plcId
        ScanRequest.Create(requestId, plcId, tagIds, Read)

    /// Create write request
    let createWriteRequest (plcId: string) (tagId: string) (value: PlcValue) =
        let requestId = generateRequestId plcId
        ScanRequest.Create(requestId, plcId, [tagId], Write value)

    /// Calculate scan statistics from results
    let calculateStatistics (plcId: string) (results: ScanResult list) (timeWindow: TimeSpan) =
        if results.IsEmpty then
            ScanStatistics.Empty(plcId)
        else
            let now = DateTime.UtcNow
            let windowStart = now.Subtract(timeWindow)
            let recentResults = results |> List.filter (fun r -> r.Timestamp >= windowStart)
            
            let total = int64 recentResults.Length
            let successful = recentResults |> List.filter (_.IsSuccessful) |> List.length |> int64
            let failed = total - successful
            
            let responseTimes = recentResults |> List.choose (_.ResponseTime)
            let averageResponseTime = 
                if responseTimes.IsEmpty then TimeSpan.Zero
                else
                    let totalMs = responseTimes |> List.sumBy (_.TotalMilliseconds)
                    TimeSpan.FromMilliseconds(totalMs / float responseTimes.Length)
            
            let maxResponseTime = 
                if responseTimes.IsEmpty then TimeSpan.Zero
                else responseTimes |> List.max
            
            let minResponseTime = 
                if responseTimes.IsEmpty then TimeSpan.Zero
                else responseTimes |> List.min
            
            let sortedTimes = responseTimes |> List.sort
            let medianResponseTime = 
                if sortedTimes.IsEmpty then TimeSpan.Zero
                elif sortedTimes.Length % 2 = 0 then
                    let mid = sortedTimes.Length / 2
                    let sum = sortedTimes.[mid-1].TotalMilliseconds + sortedTimes.[mid].TotalMilliseconds
                    TimeSpan.FromMilliseconds(sum / 2.0)
                else
                    sortedTimes.[sortedTimes.Length / 2]
            
            let requestsPerSecond = float total / timeWindow.TotalSeconds
            let tagsPerSecond = requestsPerSecond
            let successfulTagsPerSecond = float successful / timeWindow.TotalSeconds
            
            let qualityDistribution = 
                recentResults 
                |> List.groupBy (_.Quality)
                |> List.map (fun (q, rs) -> (q, int64 rs.Length))
                |> Map.ofList
            
            let averageQuality = 
                let qualityScores = recentResults |> List.map (fun r -> r.Quality.Score)
                if qualityScores.IsEmpty then 0.0
                else qualityScores |> List.map float |> List.average
            
            {
                PlcId = plcId
                TimeWindow = timeWindow
                WindowStart = windowStart
                WindowEnd = now
                TotalRequests = total
                SuccessfulRequests = successful
                FailedRequests = failed
                AverageResponseTime = averageResponseTime
                MaxResponseTime = maxResponseTime
                MinResponseTime = minResponseTime
                MedianResponseTime = medianResponseTime
                RequestsPerSecond = requestsPerSecond
                TagsPerSecond = tagsPerSecond
                SuccessfulTagsPerSecond = successfulTagsPerSecond
                QualityDistribution = qualityDistribution
                AverageQualityScore = averageQuality
                LastUpdated = now
            }

    /// Group scan results by criteria
    let groupResults (groupFunc: ScanResult -> string) (results: ScanResult list) =
        results |> List.groupBy groupFunc

    /// Filter results by time range
    let filterByTimeRange (startTime: DateTime) (endTime: DateTime) (results: ScanResult list) =
        results |> List.filter (fun r -> r.Timestamp >= startTime && r.Timestamp <= endTime)

    /// Filter results by quality
    let filterByQuality (minimumQuality: DataQuality) (results: ScanResult list) =
        results |> List.filter (fun r -> r.Quality.Score >= minimumQuality.Score)

    /// Get failed results
    let getFailedResults (results: ScanResult list) =
        results |> List.filter (_.HasError)

    /// Get successful results
    let getSuccessfulResults (results: ScanResult list) =
        results |> List.filter (_.IsSuccessful)