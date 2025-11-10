namespace DSPLCServer.Database

open System
open System.Threading.Tasks
open DSPLCServer.Common

/// Mock repository implementation for build testing
type MockRepository() =
    
    interface IDataRepository with
        
        // ===== Database Management =====
        member this.InitializeAsync() = Task.FromResult(())
        member this.HealthCheckAsync() = Task.FromResult(true)
        member this.GetDatabaseStatisticsAsync() = Task.FromResult(Map.empty<string, obj>)
        
        // ===== PLC Configuration Management =====
        member this.SavePlcConfigurationAsync(config: PlcServerConfig) = Task.FromResult(())
        member this.GetPlcConfigurationAsync(plcId: string) = Task.FromResult(None: PlcServerConfig option)
        member this.GetAllPlcConfigurationsAsync() = Task.FromResult([] : PlcServerConfig list)
        member this.DeletePlcConfigurationAsync(plcId: string) = Task.FromResult(false)
        member this.PlcConfigurationExistsAsync(plcId: string) = Task.FromResult(false)
        
        // ===== Tag Configuration Management =====
        member this.SaveTagConfigurationAsync(config: TagConfiguration) = Task.FromResult(())
        member this.SaveTagConfigurationsAsync(configs: TagConfiguration list) = Task.FromResult(())
        member this.GetTagConfigurationAsync(tagId: string) = Task.FromResult(None: TagConfiguration option)
        member this.GetTagConfigurationsByPlcAsync(plcId: string) = Task.FromResult([] : TagConfiguration list)
        member this.GetAllTagConfigurationsAsync() = Task.FromResult([] : TagConfiguration list)
        member this.DeleteTagConfigurationAsync(tagId: string) = Task.FromResult(false)
        member this.DeleteTagConfigurationsByPlcAsync(plcId: string) = Task.FromResult(0)
        
        // ===== Data Point Management =====
        member this.InsertDataPointAsync(dataPoint: LoggedDataPoint) = Task.FromResult(())
        member this.InsertDataPointsAsync(dataPoints: LoggedDataPoint list) = Task.FromResult(())
        member this.GetLatestDataPointsByPlcAsync(plcId: string, limit: int) = Task.FromResult([] : LoggedDataPoint list)
        member this.GetLatestDataPointByTagAsync(tagId: string) = Task.FromResult(None: LoggedDataPoint option)
        member this.GetDataPointsAsync(startTime: DateTime, endTime: DateTime, plcIds: string list) = Task.FromResult([] : LoggedDataPoint list)
        member this.GetDataPointsByTagsAsync(tagIds: string list, startTime: DateTime, endTime: DateTime) = Task.FromResult([] : LoggedDataPoint list)
        member this.GetAggregatedDataAsync(plcId: string, tagId: string, startTime: DateTime, endTime: DateTime, aggregationType: string, intervalMinutes: int) = Task.FromResult(None: Map<DateTime, float> option)
        
        // ===== Data Cleanup =====
        member this.DeleteDataPointsBeforeAsync(cutoffDate: DateTime) = Task.FromResult(0L)
        member this.DeleteDataPointsByPlcAsync(plcId: string) = Task.FromResult(0L)
        member this.DeleteDataPointsByTagAsync(tagId: string) = Task.FromResult(0L)
        member this.GetDataRetentionStatsAsync() = Task.FromResult(Map.empty<string, obj>)
        
        // ===== Statistics and Monitoring =====
        member this.GetDataPointCountAsync() = Task.FromResult(0L)
        member this.GetDataPointCountByPlcAsync(plcId: string) = Task.FromResult(0L)
        member this.GetDataPointCountByTagAsync(tagId: string) = Task.FromResult(0L)
        member this.GetDataPointCountInRangeAsync(startTime: DateTime, endTime: DateTime) = Task.FromResult(0L)
        member this.GetDatabaseSizeAsync() = Task.FromResult(0L)
        member this.GetPerformanceMetricsAsync() = Task.FromResult(Map.empty<string, obj>)
        
        // ===== Scan Job Management =====
        member this.SaveScanJobAsync(job: ScanJob) = Task.FromResult(())
        member this.GetScanJobAsync(jobId: string) = Task.FromResult(None: ScanJob option)
        member this.GetScanJobsByPlcAsync(plcId: string) = Task.FromResult([] : ScanJob list)
        member this.GetAllScanJobsAsync() = Task.FromResult([] : ScanJob list)
        member this.DeleteScanJobAsync(jobId: string) = Task.FromResult(false)
        member this.UpdateScanJobStatusAsync(jobId: string, enabled: bool) = Task.FromResult(false)
        
        // ===== Scan Job Results =====
        member this.LogScanJobResultAsync(result: ScanJobResult) = Task.FromResult(())
        member this.GetScanJobResultsAsync(startTime: DateTime, endTime: DateTime, plcId: string option) = Task.FromResult([] : ScanJobResult list)
        member this.GetLatestScanJobResultsAsync(plcId: string, limit: int) = Task.FromResult([] : ScanJobResult list)
        member this.GetScanJobStatisticsAsync(plcId: string option, startTime: DateTime, endTime: DateTime) = Task.FromResult(Map.empty<string, obj>)
        
        // ===== Event Logging =====
        member this.LogServerEventAsync(event: ServerEvent) = Task.FromResult(())
        member this.GetServerEventsAsync(startTime: DateTime, endTime: DateTime, eventTypes: string list option) = Task.FromResult([] : ServerEvent list)
        member this.GetLatestServerEventsAsync(limit: int) = Task.FromResult([] : ServerEvent list)
        
        // ===== Diagnostic Data =====
        member this.SavePlcDiagnosticsAsync(plcId: string, diagnostics: PlcDiagnostics) = Task.FromResult(())
        member this.GetLatestPlcDiagnosticsAsync(plcId: string) = Task.FromResult(None: PlcDiagnostics option)
        member this.GetPlcDiagnosticsHistoryAsync(plcId: string, startTime: DateTime, endTime: DateTime) = Task.FromResult([] : (DateTime * PlcDiagnostics) list)
        member this.SavePerformanceMetricsAsync(plcId: string, metrics: PerformanceMetrics) = Task.FromResult(())
        member this.GetPerformanceMetricsHistoryAsync(plcId: string, startTime: DateTime, endTime: DateTime) = Task.FromResult([] : (DateTime * PerformanceMetrics) list)
        
        // ===== Connection Monitoring =====
        member this.LogConnectionStateChangeAsync(plcId: string, oldState: ConnectionStatus, newState: ConnectionStatus, timestamp: DateTime) = Task.FromResult(())
        member this.GetConnectionHistoryAsync(plcId: string, startTime: DateTime, endTime: DateTime) = Task.FromResult([] : (DateTime * ConnectionStatus) list)
        member this.GetCurrentConnectionStatesAsync() = Task.FromResult(Map.empty<string, ConnectionStatus>)
        
        // ===== Data Quality Monitoring =====
        member this.GetDataQualityStatsAsync(plcId: string option, startTime: DateTime, endTime: DateTime) = Task.FromResult(Map.empty<string, obj>)
        member this.GetDataQualityTrendsAsync(plcId: string, startTime: DateTime, endTime: DateTime, intervalMinutes: int) = Task.FromResult(Map.empty<DateTime, float>)
        
        // ===== Alarm and Alert Management =====
        member this.SaveAlarmConfigurationAsync(plcId: string, tagId: string, config: Map<string, obj>) = Task.FromResult(())
        member this.GetAlarmConfigurationsAsync(plcId: string) = Task.FromResult(Map.empty<string, Map<string, obj>>)
        member this.LogAlarmEventAsync(plcId: string, tagId: string, alarmType: string, value: PlcValue, timestamp: DateTime) = Task.FromResult(())
        member this.GetActiveAlarmsAsync(plcId: string option) = Task.FromResult([] : Map<string, obj> list)
        member this.GetAlarmHistoryAsync(plcId: string option, startTime: DateTime, endTime: DateTime) = Task.FromResult([] : Map<string, obj> list)