namespace Ev2.Cpu.Perf.Core

open System
open System.Collections.Generic
open System.Collections.Concurrent

/// 메트릭 계산 유틸리티
module MetricCalculations =
    
    /// 평균 계산
    let average (values: float list) =
        if List.isEmpty values then 0.0
        else List.average values
    
    /// 표준편차 계산
    let standardDeviation (values: float list) =
        if List.length values < 2 then 0.0
        else
            let avg = average values
            let variance = values |> List.map (fun x -> (x - avg) ** 2.0) |> average
            sqrt variance
    
    /// 백분위수 계산
    let percentile (values: float list) (p: float) =
        if List.isEmpty values then 0.0
        else
            let sorted = List.sort values
            let index = (float (List.length sorted - 1)) * p / 100.0
            let lower = int (floor index)
            let upper = int (ceil index)
            if lower = upper then sorted.[lower]
            else
                let weight = index - float lower
                sorted.[lower] * (1.0 - weight) + sorted.[upper] * weight
    
    /// 이동 평균 계산
    let movingAverage (windowSize: int) (values: float list) =
        values
        |> List.windowed windowSize
        |> List.map average
    
    /// 변화율 계산
    let changeRate (oldValue: float) (newValue: float) =
        if oldValue = 0.0 then
            if newValue = 0.0 then 0.0 else Double.PositiveInfinity
        else
            (newValue - oldValue) / oldValue * 100.0

/// 메트릭 저장소
type MetricStore() =
    let data = ConcurrentDictionary<string, Queue<MetricValue>>()
    let maxSize = 10000
    
    member this.Add(metricName: string, value: MetricValue) =
        let queue = data.GetOrAdd(metricName, fun _ -> Queue<MetricValue>())
        lock queue (fun () ->
            queue.Enqueue(value)
            while queue.Count > maxSize do
                queue.Dequeue() |> ignore
        )
    
    member this.GetValues(metricName: string) =
        match data.TryGetValue(metricName) with
        | true, queue ->
            lock queue (fun () -> queue.ToArray() |> Array.toList)
        | false, _ -> []
    
    member this.GetLatest(metricName: string) =
        match data.TryGetValue(metricName) with
        | true, queue ->
            lock queue (fun () ->
                if queue.Count > 0 then
                    Some (queue.ToArray() |> Array.last)
                else None
            )
        | false, _ -> None
    
    member this.GetValuesInRange(metricName: string, startTime: DateTime, endTime: DateTime) =
        data.TryGetValue(metricName)
        |> function
            | true, queue ->
                lock queue (fun () ->
                    queue.ToArray()
                    |> Array.filter (fun v -> v.Timestamp >= startTime && v.Timestamp <= endTime)
                    |> Array.toList
                )
            | false, _ -> []
    
    member this.Clear(metricName: string option) =
        match metricName with
        | Some name ->
            match data.TryGetValue(name) with
            | true, queue -> lock queue (fun () -> queue.Clear())
            | false, _ -> ()
        | None ->
            for KeyValue(_, queue) in data do
                lock queue (fun () -> queue.Clear())
    
    member this.GetMetricNames() =
        data.Keys |> Seq.toList
    
    member this.GetCount(metricName: string) =
        match data.TryGetValue(metricName) with
        | true, queue -> lock queue (fun () -> queue.Count)
        | false, _ -> 0

/// 메트릭 집계기
type MetricAggregator(store: MetricStore) =
    
    member this.CreateMeasurement(definition: MetricDefinition, startTime: DateTime, endTime: DateTime) =
        let values = store.GetValuesInRange(definition.Name, startTime, endTime)
        let floatValues = values |> List.map (fun v -> v.Value)
        
        if List.isEmpty floatValues then
            {
                Definition = definition
                Values = values
                StartTime = startTime
                EndTime = endTime
                SampleCount = 0
                Average = 0.0
                Minimum = 0.0
                Maximum = 0.0
                StandardDeviation = 0.0
            }
        else
            {
                Definition = definition
                Values = values
                StartTime = startTime
                EndTime = endTime
                SampleCount = List.length values
                Average = MetricCalculations.average floatValues
                Minimum = List.min floatValues
                Maximum = List.max floatValues
                StandardDeviation = MetricCalculations.standardDeviation floatValues
            }
    
    member this.GetStatistics(metricNames: string list, duration: TimeSpan) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        
        let measurements = 
            metricNames
            |> List.choose (fun name ->
                let values = store.GetValuesInRange(name, startTime, endTime)
                if List.isEmpty values then None
                else Some (name, values)
            )
        
        let totalSamples = measurements |> List.sumBy (fun (_, values) -> List.length values) |> int64
        let runtime = endTime - startTime
        
        // CPU 사용률 평균
        let avgCpuUsage = 
            measurements
            |> List.tryFind (fun (name, _) -> name.Contains("CPU"))
            |> Option.map (fun (_, values) -> values |> List.map (fun v -> v.Value) |> MetricCalculations.average)
            |> Option.defaultValue 0.0
        
        // 메모리 사용량 피크
        let peakMemory = 
            measurements
            |> List.tryFind (fun (name, _) -> name.Contains("Memory"))
            |> Option.map (fun (_, values) -> values |> List.map (fun v -> v.Value) |> List.max)
            |> Option.defaultValue 0.0 |> int64
        
        // 스캔 시간 통계
        let scanTimes = 
            measurements
            |> List.tryFind (fun (name, _) -> name.Contains("Scan"))
            |> Option.map (fun (_, values) -> values |> List.map (fun v -> v.Value))
            |> Option.defaultValue []
        
        let avgScanTime = 
            if List.isEmpty scanTimes then TimeSpan.Zero
            else TimeSpan.FromMilliseconds(MetricCalculations.average scanTimes)
        
        let maxScanTime = 
            if List.isEmpty scanTimes then TimeSpan.Zero
            else TimeSpan.FromMilliseconds(List.max scanTimes)
        
        {
            TotalSamples = totalSamples
            TotalRuntime = runtime
            AverageCpuUsage = avgCpuUsage
            PeakMemoryUsage = peakMemory
            AverageScanTime = avgScanTime
            MaxScanTime = maxScanTime
            TotalStatements = 0L // 별도로 추적 필요
            StatementsPerSecond = 0.0 // 계산 필요
            ErrorCount = 0L // 별도로 추적 필요
            AlertCount = 0 // 별도로 추적 필요
        }
    
    member this.GetTrend(metricName: string, duration: TimeSpan, windowSize: int) =
        let endTime = DateTime.UtcNow
        let startTime = endTime.Subtract(duration)
        let values = store.GetValuesInRange(metricName, startTime, endTime)
        let floatValues = values |> List.map (fun v -> v.Value)
        
        if List.length floatValues >= windowSize then
            MetricCalculations.movingAverage windowSize floatValues
        else
            floatValues

/// 임계값 검사기
type ThresholdChecker() =
    
    member this.CheckThreshold(value: float, threshold: Threshold) : AlertSeverity option =
        match threshold.Critical, threshold.Warning with
        | Some critical, _ when value >= critical -> Some Critical
        | _, Some warning when value >= warning -> Some Warning
        | _ -> None
    
    member this.CreateAlert(metricName: string, value: float, threshold: float, severity: AlertSeverity) : PerfAlert =
        {
            MetricName = metricName
            Threshold = threshold
            CurrentValue = value
            Severity = severity
            Timestamp = DateTime.UtcNow
            Message = $"{metricName} value {value:F2} exceeded {severity} threshold {threshold:F2}"
        }

/// 메트릭 포매터
module MetricFormatter =
    
    let formatValue (value: float) (unit: MetricUnit) =
        match unit with
        | Percentage -> $"{value:F1}%%"
        | Milliseconds -> $"{value:F2} ms"
        | Microseconds -> $"{value:F2} μs"
        | Bytes when value >= 1024.0 * 1024.0 * 1024.0 -> $"{value / (1024.0 * 1024.0 * 1024.0):F2} GB"
        | Bytes when value >= 1024.0 * 1024.0 -> $"{value / (1024.0 * 1024.0):F2} MB"
        | Bytes when value >= 1024.0 -> $"{value / 1024.0:F2} KB"
        | Bytes -> $"{value:F0} B"
        | Kilobytes -> $"{value:F2} KB"
        | Megabytes -> $"{value:F2} MB"
        | Count -> $"{value:F0}"
        | Rate -> $"{value:F2}/sec"
        | Custom unit -> $"{value:F2} {unit}"
    
    let formatDuration (duration: TimeSpan) =
        if duration.TotalMilliseconds < 1.0 then
            $"{duration.TotalMicroseconds:F2} μs"
        elif duration.TotalSeconds < 1.0 then
            $"{duration.TotalMilliseconds:F2} ms"
        elif duration.TotalMinutes < 1.0 then
            $"{duration.TotalSeconds:F2} s"
        else
            $"{duration.TotalMinutes:F2} min"
    
    let formatTimestamp (timestamp: DateTime) =
        timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")