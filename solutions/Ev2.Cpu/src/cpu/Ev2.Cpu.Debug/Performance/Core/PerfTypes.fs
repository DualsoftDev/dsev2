namespace Ev2.Cpu.Perf.Core

open System
open System.Collections.Generic
open Ev2.Cpu.Core

/// 성능 메트릭 타입
type MetricType =
    | CpuUsage
    | MemoryUsage
    | ScanTime
    | ThroughputRate
    | ExecutionCount
    | ErrorRate
    | ResponseTime
    | QueueDepth

/// 성능 측정 단위
type MetricUnit =
    | Percentage
    | Milliseconds
    | Microseconds
    | Bytes
    | Kilobytes
    | Megabytes
    | Count
    | Rate
    | Custom of string

/// 성능 메트릭 데이터
type MetricValue = {
    Timestamp: DateTime
    Value: float
    Unit: MetricUnit
    Tags: Map<string, string>
}

/// 성능 임계값
type Threshold = {
    Warning: float option
    Critical: float option
    Unit: MetricUnit
}

/// 성능 메트릭 정의
type MetricDefinition = {
    Name: string
    Type: MetricType
    Description: string
    Unit: MetricUnit
    Threshold: Threshold option
    SampleInterval: TimeSpan
}

/// 성능 측정 결과
type PerfMeasurement = {
    Definition: MetricDefinition
    Values: MetricValue list
    StartTime: DateTime
    EndTime: DateTime
    SampleCount: int
    Average: float
    Minimum: float
    Maximum: float
    StandardDeviation: float
}

/// 성능 모니터링 상태
type MonitoringState =
    | Stopped
    | Starting
    | Running
    | Pausing
    | Paused
    | Stopping
    | Error of string

/// 성능 모니터링 설정
type MonitoringConfig = {
    SampleInterval: TimeSpan
    BufferSize: int
    EnabledMetrics: Set<MetricType>
    Thresholds: Map<MetricType, Threshold>
    AutoStart: bool
    LogToFile: bool
    LogPath: string option
    RealtimeReporting: bool
}

/// 성능 경고 이벤트
type PerfAlert = {
    MetricName: string
    Threshold: float
    CurrentValue: float
    Severity: AlertSeverity
    Timestamp: DateTime
    Message: string
}

and AlertSeverity =
    | Info
    | Warning
    | Critical

/// 성능 통계 요약
type PerfStatistics = {
    TotalSamples: int64
    TotalRuntime: TimeSpan
    AverageCpuUsage: float
    PeakMemoryUsage: int64
    AverageScanTime: TimeSpan
    MaxScanTime: TimeSpan
    TotalStatements: int64
    StatementsPerSecond: float
    ErrorCount: int64
    AlertCount: int
}

/// 성능 벤치마크 결과
type BenchmarkResult = {
    Name: string
    Configuration: string
    Duration: TimeSpan
    Iterations: int
    AverageTime: TimeSpan
    MinTime: TimeSpan
    MaxTime: TimeSpan
    ThroughputPerSecond: float
    MemoryUsage: int64
    Success: bool
    ErrorMessage: string option
}

/// 성능 비교 결과
type ComparisonResult = {
    BaselineName: string
    CurrentName: string
    ImprovementRatio: float
    Performance: PerformanceChange
    Details: Map<MetricType, float>
}

and PerformanceChange =
    | Improved of float
    | Degraded of float
    | NoChange
    | Inconclusive

/// 기본 메트릭 정의들
module DefaultMetrics =
    
    let cpuUsage = {
        Name = "CPU Usage"
        Type = CpuUsage
        Description = "CPU utilization percentage"
        Unit = Percentage
        Threshold = Some { Warning = Some 70.0; Critical = Some 90.0; Unit = Percentage }
        SampleInterval = TimeSpan.FromSeconds(1.0)
    }
    
    let memoryUsage = {
        Name = "Memory Usage"
        Type = MemoryUsage
        Description = "Memory consumption in bytes"
        Unit = Bytes
        Threshold = Some { Warning = Some 100_000_000.0; Critical = Some 500_000_000.0; Unit = Bytes }
        SampleInterval = TimeSpan.FromSeconds(5.0)
    }
    
    let scanTime = {
        Name = "Scan Time"
        Type = ScanTime
        Description = "Time taken for one scan cycle"
        Unit = Milliseconds
        Threshold = Some { Warning = Some 10.0; Critical = Some 50.0; Unit = Milliseconds }
        SampleInterval = TimeSpan.FromMilliseconds(100.0)
    }
    
    let executionCount = {
        Name = "Execution Count"
        Type = ExecutionCount
        Description = "Number of statements executed"
        Unit = Count
        Threshold = None
        SampleInterval = TimeSpan.FromSeconds(1.0)
    }
    
    let errorRate = {
        Name = "Error Rate"
        Type = ErrorRate
        Description = "Number of errors per second"
        Unit = Rate
        Threshold = Some { Warning = Some 1.0; Critical = Some 5.0; Unit = Rate }
        SampleInterval = TimeSpan.FromSeconds(10.0)
    }

/// 기본 모니터링 설정
module DefaultConfig =
    
    let standard =
        { SampleInterval = TimeSpan.FromSeconds(1.0)
          BufferSize = 1000
          EnabledMetrics = Set.ofList [CpuUsage; MemoryUsage; ScanTime; ExecutionCount]
          Thresholds =
            Map.ofList [
                (CpuUsage, { Warning = Some 70.0; Critical = Some 90.0; Unit = Percentage })
                (MemoryUsage, { Warning = Some 100_000_000.0; Critical = Some 500_000_000.0; Unit = Bytes })
                (ScanTime, { Warning = Some 10.0; Critical = Some 50.0; Unit = Milliseconds })
            ]
          AutoStart = false
          LogToFile = true
          LogPath = Some "perf.log"
          RealtimeReporting = false }
    
    let highFrequency =
        { standard with
            SampleInterval = TimeSpan.FromMilliseconds(100.0)
            BufferSize = 10000
            RealtimeReporting = true }
    
    let production =
        { standard with
            SampleInterval = TimeSpan.FromSeconds(5.0)
            EnabledMetrics = Set.ofList [CpuUsage; MemoryUsage; ScanTime; ErrorRate]
            LogToFile = true
            RealtimeReporting = false }
