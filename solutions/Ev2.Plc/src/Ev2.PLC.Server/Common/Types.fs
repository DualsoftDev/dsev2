namespace DSPLCServer.Common

open System
open Ev2.PLC.Common.Types



// Universal driver interfaces
type IPlcDriver = Ev2.PLC.Common.Interfaces.IPlcDriver
type IAdvancedPlcDriver = Ev2.PLC.Common.Interfaces.IAdvancedPlcDriver
type IPlcDriverFactory = Ev2.PLC.Common.Interfaces.IPlcDriverFactory

/// PLC Vendor enumeration for server configuration
type PlcVendor =
    | AllenBradley
    | Siemens  
    | Mitsubishi
    | LSElectric
    | Generic

    override this.ToString() =
        match this with
        | AllenBradley -> "Allen-Bradley"
        | Siemens -> "Siemens"
        | Mitsubishi -> "Mitsubishi"
        | LSElectric -> "LS Electric"
        | Generic -> "Generic"

/// Server-specific PLC configuration
type PlcServerConfig = {
    PlcId: string
    Vendor: PlcVendor
    Name: string
    Description: string option
    ConnectionConfig: ConnectionConfig
    IsEnabled: bool
    ScanInterval: TimeSpan
    MaxRetries: int
    Timeout: TimeSpan
    Tags: TagConfiguration list
    CreatedAt: DateTime
    UpdatedAt: DateTime
} with
    static member Create(plcId: string, vendor: PlcVendor, name: string, connectionConfig: ConnectionConfig) = {
        PlcId = plcId
        Vendor = vendor
        Name = name
        Description = None
        ConnectionConfig = connectionConfig
        IsEnabled = true
        ScanInterval = TimeSpan.FromSeconds(1.0)
        MaxRetries = 3
        Timeout = TimeSpan.FromSeconds(30.0)
        Tags = []
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

/// Server-specific scan statistics
type ServerScanStatistics = {
    PlcId: string
    TotalTags: int
    SuccessfulTags: int
    FailedTags: int
    ScanDuration: TimeSpan
    AverageResponseTime: TimeSpan
    MaxResponseTime: TimeSpan
    MinResponseTime: TimeSpan
    LastScanTime: DateTime
    ErrorRate: float
    QualityScore: float
} with
    static member FromScanBatch(batch: ScanBatch) = {
        PlcId = batch.PlcId
        TotalTags = batch.Results.Length
        SuccessfulTags = batch.Results |> List.filter (fun r -> r.Quality.IsGood) |> List.length
        FailedTags = batch.Results |> List.filter (fun r -> r.Quality.IsBad) |> List.length
        ScanDuration = 
            match batch.EndTime with
            | Some endTime -> endTime - batch.StartTime
            | None -> TimeSpan.Zero
        AverageResponseTime = 
            let times = batch.Results |> List.choose (_.ResponseTime)
            if times.IsEmpty then TimeSpan.Zero
            else
                let totalMs = times |> List.sumBy (_.TotalMilliseconds)
                TimeSpan.FromMilliseconds(totalMs / float times.Length)
        MaxResponseTime = 
            batch.Results 
            |> List.choose (_.ResponseTime)
            |> List.fold max TimeSpan.Zero
        MinResponseTime = 
            batch.Results 
            |> List.choose (_.ResponseTime)
            |> List.fold min TimeSpan.MaxValue
        LastScanTime = DateTime.UtcNow
        ErrorRate = 
            if batch.Results.IsEmpty then 0.0
            else
                let failed = batch.Results |> List.filter (fun r -> r.Quality.IsBad) |> List.length
                (float failed) / (float batch.Results.Length) * 100.0
        QualityScore = 
            if batch.Results.IsEmpty then 0.0
            else
                let totalScore = batch.Results |> List.sumBy (fun r -> r.Quality.Score)
                (float totalScore) / (float batch.Results.Length)
    }

/// Server health status
type ServerHealthStatus = {
    OverallStatus: DataQuality
    ConnectedPlcs: int
    TotalPlcs: int
    ActiveScans: int
    TotalErrors: int
    LastUpdateTime: DateTime
    Uptime: TimeSpan
} with
    static member Create() = {
        OverallStatus = DataQuality.Good
        ConnectedPlcs = 0
        TotalPlcs = 0
        ActiveScans = 0
        TotalErrors = 0
        LastUpdateTime = DateTime.UtcNow
        Uptime = TimeSpan.Zero
    }

/// Server events
type ServerEvent =
    | PlcConnected of plcId: string
    | PlcDisconnected of plcId: string * reason: string
    | ScanCompleted of plcId: string * statistics: ServerScanStatistics
    | ScanFailed of plcId: string * error: string
    | ConfigurationChanged of plcId: string
    | ServerStarted
    | ServerStopped
    | HealthCheckCompleted of status: ServerHealthStatus

/// Server configuration
type ServerConfiguration = {
    ServerId: string
    Port: int
    LogLevel: string
    DatabaseConnectionString: string
    PlcConfigurations: PlcServerConfig list
    GlobalScanInterval: TimeSpan
    HealthCheckInterval: TimeSpan
    MaxConcurrentScans: int
    EnableDiagnostics: bool
    DataRetentionDays: int
} with
    static member Default = {
        ServerId = "DSPLCServer"
        Port = 8080
        LogLevel = "Information"
        DatabaseConnectionString = "Data Source=plcserver.db"
        PlcConfigurations = []
        GlobalScanInterval = TimeSpan.FromSeconds(1.0)
        HealthCheckInterval = TimeSpan.FromMinutes(1.0)
        MaxConcurrentScans = 10
        EnableDiagnostics = true
        DataRetentionDays = 30
    }

/// Data point for logging with metadata
type LoggedDataPoint = {
    Id: int64
    TagId: string
    PlcId: string
    Value: PlcValue
    Quality: DataQuality
    Status: DataStatus
    Timestamp: DateTime
    SourceAddress: string option
    ResponseTime: TimeSpan option
} with
    static member FromScanResult(result: ScanResult) = {
        Id = 0L  // Auto-increment in database
        TagId = result.TagId
        PlcId = result.PlcId
        Value = result.Value
        Quality = result.Quality
        Status = result.Status
        Timestamp = result.Timestamp
        SourceAddress = None  // ScanResult doesn't have SourceAddress yet
        ResponseTime = result.ResponseTime
    }

/// Scan job priority levels
type ScanJobPriority =
    | Low = 1
    | Normal = 2
    | High = 3
    | Critical = 4

/// Scan job definition
type ScanJob = {
    JobId: string
    PlcId: string
    Priority: ScanJobPriority
    Tags: TagConfiguration list
    Interval: TimeSpan
    MaxRetries: int
    CurrentRetries: int
    NextRun: DateTime
    LastRun: DateTime option
    IsEnabled: bool
}

/// Scan job result
type ScanJobResult = {
    JobId: string
    PlcId: string
    Success: bool
    Statistics: ServerScanStatistics option
    Error: string option
    ExecutionTime: TimeSpan
    Timestamp: DateTime
}