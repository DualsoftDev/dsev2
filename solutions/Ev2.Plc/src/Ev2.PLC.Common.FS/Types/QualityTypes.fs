namespace Ev2.PLC.Common.Types

open System

// ===================================
// Data Quality Types - Universal quality indicators for PLC data
// ===================================

/// Quality status for PLC data values
/// Based on OPC-UA quality model but simplified for PLC usage
type DataQuality =
    /// Data is valid and of good quality
    | Good
    /// Data may be valid but has some quality issues
    | Uncertain of reason: string
    /// Data is invalid or has significant quality issues
    | Bad of reason: string


    /// Get human-readable description of the quality
    member this.Description =
        match this with
        | Good -> "Good"
        | Uncertain reason -> $"Uncertain: {reason}"
        | Bad reason -> $"Bad: {reason}"

    /// Get quality as a numeric score (0-100)
    member this.Score =
        match this with
        | Good -> 100
        | Uncertain _ -> 50
        | Bad _ -> 0

/// Specific quality reasons for uncertain data
module UncertainReasons =
    let [<Literal>] SensorNoise = "Sensor noise detected"
    let [<Literal>] EngUnitRangeExceeded = "Engineering units range exceeded"
    let [<Literal>] SimulatedValue = "Simulated value"
    let [<Literal>] SensorNotAccurate = "Sensor not accurate"
    let [<Literal>] EuRangeExceeded = "EU range exceeded"
    let [<Literal>] SubNormal = "Sub-normal value"
    let [<Literal>] InitialValue = "Initial value"
    let [<Literal>] ConfigurationError = "Configuration error"
    let [<Literal>] NotConnected = "Not connected"
    let [<Literal>] DeviceFailure = "Device failure"
    let [<Literal>] SensorFailure = "Sensor failure"
    let [<Literal>] LastKnownValue = "Last known value"
    let [<Literal>] CommFailure = "Communication failure"
    let [<Literal>] OutOfService = "Out of service"

/// Specific quality reasons for bad data
module BadReasons =
    let [<Literal>] DeviceNotConnected = "Device not connected"
    let [<Literal>] CommunicationError = "Communication error"
    let [<Literal>] SensorFailure = "Sensor failure"
    let [<Literal>] OutOfRange = "Value out of range"
    let [<Literal>] InvalidAddress = "Invalid address"
    let [<Literal>] DataTypeError = "Data type error"
    let [<Literal>] AccessDenied = "Access denied"
    let [<Literal>] DeviceError = "Device error"
    let [<Literal>] ConfigurationError = "Configuration error"
    let [<Literal>] NotSupported = "Operation not supported"
    let [<Literal>] Timeout = "Timeout"
    let [<Literal>] UnknownError = "Unknown error"
    let [<Literal>] ShutdownInProgress = "Shutdown in progress"
    let [<Literal>] SecurityError = "Security error"
    let [<Literal>] LicenseError = "License error"

/// Combined status that includes both data quality and additional metadata
type DataStatus = {
    Quality: DataQuality
    Timestamp: DateTime
    Source: string option
    StatusCode: int option
    Severity: DataSeverity
    AdditionalInfo: Map<string, string>
} with
    static member CreateGood(?source: string, ?statusCode: int) = {
        Quality = Good
        Timestamp = DateTime.UtcNow
        Source = source
        StatusCode = statusCode
        Severity = Info
        AdditionalInfo = Map.empty
    }

    static member CreateUncertain(reason: string, ?source: string, ?statusCode: int, ?severity: DataSeverity) = {
        Quality = Uncertain reason
        Timestamp = DateTime.UtcNow
        Source = source
        StatusCode = statusCode
        Severity = defaultArg severity Warning
        AdditionalInfo = Map.empty
    }

    static member CreateBad(reason: string, ?source: string, ?statusCode: int, ?severity: DataSeverity) = {
        Quality = Bad reason
        Timestamp = DateTime.UtcNow
        Source = source
        StatusCode = statusCode
        Severity = defaultArg severity Error
        AdditionalInfo = Map.empty
    }

    member this.IsGood = this.Quality.IsGood
    member this.IsBad = this.Quality.IsBad
    member this.IsUncertain = this.Quality.IsUncertain

    member this.WithAdditionalInfo(key: string, value: string) = 
        { this with AdditionalInfo = this.AdditionalInfo |> Map.add key value }

    member this.WithSource(source: string) = 
        { this with Source = Some source }

    member this.WithStatusCode(code: int) = 
        { this with StatusCode = Some code }

/// Severity level for data status
and DataSeverity =
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

    member this.Name =
        match this with
        | Info -> "Info"
        | Warning -> "Warning"
        | Error -> "Error"
        | Critical -> "Critical"

/// Quality statistics for monitoring data quality over time
type QualityStatistics = {
    TotalSamples: int64
    GoodSamples: int64
    UncertainSamples: int64
    BadSamples: int64
    LastGoodTime: DateTime option
    LastBadTime: DateTime option
    WorstQuality: DataQuality option
    QualityTrend: QualityTrend
    WindowStartTime: DateTime
    WindowEndTime: DateTime
} with
    static member Empty = {
        TotalSamples = 0L
        GoodSamples = 0L
        UncertainSamples = 0L
        BadSamples = 0L
        LastGoodTime = None
        LastBadTime = None
        WorstQuality = None
        QualityTrend = Stable
        WindowStartTime = DateTime.UtcNow
        WindowEndTime = DateTime.UtcNow
    }

    member this.GoodPercentage =
        if this.TotalSamples = 0L then 0.0
        else (float this.GoodSamples) / (float this.TotalSamples) * 100.0

    member this.UncertainPercentage =
        if this.TotalSamples = 0L then 0.0
        else (float this.UncertainSamples) / (float this.TotalSamples) * 100.0

    member this.BadPercentage =
        if this.TotalSamples = 0L then 0.0
        else (float this.BadSamples) / (float this.TotalSamples) * 100.0

    member this.OverallQuality =
        if this.TotalSamples = 0L then Good
        elif this.BadPercentage > 50.0 then Bad "More than 50% bad samples"
        elif this.UncertainPercentage > 30.0 then Uncertain "More than 30% uncertain samples"
        else Good

/// Quality trend indication
and QualityTrend =
    | Improving
    | Stable
    | Degrading
    | Unknown

/// Module for working with data quality
module DataQuality =

    /// Combine multiple quality values into a single quality
    let combine (qualities: DataQuality list) =
        match qualities with
        | [] -> Good
        | qualities ->
            let hasBad = qualities |> List.exists (_.IsBad)
            let hasUncertain = qualities |> List.exists (_.IsUncertain)
            
            if hasBad then
                let badReasons = qualities |> List.choose (function Bad reason -> Some reason | _ -> None)
                Bad (String.concat "; " badReasons)
            elif hasUncertain then
                let uncertainReasons = qualities |> List.choose (function Uncertain reason -> Some reason | _ -> None)
                Uncertain (String.concat "; " uncertainReasons)
            else
                Good

    /// Check if a quality meets a minimum threshold
    let meetsThreshold (minScore: int) (quality: DataQuality) =
        quality.Score >= minScore

    /// Create quality based on exception
    let fromException (ex: exn) =
        match ex with
        | :? TimeoutException -> Bad BadReasons.Timeout
        | :? UnauthorizedAccessException -> Bad BadReasons.AccessDenied
        | :? NotSupportedException -> Bad BadReasons.NotSupported
        | :? ArgumentException -> Bad BadReasons.InvalidAddress
        | _ -> Bad $"Error: {ex.Message}"

    /// Create quality based on status code
    let fromStatusCode (code: int) =
        match code with
        | 0 -> Good
        | c when c >= 200 && c < 300 -> Good
        | c when c >= 300 && c < 400 -> Uncertain $"Status code: {c}"
        | c when c >= 400 -> Bad $"Status code: {c}"
        | _ -> Bad $"Unknown status code: {code}"

/// Module for working with quality statistics
module QualityStatistics =

    /// Update statistics with a new quality sample
    let update (stats: QualityStatistics) (quality: DataQuality) (timestamp: DateTime) =
        let newStats = {
            stats with
                TotalSamples = stats.TotalSamples + 1L
                WindowEndTime = timestamp
        }

        match quality with
        | Good -> 
            { newStats with 
                GoodSamples = newStats.GoodSamples + 1L
                LastGoodTime = Some timestamp }
        | Uncertain _ -> 
            { newStats with UncertainSamples = newStats.UncertainSamples + 1L }
        | Bad _ -> 
            { newStats with 
                BadSamples = newStats.BadSamples + 1L
                LastBadTime = Some timestamp }

    /// Calculate quality trend based on recent history
    let calculateTrend (recentQualities: (DataQuality * DateTime) list) =
        if recentQualities.Length < 5 then Unknown
        else
            let scores = recentQualities |> List.map (fst >> (_.Score))
            let firstHalf = scores |> List.take (scores.Length / 2) |> List.map float |> List.average
            let secondHalf = scores |> List.skip (scores.Length / 2) |> List.map float |> List.average
            
            let diff = secondHalf - firstHalf
            if diff > 10.0 then Improving
            elif diff < -10.0 then Degrading
            else Stable

    /// Create statistics for a time window
    let forTimeWindow (startTime: DateTime) (endTime: DateTime) (samples: (DataQuality * DateTime) list) =
        let windowSamples = samples |> List.filter (fun (_, timestamp) -> 
            timestamp >= startTime && timestamp <= endTime)
        
        let mutable stats = { QualityStatistics.Empty with WindowStartTime = startTime; WindowEndTime = endTime }
        
        for (quality, timestamp) in windowSamples do
            stats <- update stats quality timestamp
        
        { stats with QualityTrend = calculateTrend windowSamples }