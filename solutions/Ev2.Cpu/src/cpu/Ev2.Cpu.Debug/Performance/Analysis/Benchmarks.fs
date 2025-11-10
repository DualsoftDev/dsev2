namespace Ev2.Cpu.Perf.Analysis

open System
open System.Diagnostics
open System.Threading.Tasks
open System.Collections.Generic
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Runtime
open Ev2.Cpu.Perf.Core
open Ev2.Cpu.Perf.Monitoring

/// 벤치마크 실행기
type BenchmarkRunner() =
    
    /// 기본 스캔 성능 벤치마크
    member this.RunScanPerformanceBenchmark(program: Program, iterations: int) =
        let context = Context.create()
        let scanEngine = new CpuScanEngine(program, context, None, None, None)

        // 워밍업
        for _ in 1..10 do
            scanEngine.ScanOnce() |> ignore
        
        // 메모리 정리
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        
        let stopwatch = Stopwatch.StartNew()
        let mutable minTime = TimeSpan.MaxValue
        let mutable maxTime = TimeSpan.Zero
        let mutable totalTime = TimeSpan.Zero
        
        for _ in 1..iterations do
            let iterStopwatch = Stopwatch.StartNew()
            let scanTime = scanEngine.ScanOnce()
            iterStopwatch.Stop()
            
            let iterTime = iterStopwatch.Elapsed
            totalTime <- totalTime.Add(iterTime)
            if iterTime < minTime then minTime <- iterTime
            if iterTime > maxTime then maxTime <- iterTime
        
        stopwatch.Stop()
        
        let memoryUsage = GC.GetTotalMemory(false)
        let averageTime = TimeSpan.FromTicks(totalTime.Ticks / int64 iterations)
        let throughput = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
        
        {
            Name = "Scan Performance"
            Configuration = $"Iterations: {iterations}, Statements: {List.length program.Body}"
            Duration = stopwatch.Elapsed
            Iterations = iterations
            AverageTime = averageTime
            MinTime = minTime
            MaxTime = maxTime
            ThroughputPerSecond = throughput
            MemoryUsage = memoryUsage
            Success = true
            ErrorMessage = None
        }
    
    /// 메모리 사용량 벤치마크
    member this.RunMemoryBenchmark(program: Program, iterations: int) =
        let context = Context.create()
        let scanEngine = new CpuScanEngine(program, context, None, None, None)

        let initialMemory = GC.GetTotalMemory(true)
        let stopwatch = Stopwatch.StartNew()
        
        try
            for _ in 1..iterations do
                scanEngine.ScanOnce() |> ignore
                
                // 가끔 메모리 압박 테스트
                if iterations % 100 = 0 then
                    GC.Collect(0, GCCollectionMode.Optimized)
            
            stopwatch.Stop()
            let finalMemory = GC.GetTotalMemory(false)
            let memoryUsed = finalMemory - initialMemory
            
            {
                Name = "Memory Usage"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations)
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                MemoryUsage = memoryUsed
                Success = true
                ErrorMessage = None
            }
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Memory Usage"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = 0
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
    
    /// 동시성 벤치마크
    member this.RunConcurrencyBenchmark(program: Program, threadCount: int, iterationsPerThread: int) =
        let stopwatch = Stopwatch.StartNew()
        let results = Array.zeroCreate<BenchmarkResult> threadCount
        let tasks = Array.zeroCreate<Task> threadCount
        
        try
            for i in 0 .. threadCount - 1 do
                tasks.[i] <- Task.Run(fun () ->
                    let context = Context.create()
                    let scanEngine = new CpuScanEngine(program, context, None, None, None)
                    let threadStopwatch = Stopwatch.StartNew()
                    
                    for _ in 1..iterationsPerThread do
                        scanEngine.ScanOnce() |> ignore
                    
                    threadStopwatch.Stop()
                    
                    results.[i] <- {
                        Name = $"Thread {i + 1}"
                        Configuration = $"Thread {i + 1}/{threadCount}"
                        Duration = threadStopwatch.Elapsed
                        Iterations = iterationsPerThread
                        AverageTime = TimeSpan.FromTicks(threadStopwatch.Elapsed.Ticks / int64 iterationsPerThread)
                        MinTime = TimeSpan.Zero
                        MaxTime = TimeSpan.Zero
                        ThroughputPerSecond = if threadStopwatch.Elapsed.TotalSeconds > 0.0 then float iterationsPerThread / threadStopwatch.Elapsed.TotalSeconds else 0.0
                        MemoryUsage = 0L
                        Success = true
                        ErrorMessage = None
                    }
                )
            
            Task.WaitAll(tasks)
            stopwatch.Stop()
            
            let totalIterations = threadCount * iterationsPerThread
            let totalThroughput = results |> Array.sumBy (fun r -> r.ThroughputPerSecond)
            let averageTime = results |> Array.map (fun r -> r.AverageTime.TotalMilliseconds) |> Array.average |> TimeSpan.FromMilliseconds
            let memoryUsage = GC.GetTotalMemory(false)
            
            {
                Name = "Concurrency"
                Configuration = $"Threads: {threadCount}, Iterations per thread: {iterationsPerThread}"
                Duration = stopwatch.Elapsed
                Iterations = totalIterations
                AverageTime = averageTime
                MinTime = results |> Array.map (fun r -> r.AverageTime) |> Array.min
                MaxTime = results |> Array.map (fun r -> r.AverageTime) |> Array.max
                ThroughputPerSecond = totalThroughput
                MemoryUsage = memoryUsage
                Success = results |> Array.forall (fun r -> r.Success)
                ErrorMessage = None
            }
            
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Concurrency"
                Configuration = $"Threads: {threadCount}, Iterations per thread: {iterationsPerThread}"
                Duration = stopwatch.Elapsed
                Iterations = 0
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
    
    /// 확장성 벤치마크 (구문 수에 따른 성능 변화)
    member this.RunScalabilityBenchmark(baseProgramGenerator: int -> Program, scales: int list, iterations: int) =
        scales
        |> List.map (fun scale ->
            try
                let program = baseProgramGenerator scale
                let context = Context.create()
                let scanEngine = new CpuScanEngine(program, context, None, None, None)

                // 워밍업
                for _ in 1..5 do
                    scanEngine.ScanOnce() |> ignore
                
                GC.Collect()
                
                let stopwatch = Stopwatch.StartNew()
                for _ in 1..iterations do
                    scanEngine.ScanOnce() |> ignore
                stopwatch.Stop()
                
                let avgTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations)
                let throughput = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                
                {
                    Name = "Scalability"
                    Configuration = $"Scale: {scale} statements"
                    Duration = stopwatch.Elapsed
                    Iterations = iterations
                    AverageTime = avgTime
                    MinTime = avgTime
                    MaxTime = avgTime
                    ThroughputPerSecond = throughput
                    MemoryUsage = GC.GetTotalMemory(false)
                    Success = true
                    ErrorMessage = None
                }
            with
            | ex ->
                {
                    Name = "Scalability"
                    Configuration = $"Scale: {scale} statements"
                    Duration = TimeSpan.Zero
                    Iterations = 0
                    AverageTime = TimeSpan.Zero
                    MinTime = TimeSpan.Zero
                    MaxTime = TimeSpan.Zero
                    ThroughputPerSecond = 0.0
                    MemoryUsage = 0L
                    Success = false
                    ErrorMessage = Some ex.Message
                }
        )

/// 벤치마크 스위트
type BenchmarkSuite() =
    let runner = BenchmarkRunner()
    
    /// 표준 벤치마크 스위트 실행
    member this.RunStandardSuite(program: Program) =
        let results = List<BenchmarkResult>()
        
        // 1. 기본 성능 벤치마크
        printfn "Running basic performance benchmark..."
        let perfResult = runner.RunScanPerformanceBenchmark(program, 1000)
        results.Add(perfResult)
        
        // 2. 메모리 벤치마크
        printfn "Running memory benchmark..."
        let memResult = runner.RunMemoryBenchmark(program, 5000)
        results.Add(memResult)
        
        // 3. 동시성 벤치마크
        printfn "Running concurrency benchmark..."
        let concResult = runner.RunConcurrencyBenchmark(program, 4, 250)
        results.Add(concResult)
        
        // 4. 확장성 벤치마크
        printfn "Running scalability benchmark..."
        let scales = [10; 50; 100; 500; 1000]
        let scalabilityResults = runner.RunScalabilityBenchmark(
            (fun scale -> this.GenerateScaledProgram(program, scale)), 
            scales, 
            100
        )
        results.AddRange(scalabilityResults)
        
        results.ToArray() |> Array.toList
    
    /// 마이크로 벤치마크
    member this.RunMicroBenchmarks() =
        let results = List<BenchmarkResult>()
        
        // 표현식 평가 벤치마크
        let exprBenchmark = this.BenchmarkExpressionEvaluation(10000)
        results.Add(exprBenchmark)
        
        // 메모리 액세스 벤치마크
        let memoryBenchmark = this.BenchmarkMemoryAccess(50000)
        results.Add(memoryBenchmark)
        
        // 타이머 벤치마크
        let timerBenchmark = this.BenchmarkTimerOperations(5000)
        results.Add(timerBenchmark)
        
        results.ToArray() |> Array.toList
    
    /// 스트레스 테스트
    member this.RunStressTest(program: Program, duration: TimeSpan) =
        let context = Context.create()
        let scanEngine = new CpuScanEngine(program, context, None, None, None)
        let stopwatch = Stopwatch.StartNew()
        let mutable iterations = 0
        let mutable errors = 0
        let mutable maxScanTime = TimeSpan.Zero
        
        try
            while stopwatch.Elapsed < duration do
                let scanStopwatch = Stopwatch.StartNew()
                try
                    scanEngine.ScanOnce() |> ignore
                    scanStopwatch.Stop()
                    if scanStopwatch.Elapsed > maxScanTime then
                        maxScanTime <- scanStopwatch.Elapsed
                    iterations <- iterations + 1
                with
                | _ -> 
                    errors <- errors + 1
                    scanStopwatch.Stop()
            
            stopwatch.Stop()
            
            {
                Name = "Stress Test"
                Configuration = $"Duration: {duration.TotalMinutes:F1} minutes"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = if iterations > 0 then TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations) else TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = maxScanTime
                ThroughputPerSecond = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                MemoryUsage = GC.GetTotalMemory(false)
                Success = errors = 0
                ErrorMessage = if errors > 0 then Some $"{errors} errors occurred" else None
            }
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Stress Test"
                Configuration = $"Duration: {duration.TotalMinutes:F1} minutes"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
    
    // 개인 헬퍼 메서드들
    member private _.GenerateScaledProgram(baseProgram: Program, scale: int) =
        let scaledBody = 
            List.init scale (fun i ->
                let stepNumber = (i + 1) * 10
                let varName = $"ScaledVar_{i:D4}"
                let inputName = baseProgram.Inputs |> List.tryHead |> Option.map fst |> Option.defaultValue "DefaultInput"
                
                Statement.assignWithStep stepNumber 
                    (DsTag.Bool(varName)) 
                    (Terminal(DsTag.Bool(inputName)))
            )
        
        { baseProgram with Body = scaledBody }
    
    member private _.BenchmarkExpressionEvaluation(iterations: int) =
        let stopwatch = Stopwatch.StartNew()
        
        try
            for i in 1..iterations do
                // 간단한 표현식 평가 시뮬레이션
                let result = Math.Sin(float i) + Math.Cos(float i)
                result |> ignore
            
            stopwatch.Stop()
            
            {
                Name = "Expression Evaluation"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations)
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                MemoryUsage = GC.GetTotalMemory(false)
                Success = true
                ErrorMessage = None
            }
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Expression Evaluation"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = 0
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
    
    member private _.BenchmarkMemoryAccess(iterations: int) =
        let context = Context.create()
        let stopwatch = Stopwatch.StartNew()
        
        try
            // 메모리에 변수 선언
            for i in 1..100 do
                context.Memory.DeclareLocal($"TestVar_{i}", DsDataType.TBool)
            
            // 메모리 액세스 벤치마크
            for i in 1..iterations do
                let varName = $"TestVar_{(i % 100) + 1}"
                context.Memory.Set(varName, box (i % 2 = 0))
                let value = context.Memory.Get(varName)
                value |> ignore
            
            stopwatch.Stop()
            
            {
                Name = "Memory Access"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations)
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                MemoryUsage = GC.GetTotalMemory(false)
                Success = true
                ErrorMessage = None
            }
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Memory Access"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = 0
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
    
    member private _.BenchmarkTimerOperations(iterations: int) =
        let stopwatch = Stopwatch.StartNew()
        
        try
            for i in 1..iterations do
                // 타이머 작업 시뮬레이션
                let timestamp = DateTime.UtcNow
                let elapsed = timestamp - DateTime.UtcNow.AddMilliseconds(-1.0)
                elapsed.TotalMilliseconds |> ignore
            
            stopwatch.Stop()
            
            {
                Name = "Timer Operations"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = iterations
                AverageTime = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / int64 iterations)
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = if stopwatch.Elapsed.TotalSeconds > 0.0 then float iterations / stopwatch.Elapsed.TotalSeconds else 0.0
                MemoryUsage = GC.GetTotalMemory(false)
                Success = true
                ErrorMessage = None
            }
        with
        | ex ->
            stopwatch.Stop()
            {
                Name = "Timer Operations"
                Configuration = $"Iterations: {iterations}"
                Duration = stopwatch.Elapsed
                Iterations = 0
                AverageTime = TimeSpan.Zero
                MinTime = TimeSpan.Zero
                MaxTime = TimeSpan.Zero
                ThroughputPerSecond = 0.0
                MemoryUsage = 0L
                Success = false
                ErrorMessage = Some ex.Message
            }
