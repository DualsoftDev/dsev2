namespace Ev2.Cpu.Runtime

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics

/// Phase 2.3: 성능 프로파일링 도구
module PerformanceProfiler =
    
    /// 성능 측정 항목
    type ProfileMetric = {
        Name: string
        mutable TotalTime: int64
        mutable CallCount: int64
        mutable MinTime: int64
        mutable MaxTime: int64
        mutable LastMeasurement: int64
    }
    
    /// 스캔 성능 통계
    type ScanPerformanceStats = {
        mutable ScanCount: int64
        mutable TotalScanTime: int64
        mutable MinScanTime: int64
        mutable MaxScanTime: int64
        mutable LastScanTime: int64
        mutable OverrunCount: int64
        mutable AverageScanTime: float
    }
    
    /// 메모리 사용량 통계
    type MemoryStats = {
        mutable AllocatedBytes: int64
        mutable Gen0Collections: int
        mutable Gen1Collections: int
        mutable Gen2Collections: int
        mutable LastGCTime: DateTime
    }
    
    /// 프로파일러 인스턴스
    type Profiler() =
        let metrics = ConcurrentDictionary<string, ProfileMetric>()
        let scanStats = { ScanCount = 0L; TotalScanTime = 0L; MinScanTime = Int64.MaxValue; 
                         MaxScanTime = 0L; LastScanTime = 0L; OverrunCount = 0L; AverageScanTime = 0.0 }
        let memoryStats = { AllocatedBytes = 0L; Gen0Collections = 0; Gen1Collections = 0; 
                           Gen2Collections = 0; LastGCTime = DateTime.MinValue }
        let stopwatch = Stopwatch.StartNew()
        
        member _.StartMeasurement(name: string) =
            Stopwatch.GetTimestamp()
        
        member _.EndMeasurement(name: string, startTime: int64) =
            let endTime = Stopwatch.GetTimestamp()
            let elapsed = endTime - startTime
            
            let metric = metrics.GetOrAdd(name, fun _ -> {
                Name = name
                TotalTime = 0L
                CallCount = 0L
                MinTime = Int64.MaxValue
                MaxTime = 0L
                LastMeasurement = 0L
            })
            
            lock metric (fun () ->
                metric.TotalTime <- metric.TotalTime + elapsed
                metric.CallCount <- metric.CallCount + 1L
                metric.MinTime <- min metric.MinTime elapsed
                metric.MaxTime <- max metric.MaxTime elapsed
                metric.LastMeasurement <- elapsed)
        
        member _.RecordScanTime(scanTimeMs: int64, targetCycleMs: int) =
            lock scanStats (fun () ->
                scanStats.ScanCount <- scanStats.ScanCount + 1L
                scanStats.TotalScanTime <- scanStats.TotalScanTime + scanTimeMs
                scanStats.MinScanTime <- min scanStats.MinScanTime scanTimeMs
                scanStats.MaxScanTime <- max scanStats.MaxScanTime scanTimeMs
                scanStats.LastScanTime <- scanTimeMs
                scanStats.AverageScanTime <- float scanStats.TotalScanTime / float scanStats.ScanCount
                
                if scanTimeMs > int64 targetCycleMs then
                    scanStats.OverrunCount <- scanStats.OverrunCount + 1L)
        
        member _.UpdateMemoryStats() =
            lock memoryStats (fun () ->
                memoryStats.AllocatedBytes <- GC.GetTotalMemory(false)
                memoryStats.Gen0Collections <- GC.CollectionCount(0)
                memoryStats.Gen1Collections <- GC.CollectionCount(1)
                memoryStats.Gen2Collections <- GC.CollectionCount(2)
                memoryStats.LastGCTime <- DateTime.Now)
        
        member _.GetMetrics() = 
            metrics.Values |> Seq.toArray
        
        member _.GetScanStats() = scanStats
        
        member _.GetMemoryStats() = 
            memoryStats
        
        member _.Reset() =
            metrics.Clear()
            scanStats.ScanCount <- 0L
            scanStats.TotalScanTime <- 0L
            scanStats.MinScanTime <- Int64.MaxValue
            scanStats.MaxScanTime <- 0L
            scanStats.OverrunCount <- 0L
            scanStats.AverageScanTime <- 0.0
        
        member _.GenerateReport() =
            let sb = System.Text.StringBuilder()
            
            sb.AppendLine("=== Performance Profiling Report ===") |> ignore
            sb.AppendLine(sprintf "Generated: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))) |> ignore
            sb.AppendLine() |> ignore
            
            // 스캔 성능
            sb.AppendLine("Scan Performance:") |> ignore
            sb.AppendLine(sprintf "  Total Scans: %d" scanStats.ScanCount) |> ignore
            sb.AppendLine(sprintf "  Average Time: %.2f ms" scanStats.AverageScanTime) |> ignore
            sb.AppendLine(sprintf "  Min Time: %d ms" scanStats.MinScanTime) |> ignore
            sb.AppendLine(sprintf "  Max Time: %d ms" scanStats.MaxScanTime) |> ignore
            sb.AppendLine(sprintf "  Last Time: %d ms" scanStats.LastScanTime) |> ignore
            sb.AppendLine(sprintf "  Overruns: %d" scanStats.OverrunCount) |> ignore
            sb.AppendLine() |> ignore
            
            // 메모리 통계
            sb.AppendLine("Memory Statistics:") |> ignore
            sb.AppendLine(sprintf "  Allocated: %d KB" (memoryStats.AllocatedBytes / 1024L)) |> ignore
            sb.AppendLine(sprintf "  Gen 0 GC: %d" memoryStats.Gen0Collections) |> ignore
            sb.AppendLine(sprintf "  Gen 1 GC: %d" memoryStats.Gen1Collections) |> ignore
            sb.AppendLine(sprintf "  Gen 2 GC: %d" memoryStats.Gen2Collections) |> ignore
            sb.AppendLine() |> ignore
            
            // 함수별 성능
            sb.AppendLine("Function Performance:") |> ignore
            let sortedMetrics = metrics.Values |> Seq.sortByDescending (fun m -> m.TotalTime)
            for metric in sortedMetrics do
                let avgTime = if metric.CallCount > 0L then float metric.TotalTime / float metric.CallCount else 0.0
                sb.AppendLine(sprintf "  %s:" metric.Name) |> ignore
                sb.AppendLine(sprintf "    Calls: %d" metric.CallCount) |> ignore
                sb.AppendLine(sprintf "    Total: %d ticks" metric.TotalTime) |> ignore
                sb.AppendLine(sprintf "    Average: %.2f ticks" avgTime) |> ignore
                sb.AppendLine(sprintf "    Min: %d ticks" metric.MinTime) |> ignore
                sb.AppendLine(sprintf "    Max: %d ticks" metric.MaxTime) |> ignore
            
            sb.ToString()
    
    /// 측정 도우미 함수
    let inline measureFunction (profiler: Profiler) (name: string) (func: unit -> 'T) : 'T =
        let startTime = profiler.StartMeasurement(name)
        try
            func()
        finally
            profiler.EndMeasurement(name, startTime)
    
    /// 전역 프로파일러 인스턴스
    let globalProfiler = Profiler()
    
    /// 편의 함수들
    let startMeasurement name = globalProfiler.StartMeasurement(name)
    let endMeasurement name startTime = globalProfiler.EndMeasurement(name, startTime)
    let recordScanTime timeMs cycleMs = globalProfiler.RecordScanTime(timeMs, cycleMs)
    let updateMemoryStats() = globalProfiler.UpdateMemoryStats()
    let generateReport() = globalProfiler.GenerateReport()
    let reset() = globalProfiler.Reset()
    
    /// 측정 매크로
    let inline measure name func = measureFunction globalProfiler name func