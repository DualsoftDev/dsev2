namespace Ev2.PLC.Common.Types

open System

// ===================================
// Diagnostics Types - Universal diagnostics and monitoring for PLC systems
// ===================================

/// Health status levels
type HealthStatus =
    | Healthy
    | Warning
    | Critical
    | Unknown

    member this.Level =
        match this with
        | Healthy -> 0
        | Warning -> 1
        | Critical -> 2
        | Unknown -> 3

    member this.Name =
        match this with
        | Healthy -> "Healthy"
        | Warning -> "Warning"
        | Critical -> "Critical"
        | Unknown -> "Unknown"

    member this.HasIssues = this = Warning || this = Critical

/// Diagnostic severity
type DiagnosticSeverity =
    | Info
    | Warning
    | Error
    | Critical

    member this.Level =
        match this with
        | Info -> 0
        | Warning -> 1
        | Error -> 2
        | Critical -> 3

/// Diagnostic category
type DiagnosticCategory =
    | System
    | Communication
    | Performance
    | Security
    | Configuration
    | Hardware
    | Software
    | Protocol
    | Data

    member this.Name =
        match this with
        | System -> "System"
        | Communication -> "Communication"
        | Performance -> "Performance"
        | Security -> "Security"
        | Configuration -> "Configuration"
        | Hardware -> "Hardware"
        | Software -> "Software"
        | Protocol -> "Protocol"
        | Data -> "Data"

/// Individual diagnostic message
type DiagnosticMessage = {
    Id: string
    PlcId: string
    Category: DiagnosticCategory
    Severity: DiagnosticSeverity
    Code: string option
    Title: string
    Description: string
    Source: string option
    Timestamp: DateTime
    Count: int
    FirstOccurrence: DateTime
    LastOccurrence: DateTime
    IsResolved: bool
    ResolvedAt: DateTime option
    ResolvedBy: string option
    Tags: string list
    RelatedMessages: string list
    Metadata: Map<string, string>
} with
    static member Create(id: string, plcId: string, category: DiagnosticCategory, severity: DiagnosticSeverity, title: string, description: string) = {
        Id = id
        PlcId = plcId
        Category = category
        Severity = severity
        Code = None
        Title = title
        Description = description
        Source = None
        Timestamp = DateTime.UtcNow
        Count = 1
        FirstOccurrence = DateTime.UtcNow
        LastOccurrence = DateTime.UtcNow
        IsResolved = false
        ResolvedAt = None
        ResolvedBy = None
        Tags = []
        RelatedMessages = []
        Metadata = Map.empty
    }

    member this.IsActive = not this.IsResolved
    member this.Duration = this.LastOccurrence - this.FirstOccurrence
    member this.AgeHours = (DateTime.UtcNow - this.FirstOccurrence).TotalHours

    member this.Resolve(?resolvedBy: string) =
        { this with 
            IsResolved = true
            ResolvedAt = Some DateTime.UtcNow
            ResolvedBy = resolvedBy }

    member this.Reoccur() =
        { this with 
            Count = this.Count + 1
            LastOccurrence = DateTime.UtcNow
            IsResolved = false
            ResolvedAt = None
            ResolvedBy = None }

    member this.AddTag(tag: string) =
        if this.Tags |> List.contains tag then this
        else { this with Tags = tag :: this.Tags }

    member this.SetMetadata(key: string, value: string) =
        { this with Metadata = this.Metadata |> Map.add key value }

/// PLC system information
type PlcSystemInfo = {
    PlcId: string
    CpuType: string option
    CpuModel: string option
    FirmwareVersion: string option
    SoftwareVersion: string option
    SerialNumber: string option
    ManufactureDate: DateTime option
    LastBootTime: DateTime option
    OperatingMode: string option
    ProcessorLoad: float option // percentage
    MemoryTotal: int64 option // bytes
    MemoryUsed: int64 option // bytes
    StorageTotal: int64 option // bytes
    StorageUsed: int64 option // bytes
    Temperature: float option // celsius
    PowerSupplyVoltage: float option // volts
    Uptime: TimeSpan option
    LastUpdated: DateTime
    CustomProperties: Map<string, string>
} with
    static member Empty(plcId: string) = {
        PlcId = plcId
        CpuType = None
        CpuModel = None
        FirmwareVersion = None
        SoftwareVersion = None
        SerialNumber = None
        ManufactureDate = None
        LastBootTime = None
        OperatingMode = None
        ProcessorLoad = None
        MemoryTotal = None
        MemoryUsed = None
        StorageTotal = None
        StorageUsed = None
        Temperature = None
        PowerSupplyVoltage = None
        Uptime = None
        LastUpdated = DateTime.UtcNow
        CustomProperties = Map.empty
    }

    member this.MemoryUsagePercentage =
        match this.MemoryTotal, this.MemoryUsed with
        | Some total, Some used when total > 0L -> Some (float used / float total * 100.0)
        | _ -> None

    member this.StorageUsagePercentage =
        match this.StorageTotal, this.StorageUsed with
        | Some total, Some used when total > 0L -> Some (float used / float total * 100.0)
        | _ -> None

    member this.IsOverloaded =
        match this.ProcessorLoad with
        | Some load -> load > 80.0
        | None -> false

    member this.IsLowMemory =
        match this.MemoryUsagePercentage with
        | Some usage -> usage > 90.0
        | None -> false

    member this.IsLowStorage =
        match this.StorageUsagePercentage with
        | Some usage -> usage > 85.0
        | None -> false

/// Performance metrics
type PerformanceMetrics = {
    PlcId: string
    TimeWindow: TimeSpan
    WindowStart: DateTime
    WindowEnd: DateTime
    
    // Communication metrics
    TotalRequests: int64
    SuccessfulRequests: int64
    FailedRequests: int64
    TimeoutRequests: int64
    
    // Timing metrics
    AverageResponseTime: TimeSpan
    MaxResponseTime: TimeSpan
    MinResponseTime: TimeSpan
    PercentileP50: TimeSpan
    PercentileP95: TimeSpan
    PercentileP99: TimeSpan
    
    // Throughput metrics
    RequestsPerSecond: float
    BytesPerSecond: float
    TagsPerSecond: float
    
    // Error metrics
    ErrorRate: float // percentage
    TimeoutRate: float // percentage
    
    // Resource usage
    CpuUsage: float option // percentage
    MemoryUsage: float option // percentage
    NetworkUsage: float option // percentage
    
    // Quality metrics
    DataQualityScore: float // 0-100
    ConnectionStability: float // 0-100
    
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
        TimeoutRequests = 0L
        AverageResponseTime = TimeSpan.Zero
        MaxResponseTime = TimeSpan.Zero
        MinResponseTime = TimeSpan.MaxValue
        PercentileP50 = TimeSpan.Zero
        PercentileP95 = TimeSpan.Zero
        PercentileP99 = TimeSpan.Zero
        RequestsPerSecond = 0.0
        BytesPerSecond = 0.0
        TagsPerSecond = 0.0
        ErrorRate = 0.0
        TimeoutRate = 0.0
        CpuUsage = None
        MemoryUsage = None
        NetworkUsage = None
        DataQualityScore = 100.0
        ConnectionStability = 100.0
        LastUpdated = DateTime.UtcNow
    }

    member this.SuccessRate =
        if this.TotalRequests = 0L then 0.0
        else (float this.SuccessfulRequests) / (float this.TotalRequests) * 100.0

    member this.IsPerformingWell =
        this.SuccessRate > 95.0 && 
        this.ErrorRate < 5.0 && 
        this.AverageResponseTime < TimeSpan.FromSeconds(1.0)

    member this.GetOverallScore() =
        let successWeight = 0.3
        let qualityWeight = 0.3
        let performanceWeight = 0.4

        let successScore = this.SuccessRate / 100.0
        let qualityScore = this.DataQualityScore / 100.0
        let performanceScore = 
            if this.AverageResponseTime.TotalSeconds > 0.0 then
                min 1.0 (1.0 / this.AverageResponseTime.TotalSeconds)
            else 1.0

        (successScore * successWeight) + 
        (qualityScore * qualityWeight) + 
        (performanceScore * performanceWeight)

/// Comprehensive diagnostics information
type PlcDiagnostics = {
    PlcId: string
    HealthStatus: HealthStatus
    SystemInfo: PlcSystemInfo
    Performance: PerformanceMetrics
    Connection: ConnectionInfo
    Messages: DiagnosticMessage list
    LastDiagnosticTime: DateTime
    NextDiagnosticTime: DateTime option
    DiagnosticInterval: TimeSpan
    AlertThresholds: Map<string, float>
    IsMonitoringEnabled: bool
} with
    static member Create(plcId: string, ?diagnosticInterval: TimeSpan) = {
        PlcId = plcId
        HealthStatus = Unknown
        SystemInfo = PlcSystemInfo.Empty(plcId)
        Performance = PerformanceMetrics.Empty(plcId)
        Connection = ConnectionInfo.Create(plcId, ConnectionConfig.Default)
        Messages = []
        LastDiagnosticTime = DateTime.UtcNow
        NextDiagnosticTime = None
        DiagnosticInterval = defaultArg diagnosticInterval (TimeSpan.FromMinutes(5.0))
        AlertThresholds = Map.empty
        IsMonitoringEnabled = true
    }

    member this.ActiveMessages = 
        this.Messages |> List.filter (_.IsActive)

    member this.CriticalMessages = 
        this.ActiveMessages |> List.filter (fun m -> m.Severity = Critical)

    member this.WarningMessages = 
        this.ActiveMessages |> List.filter (fun m -> m.Severity = Warning)

    member this.HasCriticalIssues = not this.CriticalMessages.IsEmpty
    member this.HasWarnings = not this.WarningMessages.IsEmpty

    member this.CalculateHealthStatus() : HealthStatus =
        if this.HasCriticalIssues then HealthStatus.Critical
        elif this.HasWarnings then HealthStatus.Warning
        elif this.Performance.IsPerformingWell && this.Connection.IsOperational then HealthStatus.Healthy
        else HealthStatus.Warning

    member this.AddMessage(message: DiagnosticMessage) =
        let existingMessage = 
            this.Messages |> List.tryFind (fun m -> 
                m.Category = message.Category && 
                m.Code = message.Code && 
                m.Title = message.Title)

        match existingMessage with
        | Some existing -> 
            let updatedMessages = this.Messages |> List.map (fun m -> 
                if m.Id = existing.Id then existing.Reoccur() else m)
            { this with Messages = updatedMessages }
        | None -> 
            { this with Messages = message :: this.Messages }

    member this.ResolveMessage(messageId: string, ?resolvedBy: string) =
        let updatedMessages = this.Messages |> List.map (fun m -> 
            if m.Id = messageId then m.Resolve(?resolvedBy = resolvedBy) else m)
        { this with Messages = updatedMessages }

    member this.ClearResolvedMessages(olderThan: TimeSpan) =
        let cutoffTime = DateTime.UtcNow.Subtract(olderThan)
        let filteredMessages = this.Messages |> List.filter (fun m -> 
            not m.IsResolved || 
            (m.ResolvedAt |> Option.map (fun t -> t > cutoffTime) |> Option.defaultValue true))
        { this with Messages = filteredMessages }

    member this.SetThreshold(metricName: string, threshold: float) =
        { this with AlertThresholds = this.AlertThresholds |> Map.add metricName threshold }

    member this.IsTimeForDiagnostic() =
        match this.NextDiagnosticTime with
        | Some nextTime -> this.IsMonitoringEnabled && DateTime.UtcNow >= nextTime
        | None -> this.IsMonitoringEnabled

    member this.UpdateDiagnosticTime() =
        let now = DateTime.UtcNow
        { this with 
            LastDiagnosticTime = now
            NextDiagnosticTime = Some (now.Add(this.DiagnosticInterval)) }

/// Diagnostic event for notifications
type DiagnosticEvent = {
    EventId: string
    PlcId: string
    EventType: DiagnosticEventType
    Severity: DiagnosticSeverity
    Title: string
    Description: string
    Data: Map<string, obj>
    Timestamp: DateTime
    Source: string option
} with
    static member Create(eventId: string, plcId: string, eventType: DiagnosticEventType, severity: DiagnosticSeverity, title: string, description: string) = {
        EventId = eventId
        PlcId = plcId
        EventType = eventType
        Severity = severity
        Title = title
        Description = description
        Data = Map.empty
        Timestamp = DateTime.UtcNow
        Source = None
    }

/// Types of diagnostic events
and DiagnosticEventType =
    | HealthStatusChanged
    | PerformanceThresholdExceeded
    | ConnectionStateChanged
    | ErrorOccurred
    | SystemInfoUpdated
    | DiagnosticCompleted

/// Diagnostic trend analysis
type DiagnosticTrend = {
    MetricName: string
    PlcId: string
    TimeWindow: TimeSpan
    DataPoints: (DateTime * float) list
    TrendDirection: TrendDirection
    ChangeRate: float // per hour
    Confidence: float // 0-1
    PredictedValue: float option
    PredictionTime: DateTime option
    LastAnalyzed: DateTime
} with
    static member Create(metricName: string, plcId: string, dataPoints: (DateTime * float) list) = {
        MetricName = metricName
        PlcId = plcId
        TimeWindow = 
            if dataPoints.IsEmpty then TimeSpan.Zero
            else (dataPoints |> List.maxBy fst |> fst) - (dataPoints |> List.minBy fst |> fst)
        DataPoints = dataPoints
        TrendDirection = Stable
        ChangeRate = 0.0
        Confidence = 0.0
        PredictedValue = None
        PredictionTime = None
        LastAnalyzed = DateTime.UtcNow
    }

    member this.IsImproving = this.TrendDirection = Improving
    member this.IsDegrading = this.TrendDirection = Degrading
    member this.HasSignificantTrend = this.Confidence > 0.7

/// Trend direction
and TrendDirection =
    | Improving
    | Stable
    | Degrading
    | Unknown

/// Module for working with diagnostics
module Diagnostics =

    /// Generate diagnostic message ID
    let generateMessageId (plcId: string) (category: DiagnosticCategory) =
        let now = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
        let guid = System.Guid.NewGuid().ToString("N").[..7]
        $"{plcId}_{category}_{now}_{guid}"

    /// Generate diagnostic event ID
    let generateEventId (plcId: string) =
        let now = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
        let guid = System.Guid.NewGuid().ToString("N").[..7]
        $"{plcId}_event_{now}_{guid}"

    /// Check if metric exceeds threshold
    let checkThreshold (thresholds: Map<string, float>) (metricName: string) (value: float) =
        match thresholds |> Map.tryFind metricName with
        | Some threshold -> value > threshold
        | None -> false

    /// Calculate system health score
    let calculateHealthScore (diagnostics: PlcDiagnostics) =
        let performanceScore = diagnostics.Performance.GetOverallScore() * 40.0
        let connectionScore = if diagnostics.Connection.IsOperational then 30.0 else 0.0
        let errorScore = 
            let errorPenalty = float diagnostics.CriticalMessages.Length * 10.0 + float diagnostics.WarningMessages.Length * 5.0
            max 0.0 (30.0 - errorPenalty)
        
        min 100.0 (performanceScore + connectionScore + errorScore)

    /// Analyze metric trends
    let analyzeTrend (dataPoints: (DateTime * float) list) =
        if dataPoints.Length < 3 then
            TrendDirection.Unknown, 0.0, 0.0
        else
            let sortedPoints = dataPoints |> List.sortBy fst
            let values = sortedPoints |> List.map snd
            let n = float values.Length
            
            // Simple linear regression
            let sumX = [0.0 .. n-1.0] |> List.sum
            let sumY = values |> List.sum
            let sumXY = List.zip [0.0 .. n-1.0] values |> List.sumBy (fun (x, y) -> x * y)
            let sumXX = [0.0 .. n-1.0] |> List.sumBy (fun x -> x * x)
            
            let slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX)
            let correlation = 
                let avgX = sumX / n
                let avgY = sumY / n
                let numerator = List.zip [0.0 .. n-1.0] values |> List.sumBy (fun (x, y) -> (x - avgX) * (y - avgY))
                let denomX = [0.0 .. n-1.0] |> List.sumBy (fun x -> (x - avgX) * (x - avgX)) |> sqrt
                let denomY = values |> List.sumBy (fun y -> (y - avgY) * (y - avgY)) |> sqrt
                if denomX = 0.0 || denomY = 0.0 then 0.0 else numerator / (denomX * denomY)
            
            let direction = 
                if abs slope < 0.01 then Stable
                elif slope > 0.0 then Improving
                else Degrading
            
            (direction, slope, abs correlation)

    /// Create diagnostic summary
    let createSummary (diagnostics: PlcDiagnostics) = {|
        PlcId = diagnostics.PlcId
        HealthStatus = diagnostics.HealthStatus
        HealthScore = calculateHealthScore diagnostics
        ActiveIssues = diagnostics.ActiveMessages.Length
        CriticalIssues = diagnostics.CriticalMessages.Length
        WarningIssues = diagnostics.WarningMessages.Length
        LastUpdate = diagnostics.LastDiagnosticTime
        PlcConnectionStatus = diagnostics.Connection.Status
        PerformanceScore = diagnostics.Performance.GetOverallScore() * 100.0
        IsOperational = diagnostics.Connection.IsOperational && not diagnostics.HasCriticalIssues
    |}

    /// Filter messages by criteria
    let filterMessages (criteria: DiagnosticMessage -> bool) (diagnostics: PlcDiagnostics) =
        diagnostics.Messages |> List.filter criteria

    /// Group messages by category
    let groupMessagesByCategory (diagnostics: PlcDiagnostics) =
        diagnostics.Messages |> List.groupBy (_.Category)

    /// Get messages from time range
    let getMessagesInRange (startTime: DateTime) (endTime: DateTime) (diagnostics: PlcDiagnostics) =
        diagnostics.Messages |> List.filter (fun m -> 
            m.Timestamp >= startTime && m.Timestamp <= endTime)

/// Module for performance analysis
module PerformanceAnalysis =

    /// Calculate percentile from response times
    let calculatePercentile (percentile: float) (responseTimes: TimeSpan list) =
        if responseTimes.IsEmpty then TimeSpan.Zero
        else
            let sorted = responseTimes |> List.sort
            let index = int (float (sorted.Length - 1) * percentile / 100.0)
            sorted.[index]

    /// Update performance metrics with new data
    let updateMetrics (metrics: PerformanceMetrics) (responseTime: TimeSpan) (wasSuccessful: bool) =
        let newTotal = metrics.TotalRequests + 1L
        let newSuccessful = if wasSuccessful then metrics.SuccessfulRequests + 1L else metrics.SuccessfulRequests
        let newFailed = if not wasSuccessful then metrics.FailedRequests + 1L else metrics.FailedRequests
        
        let newAverage = 
            if metrics.TotalRequests = 0L then responseTime
            else
                let totalMs = metrics.AverageResponseTime.TotalMilliseconds * float metrics.TotalRequests + responseTime.TotalMilliseconds
                TimeSpan.FromMilliseconds(totalMs / float newTotal)

        { metrics with
            TotalRequests = newTotal
            SuccessfulRequests = newSuccessful
            FailedRequests = newFailed
            AverageResponseTime = newAverage
            MaxResponseTime = max metrics.MaxResponseTime responseTime
            MinResponseTime = min metrics.MinResponseTime responseTime
            ErrorRate = (float newFailed) / (float newTotal) * 100.0
            LastUpdated = DateTime.UtcNow }

    /// Detect performance anomalies
    let detectAnomalies (metrics: PerformanceMetrics) (baseline: PerformanceMetrics) =
        let anomalies = [
            if metrics.ErrorRate > baseline.ErrorRate * 2.0 then
                "Error rate significantly increased"
            if metrics.AverageResponseTime > baseline.AverageResponseTime.Add(TimeSpan.FromSeconds(1.0)) then
                "Response time significantly increased"
            if metrics.RequestsPerSecond < baseline.RequestsPerSecond * 0.5 then
                "Throughput significantly decreased"
        ]
        anomalies

/// Driver capabilities descriptor
type DriverCapabilities = {
    /// Driver name and version
    Name: string
    Version: string
    
    /// Supported PLC vendors/models
    SupportedVendors: string list
    SupportedModels: string list
    
    /// Supported protocols
    SupportedProtocols: string list
    
    /// Supported data types
    SupportedDataTypes: PlcDataType list
    
    /// Connection capabilities
    SupportsSerial: bool
    SupportsTCP: bool
    SupportsUDP: bool
    
    /// Operation capabilities
    SupportsRead: bool
    SupportsWrite: bool
    SupportsBulkOperations: bool
    SupportsSubscriptions: bool
    
    /// Advanced capabilities
    SupportsStructures: bool
    SupportsArrays: bool
    SupportsBitOperations: bool
    SupportsProgramControl: bool
    SupportsFileOperations: bool
    SupportsAuthentication: bool
    SupportsDiagnostics: bool
    
    /// Performance characteristics
    MaxConcurrentOperations: int
    MaxTagsPerRequest: int
    MaxDataSizePerRequest: int
    TypicalResponseTime: TimeSpan
    
    /// Vendor-specific capabilities
    VendorSpecificCapabilities: Map<string, obj>
} with
    static member Default = {
        Name = "Generic PLC Driver"
        Version = "1.0.0"
        SupportedVendors = []
        SupportedModels = []
        SupportedProtocols = []
        SupportedDataTypes = []
        SupportsSerial = false
        SupportsTCP = false
        SupportsUDP = false
        SupportsRead = true
        SupportsWrite = true
        SupportsBulkOperations = false
        SupportsSubscriptions = false
        SupportsStructures = false
        SupportsArrays = false
        SupportsBitOperations = false
        SupportsProgramControl = false
        SupportsFileOperations = false
        SupportsAuthentication = false
        SupportsDiagnostics = false
        MaxConcurrentOperations = 1
        MaxTagsPerRequest = 1
        MaxDataSizePerRequest = 1024
        TypicalResponseTime = TimeSpan.FromMilliseconds(100.0)
        VendorSpecificCapabilities = Map.empty
    }