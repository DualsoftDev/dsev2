namespace DSPLCServer.Database

open System
open System.Threading.Tasks
open DSPLCServer.Common

/// Data repository interface for PLC server operations
type IDataRepository =
    
    // ===== Database Management =====
    
    /// Initialize database schema and connections
    abstract member InitializeAsync: unit -> Task<unit>
    
    /// Check database health and connectivity
    abstract member HealthCheckAsync: unit -> Task<bool>
    
    /// Get database statistics
    abstract member GetDatabaseStatisticsAsync: unit -> Task<Map<string, obj>>
    
    // ===== PLC Configuration Management =====
    
    /// Save or update PLC configuration
    abstract member SavePlcConfigurationAsync: config: PlcServerConfig -> Task<unit>
    
    /// Get PLC configuration by ID
    abstract member GetPlcConfigurationAsync: plcId: string -> Task<PlcServerConfig option>
    
    /// Get all PLC configurations
    abstract member GetAllPlcConfigurationsAsync: unit -> Task<PlcServerConfig list>
    
    /// Delete PLC configuration
    abstract member DeletePlcConfigurationAsync: plcId: string -> Task<bool>
    
    /// Check if PLC configuration exists
    abstract member PlcConfigurationExistsAsync: plcId: string -> Task<bool>
    
    // ===== Tag Configuration Management =====
    
    /// Save or update tag configuration
    abstract member SaveTagConfigurationAsync: config: TagConfiguration -> Task<unit>
    
    /// Save multiple tag configurations
    abstract member SaveTagConfigurationsAsync: configs: TagConfiguration list -> Task<unit>
    
    /// Get tag configuration by ID
    abstract member GetTagConfigurationAsync: tagId: string -> Task<TagConfiguration option>
    
    /// Get all tag configurations for a PLC
    abstract member GetTagConfigurationsByPlcAsync: plcId: string -> Task<TagConfiguration list>
    
    /// Get all tag configurations
    abstract member GetAllTagConfigurationsAsync: unit -> Task<TagConfiguration list>
    
    /// Delete tag configuration
    abstract member DeleteTagConfigurationAsync: tagId: string -> Task<bool>
    
    /// Delete all tag configurations for a PLC
    abstract member DeleteTagConfigurationsByPlcAsync: plcId: string -> Task<int>
    
    // ===== Data Point Management =====
    
    /// Insert single data point
    abstract member InsertDataPointAsync: dataPoint: LoggedDataPoint -> Task<unit>
    
    /// Insert multiple data points (batch operation)
    abstract member InsertDataPointsAsync: dataPoints: LoggedDataPoint list -> Task<unit>
    
    /// Get latest data points for a PLC
    abstract member GetLatestDataPointsByPlcAsync: plcId: string * limit: int -> Task<LoggedDataPoint list>
    
    /// Get latest data point for a specific tag
    abstract member GetLatestDataPointByTagAsync: tagId: string -> Task<LoggedDataPoint option>
    
    /// Get data points within time range
    abstract member GetDataPointsAsync: startTime: DateTime * endTime: DateTime * plcIds: string list -> Task<LoggedDataPoint list>
    
    /// Get data points for specific tags within time range
    abstract member GetDataPointsByTagsAsync: tagIds: string list * startTime: DateTime * endTime: DateTime -> Task<LoggedDataPoint list>
    
    /// Get aggregated data (e.g., averages, min/max) for time periods
    abstract member GetAggregatedDataAsync: plcId: string * tagId: string * startTime: DateTime * endTime: DateTime * aggregationType: string * intervalMinutes: int -> Task<Map<DateTime, float> option>
    
    // ===== Data Cleanup =====
    
    /// Delete data points older than specified date
    abstract member DeleteDataPointsBeforeAsync: cutoffDate: DateTime -> Task<int64>
    
    /// Delete data points for specific PLC
    abstract member DeleteDataPointsByPlcAsync: plcId: string -> Task<int64>
    
    /// Delete data points for specific tag
    abstract member DeleteDataPointsByTagAsync: tagId: string -> Task<int64>
    
    /// Get data retention statistics
    abstract member GetDataRetentionStatsAsync: unit -> Task<Map<string, obj>>
    
    // ===== Statistics and Monitoring =====
    
    /// Get total data point count
    abstract member GetDataPointCountAsync: unit -> Task<int64>
    
    /// Get data point count by PLC
    abstract member GetDataPointCountByPlcAsync: plcId: string -> Task<int64>
    
    /// Get data point count by tag
    abstract member GetDataPointCountByTagAsync: tagId: string -> Task<int64>
    
    /// Get data point count within time range
    abstract member GetDataPointCountInRangeAsync: startTime: DateTime * endTime: DateTime -> Task<int64>
    
    /// Get database size information
    abstract member GetDatabaseSizeAsync: unit -> Task<int64>
    
    /// Get performance metrics
    abstract member GetPerformanceMetricsAsync: unit -> Task<Map<string, obj>>
    
    // ===== Scan Job Management =====
    
    /// Save scan job configuration
    abstract member SaveScanJobAsync: job: ScanJob -> Task<unit>
    
    /// Get scan job by ID
    abstract member GetScanJobAsync: jobId: string -> Task<ScanJob option>
    
    /// Get all scan jobs for a PLC
    abstract member GetScanJobsByPlcAsync: plcId: string -> Task<ScanJob list>
    
    /// Get all scan jobs
    abstract member GetAllScanJobsAsync: unit -> Task<ScanJob list>
    
    /// Delete scan job
    abstract member DeleteScanJobAsync: jobId: string -> Task<bool>
    
    /// Update scan job status
    abstract member UpdateScanJobStatusAsync: jobId: string * enabled: bool -> Task<bool>
    
    // ===== Scan Job Results =====
    
    /// Log scan job result
    abstract member LogScanJobResultAsync: result: ScanJobResult -> Task<unit>
    
    /// Get scan job results within time range
    abstract member GetScanJobResultsAsync: startTime: DateTime * endTime: DateTime * plcId: string option -> Task<ScanJobResult list>
    
    /// Get latest scan job results for a PLC
    abstract member GetLatestScanJobResultsAsync: plcId: string * limit: int -> Task<ScanJobResult list>
    
    /// Get scan job performance statistics
    abstract member GetScanJobStatisticsAsync: plcId: string option * startTime: DateTime * endTime: DateTime -> Task<Map<string, obj>>
    
    // ===== Event Logging =====
    
    /// Log server event
    abstract member LogServerEventAsync: event: ServerEvent -> Task<unit>
    
    /// Get server events within time range
    abstract member GetServerEventsAsync: startTime: DateTime * endTime: DateTime * eventTypes: string list option -> Task<ServerEvent list>
    
    /// Get latest server events
    abstract member GetLatestServerEventsAsync: limit: int -> Task<ServerEvent list>
    
    // ===== Diagnostic Data =====
    
    /// Save PLC diagnostics data
    abstract member SavePlcDiagnosticsAsync: plcId: string * diagnostics: PlcDiagnostics -> Task<unit>
    
    /// Get latest PLC diagnostics
    abstract member GetLatestPlcDiagnosticsAsync: plcId: string -> Task<PlcDiagnostics option>
    
    /// Get PLC diagnostics history
    abstract member GetPlcDiagnosticsHistoryAsync: plcId: string * startTime: DateTime * endTime: DateTime -> Task<(DateTime * PlcDiagnostics) list>
    
    /// Save performance metrics
    abstract member SavePerformanceMetricsAsync: plcId: string * metrics: PerformanceMetrics -> Task<unit>
    
    /// Get performance metrics history
    abstract member GetPerformanceMetricsHistoryAsync: plcId: string * startTime: DateTime * endTime: DateTime -> Task<(DateTime * PerformanceMetrics) list>
    
    // ===== Connection Monitoring =====
    
    /// Log connection state change
    abstract member LogConnectionStateChangeAsync: plcId: string * oldState: ConnectionStatus * newState: ConnectionStatus * timestamp: DateTime -> Task<unit>
    
    /// Get connection history for a PLC
    abstract member GetConnectionHistoryAsync: plcId: string * startTime: DateTime * endTime: DateTime -> Task<(DateTime * ConnectionStatus) list>
    
    /// Get current connection states for all PLCs
    abstract member GetCurrentConnectionStatesAsync: unit -> Task<Map<string, ConnectionStatus>>
    
    // ===== Data Quality Monitoring =====
    
    /// Get data quality statistics for time range
    abstract member GetDataQualityStatsAsync: plcId: string option * startTime: DateTime * endTime: DateTime -> Task<Map<string, obj>>
    
    /// Get data quality trends
    abstract member GetDataQualityTrendsAsync: plcId: string * startTime: DateTime * endTime: DateTime * intervalMinutes: int -> Task<Map<DateTime, float>>
    
    // ===== Alarm and Alert Management =====
    
    /// Save alarm configuration
    abstract member SaveAlarmConfigurationAsync: plcId: string * tagId: string * config: Map<string, obj> -> Task<unit>
    
    /// Get alarm configurations for PLC
    abstract member GetAlarmConfigurationsAsync: plcId: string -> Task<Map<string, Map<string, obj>>>
    
    /// Log alarm event
    abstract member LogAlarmEventAsync: plcId: string * tagId: string * alarmType: string * value: PlcValue * timestamp: DateTime -> Task<unit>
    
    /// Get active alarms
    abstract member GetActiveAlarmsAsync: plcId: string option -> Task<Map<string, obj> list>
    
    /// Get alarm history
    abstract member GetAlarmHistoryAsync: plcId: string option * startTime: DateTime * endTime: DateTime -> Task<Map<string, obj> list>

/// Repository query options for filtering and pagination
type QueryOptions = {
    StartTime: DateTime option
    EndTime: DateTime option
    PlcIds: string list option
    TagIds: string list option
    Limit: int option
    Offset: int option
    OrderBy: string option
    OrderDescending: bool
    IncludeQuality: bool
    MinQuality: DataQuality option
}

/// Repository statistics
type RepositoryStatistics = {
    TotalDataPoints: int64
    DataPointsByPlc: Map<string, int64>
    DataPointsByQuality: Map<DataQuality, int64>
    DatabaseSize: int64
    OldestDataPoint: DateTime option
    NewestDataPoint: DateTime option
    AveragePointsPerDay: float
    DataRetentionDays: int
}

/// Module for repository utilities
module RepositoryUtils =
    
    /// Create default query options
    let defaultQueryOptions = {
        StartTime = None
        EndTime = None
        PlcIds = None
        TagIds = None
        Limit = None
        Offset = None
        OrderBy = None
        OrderDescending = false
        IncludeQuality = true
        MinQuality = None
    }
    
    /// Create query options for recent data
    let recentDataOptions (hours: int) (limit: int) = {
        defaultQueryOptions with
            StartTime = Some (DateTime.UtcNow.AddHours(-float hours))
            EndTime = Some DateTime.UtcNow
            Limit = Some limit
            OrderDescending = true
    }
    
    /// Create query options for specific PLC
    let plcQueryOptions (plcId: string) (startTime: DateTime) (endTime: DateTime) = {
        defaultQueryOptions with
            PlcIds = Some [plcId]
            StartTime = Some startTime
            EndTime = Some endTime
            OrderDescending = true
    }
    
    /// Create query options for specific tags
    let tagQueryOptions (tagIds: string list) (startTime: DateTime) (endTime: DateTime) = {
        defaultQueryOptions with
            TagIds = Some tagIds
            StartTime = Some startTime
            EndTime = Some endTime
            OrderDescending = true
    }