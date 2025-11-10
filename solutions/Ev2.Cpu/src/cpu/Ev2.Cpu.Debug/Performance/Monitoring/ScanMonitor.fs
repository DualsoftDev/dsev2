namespace Ev2.Cpu.Perf.Monitoring

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Collections.Generic
open System.Collections.Concurrent
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Runtime
open Ev2.Cpu.Perf.Core

/// 스캔 성능 모니터
type ScanPerformanceMonitor() =
    let mutable isEnabled = true
    let scanHistory = ConcurrentQueue<ScanMetrics>()
    let maxHistorySize = 1000
    let mutable totalScans = 0L
    
    interface IMetricCollector with
        member this.Name = "Scan Performance Monitor"
        member this.SupportedMetrics = Set.ofList [ScanTime; ThroughputRate; ExecutionCount]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // 최근 스캔 통계
                let recentScans = 
                    scanHistory
                    |> Seq.filter (fun s -> s.Timestamp > timestamp.AddSeconds(-60.0))
                    |> Seq.toList
                
                let avgScanTime = 
                    if List.isEmpty recentScans then 0.0
                    else recentScans |> List.map (fun s -> s.Duration.TotalMilliseconds) |> List.average
                
                let maxScanTime = 
                    if List.isEmpty recentScans then 0.0
                    else recentScans |> List.map (fun s -> s.Duration.TotalMilliseconds) |> List.max
                
                let minScanTime = 
                    if List.isEmpty recentScans then 0.0
                    else recentScans |> List.map (fun s -> s.Duration.TotalMilliseconds) |> List.min
                
                let scansPerSecond = float (List.length recentScans) / 60.0
                
                let avgStatementsPerScan = 
                    if List.isEmpty recentScans then 0.0
                    else recentScans |> List.map (fun s -> float s.StatementsExecuted) |> List.average
                
                [
                    {
                        Timestamp = timestamp
                        Value = avgScanTime
                        Unit = Milliseconds
                        Tags = Map.ofList ["type", "scan"; "component", "average_time"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = maxScanTime
                        Unit = Milliseconds
                        Tags = Map.ofList ["type", "scan"; "component", "max_time"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = minScanTime
                        Unit = Milliseconds
                        Tags = Map.ofList ["type", "scan"; "component", "min_time"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = scansPerSecond
                        Unit = Rate
                        Tags = Map.ofList ["type", "scan"; "component", "scans_per_second"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = avgStatementsPerScan
                        Unit = Count
                        Tags = Map.ofList ["type", "scan"; "component", "avg_statements"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float totalScans
                        Unit = Count
                        Tags = Map.ofList ["type", "scan"; "component", "total_scans"]
                    }
                ]
            )
    
    member this.RecordScan(duration: TimeSpan, statementsExecuted: int, scanType: ScanType) =
        let scanMetric = {
            Timestamp = DateTime.UtcNow
            Duration = duration
            StatementsExecuted = statementsExecuted
            ScanType = scanType
            Success = true
            ErrorMessage = None
        }

        // Thread-safe queue management
        while scanHistory.Count >= maxHistorySize do
            let mutable dummy = Unchecked.defaultof<ScanMetrics>
            scanHistory.TryDequeue(&dummy) |> ignore

        scanHistory.Enqueue(scanMetric)
        System.Threading.Interlocked.Increment(&totalScans) |> ignore
    
    member this.RecordScanError(duration: TimeSpan, errorMessage: string, scanType: ScanType) =
        let scanMetric = {
            Timestamp = DateTime.UtcNow
            Duration = duration
            StatementsExecuted = 0
            ScanType = scanType
            Success = false
            ErrorMessage = Some errorMessage
        }

        // Thread-safe queue management
        while scanHistory.Count >= maxHistorySize do
            let mutable dummy = Unchecked.defaultof<ScanMetrics>
            scanHistory.TryDequeue(&dummy) |> ignore

        scanHistory.Enqueue(scanMetric)
        System.Threading.Interlocked.Increment(&totalScans) |> ignore
    
    member this.GetScanStatistics() =
        let scans = scanHistory.ToArray()
        let averageDuration =
            if scans.Length > 0 then
                scans |> Array.map (fun s -> s.Duration.TotalMilliseconds) |> Array.average
            else
                0.0

        let averageStatements =
            if scans.Length > 0 then
                scans |> Array.map (fun s -> float s.StatementsExecuted) |> Array.average
            else
                0.0

        let totalDuration =
            if scans.Length > 0 then
                scans |> Array.map (fun s -> s.Duration) |> Array.reduce (+)
            else
                TimeSpan.Zero

        {
            TotalScans = int64 scans.Length
            SuccessfulScans = scans |> Array.filter (fun s -> s.Success) |> Array.length |> int64
            FailedScans = scans |> Array.filter (fun s -> not s.Success) |> Array.length |> int64
            AverageDuration = averageDuration
            TotalDuration = totalDuration
            AverageStatementsPerScan = averageStatements
        }

and ScanMetrics = {
    Timestamp: DateTime
    Duration: TimeSpan
    StatementsExecuted: int
    ScanType: ScanType
    Success: bool
    ErrorMessage: string option
}

and ScanType =
    | FullScan
    | SelectiveScan
    | EventDrivenScan
    | ManualScan

and ScanStatistics = {
    TotalScans: int64
    SuccessfulScans: int64
    FailedScans: int64
    AverageDuration: float
    TotalDuration: TimeSpan
    AverageStatementsPerScan: float
}

/// 구문 실행 모니터
type StatementExecutionMonitor() =
    let mutable isEnabled = true
    let executionData = ConcurrentDictionary<int, StatementExecutionStats>()
    let recentExecutions = ConcurrentQueue<StatementExecution>()
    let maxRecentExecutions = 5000
    
    interface IMetricCollector with
        member this.Name = "Statement Execution Monitor"
        member this.SupportedMetrics = Set.ofList [ExecutionCount; ResponseTime; ThroughputRate]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // 최근 1분간의 실행 통계
                let recentExecutionsList = 
                    recentExecutions
                    |> Seq.filter (fun e -> e.Timestamp > timestamp.AddMinutes(-1.0))
                    |> Seq.toList
                
                let totalExecutions = List.length recentExecutionsList
                let avgExecutionTime = 
                    if totalExecutions > 0 then
                        recentExecutionsList |> List.map (fun e -> e.Duration.TotalMicroseconds) |> List.average
                    else 0.0
                
                let executionsPerSecond = float totalExecutions / 60.0
                
                // 단계별 실행 통계
                let stepStats = 
                    recentExecutionsList
                    |> List.groupBy (fun e -> e.StepNumber)
                    |> List.map (fun (step, executions) ->
                        let avgTime = executions |> List.map (fun e -> e.Duration.TotalMicroseconds) |> List.average
                        step, avgTime, List.length executions
                    )
                
                let results = [
                    {
                        Timestamp = timestamp
                        Value = float totalExecutions
                        Unit = Count
                        Tags = Map.ofList ["type", "execution"; "component", "total_per_minute"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = avgExecutionTime
                        Unit = Microseconds
                        Tags = Map.ofList ["type", "execution"; "component", "avg_time"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = executionsPerSecond
                        Unit = Rate
                        Tags = Map.ofList ["type", "execution"; "component", "executions_per_second"]
                    }
                ]
                
                // 단계별 통계 추가
                let stepResults = 
                    stepStats
                    |> List.collect (fun (step, avgTime, count) -> [
                        {
                            Timestamp = timestamp
                            Value = avgTime
                            Unit = Microseconds
                            Tags = Map.ofList ["type", "execution"; "component", "step_avg_time"; "step", string step]
                        }
                        {
                            Timestamp = timestamp
                            Value = float count
                            Unit = Count
                            Tags = Map.ofList ["type", "execution"; "component", "step_count"; "step", string step]
                        }
                    ])
                
                results @ stepResults
            )
    
    member this.RecordExecution(stepNumber: int, duration: TimeSpan, statementType: string) =
        let execution = {
            Timestamp = DateTime.UtcNow
            StepNumber = stepNumber
            Duration = duration
            StatementType = statementType
            Success = true
            ErrorMessage = None
        }

        // Thread-safe queue management
        while recentExecutions.Count >= maxRecentExecutions do
            let mutable dummy = Unchecked.defaultof<StatementExecution>
            recentExecutions.TryDequeue(&dummy) |> ignore
        recentExecutions.Enqueue(execution)
        
        // 단계별 통계 업데이트
        let stats = executionData.GetOrAdd(stepNumber, fun _ -> {
            TotalExecutions = 0L
            TotalDuration = TimeSpan.Zero
            AverageDuration = TimeSpan.Zero
            MaxDuration = TimeSpan.Zero
            MinDuration = TimeSpan.MaxValue
            SuccessfulExecutions = 0L
            FailedExecutions = 0L
        })
        
        let newStats = {
            TotalExecutions = stats.TotalExecutions + 1L
            TotalDuration = stats.TotalDuration.Add(duration)
            AverageDuration = TimeSpan.FromTicks((stats.TotalDuration.Add(duration)).Ticks / (stats.TotalExecutions + 1L))
            MaxDuration = if duration > stats.MaxDuration then duration else stats.MaxDuration
            MinDuration = if duration < stats.MinDuration then duration else stats.MinDuration
            SuccessfulExecutions = stats.SuccessfulExecutions + 1L
            FailedExecutions = stats.FailedExecutions
        }
        
        executionData.TryUpdate(stepNumber, newStats, stats) |> ignore
    
    member this.RecordExecutionError(stepNumber: int, duration: TimeSpan, errorMessage: string, statementType: string) =
        let execution = {
            Timestamp = DateTime.UtcNow
            StepNumber = stepNumber
            Duration = duration
            StatementType = statementType
            Success = false
            ErrorMessage = Some errorMessage
        }

        // Thread-safe queue management
        while recentExecutions.Count >= maxRecentExecutions do
            let mutable dummy = Unchecked.defaultof<StatementExecution>
            recentExecutions.TryDequeue(&dummy) |> ignore
        recentExecutions.Enqueue(execution)
        
        // 실패 통계 업데이트
        let stats = executionData.GetOrAdd(stepNumber, fun _ -> {
            TotalExecutions = 0L
            TotalDuration = TimeSpan.Zero
            AverageDuration = TimeSpan.Zero
            MaxDuration = TimeSpan.Zero
            MinDuration = TimeSpan.MaxValue
            SuccessfulExecutions = 0L
            FailedExecutions = 0L
        })
        
        let newStats = {
            stats with 
                TotalExecutions = stats.TotalExecutions + 1L
                FailedExecutions = stats.FailedExecutions + 1L
        }
        
        executionData.TryUpdate(stepNumber, newStats, stats) |> ignore
    
    member this.GetStatementStatistics() =
        executionData
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value)
        |> Seq.toList

and StatementExecution = {
    Timestamp: DateTime
    StepNumber: int
    Duration: TimeSpan
    StatementType: string
    Success: bool
    ErrorMessage: string option
}

and StatementExecutionStats = {
    TotalExecutions: int64
    TotalDuration: TimeSpan
    AverageDuration: TimeSpan
    MaxDuration: TimeSpan
    MinDuration: TimeSpan
    SuccessfulExecutions: int64
    FailedExecutions: int64
}

/// 스캔 엔진 래퍼 (성능 측정 포함)
type InstrumentedScanEngine(engine: CpuScanEngine, scanMonitor: ScanPerformanceMonitor, stmtMonitor: StatementExecutionMonitor) =
    
    member this.ScanOnce() =
        let stopwatch = Stopwatch.StartNew()
        try
            let result = engine.ScanOnce()
            stopwatch.Stop()
            
            // 스캔 성능 기록 (실제 실행된 구문 수는 엔진에서 제공해야 함)
            scanMonitor.RecordScan(stopwatch.Elapsed, 0, FullScan) // 임시로 0 사용
            
            result
        with
        | ex ->
            stopwatch.Stop()
            scanMonitor.RecordScanError(stopwatch.Elapsed, ex.Message, FullScan)
            reraise()
    
    member this.ScanOnceWithInstrumentation(statements: DsStmt list) =
        let stopwatch = Stopwatch.StartNew()
        let mutable executedCount = 0
        
        try
            for stmt in statements do
                let stmtStopwatch = Stopwatch.StartNew()
                try
                    // 실제 구문 실행 (여기서는 시뮬레이션)
                    let stepNumber = Statement.getStepNumber stmt
                    let statementType =
                        match stmt with
                        | Assign(_, _, _) -> "Assign"
                        | Command(_, _, _) -> "Command"
                        | _ -> "Other"

                    // 구문 실행 시뮬레이션
                    System.Threading.Thread.Sleep(1) // 1ms 지연

                    stmtStopwatch.Stop()
                    stmtMonitor.RecordExecution(stepNumber, stmtStopwatch.Elapsed, statementType)
                    executedCount <- executedCount + 1

                with
                | ex ->
                    stmtStopwatch.Stop()
                    let stepNumber = Statement.getStepNumber stmt
                    let statementType =
                        match stmt with
                        | Assign(_, _, _) -> "Assign"
                        | Command(_, _, _) -> "Command"
                        | _ -> "Other"
                    stmtMonitor.RecordExecutionError(stepNumber, stmtStopwatch.Elapsed, ex.Message, statementType)
            
            stopwatch.Stop()
            scanMonitor.RecordScan(stopwatch.Elapsed, executedCount, FullScan)
            int stopwatch.ElapsedMilliseconds
            
        with
        | ex ->
            stopwatch.Stop()
            scanMonitor.RecordScanError(stopwatch.Elapsed, ex.Message, FullScan)
            reraise()

/// 성능 예측기
type PerformancePredictor(scanMonitor: ScanPerformanceMonitor) =
    
    member this.PredictScanTime(statementCount: int) =
        let stats = scanMonitor.GetScanStatistics()
        if stats.AverageStatementsPerScan > 0.0 then
            let timePerStatement = stats.AverageDuration / stats.AverageStatementsPerScan
            TimeSpan.FromMilliseconds(timePerStatement * float statementCount)
        else
            TimeSpan.Zero
    
    member this.PredictThroughput(duration: TimeSpan) =
        let stats = scanMonitor.GetScanStatistics()
        if stats.AverageDuration > 0.0 then
            let scansPerSecond = 1000.0 / stats.AverageDuration
            let totalScans = scansPerSecond * duration.TotalSeconds
            totalScans * stats.AverageStatementsPerScan
        else
            0.0
    
    member this.EstimateResourceRequirements(targetThroughput: float) =
        let stats = scanMonitor.GetScanStatistics()
        if stats.AverageStatementsPerScan > 0.0 && stats.AverageDuration > 0.0 then
            let requiredScansPerSecond = targetThroughput / stats.AverageStatementsPerScan
            let requiredScanTime = 1000.0 / requiredScansPerSecond
            let performanceRatio = stats.AverageDuration / requiredScanTime
            
            {|
                RequiredScanTime = TimeSpan.FromMilliseconds(requiredScanTime)
                CurrentScanTime = TimeSpan.FromMilliseconds(stats.AverageDuration)
                PerformanceGapRatio = performanceRatio
                Feasible = performanceRatio <= 1.0
                Recommendation = 
                    if performanceRatio <= 1.0 then "Target achievable with current performance"
                    elif performanceRatio <= 2.0 then "Moderate optimization required"
                    else "Significant optimization required"
            |}
        else
            {|
                RequiredScanTime = TimeSpan.Zero
                CurrentScanTime = TimeSpan.Zero
                PerformanceGapRatio = Double.PositiveInfinity
                Feasible = false
                Recommendation = "Insufficient data for prediction"
            |}
