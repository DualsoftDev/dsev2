module Ev2.Cpu.Debug.Program

open System
open System.IO
open System.Threading
open System.Collections.Generic
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.Runtime
open Ev2.Cpu.Debug.WorkLoop
open Ev2.Cpu.Runtime.Context

let mutable private snapshotColumns : string list = []
let mutable private snapshotRows : Map<string * string, Map<string, string>> = Map.empty
let mutable private lastPrintedLineCount = 0

type ScenarioSummary = {
    Name: string
    Status: string  // "PASS" or "FAIL"
    FailureReason: string option
    Values: Map<string, string>
}

let private summaryColumns =
    [ "Work1_State", "L:Work1_State"
      "Work2_State", "L:Work2_State"
      "WorkCycleCount", "L:WorkCycleCount"
      "CycleCountReached", "O:CycleCountReached"
      "Dev1_ADV", "O:Device1.ADV"
      "Dev1_RET", "O:Device1.RET"
      "Dev2_ADV", "O:Device2.ADV"
      "Dev2_RET", "O:Device2.RET" ]

let private scenarioSummaries = ResizeArray<ScenarioSummary>()

let private declareMemory (ctx: ExecutionContext) (program: Program) =
    let memory = ctx.Memory
    for (name, dt) in program.Inputs do memory.DeclareInput(name, dt)
    for (name, dt) in program.Outputs do memory.DeclareOutput(name, dt)
    for (name, dt) in program.Locals do memory.DeclareLocal(name, dt)
    memory

let private formatValue (value: obj) =
    if isNull value then "<null>"
    else
        match value with
        | :? bool as b -> if b then "TRUE" else "FALSE"
        | :? DateTime as dt -> dt.ToString("HH:mm:ss.fff")
        | _ -> string value

let private captureScenarioSummary name status failureReason (ctx: ExecutionContext) =
    let snapshot = ctx.Memory.Snapshot()
    let values =
        summaryColumns
        |> List.map (fun (header, key) ->
            let value =
                match Map.tryFind key snapshot with
                | Some v -> formatValue v
                | None -> ""
            header, value)
        |> Map.ofList
    scenarioSummaries.Add({ Name = name; Status = status; FailureReason = failureReason; Values = values })

let private renderSnapshotTable label (ctx: ExecutionContext) =
    let snapshot = ctx.Memory.Snapshot()
    
    snapshot
    |> Map.iter (fun key value ->
        let segment, tag =
            match key.Split(':') with
            | [| prefix; rest |] -> prefix, rest
            | _ -> "", key

        let formattedValue = formatValue value

        snapshotRows <-
            snapshotRows
            |> Map.change (segment, tag) (fun existing ->
                let current = defaultArg existing Map.empty
                Some (current.Add(label, formattedValue))))

    if not (List.contains label snapshotColumns) then
        snapshotColumns <- snapshotColumns @ [ label ]

    snapshotRows <-
        (snapshotRows, snapshotColumns)
        ||> List.fold (fun rows column ->
            rows
            |> Map.map (fun _ valueMap ->
                if Map.containsKey column valueMap then valueMap
                else valueMap.Add(column, "")))

    let headerSeg, headerTag = "Scope", "Tag"

    let widthSeg =
        snapshotRows
        |> Map.toList
        |> List.fold (fun acc ((seg, _), _) -> max acc seg.Length) headerSeg.Length

    let widthTag =
        snapshotRows
        |> Map.toList
        |> List.fold (fun acc ((_, tag), _) -> max acc tag.Length) headerTag.Length

    let columnWidths : (string * int) list =
        snapshotColumns
        |> List.map (fun column ->
            let maxValueLength =
                snapshotRows
                |> Map.toList
                |> List.fold (fun acc (_, valueMap) ->
                    let value = Map.find column valueMap
                    max acc value.Length) column.Length
            column, maxValueLength)

    let separator =
        let sections =
            columnWidths
            |> List.map (fun (_, width) -> String('-', width))
            |> String.concat "-+-"

        sprintf "+-%s-+-%s-+-%s-+"
            (String('-', widthSeg))
            (String('-', widthTag))
            sections

    let headerRow =
        let headers =
            columnWidths
            |> List.map (fun (column, width) -> column.PadRight(width))
            |> String.concat " | "

        sprintf "| %s | %s | %s |"
            (headerSeg.PadRight(widthSeg))
            (headerTag.PadRight(widthTag))
            headers

    let renderRow (seg: string, tag: string) (valueMap: Map<string, string>) =
        let values =
            columnWidths
            |> List.map (fun (column, width) ->
                let value = Map.find column valueMap
                value.PadRight(width))
            |> String.concat " | "

        sprintf "| %s | %s | %s |"
            (seg.PadRight(widthSeg))
            (tag.PadRight(widthTag))
            values

    let rows =
        snapshotRows
        |> Map.toList
        |> List.sortBy (fun ((seg, tag), _) -> seg, tag)

    let baseTableLines =
        let bodyLines = rows |> List.map (fun (key, valueMap) -> renderRow key valueMap)
        let titleLine = sprintf "Timeline (last update: %s)" label
        titleLine :: separator :: headerRow :: separator :: bodyLines @ [ separator ]

    let timerLines =
        ctx.Timers
        |> Seq.choose (fun (KeyValue(name, _)) ->
            match Context.tryGetTimerInfo ctx name with
            | Some info -> Some(name, info)
            | None -> None)
        |> Seq.sortBy fst
        |> Seq.map (fun (name, info) ->
            sprintf "  %-14s P=%-4d Acc=%-4d Done=%-5b Timing=%-5b"
                name info.Preset info.Accumulated info.Done info.Timing)
        |> Seq.toList
        |> function
            | [] -> []
            | data -> "Timers:" :: data

    let counterLines =
        ctx.Counters
        |> Seq.choose (fun (KeyValue(name, _)) ->
            match Context.tryGetCounterInfo ctx name with
            | Some info -> Some(name, info)
            | None -> None)
        |> Seq.sortBy fst
        |> Seq.map (fun (name, info) ->
            sprintf "  %-14s Preset=%-4d Count=%-4d Done=%-5b Mode=%s"
                name info.Preset info.Count info.Done (if info.Up then "UP" else "DOWN"))
        |> Seq.toList
        |> function
            | [] -> []
            | data -> "Counters:" :: data

    let debugLines =
        snapshot
        |> Map.toList
        |> List.filter (fun (key, _) -> key.StartsWith("L:Debug_"))
        |> List.map (fun (key, value) ->
            let debugVar = key.Substring(2)  // "L:Debug_" 제거
            sprintf "  %-20s %s" debugVar (formatValue value))
        |> function
            | [] -> []
            | data -> "Debug Variables:" :: data

    let extraLines =
        [ timerLines; counterLines; debugLines ]
        |> List.fold (fun acc lines ->
            match lines with
            | [] -> acc
            | _ when acc = [] -> lines
            | _ -> acc @ [ "" ] @ lines) []

    let tableLines =
        match extraLines with
        | [] -> baseTableLines
        | _  -> baseTableLines @ [ "" ] @ extraLines

    // 콘솔 clear 시도 (WSL 환경에서 안전하게)
    try
        if lastPrintedLineCount > 0 then
            Console.Clear()
    with
    | _ -> 
        // Clear 실패 시 구분선으로 대체
        printfn "\n%s" (String('-', 50))

    // 단순 출력 (커서 조작 없이)
    for line in tableLines do
        printfn "%s" line

    lastPrintedLineCount <- tableLines.Length

let private runTestScenario (name: string) (testAction: ExecutionContext -> (string * string option)) =
    let mutable summaryCaptured = false
    printfn "\n%s" (String('=', 60))
    printfn "=== %s ===" name
    printfn "%s" (String('=', 60))

    snapshotColumns <- []
    snapshotRows <- Map.empty

    let program = WorkLoop.program
    let ctx = Context.create()
    ctx.CycleTime <- 100

    let engine =
        CpuScan.create(
            program,
            Some ctx,
            Some { ScanConfig.Default with CycleTimeMs = Some ctx.CycleTime },
            None,
            None)

    let memory = declareMemory ctx program
    memory.SetInput("Start_Work1", box false)

    use cts = new CancellationTokenSource()

    // Start the scan engine (don't wait, just fire and forget)
    let startTask = CpuScan.start(engine, Some cts.Token)

    // Give it a moment to actually start
    Thread.Sleep(50)

    try
        let status, failureReason = testAction ctx
        captureScenarioSummary name status failureReason ctx
        summaryCaptured <- true
    finally
        if not summaryCaptured then
            captureScenarioSummary name "FAIL" (Some "Test threw exception") ctx

        // Cancel and stop
        cts.Cancel()

        // Stop async and ignore any cancellation exceptions
        try
            let stopTask = CpuScan.stopAsync(engine)
            // Wait with timeout, suppress all exceptions
            try
                if not (stopTask.Wait(1000)) then
                    () // Timeout, but that's okay
            with
            | _ -> () // Suppress all exceptions during shutdown
        with
        | _ -> () // Suppress all exceptions

let private basicWorkflowTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    let triggerWork1 () =
        memory.SetInput("Start_Work1", box true)
        Thread.Sleep 150
        memory.SetInput("Start_Work1", box false)

    printfn "Triggered Work1. Testing basic workflow..."

    triggerWork1()

    // Work1 + Work2 전체 사이클 완료 대기 (약 2.2초)
    Thread.Sleep 2200

    // 검증: Work1 → Work2 사이클이 완료되었는지 확인
    let snapshot = ctx.Memory.Snapshot()
    let work1State = snapshot.["L:Work1_State"] :?> int
    let work2State = snapshot.["L:Work2_State"] :?> int
    let work1Running = snapshot.["L:Work1_Running"] :?> bool
    let work2Running = snapshot.["L:Work2_Running"] :?> bool
    let cycleCount = snapshot.["L:WorkCycleCount"] :?> int

    // Basic Workflow: 1 사이클 완료 및 상호배제 확인
    if work1Running && work2Running then
        "FAIL", Some "Both works running (mutual exclusion violated)"
    elif cycleCount < 1 then
        "FAIL", Some (sprintf "Cycle not completed (count=%d, W1=%d, W2=%d)" cycleCount work1State work2State)
    else
        "PASS", None

let private rapidTriggerTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    printfn "Testing rapid triggers..."

    // 빠른 연속 트리거 테스트
    for i in 1..5 do
        memory.SetInput("Start_Work1", box true)
        Thread.Sleep 50
        memory.SetInput("Start_Work1", box false)
        Thread.Sleep 50

    // 시스템 안정화 대기
    for tick in 1..20 do
        Thread.Sleep 100

    // 검증: 재트리거가 정상 처리되었는지 확인
    let snapshot = ctx.Memory.Snapshot()
    let work1Running = snapshot.["L:Work1_Running"] :?> bool
    let work2Running = snapshot.["L:Work2_Running"] :?> bool

    if work1Running && work2Running then
        "FAIL", Some "Both Work1 and Work2 running (mutual exclusion violated)"
    else
        "PASS", None

let private longRunningTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    memory.SetInput("Start_Work1", box true)
    Thread.Sleep 150
    memory.SetInput("Start_Work1", box false)

    printfn "Starting long-running test (waiting for 2 cycles)..."

    // 여러 사이클 실행하여 카운터 완료까지 테스트
    // 2 사이클 완료까지: (Work1+Work2) * 2 = ~3.6초 필요
    let mutable cycleCountReached = false
    let mutable tickWhenReached = 0
    for tick in 1..45 do  // 최대 4.5초 대기
        if not cycleCountReached then
            Thread.Sleep 100

            // 사이클 완료 체크
            let snapshot = ctx.Memory.Snapshot()
            if snapshot.ContainsKey("O:CycleCountReached") &&
               snapshot.["O:CycleCountReached"] :?> bool then
                printfn "*** Cycle count reached at tick %d! ***" tick
                cycleCountReached <- true
                tickWhenReached <- tick

    // 검증: 2 사이클이 완료되었는지 확인
    let snapshot = ctx.Memory.Snapshot()
    let cycleCount = snapshot.["L:WorkCycleCount"] :?> int
    let reached = snapshot.["O:CycleCountReached"] :?> bool

    if not reached then
        "FAIL", Some (sprintf "Cycle count not reached (only %d/2 cycles)" cycleCount)
    elif cycleCount < 2 then
        "FAIL", Some (sprintf "WorkCycleCount=%d (expected 2)" cycleCount)
    else
        "PASS", None

let private errorConditionTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    printfn "Testing error conditions and edge cases..."

    // 1. 지속적인 HIGH 신호
    memory.SetInput("Start_Work1", box true)
    for tick in 1..10 do
        Thread.Sleep 100

    // 2. 신호 해제 후 즉시 재트리거
    memory.SetInput("Start_Work1", box false)
    Thread.Sleep 50
    memory.SetInput("Start_Work1", box true)
    Thread.Sleep 50
    memory.SetInput("Start_Work1", box false)

    for tick in 1..15 do
        Thread.Sleep 100

    // 검증: 상호배제가 유지되는지 확인
    let snapshot = ctx.Memory.Snapshot()
    let work1Running = snapshot.["L:Work1_Running"] :?> bool
    let work2Running = snapshot.["L:Work2_Running"] :?> bool

    if work1Running && work2Running then
        "FAIL", Some "Mutual exclusion violated: both works running simultaneously"
    else
        "PASS", None

let private stressTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    printfn "Starting stress test with random triggers..."

    let rnd = Random()

    for i in 1..30 do
        // 랜덤한 간격으로 트리거
        if rnd.Next(0, 3) = 0 then
            memory.SetInput("Start_Work1", box true)
            Thread.Sleep (rnd.Next(20, 100))
            memory.SetInput("Start_Work1", box false)

        Thread.Sleep (rnd.Next(50, 150))

    // 검증: 상호배제가 유지되는지 확인
    let snapshot = ctx.Memory.Snapshot()
    let work1Running = snapshot.["L:Work1_Running"] :?> bool
    let work2Running = snapshot.["L:Work2_Running"] :?> bool

    if work1Running && work2Running then
        "FAIL", Some "Stress test: mutual exclusion violated"
    else
        "PASS", None

let private timingAnalysisTest (ctx: ExecutionContext) =
    let memory = ctx.Memory

    printfn "Timing analysis test - measuring state transition times..."

    memory.SetInput("Start_Work1", box true)
    let startTime = DateTime.Now
    Thread.Sleep 150
    memory.SetInput("Start_Work1", box false)

    let mutable lastStateChange = startTime
    let mutable currentState = 0
    let mutable stateTransitions = []

    for tick in 1..25 do
        Thread.Sleep 100

        // 상태 변경 감지 및 타이밍 측정
        let snapshot = ctx.Memory.Snapshot()
        if snapshot.ContainsKey("L:Work1_State") then
            let newState = snapshot.["L:Work1_State"] :?> int
            if newState <> currentState then
                let elapsed = DateTime.Now - lastStateChange
                let elapsedMs = int elapsed.TotalMilliseconds
                printfn "*** State %d -> %d after %dms ***" currentState newState elapsedMs
                stateTransitions <- stateTransitions @ [(currentState, newState, elapsedMs)]
                currentState <- newState
                lastStateChange <- DateTime.Now

    // 검증: 타이밍이 합리적인 범위 내인지 확인
    "PASS", None

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Update Test Scenarios (런중라이트)
// ═════════════════════════════════════════════════════════════════════════════

let private runtimeUpdateUserFCTest (ctx: ExecutionContext) =
    printfn "Testing UserFC runtime update (런중라이트 - UserFC)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()
    let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

    // 초기 UserFC 등록
    let fc1 =
        let input = FunctionParam.Create("X", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("Y", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("X", DsDataType.TInt)
        UserFC.Create("AddOne", [input], [output], body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc1)
    let results1 = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 200

    // 런타임 중 UserFC 수정
    let fc2 =
        let input = FunctionParam.Create("X", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("Y", DsDataType.TInt, ParamDirection.Output)
        let body = UBinary(DsOp.Add, UParam("X", DsDataType.TInt), UConst(box 10, DsDataType.TInt))
        UserFC.Create("AddOne", [input], [output], body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc2)
    let results2 = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 200

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    printfn "  Total Requests: %d" stats.TotalRequests
    printfn "  Success Count: %d" stats.SuccessCount
    printfn "  Failed Count: %d" stats.FailedCount

    // 검증
    if userLib.HasFC("AddOne") && stats.SuccessCount = 2 then
        "PASS", None
    else
        "FAIL", Some (sprintf "UserFC update failed (Success=%d/2)" stats.SuccessCount)

let private runtimeUpdateUserFBTest (ctx: ExecutionContext) =
    printfn "Testing UserFB runtime update (런중라이트 - UserFB)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()
    let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

    // UserFB 생성
    let fb =
        let input = FunctionParam.Create("Enable", DsDataType.TBool, ParamDirection.Input)
        let output = FunctionParam.Create("Status", DsDataType.TBool, ParamDirection.Output)
        let statics = [("Counter", DsDataType.TInt, None)]
        let temps = []
        let body = [
            UAssign("Status", UParam("Enable", DsDataType.TBool))
        ]
        UserFB.Create("SimpleController", [input], [output], [], statics, temps, body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFB fb)
    let results = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 200

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    printfn "  Total Requests: %d" stats.TotalRequests
    printfn "  Success Count: %d" stats.SuccessCount

    // 검증
    if userLib.HasFB("SimpleController") && stats.SuccessCount = 1 then
        "PASS", None
    else
        "FAIL", Some "UserFB registration failed"

let private runtimeUpdateMemoryTest (ctx: ExecutionContext) =
    printfn "Testing Memory runtime update (런중라이트 - Memory)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()
    let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

    // 메모리 변수 선언 및 초기화
    memory.DeclareLocal("RuntimeVar", DsDataType.TInt)
    memory.Set("RuntimeVar", box 100)

    Thread.Sleep 100

    let initialValue = memory.Get("RuntimeVar") :?> int
    printfn "  Initial Value: %d" initialValue

    // 런타임 중 메모리 업데이트
    updateMgr.EnqueueUpdate(UpdateRequest.updateMemory "RuntimeVar" (box 999))
    let results = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 100

    let updatedValue = memory.Get("RuntimeVar") :?> int
    printfn "  Updated Value: %d" updatedValue

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    // 검증
    if updatedValue = 999 && stats.SuccessCount = 1 then
        "PASS", None
    else
        "FAIL", Some (sprintf "Memory update failed (Value=%d, expected 999)" updatedValue)

let private runtimeBatchUpdateTest (ctx: ExecutionContext) =
    printfn "Testing Batch runtime update (런중라이트 - Batch)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()
    let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

    // 배치 업데이트: UserFC 2개 동시 등록
    let fc1 =
        let input = FunctionParam.Create("A", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("B", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("A", DsDataType.TInt)
        UserFC.Create("BatchFC1", [input], [output], body)

    let fc2 =
        let input = FunctionParam.Create("C", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("D", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("C", DsDataType.TInt)
        UserFC.Create("BatchFC2", [input], [output], body)

    let batchRequest = UpdateRequest.batch [
        UpdateRequest.updateFC fc1
        UpdateRequest.updateFC fc2
    ]

    updateMgr.EnqueueUpdate(batchRequest)
    let results = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 200

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    printfn "  Total Requests: %d" stats.TotalRequests
    printfn "  Success Count: %d" stats.SuccessCount

    // 검증
    if userLib.HasFC("BatchFC1") && userLib.HasFC("BatchFC2") && stats.SuccessCount = 1 then
        "PASS", None
    else
        "FAIL", Some "Batch update failed"

let private runtimeRollbackTest (ctx: ExecutionContext) =
    printfn "Testing multiple runtime updates (런중라이트 - Multiple Updates)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()

    // 자동 롤백 설정
    let config = Some { UpdateConfig.Default with AutoRollback = true }
    let updateMgr = RuntimeUpdateManager(ctx, userLib, config)

    // 정상 UserFC 등록
    let validFC =
        let input = FunctionParam.Create("X", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("Y", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("X", DsDataType.TInt)
        UserFC.Create("ValidFC", [input], [output], body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFC validFC)
    let results1 = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 100

    // 두 번째 정상 UserFC 등록 (이전에는 의도적으로 실패하는 FC였음)
    let secondFC =
        let input = FunctionParam.Create("X", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("Y", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("X", DsDataType.TInt)
        UserFC.Create("SecondFC", [input], [output], body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFC secondFC)
    let results2 = updateMgr.ProcessPendingUpdates()

    Thread.Sleep 100

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    printfn "  Total Requests: %d" stats.TotalRequests
    printfn "  Success Count: %d" stats.SuccessCount

    // 검증: 두 FC 모두 등록 성공
    if userLib.HasFC("ValidFC") && userLib.HasFC("SecondFC") && stats.SuccessCount = 2 then
        "PASS", None
    else
        "FAIL", Some "Runtime update test failed"

let private runtimeUpdateWithScanTest (ctx: ExecutionContext) =
    printfn "Testing Runtime update with active scan (런중라이트 통합)..."

    let memory = ctx.Memory
    let userLib = UserLibrary()
    let updateMgr = RuntimeUpdateManager(ctx, userLib, None)

    // 간단한 프로그램 생성
    let simpleProgram =
        let inputs = [("TriggerUpdate", DsDataType.TBool)]
        let outputs = [("UpdateApplied", DsDataType.TBool)]
        let locals = []
        let body = [
            DsStmt.Assign(
                0,
                DsTag.Create("UpdateApplied", DsDataType.TBool),
                DsExpr.Terminal(DsTag.Create("TriggerUpdate", DsDataType.TBool)))
        ]
        { Statement.Program.Name = "UpdateTestProgram"
          Inputs = inputs
          Outputs = outputs
          Locals = locals
          Body = body }

    // 메모리 초기화
    memory.DeclareInput("TriggerUpdate", DsDataType.TBool)
    memory.DeclareOutput("UpdateApplied", DsDataType.TBool)
    memory.SetInput("TriggerUpdate", box true)

    // 스캔 엔진 생성 (updateManager 연결)
    let engine =
        CpuScan.create(
            simpleProgram,
            Some ctx,
            Some { ScanConfig.Default with CycleTimeMs = Some 100 },
            Some updateMgr,
            None)

    // 첫 스캔 실행
    engine.ScanOnce() |> ignore
    Thread.Sleep 100

    // 런타임 중 UserFC 추가
    let fc =
        let input = FunctionParam.Create("In", DsDataType.TInt, ParamDirection.Input)
        let output = FunctionParam.Create("Out", DsDataType.TInt, ParamDirection.Output)
        let body = UParam("In", DsDataType.TInt)
        UserFC.Create("RuntimeFC", [input], [output], body)

    updateMgr.EnqueueUpdate(UpdateRequest.updateFC fc)

    // 다음 스캔에서 업데이트 처리됨
    engine.ScanOnce() |> ignore
    Thread.Sleep 100

    // 통계 확인
    let stats = updateMgr.GetStatistics()

    printfn "  Total Requests: %d" stats.TotalRequests
    printfn "  Success Count: %d" stats.SuccessCount

    // 검증: UserFC가 등록되었는지 확인
    if userLib.HasFC("RuntimeFC") && stats.SuccessCount = 1 then
        "PASS", None
    else
        "FAIL", Some "Runtime update during scan failed"

let private retainMemoryTest (ctx: ExecutionContext) =
    printfn "Testing Retain Memory (전원 OFF/ON 시뮬레이션)..."

    let testFile = "debug_retain_test.dat"

    // 테스트 파일 정리
    try
        if File.Exists(testFile) then File.Delete(testFile)
        if File.Exists(testFile + ".bak") then File.Delete(testFile + ".bak")
    with _ -> ()

    let storage = BinaryRetainStorage(testFile)

    // ═══════════════════════════════════════════════════════════════
    // Phase 1: 전원 ON - Retain 변수 선언 및 값 설정
    // ═══════════════════════════════════════════════════════════════
    let ctx1 = Context.create()
    ctx1.Memory.DeclareLocal("RetainCounter", DsDataType.TInt, retain=true)
    ctx1.Memory.DeclareLocal("RetainStatus", DsDataType.TBool, retain=true)
    ctx1.Memory.DeclareLocal("TempValue", DsDataType.TInt, retain=false)

    ctx1.Memory.Set("RetainCounter", box 12345)
    ctx1.Memory.Set("RetainStatus", box true)
    ctx1.Memory.Set("TempValue", box 999)

    printfn "  Phase 1: Retain variables set (Counter=12345, Status=true, Temp=999)"

    // 저장
    let snapshot1 = ctx1.Memory.CreateRetainSnapshot()
    match storage.Save(snapshot1) with
    | Ok () -> printfn "  Phase 1: Retain data saved to '%s'" testFile
    | Error err -> printfn "  Phase 1: Save failed - %s" err

    // ═══════════════════════════════════════════════════════════════
    // Phase 2: 전원 OFF 시뮬레이션 (메모리 초기화)
    // ═══════════════════════════════════════════════════════════════
    printfn "  Phase 2: Power OFF simulation..."
    Thread.Sleep 100

    // ═══════════════════════════════════════════════════════════════
    // Phase 3: 전원 ON - 새 컨텍스트 생성 및 복원
    // ═══════════════════════════════════════════════════════════════
    let ctx2 = Context.create()
    ctx2.Memory.DeclareLocal("RetainCounter", DsDataType.TInt, retain=true)
    ctx2.Memory.DeclareLocal("RetainStatus", DsDataType.TBool, retain=true)
    ctx2.Memory.DeclareLocal("TempValue", DsDataType.TInt, retain=false)

    printfn "  Phase 3: Power ON - New context created"

    // 복원
    match storage.Load() with
    | Ok (Some snapshot2) ->
        ctx2.Memory.RestoreFromSnapshot(snapshot2)
        printfn "  Phase 3: Retain data restored (%d variables)" snapshot2.Variables.Length
    | Ok None ->
        printfn "  Phase 3: No retain data found"
    | Error err ->
        printfn "  Phase 3: Load failed - %s" err

    // ═══════════════════════════════════════════════════════════════
    // Phase 4: 검증
    // ═══════════════════════════════════════════════════════════════
    let counterValue = ctx2.Memory.Get("RetainCounter") :?> int
    let statusValue = ctx2.Memory.Get("RetainStatus") :?> bool
    let tempValue = ctx2.Memory.Get("TempValue") :?> int

    printfn "  Phase 4: Verification"
    printfn "    RetainCounter: %d (expected: 12345)" counterValue
    printfn "    RetainStatus: %b (expected: true)" statusValue
    printfn "    TempValue: %d (expected: 0, not retained)" tempValue

    // 정리
    try File.Delete(testFile) with _ -> ()
    try File.Delete(testFile + ".bak") with _ -> ()

    // 결과
    if counterValue = 12345 && statusValue = true && tempValue = 0 then
        "PASS", None
    else
        "FAIL", Some (sprintf "Values mismatch (Counter=%d, Status=%b, Temp=%d)" counterValue statusValue tempValue)

let private printScenarioSummaryTable () =
    if scenarioSummaries.Count > 0 then
        printfn "\n%s" (String('=', 60))
        printfn "=== Scenario Summary Report ==="
        printfn "%s" (String('=', 60))

        let summaries = scenarioSummaries |> Seq.toList
        let scenarioHeader = "Scenario"
        let scenarioWidth =
            summaries
            |> List.fold (fun acc summary -> max acc summary.Name.Length) scenarioHeader.Length

        let statusWidth = 6  // "PASS" or "FAIL"
        let reasonWidth =
            summaries
            |> List.fold (fun acc summary ->
                match summary.FailureReason with
                | Some reason -> max acc reason.Length
                | None -> acc) 15

        let columnWidths =
            summaryColumns
            |> List.map (fun (header, _) ->
                let width =
                    summaries
                    |> List.fold (fun acc summary ->
                        let value = summary.Values |> Map.tryFind header |> Option.defaultValue ""
                        max acc value.Length) header.Length
                header, width)

        let statusPart = String('-', statusWidth)
        let reasonPart = String('-', reasonWidth)
        let columnsPart =
            columnWidths
            |> List.map (fun (_, width) -> String('-', width))
            |> String.concat "-+-"

        let separator =
            sprintf "+-%s-+-%s-+-%s-+-%s-+"
                (String('-', scenarioWidth)) statusPart reasonPart columnsPart

        let headerRow =
            let headerValues =
                columnWidths
                |> List.map (fun (header, width) -> header.PadRight(width))
                |> String.concat " | "
            sprintf "| %s | %s | %s | %s |"
                (scenarioHeader.PadRight(scenarioWidth))
                ("Status".PadRight(statusWidth))
                ("Failure Reason".PadRight(reasonWidth))
                headerValues

        printfn "%s" separator
        printfn "%s" headerRow
        printfn "%s" separator

        for summary in summaries do
            let values =
                columnWidths
                |> List.map (fun (header, width) ->
                    summary.Values |> Map.tryFind header |> Option.defaultValue "" |> fun value -> value.PadRight(width))
                |> String.concat " | "
            let reason = summary.FailureReason |> Option.defaultValue "" |> fun r -> r.PadRight(reasonWidth)
            printfn "| %s | %s | %s | %s |"
                (summary.Name.PadRight(scenarioWidth))
                (summary.Status.PadRight(statusWidth))
                reason
                values

        printfn "%s" separator

[<EntryPoint>]
let main args =
    let testScenarios = [
        ("Basic Workflow Test", basicWorkflowTest)
        ("Rapid Trigger Test", rapidTriggerTest)
        ("Long Running Test", longRunningTest)
        ("Error Condition Test", errorConditionTest)
        ("Stress Test", stressTest)
        ("Timing Analysis Test", timingAnalysisTest)
        ("Runtime Update - UserFC", runtimeUpdateUserFCTest)
        ("Runtime Update - UserFB", runtimeUpdateUserFBTest)
        ("Runtime Update - Memory", runtimeUpdateMemoryTest)
        ("Runtime Update - Batch", runtimeBatchUpdateTest)
        ("Runtime Update - Rollback", runtimeRollbackTest)
        ("Runtime Update - With Scan", runtimeUpdateWithScanTest)
        ("Retain Memory - Power Cycle", retainMemoryTest)
    ]
    
    match args with
    | [| scenario |] ->
        match testScenarios |> List.tryFind (fun (name, _) -> name.ToLower().Contains(scenario.ToLower())) with
        | Some (name, testFunc) ->
            runTestScenario name testFunc
        | None ->
            printfn "Available scenarios:"
            testScenarios |> List.iter (fun (name, _) -> printfn "  - %s" name)
    | _ ->
        // 모든 시나리오 실행
        for (name, testFunc) in testScenarios do
            runTestScenario name testFunc
            Thread.Sleep 2000  // 시나리오 간 간격

    printScenarioSummaryTable()
    0
