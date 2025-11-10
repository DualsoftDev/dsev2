namespace Ev2.Cpu.Perf.Core

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

/// 메트릭 수집기 인터페이스
type IMetricCollector =
    abstract member Name: string
    abstract member SupportedMetrics: Set<MetricType>
    abstract member CollectAsync: unit -> Task<MetricValue list>
    abstract member IsEnabled: bool with get, set

/// 메트릭 수집 관리자
type MetricsCollector(store: MetricStore, config: MonitoringConfig) =
    let collectors = List<IMetricCollector>()
    let mutable state = MonitoringState.Stopped
    let mutable cancellationTokenSource: CancellationTokenSource option = None
    let alertChecker = ThresholdChecker()
    let mutable alertQueue = Queue<PerfAlert>()
    let lockObj = obj()
    
    // 이벤트
    let alertRaised = Event<PerfAlert>()
    let stateChanged = Event<MonitoringState>()
    let metricsCollected = Event<MetricValue list>()
    
    member this.State = state
    member this.AlertRaised = alertRaised.Publish
    member this.StateChanged = stateChanged.Publish
    member this.MetricsCollected = metricsCollected.Publish
    
    member this.AddCollector(collector: IMetricCollector) =
        lock lockObj (fun () ->
            collectors.Add(collector)
        )
    
    member this.RemoveCollector(collectorName: string) =
        lock lockObj (fun () ->
            let toRemove = collectors |> Seq.tryFind (fun c -> c.Name = collectorName)
            match toRemove with
            | Some collector -> collectors.Remove(collector) |> ignore
            | None -> ()
        )
    
    member this.Start() =
        lock lockObj (fun () ->
            match state with
            | Stopped ->
                state <- Starting
                stateChanged.Trigger(state)
                
                cancellationTokenSource <- Some (new CancellationTokenSource())
                let token = cancellationTokenSource.Value.Token
                
                // 수집 태스크 시작
                Async.Start(this.CollectionLoop token, cancellationToken = token)
                
                state <- Running
                stateChanged.Trigger(state)
            | _ -> ()
        )
    
    member this.Stop() =
        lock lockObj (fun () ->
            match state with
            | Running | Paused ->
                state <- Stopping
                stateChanged.Trigger(state)
                
                cancellationTokenSource |> Option.iter (fun cts -> cts.Cancel())
                cancellationTokenSource <- None
                
                state <- Stopped
                stateChanged.Trigger(state)
            | _ -> ()
        )
    
    member this.Pause() =
        lock lockObj (fun () ->
            if state = Running then
                state <- Paused
                stateChanged.Trigger(state)
        )
    
    member this.Resume() =
        lock lockObj (fun () ->
            if state = Paused then
                state <- Running
                stateChanged.Trigger(state)
        )
    
    member this.GetAlerts() =
        lock lockObj (fun () ->
            let alerts = alertQueue.ToArray() |> Array.toList
            alertQueue.Clear()
            alerts
        )
    
    member this.GetCollectors() =
        lock lockObj (fun () ->
            collectors.ToArray() |> Array.toList
        )
    
    member private this.CollectionLoop(cancellationToken: CancellationToken) =
        async {
            while not cancellationToken.IsCancellationRequested do
                try
                    if state = Running then
                        let! metrics = this.CollectAllMetrics()
                        
                        // 메트릭 저장 (타입과 컴포넌트로 고유 키 생성)
                        for metric in metrics do
                            let key =
                                let typ = metric.Tags.TryFind "type" |> Option.defaultValue "unknown"
                                let comp = metric.Tags.TryFind "component" |> Option.defaultValue "default"
                                let step = metric.Tags.TryFind "step" |> Option.defaultValue ""
                                if String.IsNullOrEmpty step then
                                    sprintf "%s_%s" typ comp
                                else
                                    sprintf "%s_%s_step%s" typ comp step
                            store.Add(key, metric)
                        
                        // 임계값 검사
                        this.CheckThresholds(metrics)
                        
                        // 이벤트 발생
                        metricsCollected.Trigger(metrics)
                    
                    do! Async.Sleep(int config.SampleInterval.TotalMilliseconds)
                    
                with
                | :? OperationCanceledException -> ()
                | ex ->
                    state <- Error ex.Message
                    stateChanged.Trigger(state)
        }
    
    member private this.CollectAllMetrics() =
        async {
            let tasks = 
                collectors
                |> Seq.filter (fun c -> c.IsEnabled)
                |> Seq.map (fun c -> c.CollectAsync())
                |> Seq.toArray
            
            if tasks.Length = 0 then
                return []
            else
                let! results = Task.WhenAll(tasks) |> Async.AwaitTask
                return
                    results
                    |> Array.collect (fun metricList -> metricList |> List.toArray)
                    |> Array.toList
        }
    
    member private this.CheckThresholds(metrics: MetricValue list) =
        for metric in metrics do
            let metricTypeOpt = 
                match metric.Tags.TryFind "type" with
                | Some "cpu" -> Some CpuUsage
                | Some "memory" -> Some MemoryUsage
                | Some "scan" -> Some ScanTime
                | Some "execution" -> Some ExecutionCount
                | Some "error" -> Some ErrorRate
                | _ -> None
            
            match metricTypeOpt with
            | Some metricType ->
                match config.Thresholds.TryFind metricType with
                | Some threshold ->
                    match alertChecker.CheckThreshold(metric.Value, threshold) with
                    | Some severity ->
                        let thresholdValue = 
                            match severity with
                            | Critical -> threshold.Critical |> Option.defaultValue 0.0
                            | Warning -> threshold.Warning |> Option.defaultValue 0.0
                            | Info -> 0.0
                        
                        let alert = alertChecker.CreateAlert(
                            metricType.ToString(), 
                            metric.Value, 
                            thresholdValue, 
                            severity
                        )
                        
                        lock lockObj (fun () ->
                            alertQueue.Enqueue(alert)
                        )
                        
                        alertRaised.Trigger(alert)
                    | None -> ()
                | None -> ()
            | None -> ()

/// 기본 시스템 메트릭 수집기
type SystemMetricsCollector() =
    let mutable isEnabled = true
    
    interface IMetricCollector with
        member this.Name = "System Metrics"
        member this.SupportedMetrics = Set.ofList [CpuUsage; MemoryUsage]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                let tags = Map.empty
                
                [
                    // CPU 사용률 (간단한 근사치)
                    {
                        Timestamp = timestamp
                        Value = Environment.ProcessorCount |> float |> (*) 10.0 |> min 100.0
                        Unit = Percentage
                        Tags = tags |> Map.add "type" "cpu"
                    }
                    
                    // 메모리 사용량
                    {
                        Timestamp = timestamp
                        Value = GC.GetTotalMemory(false) |> float
                        Unit = Bytes
                        Tags = tags |> Map.add "type" "memory"
                    }
                ]
            )

/// 커스텀 메트릭 수집기
type CustomMetricsCollector(name: string, collectFunction: unit -> MetricValue list) =
    let mutable isEnabled = true
    
    interface IMetricCollector with
        member this.Name = name
        member this.SupportedMetrics = Set.empty // 커스텀이므로 모든 타입 허용
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(collectFunction)

/// 메트릭 내보내기
type MetricsExporter(store: MetricStore) =
    
    member this.ExportToCsv(filePath: string, metricNames: string list, startTime: DateTime, endTime: DateTime) =
        use writer = new System.IO.StreamWriter(filePath)

        // 헤더 작성 (3-column format: Timestamp, MetricName, Value)
        writer.WriteLine("Timestamp,MetricName,Value")
        
        // 시간 기준으로 정렬된 모든 데이터 포인트 수집
        let allData = 
            metricNames
            |> List.collect (fun name ->
                store.GetValuesInRange(name, startTime, endTime)
                |> List.map (fun v -> name, v)
            )
            |> List.sortBy (fun (_, v) -> v.Timestamp)
        
        // 데이터 작성
        for (metricName, value) in allData do
            let timestamp = value.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")
            writer.WriteLine($"{timestamp},{metricName},{value.Value}")
    
    member this.ExportToJson(filePath: string, metricNames: string list, startTime: DateTime, endTime: DateTime) =
        let data = 
            metricNames
            |> List.map (fun name ->
                let values = store.GetValuesInRange(name, startTime, endTime)
                name, values
            )
            |> Map.ofList
        
        // 간단한 JSON 형태로 직렬화 (실제 구현에서는 JSON 라이브러리 사용 권장)
        use writer = new System.IO.StreamWriter(filePath)
        writer.WriteLine("{")
        writer.WriteLine("  \"exportTime\": \"" + DateTime.UtcNow.ToString("o") + "\",")
        writer.WriteLine("  \"startTime\": \"" + startTime.ToString("o") + "\",")
        writer.WriteLine("  \"endTime\": \"" + endTime.ToString("o") + "\",")
        writer.WriteLine("  \"metrics\": {")
        
        let metricCount = data.Count
        let mutable index = 0
        
        for KeyValue(metricName, values) in data do
            writer.WriteLine($"    \"{metricName}\": [")
            let valueCount = List.length values
            values |> List.iteri (fun i value ->
                let comma = if i < valueCount - 1 then "," else ""
                writer.WriteLine($"      {{\"timestamp\": \"{value.Timestamp:o}\", \"value\": {value.Value}}}{comma}")
            )
            index <- index + 1
            let comma = if index < metricCount then "," else ""
            writer.WriteLine($"    ]{comma}")
        
        writer.WriteLine("  }")
        writer.WriteLine("}")
