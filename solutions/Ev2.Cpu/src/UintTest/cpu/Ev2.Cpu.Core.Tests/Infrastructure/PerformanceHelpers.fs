namespace Ev2.Cpu.Tests.Infrastructure

open System
open System.Diagnostics
open System.Collections.Generic

// ═══════════════════════════════════════════════════════════════════════
// Performance Helpers Module - 성능 측정 및 벤치마킹 유틸리티
// ═══════════════════════════════════════════════════════════════════════
// Phase 1: 기반 인프라
// 실행 시간, 메모리 사용량, 처리량 측정을 위한 유틸리티
// ═══════════════════════════════════════════════════════════════════════

module PerformanceHelpers =

    // ───────────────────────────────────────────────────────────────────
    // Timing Measurements
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Result of a timing measurement</summary>
    type TimingResult<'T> = {
        Result: 'T
        ElapsedMs: float
        ElapsedTicks: int64
    }

    /// <summary>Measure execution time of an action</summary>
    /// <returns>Result and elapsed time in milliseconds</returns>
    let measureTime (action: unit -> 'T) : TimingResult<'T> =
        let sw = Stopwatch.StartNew()
        let result = action()
        sw.Stop()
        {
            Result = result
            ElapsedMs = sw.Elapsed.TotalMilliseconds
            ElapsedTicks = sw.ElapsedTicks
        }

    /// <summary>Measure execution time with higher precision (ticks)</summary>
    let measureTimePrecise (action: unit -> 'T) : 'T * int64 =
        let start = Stopwatch.GetTimestamp()
        let result = action()
        let elapsed = Stopwatch.GetTimestamp() - start
        (result, elapsed)

    /// <summary>Measure execution time and print to console</summary>
    let measureAndPrint (label: string) (action: unit -> 'T) : 'T =
        printfn "Starting: %s" label
        let sw = Stopwatch.StartNew()
        let result = action()
        sw.Stop()
        printfn "Completed: %s in %.3f ms" label sw.Elapsed.TotalMilliseconds
        result

    // ───────────────────────────────────────────────────────────────────
    // Benchmarking
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Benchmark statistics</summary>
    type BenchmarkStats = {
        Iterations: int
        TotalMs: float
        MinMs: float
        MaxMs: float
        MeanMs: float
        MedianMs: float
        StdDevMs: float
        Percentile95Ms: float
        Percentile99Ms: float
    }

    /// <summary>Calculate statistics from timing samples</summary>
    let private calculateStats (samples: float list) (iterations: int) : BenchmarkStats =
        let sorted = samples |> List.sort
        let total = List.sum sorted
        let mean = total / float sorted.Length
        let variance =
            sorted
            |> List.map (fun x -> (x - mean) ** 2.0)
            |> List.average
        let stdDev = sqrt variance

        let percentile p =
            let index = int (float sorted.Length * p) - 1
            let index = max 0 (min (sorted.Length - 1) index)
            sorted.[index]

        {
            Iterations = iterations
            TotalMs = total
            MinMs = List.head sorted
            MaxMs = List.last sorted
            MeanMs = mean
            MedianMs = percentile 0.5
            StdDevMs = stdDev
            Percentile95Ms = percentile 0.95
            Percentile99Ms = percentile 0.99
        }

    /// <summary>Run benchmark with specified iterations</summary>
    /// <param name="iterations">Number of times to run the action</param>
    /// <param name="action">Action to benchmark</param>
    let benchmark (iterations: int) (action: unit -> 'T) : BenchmarkStats =
        // Warmup run (not measured)
        action() |> ignore

        // Collect timing samples
        let samples =
            List.init iterations (fun _ ->
                let sw = Stopwatch.StartNew()
                action() |> ignore
                sw.Stop()
                sw.Elapsed.TotalMilliseconds)

        calculateStats samples iterations

    /// <summary>Run benchmark and print results</summary>
    let benchmarkAndPrint (label: string) (iterations: int) (action: unit -> 'T) : BenchmarkStats =
        printfn "Benchmarking: %s (%d iterations)" label iterations
        let stats = benchmark iterations action
        printfn "  Mean:   %.3f ms" stats.MeanMs
        printfn "  Median: %.3f ms" stats.MedianMs
        printfn "  Min:    %.3f ms" stats.MinMs
        printfn "  Max:    %.3f ms" stats.MaxMs
        printfn "  StdDev: %.3f ms" stats.StdDevMs
        printfn "  95%%:    %.3f ms" stats.Percentile95Ms
        printfn "  99%%:    %.3f ms" stats.Percentile99Ms
        stats

    /// <summary>Compare two implementations and return which is faster</summary>
    let comparePerformance
        (iterations: int)
        (name1: string)
        (action1: unit -> 'T)
        (name2: string)
        (action2: unit -> 'T) : string * BenchmarkStats * BenchmarkStats =

        printfn "Comparing: '%s' vs '%s'" name1 name2
        let stats1 = benchmark iterations action1
        let stats2 = benchmark iterations action2

        printfn "  %s: %.3f ms (mean)" name1 stats1.MeanMs
        printfn "  %s: %.3f ms (mean)" name2 stats2.MeanMs

        let faster, ratio =
            if stats1.MeanMs < stats2.MeanMs then
                name1, stats2.MeanMs / stats1.MeanMs
            else
                name2, stats1.MeanMs / stats2.MeanMs

        printfn "  Winner: %s (%.2fx faster)" faster ratio
        (faster, stats1, stats2)

    // ───────────────────────────────────────────────────────────────────
    // Throughput Measurements
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Throughput measurement result</summary>
    type ThroughputResult = {
        ItemsProcessed: int
        ElapsedMs: float
        ItemsPerSecond: float
        MsPerItem: float
    }

    /// <summary>Measure throughput (items per second)</summary>
    /// <param name="itemCount">Number of items to process</param>
    /// <param name="action">Action that processes N items</param>
    let measureThroughput (itemCount: int) (action: unit -> unit) : ThroughputResult =
        let sw = Stopwatch.StartNew()
        action()
        sw.Stop()
        let elapsedMs = sw.Elapsed.TotalMilliseconds
        {
            ItemsProcessed = itemCount
            ElapsedMs = elapsedMs
            ItemsPerSecond = float itemCount / (elapsedMs / 1000.0)
            MsPerItem = elapsedMs / float itemCount
        }

    /// <summary>Measure throughput and print results</summary>
    let measureThroughputAndPrint (label: string) (itemCount: int) (action: unit -> unit) : ThroughputResult =
        printfn "Measuring throughput: %s (%d items)" label itemCount
        let result = measureThroughput itemCount action
        printfn "  Elapsed:  %.3f ms" result.ElapsedMs
        printfn "  Rate:     %.0f items/sec" result.ItemsPerSecond
        printfn "  Per item: %.6f ms" result.MsPerItem
        result

    // ───────────────────────────────────────────────────────────────────
    // Memory Measurements
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Memory measurement result</summary>
    type MemoryResult<'T> = {
        Result: 'T
        AllocatedBytes: int64
        Gen0Collections: int
        Gen1Collections: int
        Gen2Collections: int
    }

    /// <summary>Measure memory allocations during action execution</summary>
    let measureMemory (action: unit -> 'T) : MemoryResult<'T> =
        // Force garbage collection before measurement
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()

        let gen0Before = GC.CollectionCount(0)
        let gen1Before = GC.CollectionCount(1)
        let gen2Before = GC.CollectionCount(2)
        let memBefore = GC.GetTotalMemory(false)

        let result = action()

        let memAfter = GC.GetTotalMemory(false)
        let gen0After = GC.CollectionCount(0)
        let gen1After = GC.CollectionCount(1)
        let gen2After = GC.CollectionCount(2)

        {
            Result = result
            AllocatedBytes = memAfter - memBefore
            Gen0Collections = gen0After - gen0Before
            Gen1Collections = gen1After - gen1Before
            Gen2Collections = gen2After - gen2Before
        }

    /// <summary>Measure memory and print results</summary>
    let measureMemoryAndPrint (label: string) (action: unit -> 'T) : MemoryResult<'T> =
        printfn "Measuring memory: %s" label
        let result = measureMemory action
        printfn "  Allocated: %.2f KB" (float result.AllocatedBytes / 1024.0)
        printfn "  GC Gen0:   %d" result.Gen0Collections
        printfn "  GC Gen1:   %d" result.Gen1Collections
        printfn "  GC Gen2:   %d" result.Gen2Collections
        result

    // ───────────────────────────────────────────────────────────────────
    // Performance Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert action completes within time limit</summary>
    let assertCompletesWithin (maxMs: float) (action: unit -> 'T) (context: string) : 'T =
        let result = measureTime action
        if result.ElapsedMs > maxMs then
            failwithf "Performance assertion failed: %s\nExpected: <= %.1f ms\nActual:   %.1f ms"
                context maxMs result.ElapsedMs
        result.Result

    /// <summary>Assert action completes faster than threshold</summary>
    let assertFasterThan (thresholdMs: float) (action: unit -> 'T) (context: string) : 'T * float =
        let result = measureTime action
        if result.ElapsedMs >= thresholdMs then
            failwithf "Performance assertion failed: %s\nExpected: < %.1f ms\nActual:   %.1f ms"
                context thresholdMs result.ElapsedMs
        (result.Result, result.ElapsedMs)

    /// <summary>Assert action allocates less than max bytes</summary>
    let assertAllocatesLessThan (maxBytes: int64) (action: unit -> 'T) (context: string) : 'T =
        let result = measureMemory action
        if result.AllocatedBytes > maxBytes then
            failwithf "Memory assertion failed: %s\nExpected: <= %d bytes\nActual:   %d bytes"
                context maxBytes result.AllocatedBytes
        result.Result

    /// <summary>Assert throughput meets minimum rate</summary>
    let assertThroughputAtLeast (minItemsPerSecond: float) (itemCount: int) (action: unit -> unit) (context: string) =
        let result = measureThroughput itemCount action
        if result.ItemsPerSecond < minItemsPerSecond then
            failwithf "Throughput assertion failed: %s\nExpected: >= %.0f items/sec\nActual:   %.0f items/sec"
                context minItemsPerSecond result.ItemsPerSecond

    // ───────────────────────────────────────────────────────────────────
    // Load Testing
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Load test result</summary>
    type LoadTestResult = {
        TotalRequests: int
        SuccessCount: int
        FailureCount: int
        ElapsedMs: float
        RequestsPerSecond: float
        MeanResponseMs: float
        Errors: string list
    }

    /// <summary>Run load test with concurrent requests</summary>
    /// <param name="concurrency">Number of concurrent workers</param>
    /// <param name="requestsPerWorker">Number of requests per worker</param>
    /// <param name="action">Action to execute (returns true on success)</param>
    let loadTest (concurrency: int) (requestsPerWorker: int) (action: unit -> bool) : LoadTestResult =
        let totalRequests = concurrency * requestsPerWorker
        let successes = ref 0
        let failures = ref 0
        let errors = System.Collections.Concurrent.ConcurrentBag<string>()
        let responseTimes = System.Collections.Concurrent.ConcurrentBag<float>()

        let sw = Stopwatch.StartNew()

        let threads = Array.init concurrency (fun workerId ->
            let thread = System.Threading.Thread(fun () ->
                for requestId = 0 to requestsPerWorker - 1 do
                    let reqSw = Stopwatch.StartNew()
                    try
                        let success = action()
                        reqSw.Stop()
                        responseTimes.Add(reqSw.Elapsed.TotalMilliseconds)
                        if success then
                            System.Threading.Interlocked.Increment(successes) |> ignore
                        else
                            System.Threading.Interlocked.Increment(failures) |> ignore
                            errors.Add(sprintf "Worker %d, Request %d: Action returned false" workerId requestId)
                    with ex ->
                        reqSw.Stop()
                        System.Threading.Interlocked.Increment(failures) |> ignore
                        errors.Add(sprintf "Worker %d, Request %d: %s" workerId requestId ex.Message)
            )
            thread)

        // Start all threads
        threads |> Array.iter (fun t -> t.Start())

        // Wait for all to complete
        threads |> Array.iter (fun t -> t.Join())

        sw.Stop()

        let times = responseTimes |> Seq.toList
        let meanResponse = if List.isEmpty times then 0.0 else List.average times

        {
            TotalRequests = totalRequests
            SuccessCount = !successes
            FailureCount = !failures
            ElapsedMs = sw.Elapsed.TotalMilliseconds
            RequestsPerSecond = float totalRequests / (sw.Elapsed.TotalMilliseconds / 1000.0)
            MeanResponseMs = meanResponse
            Errors = errors |> Seq.toList
        }

    /// <summary>Run load test and print results</summary>
    let loadTestAndPrint (label: string) (concurrency: int) (requestsPerWorker: int) (action: unit -> bool) : LoadTestResult =
        printfn "Load testing: %s (%d workers × %d requests)" label concurrency requestsPerWorker
        let result = loadTest concurrency requestsPerWorker action
        printfn "  Total requests: %d" result.TotalRequests
        printfn "  Success:        %d (%.1f%%)" result.SuccessCount (100.0 * float result.SuccessCount / float result.TotalRequests)
        printfn "  Failures:       %d (%.1f%%)" result.FailureCount (100.0 * float result.FailureCount / float result.TotalRequests)
        printfn "  Elapsed:        %.2f sec" (result.ElapsedMs / 1000.0)
        printfn "  Throughput:     %.0f req/sec" result.RequestsPerSecond
        printfn "  Mean response:  %.3f ms" result.MeanResponseMs
        if not (List.isEmpty result.Errors) then
            printfn "  First 5 errors:"
            result.Errors |> List.take (min 5 (List.length result.Errors)) |> List.iter (printfn "    - %s")
        result

    // ───────────────────────────────────────────────────────────────────
    // Stress Testing
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Run stress test until failure or max duration</summary>
    /// <param name="maxDurationMs">Maximum duration in milliseconds</param>
    /// <param name="action">Action to execute repeatedly</param>
    /// <returns>Number of iterations completed before failure or timeout</returns>
    let stressTestUntilFailure (maxDurationMs: float) (action: unit -> unit) : int * string option =
        let sw = Stopwatch.StartNew()
        let mutable iterations = 0
        let mutable error = None

        while sw.Elapsed.TotalMilliseconds < maxDurationMs && Option.isNone error do
            try
                action()
                iterations <- iterations + 1
            with ex ->
                error <- Some ex.Message

        sw.Stop()
        (iterations, error)

    /// <summary>Run stress test and print results</summary>
    let stressTestUntilFailureAndPrint (label: string) (maxDurationMs: float) (action: unit -> unit) =
        printfn "Stress testing: %s (max %.0f sec)" label (maxDurationMs / 1000.0)
        let (iterations, error) = stressTestUntilFailure maxDurationMs action
        printfn "  Iterations: %d" iterations
        match error with
        | Some msg ->
            printfn "  Result: FAILED after %d iterations" iterations
            printfn "  Error: %s" msg
        | None ->
            printfn "  Result: SUCCESS (completed duration without failure)"
        (iterations, error)

    // ───────────────────────────────────────────────────────────────────
    // Utilities
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Format bytes as human-readable string</summary>
    let formatBytes (bytes: int64) : string =
        if bytes < 1024L then sprintf "%d B" bytes
        elif bytes < 1024L * 1024L then sprintf "%.2f KB" (float bytes / 1024.0)
        elif bytes < 1024L * 1024L * 1024L then sprintf "%.2f MB" (float bytes / (1024.0 * 1024.0))
        else sprintf "%.2f GB" (float bytes / (1024.0 * 1024.0 * 1024.0))

    /// <summary>Format duration as human-readable string</summary>
    let formatDuration (ms: float) : string =
        if ms < 1.0 then sprintf "%.3f ms" ms
        elif ms < 1000.0 then sprintf "%.1f ms" ms
        elif ms < 60000.0 then sprintf "%.2f sec" (ms / 1000.0)
        else sprintf "%.2f min" (ms / 60000.0)

    /// <summary>Print benchmark results in table format</summary>
    let printBenchmarkTable (results: (string * BenchmarkStats) list) =
        printfn ""
        printfn "╔═══════════════════════════════════════════════════════════════════╗"
        printfn "║                      Benchmark Results                            ║"
        printfn "╠═══════════════════════════════════════════════════════════════════╣"
        printfn "║ %-30s │ %8s │ %8s │ %8s ║" "Name" "Mean" "Median" "95%"
        printfn "╠═══════════════════════════════════════════════════════════════════╣"
        for (name, stats) in results do
            printfn "║ %-30s │ %6.2f ms │ %6.2f ms │ %6.2f ms ║"
                (if name.Length > 30 then name.Substring(0, 27) + "..." else name)
                stats.MeanMs stats.MedianMs stats.Percentile95Ms
        printfn "╚═══════════════════════════════════════════════════════════════════╝"
        printfn ""
