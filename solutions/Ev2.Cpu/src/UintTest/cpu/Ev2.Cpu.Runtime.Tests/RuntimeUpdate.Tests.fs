namespace Ev2.Cpu.Runtime.Tests

open System
open System.Threading
open Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.Runtime

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Update Tests - 런타임 중 코드 수정 테스트
// ═════════════════════════════════════════════════════════════════════════════

module RuntimeUpdateTests =

    // ─────────────────────────────────────────────────────────────────────────
    // 테스트 헬퍼
    // ─────────────────────────────────────────────────────────────────────────

    let isSuccess result =
        match result with
        | UpdateResult.Success _ -> true
        | _ -> false

    let createSimpleFC name inputName outputName =
        let input = FunctionParam.Create(inputName, DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create(outputName, DsDataType.TInt, ParamDirection.Output)
        let body = UParam(inputName, DsDataType.TInt) // 간단히 입력을 출력으로
        UserFC.Create(name, [input], [output], body)

    let createSimpleFB name =
        let input = FunctionParam.Create("In", DsDataType.TBool, ParamDirection.Input)
        let output = FunctionParam.Create("Out", DsDataType.TBool, ParamDirection.Output)
        let statics = [("Counter", DsDataType.TInt, None)]
        let temps = []
        // Need at least one statement for validation
        let body = [UAssign("Out", UParam("In", DsDataType.TBool))]
        UserFB.Create(name, [input], [output], [], statics, temps, body)

    let createTestProgram() =
        let inputs = [("Start", DsDataType.TBool)]
        let outputs = [("Motor", DsDataType.TBool)]
        let locals = []
        let body = [
            DsStmt.Assign(
                0,
                DsTag.Create("Motor", DsDataType.TBool),
                DsExpr.Terminal(DsTag.Create("Start", DsDataType.TBool)))
        ]
        { Statement.Program.Name = "TestProgram"
          Inputs = inputs
          Outputs = outputs
          Locals = locals
          Body = body }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1: 기본 업데이트 요청 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``UpdateRequest - UserFC update request creation`` () =
        let fc = createSimpleFC "TestFC" "X" "Y"
        let request = UpdateRequest.updateFC fc

        match request with
        | UpdateRequest.UpdateUserFC (actualFc, validate) ->
            Assert.Equal(fc.Name, actualFc.Name)
            Assert.True(validate)
        | _ -> failwith "Expected UpdateUserFC request"

    [<Fact>]
    let ``UpdateRequest - Batch update request creation`` () =
        let fc1 = createSimpleFC "FC1" "A" "B"
        let fc2 = createSimpleFC "FC2" "C" "D"
        let requests = [UpdateRequest.updateFC fc1; UpdateRequest.updateFC fc2]
        let batchRequest = UpdateRequest.batch requests

        match batchRequest with
        | UpdateRequest.BatchUpdate reqs ->
            Assert.Equal(2, reqs.Length)
        | _ -> failwith "Expected BatchUpdate request"

    [<Fact>]
    let ``UpdateResult - Success result formatting`` () =
        let result = UpdateResult.success "Update completed"
        Assert.True(isSuccess result)
        Assert.Contains("SUCCESS", result.Format())

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2: VersionManager 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``VersionManager - Create and save snapshot`` () =
        let versionMgr = VersionManager(10)
        let userLib = UserLibrary()
        let fc = createSimpleFC "TestFC" "X" "Y"
        userLib.RegisterFC(fc) |> ignore

        let snapshot = versionMgr.CreateAndSave(userLib, None, "Test snapshot")

        Assert.Equal(1, versionMgr.Count)
        Assert.Equal(1, snapshot.UserFCs.Count)
        Assert.True(snapshot.UserFCs.ContainsKey("TestFC"))

    [<Fact>]
    let ``VersionManager - Restore snapshot`` () =
        let versionMgr = VersionManager(10)
        let userLib = UserLibrary()

        // 초기 FC 등록
        let fc1 = createSimpleFC "FC1" "X" "Y"
        userLib.RegisterFC(fc1) |> ignore
        let snapshot1 = versionMgr.CreateAndSave(userLib, None, "Snapshot 1")

        // FC 변경
        let fc2 = createSimpleFC "FC2" "A" "B"
        userLib.RegisterFC(fc2) |> ignore

        // 복원
        let result = versionMgr.RestoreSnapshot(snapshot1, userLib, None, None)

        Assert.True(result.IsOk)
        Assert.True(userLib.HasFC("FC1"))
        Assert.False(userLib.HasFC("FC2"))

    [<Fact>]
    let ``VersionManager - History size limit`` () =
        let maxHistory = 3
        let versionMgr = VersionManager(maxHistory)
        let userLib = UserLibrary()

        // maxHistory보다 많은 스냅샷 생성
        for i in 1..5 do
            let fc = createSimpleFC (sprintf "FC%d" i) "X" "Y"
            userLib.RegisterFC(fc) |> ignore
            versionMgr.CreateAndSave(userLib, None, sprintf "Snapshot %d" i) |> ignore

        // 최대 개수만 유지되는지 확인
        Assert.Equal(maxHistory, versionMgr.Count)

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 3: RuntimeUpdateManager - 검증 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``RuntimeUpdateManager - Valid UserFC update`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        let fc = createSimpleFC "TestFC" "Input" "Output"
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)

        let results = updateMgr.ProcessPendingUpdates()

        Assert.Equal(1, results.Length)
        Assert.True(isSuccess results.[0])
        Assert.True(userLib.HasFC("TestFC"))

    [<Fact>]
    let ``RuntimeUpdateManager - Invalid UserFC validation failure`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 잘못된 FC (출력 없음)
        let invalidFC = { (createSimpleFC "InvalidFC" "X" "Y") with Outputs = [] }
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC invalidFC)

        let results = updateMgr.ProcessPendingUpdates()

        Assert.Equal(1, results.Length)
        match results.[0] with
        | UpdateResult.ValidationFailed _ -> Assert.True(true)
        | _ -> failwith "Expected ValidationFailed"

    [<Fact>]
    let ``RuntimeUpdateManager - UserFB update`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        let fb = createSimpleFB "TestFB"
        updateMgr.EnqueueUpdate(UpdateRequest.updateFB fb)

        let results = updateMgr.ProcessPendingUpdates()

        Assert.Equal(1, results.Length)
        Assert.True(isSuccess results.[0])
        Assert.True(userLib.HasFB("TestFB"))

    [<Fact>]
    let ``RuntimeUpdateManager - Memory value update`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        ctx.Memory.DeclareLocal("TestVar", DsDataType.TInt)
        ctx.Memory.Set("TestVar", box 10)

        updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "TestVar" (box 20))
        let results = updateMgr.ProcessPendingUpdates()

        Assert.Equal(1, results.Length)
        Assert.True(isSuccess results.[0])
        Assert.Equal(box 20, ctx.Memory.Get("TestVar"))

    [<Fact>]
    let ``RuntimeUpdateManager - Batch update`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        let fc1 = createSimpleFC "FC1" "X" "Y"
        let fc2 = createSimpleFC "FC2" "A" "B"
        let requests = [UpdateRequest.updateFC fc1; UpdateRequest.updateFC fc2]

        updateMgr.EnqueueUpdate(UpdateRequest.batch requests)
        let results = updateMgr.ProcessPendingUpdates()

        Assert.Equal(1, results.Length)
        Assert.True(isSuccess results.[0])
        Assert.True(userLib.HasFC("FC1"))
        Assert.True(userLib.HasFC("FC2"))

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 4: CpuScanEngine 통합 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``CpuScanEngine - UserFC update during runtime`` () =
        let program = createTestProgram()
        let ctx = Context.create()
        let userLib = UserLibrary()

        // 메모리 초기화
        ctx.Memory.DeclareInput("Start", DsDataType.TBool)
        ctx.Memory.DeclareOutput("Motor", DsDataType.TBool)

        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)
        let engine = CpuScanEngine(program, ctx, None, Some updateMgr, None)

        // UserFC가 없는 상태 확인
        Assert.False(userLib.HasFC("NewFC"))

        // UserFC 업데이트 요청
        let fc = createSimpleFC "NewFC" "In" "Out"
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)

        // 스캔 실행 (업데이트 자동 처리)
        engine.ScanOnce() |> ignore

        // UserFC가 등록되었는지 확인 (핵심 검증)
        Assert.True(userLib.HasFC("NewFC"))

        // 통계 확인
        let stats = updateMgr.GetStatistics()
        Assert.Equal(1, stats.TotalRequests)
        Assert.Equal(1, stats.SuccessCount)

    [<Fact>]
    let ``CpuScanEngine - Program body update applied`` () =
        let program = createTestProgram()
        let ctx = Context.create()
        let userLib = UserLibrary()

        ctx.Memory.DeclareInput("Start", DsDataType.TBool)
        ctx.Memory.DeclareOutput("Motor", DsDataType.TBool)

        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)
        let engine = CpuScanEngine(program, ctx, None, Some updateMgr, None)

        // 초기 Program.Body는 1개의 statement
        Assert.Equal(1, program.Body.Length)

        // 새로운 Program.Body 생성 (2개의 statements)
        let newBody = [
            DsStmt.Assign(
                0,
                DsTag.Create("Motor", DsDataType.TBool),
                DsExpr.Const(box true, DsDataType.TBool))
            DsStmt.Assign(
                1,
                DsTag.Create("Start", DsDataType.TBool),
                DsExpr.Const(box false, DsDataType.TBool))
        ]

        updateMgr.EnqueueUpdate(UpdateRequest.updateBody newBody)

        // 스캔 실행 (업데이트 처리)
        engine.ScanOnce() |> ignore

        // 업데이트가 성공했는지 통계로 확인
        let stats = updateMgr.GetStatistics()
        Assert.Equal(1, stats.TotalRequests)
        Assert.Equal(1, stats.SuccessCount)
        Assert.Equal(0, stats.FailedCount)

        // 이벤트 로그에 성공 이벤트가 있는지 확인
        let events = updateMgr.GetEventLog()
        let hasSuccess =
            events |> List.exists (fun e ->
                match e with
                | UpdateEvent.ApplyCompleted _ -> true
                | _ -> false)
        Assert.True(hasSuccess)

    [<Fact>]
    let ``CpuScanEngine - Rollback on error`` () =
        let program = createTestProgram()
        let ctx = Context.create()
        let userLib = UserLibrary()

        ctx.Memory.DeclareInput("Start", DsDataType.TBool)
        ctx.Memory.DeclareOutput("Motor", DsDataType.TBool)

        let config = Some { UpdateConfig.Default with AutoRollback = true }
        let updateMgr = RuntimeUpdateManager(ctx, userLib, config)

        // 초기 FC 등록
        let fc1 = createSimpleFC "FC1" "X" "Y"
        userLib.RegisterFC(fc1) |> ignore

        let engine = CpuScanEngine(program, ctx, None, Some updateMgr, None)
        engine.ScanOnce() |> ignore

        // 잘못된 FC 업데이트 시도
        let invalidFC = { (createSimpleFC "FC1" "X" "Y") with Outputs = [] }
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC invalidFC)

        let results = updateMgr.ProcessPendingUpdates()

        // 검증 실패로 적용되지 않아야 함
        Assert.Equal(1, results.Length)
        match results.[0] with
        | UpdateResult.ValidationFailed _ -> Assert.True(true)
        | _ -> failwith "Expected validation failure"

        // 원래 FC는 유지되어야 함
        Assert.True(userLib.HasFC("FC1"))

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 5: 통계 및 이벤트 로그 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``RuntimeUpdateManager - Statistics tracking`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        let fc1 = createSimpleFC "FC1" "X" "Y"
        let fc2 = createSimpleFC "FC2" "A" "B"
        let invalidFC = { (createSimpleFC "InvalidFC" "X" "Y") with Outputs = [] }

        updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc1)
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc2)
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC invalidFC)

        updateMgr.ProcessPendingUpdates() |> ignore

        let stats = updateMgr.GetStatistics()

        Assert.Equal(3, stats.TotalRequests)
        Assert.Equal(2, stats.SuccessCount)
        Assert.Equal(1, stats.FailedCount)
        Assert.True(stats.SuccessRate > 60.0)

    [<Fact>]
    let ``RuntimeUpdateManager - Event log tracking`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        let fc = createSimpleFC "TestFC" "X" "Y"
        updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)
        updateMgr.ProcessPendingUpdates() |> ignore

        let events = updateMgr.GetEventLog()

        // 최소한 Requested, ValidationStarted, ApplyCompleted 이벤트가 있어야 함
        Assert.True(events.Length >= 3)

        let hasRequested =
            events |> List.exists (fun e ->
                match e with
                | UpdateEvent.Requested _ -> true
                | _ -> false)

        Assert.True(hasRequested)

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 6: 동시성 테스트
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``RuntimeUpdateManager - Concurrent update requests`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 여러 스레드에서 동시에 업데이트 요청
        let tasks =
            [1..10]
            |> List.map (fun i ->
                async {
                    let fc = createSimpleFC (sprintf "FC%d" i) "X" "Y"
                    updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)
                })

        tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 모든 업데이트가 성공해야 함
        Assert.Equal(10, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 모든 FC가 등록되어야 함
        for i in 1..10 do
            Assert.True(userLib.HasFC(sprintf "FC%d" i))

    // ═════════════════════════════════════════════════════════════════
    // Phase 7: Advanced Concurrency & Performance Tests (Phase 4 Enhancement)
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    let ``RuntimeUpdateManager - Concurrent updates to different variables`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 10개 변수 선언
        for i in 1..10 do
            ctx.Memory.DeclareLocal(sprintf "Var%d" i, DsDataType.TInt)
            ctx.Memory.Set(sprintf "Var%d" i, box 0)

        // 여러 스레드에서 다른 변수를 동시에 업데이트
        let tasks =
            [1..10]
            |> List.map (fun i ->
                async {
                    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory (sprintf "Var%d" i) (box (i * 100)))
                })

        tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 모든 업데이트 성공 확인
        Assert.Equal(10, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 각 변수가 올바른 값으로 업데이트되었는지 확인
        for i in 1..10 do
            let value = ctx.Memory.Get(sprintf "Var%d" i) :?> int
            Assert.Equal(i * 100, value)

    [<Fact>]
    let ``RuntimeUpdateManager - Concurrent updates to same variable`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        ctx.Memory.DeclareLocal("SharedVar", DsDataType.TInt)
        ctx.Memory.Set("SharedVar", box 0)

        // 여러 스레드에서 동일한 변수를 동시에 업데이트
        let tasks =
            [1..100]
            |> List.map (fun i ->
                async {
                    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "SharedVar" (box i))
                })

        tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 모든 업데이트가 큐에 추가되었는지 확인 (순서는 비결정적)
        Assert.Equal(100, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // SharedVar는 1-100 중 하나의 값을 가져야 함 (마지막 업데이트)
        let finalValue = ctx.Memory.Get("SharedVar") :?> int
        Assert.True(finalValue >= 1 && finalValue <= 100)

    [<Fact>]
    let ``RuntimeUpdateManager - Read-write consistency during updates`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        ctx.Memory.DeclareLocal("Counter", DsDataType.TInt)
        ctx.Memory.Set("Counter", box 0)

        let mutable readValues = []
        let lockObj = obj()

        // 읽기 스레드
        let readTask = async {
            for _ in 1..50 do
                Thread.Sleep(1)
                let value = ctx.Memory.Get("Counter") :?> int
                lock lockObj (fun () -> readValues <- value :: readValues)
        }

        // 쓰기 스레드들
        let writeTasks =
            [1..10]
            |> List.map (fun i ->
                async {
                    Thread.Sleep(i)
                    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "Counter" (box i))
                    updateMgr.ProcessPendingUpdates() |> ignore
                })

        // 동시 실행
        Async.Parallel (readTask :: writeTasks) |> Async.RunSynchronously |> ignore

        // 읽은 값들이 모두 유효한 범위 내에 있어야 함 (0-10)
        Assert.True(readValues.Length > 0)
        Assert.True(readValues |> List.forall (fun v -> v >= 0 && v <= 10))

    [<Fact>]
    let ``RuntimeUpdateManager - Performance benchmark 1000 updates`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 1000개 변수 선언
        for i in 1..1000 do
            ctx.Memory.DeclareLocal(sprintf "PerfVar%d" i, DsDataType.TInt)
            ctx.Memory.Set(sprintf "PerfVar%d" i, box 0)

        // 1000개 업데이트 요청 생성
        for i in 1..1000 do
            updateMgr.EnqueueUpdate(UpdateRequest.updateMemory (sprintf "PerfVar%d" i) (box i))

        // 성능 측정
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let results = updateMgr.ProcessPendingUpdates()
        sw.Stop()

        // 모든 업데이트 성공 확인
        Assert.Equal(1000, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 5초 이내 완료 확인
        Assert.True(sw.ElapsedMilliseconds < 5000L,
            sprintf "1000 updates took %dms (expected < 5000ms)" sw.ElapsedMilliseconds)

        // 몇 개 변수 값 확인
        Assert.Equal(box 1, ctx.Memory.Get("PerfVar1"))
        Assert.Equal(box 500, ctx.Memory.Get("PerfVar500"))
        Assert.Equal(box 1000, ctx.Memory.Get("PerfVar1000"))

    [<Fact>]
    let ``RuntimeUpdateManager - High frequency updates (10000 updates)`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        ctx.Memory.DeclareLocal("HighFreqVar", DsDataType.TInt)
        ctx.Memory.Set("HighFreqVar", box 0)

        // 10,000개 업데이트 요청
        for i in 1..10000 do
            updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "HighFreqVar" (box i))

        let sw = System.Diagnostics.Stopwatch.StartNew()
        let results = updateMgr.ProcessPendingUpdates()
        sw.Stop()

        // 모든 업데이트 처리 확인
        Assert.Equal(10000, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 30초 이내 완료 (고빈도 업데이트)
        Assert.True(sw.ElapsedMilliseconds < 30000L,
            sprintf "10000 updates took %dms (expected < 30000ms)" sw.ElapsedMilliseconds)

        // 최종 값은 10000이어야 함 (마지막 업데이트)
        let finalValue = ctx.Memory.Get("HighFreqVar") :?> int
        Assert.Equal(10000, finalValue)

    [<Fact>]
    let ``RuntimeUpdateManager - Memory consistency with concurrent FC updates`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 동시에 FC 업데이트 및 메모리 업데이트
        ctx.Memory.DeclareLocal("TestVar", DsDataType.TInt)
        ctx.Memory.Set("TestVar", box 0)

        let fcTasks =
            [1..20]
            |> List.map (fun i ->
                async {
                    let fc = createSimpleFC (sprintf "ConcurrentFC%d" i) "In" "Out"
                    updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)
                })

        let memTasks =
            [1..20]
            |> List.map (fun i ->
                async {
                    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "TestVar" (box i))
                })

        // 모든 작업 동시 실행
        let allTasks = fcTasks @ memTasks
        allTasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 40개 업데이트 (20 FC + 20 메모리) 모두 성공
        Assert.Equal(40, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 모든 FC가 등록되었는지 확인
        for i in 1..20 do
            Assert.True(userLib.HasFC(sprintf "ConcurrentFC%d" i))

        // TestVar가 유효한 값인지 확인 (1-20)
        let testVarValue = ctx.Memory.Get("TestVar") :?> int
        Assert.True(testVarValue >= 1 && testVarValue <= 20)

    [<Fact>]
    let ``RuntimeUpdateManager - Batch updates performance`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 100개 FC를 배치로 업데이트
        let fcList =
            [1..100]
            |> List.map (fun i -> createSimpleFC (sprintf "BatchFC%d" i) "X" "Y")

        let updateRequests =
            fcList
            |> List.map UpdateRequest.updateFC

        let batchRequest = UpdateRequest.batch updateRequests
        updateMgr.EnqueueUpdate(batchRequest)

        let sw = System.Diagnostics.Stopwatch.StartNew()
        let results = updateMgr.ProcessPendingUpdates()
        sw.Stop()

        // 1개 배치 결과 (100개 FC 포함)
        Assert.Equal(1, results.Length)
        Assert.True(isSuccess results.[0])

        // 2초 이내 완료
        Assert.True(sw.ElapsedMilliseconds < 2000L,
            sprintf "Batch update took %dms (expected < 2000ms)" sw.ElapsedMilliseconds)

        // 모든 FC 등록 확인
        for i in 1..100 do
            Assert.True(userLib.HasFC(sprintf "BatchFC%d" i))

    [<Fact>]
    let ``RuntimeUpdateManager - No lost updates under heavy load`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 500개 변수 선언
        for i in 1..500 do
            ctx.Memory.DeclareLocal(sprintf "LoadVar%d" i, DsDataType.TInt)
            ctx.Memory.Set(sprintf "LoadVar%d" i, box 0)

        // 고부하 상황 시뮬레이션: 여러 스레드에서 동시에 업데이트
        let tasks =
            [1..500]
            |> List.map (fun i ->
                async {
                    // 각 변수를 특정 값으로 업데이트
                    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory (sprintf "LoadVar%d" i) (box (i * 2)))
                })

        tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 모든 업데이트 성공 확인
        Assert.Equal(500, results.Length)
        Assert.True(results |> List.forall isSuccess)

        // 업데이트 손실 확인: 각 변수가 올바른 값을 가져야 함
        for i in 1..500 do
            let value = ctx.Memory.Get(sprintf "LoadVar%d" i) :?> int
            Assert.Equal(i * 2, value)

    [<Fact>]
    let ``RuntimeUpdateManager - Statistics accuracy under concurrent load`` () =
        let ctx = Context.create()
        let userLib = UserLibrary()
        let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

        // 성공할 업데이트 50개 (pre-declared)
        for i in 1..50 do
            ctx.Memory.DeclareLocal(sprintf "StatVar%d" i, DsDataType.TInt)

        // CRITICAL FIX (DEFECT-022-1): SetForced now auto-declares missing variables
        // Previous behavior: 10 updates to non-existent variables would fail
        // New behavior: SetForced auto-declares as Internal, so all 60 succeed
        // This is intentional for edge flags (TP/CTUD) to work without pre-declaration
        let tasks =
            [1..60]
            |> List.map (fun i ->
                async {
                    if i <= 50 then
                        // Success: update pre-declared variable
                        updateMgr.EnqueueUpdate(UpdateRequest.updateMemory (sprintf "StatVar%d" i) (box i))
                    else
                        // Success: SetForced auto-declares missing variables as Internal
                        updateMgr.EnqueueUpdate(UpdateRequest.updateMemory (sprintf "NonExistent%d" i) (box i))
                })

        tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

        let results = updateMgr.ProcessPendingUpdates()

        // 60개 결과 확인
        Assert.Equal(60, results.Length)

        // 통계 정확성 확인
        let stats = updateMgr.GetStatistics()
        Assert.Equal(60, stats.TotalRequests)

        // CRITICAL FIX (DEFECT-022-1): All updates now succeed with auto-declare
        // Previous: 50 success, 10 failures
        // New: 60 success, 0 failures (SetForced auto-declares missing vars)
        let successCount = results |> List.filter isSuccess |> List.length
        let failedCount = results.Length - successCount

        Assert.Equal(60, successCount)
        Assert.Equal(0, failedCount)
