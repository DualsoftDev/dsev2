namespace Ev2.Cpu.Perf.Monitoring

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Collections.Generic
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime
open Ev2.Cpu.Perf.Core

/// 메모리 사용량 모니터
type MemoryUsageMonitor() =
    let mutable isEnabled = true
    let mutable currentProcess = Process.GetCurrentProcess()
    
    interface IMetricCollector with
        member this.Name = "Memory Usage Monitor"
        member this.SupportedMetrics = Set.ofList [MemoryUsage]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // .NET GC 메모리 정보
                let totalMemory = GC.GetTotalMemory(false)
                let gen0Collections = GC.CollectionCount(0)
                let gen1Collections = GC.CollectionCount(1)
                let gen2Collections = GC.CollectionCount(2)
                
                // 프로세스 메모리 정보
                currentProcess.Refresh()
                let workingSet = currentProcess.WorkingSet64
                let privateMemory = currentProcess.PrivateMemorySize64
                let virtualMemory = currentProcess.VirtualMemorySize64
                
                [
                    {
                        Timestamp = timestamp
                        Value = float totalMemory
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "gc_total"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float workingSet
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "working_set"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float privateMemory
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "private"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float virtualMemory
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "virtual"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gen0Collections
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen0"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gen1Collections
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen1"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gen2Collections
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen2"]
                    }
                ]
            )

/// CPU 실행 컨텍스트 메모리 모니터
type ExecutionContextMemoryMonitor(context: ExecutionContext) =
    let mutable isEnabled = true
    let mutable lastGcCount = [GC.CollectionCount(0); GC.CollectionCount(1); GC.CollectionCount(2)]
    
    interface IMetricCollector with
        member this.Name = "Execution Context Memory Monitor"
        member this.SupportedMetrics = Set.ofList [MemoryUsage; ExecutionCount]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // 컨텍스트 메모리 통계
                let memStats = context.Memory.Stats()
                
                // GC 수집 횟수 변화
                let currentGcCount = [GC.CollectionCount(0); GC.CollectionCount(1); GC.CollectionCount(2)]
                let gcDeltas = List.map2 (-) currentGcCount lastGcCount
                lastGcCount <- currentGcCount
                
                [
                    {
                        Timestamp = timestamp
                        Value = float memStats.Total
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "context_variables"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float memStats.HistorySize
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "history_size"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float memStats.ScanCount
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "scan_count"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gcDeltas.[0]
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen0_delta"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gcDeltas.[1]
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen1_delta"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float gcDeltas.[2]
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "gc_gen2_delta"]
                    }
                ]
            )

/// 메모리 리크 감지기
type MemoryLeakDetector() =
    let mutable isEnabled = true
    let memorySnapshots = Queue<int64 * DateTime>()
    let maxSnapshots = 20
    let leakThreshold = 1.5 // 50% 증가를 리크로 판단
    
    interface IMetricCollector with
        member this.Name = "Memory Leak Detector"
        member this.SupportedMetrics = Set.ofList [MemoryUsage]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                let currentMemory = GC.GetTotalMemory(false)
                
                // 메모리 스냅샷 관리
                if memorySnapshots.Count >= maxSnapshots then
                    memorySnapshots.Dequeue() |> ignore
                memorySnapshots.Enqueue((currentMemory, timestamp))
                
                // 리크 감지
                let leakDetected, growthRate = 
                    if memorySnapshots.Count >= 5 then
                        let snapshots = memorySnapshots.ToArray()
                        let oldestMemory = fst snapshots.[0]
                        let newestMemory = fst snapshots.[snapshots.Length - 1]
                        
                        if oldestMemory > 0L then
                            let growth = float newestMemory / float oldestMemory
                            growth > leakThreshold, growth
                        else
                            false, 1.0
                    else
                        false, 1.0
                
                // 메모리 압력 계산
                let memoryPressure = 
                    let totalMemory = float currentMemory
                    let availableMemory = 1024.0 * 1024.0 * 1024.0 * 4.0 // 4GB 가정
                    (totalMemory / availableMemory) * 100.0
                
                [
                    {
                        Timestamp = timestamp
                        Value = float currentMemory
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "current_usage"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = growthRate
                        Unit = Custom "ratio"
                        Tags = Map.ofList ["type", "memory"; "component", "growth_rate"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = if leakDetected then 1.0 else 0.0
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "leak_detected"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = memoryPressure
                        Unit = Percentage
                        Tags = Map.ofList ["type", "memory"; "component", "memory_pressure"]
                    }
                ]
            )

/// 메모리 할당 추적기
type MemoryAllocationTracker() =
    let mutable isEnabled = true
    let allocationEvents = Queue<AllocationEvent>()
    let maxEvents = 1000
    
    interface IMetricCollector with
        member this.Name = "Memory Allocation Tracker"
        member this.SupportedMetrics = Set.ofList [MemoryUsage; ThroughputRate]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                // 최근 할당 이벤트 분석
                let recentEvents = 
                    allocationEvents
                    |> Seq.filter (fun e -> e.Timestamp > timestamp.AddMinutes(-1.0))
                    |> Seq.toList
                
                let totalAllocations = List.length recentEvents
                let totalSize = recentEvents |> List.sumBy (fun e -> e.Size)
                let averageSize = if totalAllocations > 0 then totalSize / int64 totalAllocations else 0L
                
                // 할당률 계산
                let allocationRate = float totalAllocations / 60.0 // per second
                
                [
                    {
                        Timestamp = timestamp
                        Value = float totalAllocations
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "allocations_per_minute"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float totalSize
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "allocated_bytes_per_minute"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float averageSize
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "average_allocation_size"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = allocationRate
                        Unit = Rate
                        Tags = Map.ofList ["type", "memory"; "component", "allocation_rate"]
                    }
                ]
            )
    
    member this.TrackAllocation(size: int64) =
        let event = {
            Timestamp = DateTime.UtcNow
            Size = size
            Type = "allocation"
        }
        
        if allocationEvents.Count >= maxEvents then
            allocationEvents.Dequeue() |> ignore
        allocationEvents.Enqueue(event)

and AllocationEvent = {
    Timestamp: DateTime
    Size: int64
    Type: string
}

/// 메모리 캐시 모니터
type MemoryCacheMonitor() =
    let mutable isEnabled = true
    let mutable cacheStats = {| Hits = 0L; Misses = 0L; Size = 0L |}
    
    interface IMetricCollector with
        member this.Name = "Memory Cache Monitor"
        member this.SupportedMetrics = Set.ofList [MemoryUsage; ThroughputRate]
        member this.IsEnabled 
            with get() = isEnabled
            and set(value) = isEnabled <- value
        
        member this.CollectAsync() =
            Task.Run(fun () ->
                let timestamp = DateTime.UtcNow
                
                let total = cacheStats.Hits + cacheStats.Misses
                let hitRate = 
                    if total > 0L then float cacheStats.Hits / float total * 100.0
                    else 0.0
                
                [
                    {
                        Timestamp = timestamp
                        Value = float cacheStats.Size
                        Unit = Bytes
                        Tags = Map.ofList ["type", "memory"; "component", "cache_size"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float cacheStats.Hits
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "cache_hits"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = float cacheStats.Misses
                        Unit = Count
                        Tags = Map.ofList ["type", "memory"; "component", "cache_misses"]
                    }
                    
                    {
                        Timestamp = timestamp
                        Value = hitRate
                        Unit = Percentage
                        Tags = Map.ofList ["type", "memory"; "component", "cache_hit_rate"]
                    }
                ]
            )
    
    member this.RecordCacheHit() =
        cacheStats <- {| cacheStats with Hits = cacheStats.Hits + 1L |}
    
    member this.RecordCacheMiss() =
        cacheStats <- {| cacheStats with Misses = cacheStats.Misses + 1L |}
    
    member this.UpdateCacheSize(size: int64) =
        cacheStats <- {| cacheStats with Size = size |}