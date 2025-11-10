namespace Ev2.Cpu.Perf.Monitoring

open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime
open Ev2.Cpu.Perf.Core

/// CPU 성능 모니터
type CpuPerformanceMonitor() =
    let mutable currentProcess = Process.GetCurrentProcess()
    let mutable lastCpuTime = TimeSpan.Zero
    let mutable lastMeasureTime = DateTime.UtcNow
    let mutable isEnabled = true
    
    // CPU 코어 개수
    let coreCount = Environment.ProcessorCount
    
    interface IMetricCollector with
        member this.Name = "CPU Performance Monitor"
        member this.SupportedMetrics = Set.ofList [CpuUsage; ThroughputRate; ResponseTime]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                let currentCpuTime = currentProcess.TotalProcessorTime
                let timeDelta = timestamp - lastMeasureTime
                
                let cpuUsage = 
                    if timeDelta.TotalMilliseconds > 0.0 then
                        let cpuDelta = currentCpuTime - lastCpuTime
                        Math.Min(100.0, cpuDelta.TotalMilliseconds / timeDelta.TotalMilliseconds / float coreCount * 100.0)
                    else 0.0
                
                lastCpuTime <- currentCpuTime
                lastMeasureTime <- timestamp
                
                [
                    {
                        Timestamp = timestamp
                        Value = cpuUsage
                        Unit = Percentage
                        Tags = Map.ofList ["type", "cpu"; "component", "process"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float coreCount
                        Unit = Count
                        Tags = Map.ofList ["type", "cpu"; "component", "cores"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = currentProcess.Threads.Count |> float
                        Unit = Count
                        Tags = Map.ofList ["type", "cpu"; "component", "threads"]
                    }
                ]
            )

/// CPU 실행 컨텍스트 모니터
type CpuExecutionMonitor(context: ExecutionContext) =
    let mutable isEnabled = true
    let mutable lastExecutionCount = 0L
    let mutable lastMeasureTime = DateTime.UtcNow
    
    interface IMetricCollector with
        member this.Name = "CPU Execution Monitor"
        member this.SupportedMetrics = Set.ofList [ExecutionCount; ThroughputRate]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                let timeDelta = timestamp - lastMeasureTime
                
                // 메모리 상태에서 실행 통계 추출
                let memStats = context.Memory.Stats()
                let currentExecutionCount = int64 memStats.ScanCount
                
                let executionRate = 
                    if timeDelta.TotalSeconds > 0.0 then
                        float (currentExecutionCount - lastExecutionCount) / timeDelta.TotalSeconds
                    else 0.0
                
                lastExecutionCount <- currentExecutionCount
                lastMeasureTime <- timestamp
                
                [
                    {
                        Timestamp = timestamp
                        Value = float currentExecutionCount
                        Unit = Count
                        Tags = Map.ofList ["type", "execution"; "component", "total_scans"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = executionRate
                        Unit = Rate
                        Tags = Map.ofList ["type", "execution"; "component", "scans_per_second"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float memStats.Total
                        Unit = Count
                        Tags = Map.ofList ["type", "execution"; "component", "total_variables"]
                    }
                ]
            )

/// CPU 스캔 성능 모니터
type CpuScanMonitor(scanEngine: CpuScanEngine option) =
    let mutable isEnabled = true
    let mutable scanTimes = Collections.Generic.Queue<float>()
    let maxScanHistory = 100
    
    interface IMetricCollector with
        member this.Name = "CPU Scan Monitor"
        member this.SupportedMetrics = Set.ofList [ScanTime; ThroughputRate; ResponseTime]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                match scanEngine with
                | Some engine ->
                    // 단일 스캔 수행 및 시간 측정
                    let stopwatch = Stopwatch.StartNew()
                    let scanTime = engine.ScanOnce()
                    stopwatch.Stop()
                    
                    // 스캔 시간 히스토리 관리
                    if scanTimes.Count >= maxScanHistory then
                        scanTimes.Dequeue() |> ignore
                    scanTimes.Enqueue(float scanTime)
                    
                    // 통계 계산
                    let scanTimesArray = scanTimes.ToArray()
                    let avgScanTime = if scanTimesArray.Length > 0 then Array.average scanTimesArray else 0.0
                    let maxScanTime = if scanTimesArray.Length > 0 then Array.max scanTimesArray else 0.0
                    let minScanTime = if scanTimesArray.Length > 0 then Array.min scanTimesArray else 0.0
                    
                    [
                        {
                            Timestamp = timestamp
                            Value = float scanTime
                            Unit = Milliseconds
                            Tags = Map.ofList ["type", "scan"; "component", "current_time"]
                        }
                        
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
                    ]
                
                | None ->
                    // 스캔 엔진이 없는 경우 기본 메트릭
                    [
                        {
                            Timestamp = timestamp
                            Value = 0.0
                            Unit = Milliseconds
                            Tags = Map.ofList ["type", "scan"; "component", "no_engine"]
                        }
                    ]
            )

/// CPU 로드 시뮬레이터
type CpuLoadSimulator(intensity: float) as this =
    let mutable isEnabled = true
    let mutable isRunning = false
    let cancellationTokenSource = new CancellationTokenSource()
    
    do
        if intensity > 0.0 then
            Task.Run(System.Action(fun () -> this.SimulateLoad(cancellationTokenSource.Token))) |> ignore
    
    interface IMetricCollector with
        member this.Name = "CPU Load Simulator"
        member this.SupportedMetrics = Set.ofList [CpuUsage; ThroughputRate]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                [
                    {
                        Timestamp = timestamp
                        Value = if isRunning then intensity else 0.0
                        Unit = Percentage
                        Tags = Map.ofList ["type", "cpu"; "component", "simulated_load"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = if isRunning then 1.0 else 0.0
                        Unit = Count
                        Tags = Map.ofList ["type", "cpu"; "component", "simulator_active"]
                    }
                ]
            )
    
    member private this.SimulateLoad(cancellationToken: CancellationToken) =
        isRunning <- true
        
        while not cancellationToken.IsCancellationRequested do
            // CPU 부하 생성 (간단한 계산 작업)
            let loadDuration = int (intensity * 10.0) // 밀리초
            let idleDuration = int ((100.0 - intensity) * 10.0)
            
            if loadDuration > 0 then
                let endTime = DateTime.UtcNow.AddMilliseconds(float loadDuration)
                while DateTime.UtcNow < endTime && not cancellationToken.IsCancellationRequested do
                    // 무의미한 계산으로 CPU 사용
                    Math.Sin(float DateTime.UtcNow.Ticks) |> ignore
            
            if idleDuration > 0 && not cancellationToken.IsCancellationRequested then
                Thread.Sleep(idleDuration)
        
        isRunning <- false
    
    interface IDisposable with
        member this.Dispose() =
            cancellationTokenSource.Cancel()
            cancellationTokenSource.Dispose()

/// CPU 성능 프로파일러
type CpuProfiler() =
    let profileData = Collections.Concurrent.ConcurrentDictionary<string, Collections.Generic.List<float>>()
    let mutable isEnabled = true
    
    interface IMetricCollector with
        member this.Name = "CPU Profiler"
        member this.SupportedMetrics = Set.ofList [ResponseTime; ThroughputRate; ExecutionCount]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // 프로파일 데이터에서 통계 생성
                let results = Collections.Generic.List<MetricValue>()
                
                for KeyValue(operation, times) in profileData do
                    let timesArray = times.ToArray()
                    if timesArray.Length > 0 then
                        let avgTime = Array.average timesArray
                        let maxTime = Array.max timesArray
                        let minTime = Array.min timesArray
                        let count = float timesArray.Length
                        
                        results.Add({
                            Timestamp = timestamp
                            Value = avgTime
                            Unit = Milliseconds
                            Tags = Map.ofList ["type", "profile"; "operation", operation; "metric", "avg_time"]
                        })
                        
                        results.Add({
                            Timestamp = timestamp
                            Value = count
                            Unit = Count
                            Tags = Map.ofList ["type", "profile"; "operation", operation; "metric", "count"]
                        })
                    
                    // 데이터 초기화 (메모리 사용량 제한)
                    times.Clear()
                
                results |> Seq.toList
            )
    
    member this.ProfileOperation<'T>(operationName: string, operation: unit -> 'T) : 'T =
        let stopwatch = Stopwatch.StartNew()
        let result = operation()
        stopwatch.Stop()
        
        let times = profileData.GetOrAdd(operationName, fun _ -> Collections.Generic.List<float>())
        times.Add(stopwatch.Elapsed.TotalMilliseconds)
        
        result
    
    member this.GetProfileSummary() =
        profileData
        |> Seq.map (fun kvp ->
            let times = kvp.Value.ToArray()
            if times.Length > 0 then
                Some (kvp.Key, {|
                    Count = times.Length
                    AverageTime = Array.average times
                    MinTime = Array.min times
                    MaxTime = Array.max times
                    TotalTime = Array.sum times
                |})
            else None
        )
        |> Seq.choose id
        |> Seq.toList