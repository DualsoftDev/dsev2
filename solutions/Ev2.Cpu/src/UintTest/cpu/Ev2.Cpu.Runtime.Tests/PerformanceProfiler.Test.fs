namespace Ev2.Cpu.Runtime.Tests

open System
open System.Threading
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Runtime.PerformanceProfiler

/// PerformanceProfiler 모듈 테스트
[<Collection("Sequential")>]
type PerformanceProfilerTest() =

    // ═════════════════════════════════════════════════════════════════
    // 기본 측정 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``Profiler - Single measurement records correctly``() =
        let profiler = Profiler()

        // 측정 시작
        let startTime = profiler.StartMeasurement("TestOperation")

        // 작업 시뮬레이션
        Thread.Sleep(10)

        // 측정 종료
        profiler.EndMeasurement("TestOperation", startTime)

        // 결과 확인
        let metrics = profiler.GetMetrics()
        metrics.Length |> should equal 1

        let metric = metrics.[0]
        metric.Name |> should equal "TestOperation"
        metric.CallCount |> should equal 1L
        metric.TotalTime |> should be (greaterThan 0L)
        metric.MinTime |> should equal metric.TotalTime
        metric.MaxTime |> should equal metric.TotalTime

    [<Fact>]
    member _.``Profiler - Multiple measurements accumulate``() =
        let profiler = Profiler()

        // 3번 측정
        for i in 1..3 do
            let startTime = profiler.StartMeasurement("Operation")
            Thread.Sleep(5)
            profiler.EndMeasurement("Operation", startTime)

        // 결과 확인
        let metrics = profiler.GetMetrics()
        let metric = metrics |> Array.find (fun m -> m.Name = "Operation")

        metric.CallCount |> should equal 3L
        metric.TotalTime |> should be (greaterThan 0L)
        metric.MinTime |> should be (lessThanOrEqualTo metric.MaxTime)

    [<Fact>]
    member _.``Profiler - Multiple metrics tracked independently``() =
        let profiler = Profiler()

        // Operation1 측정
        let start1 = profiler.StartMeasurement("Operation1")
        Thread.Sleep(5)
        profiler.EndMeasurement("Operation1", start1)

        // Operation2 측정
        let start2 = profiler.StartMeasurement("Operation2")
        Thread.Sleep(10)
        profiler.EndMeasurement("Operation2", start2)

        // 결과 확인
        let metrics = profiler.GetMetrics()
        metrics.Length |> should equal 2

        let op1 = metrics |> Array.find (fun m -> m.Name = "Operation1")
        let op2 = metrics |> Array.find (fun m -> m.Name = "Operation2")

        op1.CallCount |> should equal 1L
        op2.CallCount |> should equal 1L

    // ═════════════════════════════════════════════════════════════════
    // Min/Max 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``Profiler - Min and Max times tracked correctly``() =
        let profiler = Profiler()

        // 다양한 시간의 측정
        let start1 = profiler.StartMeasurement("VarTime")
        Thread.Sleep(5)
        profiler.EndMeasurement("VarTime", start1)

        let start2 = profiler.StartMeasurement("VarTime")
        Thread.Sleep(15)
        profiler.EndMeasurement("VarTime", start2)

        let start3 = profiler.StartMeasurement("VarTime")
        Thread.Sleep(10)
        profiler.EndMeasurement("VarTime", start3)

        // 결과 확인
        let metrics = profiler.GetMetrics()
        let metric = metrics.[0]

        metric.CallCount |> should equal 3L
        metric.MinTime |> should be (lessThan metric.MaxTime)
        metric.MinTime |> should be (greaterThan 0L)

    // ═════════════════════════════════════════════════════════════════
    // 스캔 성능 통계 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``ScanStats - Single scan recorded``() =
        let profiler = Profiler()

        profiler.RecordScanTime(10L, 100)

        let stats = profiler.GetScanStats()
        stats.ScanCount |> should equal 1L
        stats.LastScanTime |> should equal 10L
        stats.MinScanTime |> should equal 10L
        stats.MaxScanTime |> should equal 10L
        stats.AverageScanTime |> should equal 10.0
        stats.OverrunCount |> should equal 0L

    [<Fact>]
    member _.``ScanStats - Multiple scans tracked``() =
        let profiler = Profiler()

        profiler.RecordScanTime(10L, 100)
        profiler.RecordScanTime(20L, 100)
        profiler.RecordScanTime(15L, 100)

        let stats = profiler.GetScanStats()
        stats.ScanCount |> should equal 3L
        stats.TotalScanTime |> should equal 45L
        stats.MinScanTime |> should equal 10L
        stats.MaxScanTime |> should equal 20L
        stats.AverageScanTime |> should equal 15.0
        stats.OverrunCount |> should equal 0L

    [<Fact>]
    member _.``ScanStats - Overrun detected``() =
        let profiler = Profiler()
        let targetCycle = 50

        profiler.RecordScanTime(30L, targetCycle)  // OK
        profiler.RecordScanTime(60L, targetCycle)  // Overrun
        profiler.RecordScanTime(40L, targetCycle)  // OK
        profiler.RecordScanTime(70L, targetCycle)  // Overrun

        let stats = profiler.GetScanStats()
        stats.ScanCount |> should equal 4L
        stats.OverrunCount |> should equal 2L

    [<Fact>]
    member _.``ScanStats - Average calculated correctly``() =
        let profiler = Profiler()

        profiler.RecordScanTime(10L, 100)
        profiler.RecordScanTime(20L, 100)
        profiler.RecordScanTime(30L, 100)

        let stats = profiler.GetScanStats()
        stats.AverageScanTime |> should equal 20.0

    // ═════════════════════════════════════════════════════════════════
    // 메모리 통계 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``MemoryStats - Initial state``() =
        let profiler = Profiler()

        let stats = profiler.GetMemoryStats()
        stats.AllocatedBytes |> should equal 0L
        stats.Gen0Collections |> should equal 0
        stats.Gen1Collections |> should equal 0
        stats.Gen2Collections |> should equal 0

    [<Fact>]
    member _.``MemoryStats - Updated after call``() =
        let profiler = Profiler()

        profiler.UpdateMemoryStats()

        let stats = profiler.GetMemoryStats()
        stats.AllocatedBytes |> should be (greaterThan 0L)
        stats.LastGCTime |> should not' (equal DateTime.MinValue)

    [<Fact>]
    member _.``MemoryStats - GC collections tracked``() =
        let profiler = Profiler()

        // 초기 업데이트
        profiler.UpdateMemoryStats()
        let stats1 = profiler.GetMemoryStats()
        let initialGen0 = stats1.Gen0Collections

        // GC 강제 실행
        GC.Collect(0)
        GC.WaitForPendingFinalizers()

        // 재측정
        profiler.UpdateMemoryStats()
        let stats2 = profiler.GetMemoryStats()

        stats2.Gen0Collections |> should be (greaterThanOrEqualTo initialGen0)

    // ═════════════════════════════════════════════════════════════════
    // 동시성 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``Profiler - Thread-safe measurements``() =
        let profiler = Profiler()
        let iterations = 100

        // 여러 스레드에서 동시에 측정
        let tasks =
            [1..10]
            |> List.map (fun threadId ->
                async {
                    for i in 1..iterations do
                        let start = profiler.StartMeasurement($"Thread{threadId}")
                        Thread.Sleep(1)
                        profiler.EndMeasurement($"Thread{threadId}", start)
                }
            )

        tasks
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        // 결과 확인
        let metrics = profiler.GetMetrics()
        metrics.Length |> should equal 10

        for metric in metrics do
            metric.CallCount |> should equal (int64 iterations)

    [<Fact>]
    member _.``ScanStats - Thread-safe recording``() =
        let profiler = Profiler()
        let iterations = 100

        // 여러 스레드에서 동시에 기록
        let tasks =
            [1..5]
            |> List.map (fun _ ->
                async {
                    for i in 1..iterations do
                        profiler.RecordScanTime(10L, 100)
                }
            )

        tasks
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        // 결과 확인
        let stats = profiler.GetScanStats()
        stats.ScanCount |> should equal (int64 (5 * iterations))

    // ═════════════════════════════════════════════════════════════════
    // 경계값 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``Profiler - Zero time measurement``() =
        let profiler = Profiler()

        // 즉시 측정 종료
        let startTime = profiler.StartMeasurement("Instant")
        profiler.EndMeasurement("Instant", startTime)

        let metrics = profiler.GetMetrics()
        metrics.Length |> should equal 1

    [<Fact>]
    member _.``ScanStats - Zero scan time``() =
        let profiler = Profiler()

        profiler.RecordScanTime(0L, 100)

        let stats = profiler.GetScanStats()
        stats.ScanCount |> should equal 1L
        stats.MinScanTime |> should equal 0L
        stats.MaxScanTime |> should equal 0L

    [<Fact>]
    member _.``ScanStats - Exactly at target cycle``() =
        let profiler = Profiler()
        let targetCycle = 50

        profiler.RecordScanTime(50L, targetCycle)  // Exactly at limit

        let stats = profiler.GetScanStats()
        stats.OverrunCount |> should equal 0L  // Should not count as overrun

    [<Fact>]
    member _.``ScanStats - One tick over target``() =
        let profiler = Profiler()
        let targetCycle = 50

        profiler.RecordScanTime(51L, targetCycle)  // One over limit

        let stats = profiler.GetScanStats()
        stats.OverrunCount |> should equal 1L  // Should count as overrun

    // ═════════════════════════════════════════════════════════════════
    // 통합 시나리오 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``Full profiling scenario``() =
        let profiler = Profiler()

        // 여러 작업 프로파일링
        for i in 1..10 do
            // 스캔 시뮬레이션
            let scanStart = profiler.StartMeasurement("FullScan")

            // 표현식 평가
            let exprStart = profiler.StartMeasurement("ExprEval")
            Thread.Sleep(2)
            profiler.EndMeasurement("ExprEval", exprStart)

            // 문장 실행
            let stmtStart = profiler.StartMeasurement("StmtExec")
            Thread.Sleep(3)
            profiler.EndMeasurement("StmtExec", stmtStart)

            profiler.EndMeasurement("FullScan", scanStart)

            // 스캔 기록
            profiler.RecordScanTime(int64 (i * 5), 100)

        // 메모리 통계 업데이트
        profiler.UpdateMemoryStats()

        // 결과 확인
        let metrics = profiler.GetMetrics()
        metrics.Length |> should equal 3

        let scanStats = profiler.GetScanStats()
        scanStats.ScanCount |> should equal 10L

        let memStats = profiler.GetMemoryStats()
        memStats.AllocatedBytes |> should be (greaterThan 0L)
