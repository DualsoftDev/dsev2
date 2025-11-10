module Ev2.Cpu.Runtime.Tests.Execution

open System
open System.Threading
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Runtime
open Ev2.Cpu.Runtime.StmtEvaluator
open Ev2.Cpu.Runtime.ExprEvaluator

[<Fact>]
let ``StmtEvaluator assigns output`` () =
    let ctx = Context.create()
    ctx.Memory.DeclareOutput("Q1", typeof<bool>)

    let stmt = DsTag.Bool "Q1" := bool true
    exec ctx stmt

    ctx.Memory.Get("Q1") |> should equal (box true)

[<Fact>]
let ``ExprEvaluator reads declared input`` () =
    let ctx = Context.create()
    ctx.Memory.DeclareInput("Start", typeof<bool>)
    ctx.Memory.SetInput("Start", box true)

    let expr = Terminal(DsTag.Bool "Start")
    eval ctx expr |> should equal (box true)

[<Fact>]
let ``Assembly line cycle uses timer counter and pulses`` () =
    let ctx = Context.create()
    ctx.State <- ExecutionState.Running

    // Inputs coming from the line PLC
    ctx.Memory.DeclareInput("StartCycle", typeof<bool>)
    ctx.Memory.DeclareInput("EStopOk", typeof<bool>)
    ctx.Memory.DeclareInput("GateClosed", typeof<bool>)
    ctx.Memory.DeclareInput("ConveyorJam", typeof<bool>)
    ctx.Memory.DeclareInput("PartSensor", typeof<bool>)
    ctx.Memory.DeclareInput("TorqueAlarm", typeof<bool>)

    // Outputs and locals driven by the program
    ctx.Memory.DeclareOutput("ConveyorRun", typeof<bool>)
    ctx.Memory.DeclareOutput("ClampReady", typeof<bool>)
    ctx.Memory.DeclareOutput("ScrewStart", typeof<bool>)
    ctx.Memory.DeclareOutput("CycleComplete", typeof<bool>)

    ctx.Memory.DeclareLocal("ScrewPulse", typeof<bool>)
    ctx.Memory.DeclareLocal("PartCount", typeof<int>)
    ctx.Memory.DeclareLocal("PrevPartCount", typeof<int>)

    let partCountTag = DsTag.Int "PartCount"
    let prevPartCountTag = DsTag.Int "PrevPartCount"

    let program = [
        DsTag.Bool "ConveyorRun" :=
            (boolVar "StartCycle" &&. boolVar "EStopOk" &&. boolVar "GateClosed" &&. (!!. (boolVar "ConveyorJam")))

        DsTag.Bool "ClampReady" :=
            call "TON" [boolVar "ConveyorRun"; str "T_CLAMP"; num 2000]

        DsTag.Int "PartCount" :=
            call "CTU" [str "C_ASSEMBLED"; boolVar "ClampReady" &&. boolVar "PartSensor"; num 3]

        DsTag.Bool "ScrewPulse" :=
            Terminal partCountTag >>. Terminal prevPartCountTag

        DsTag.Bool "ScrewStart" :=
            boolVar "ScrewPulse" &&. (!!. (boolVar "TorqueAlarm"))

        DsTag.Int "PrevPartCount" :=
            Terminal partCountTag

        DsTag.Bool "CycleComplete" :=
            (Terminal partCountTag >=. num 3) &&. boolVar "ClampReady"
    ]

    let runScan () =
        ctx.State <- ExecutionState.Running
        execList ctx program

    // Initial machine state: cycle request and all interlocks healthy
    ctx.Memory.SetInput("StartCycle", box true)
    ctx.Memory.SetInput("EStopOk", box true)
    ctx.Memory.SetInput("GateClosed", box true)
    ctx.Memory.SetInput("ConveyorJam", box false)
    ctx.Memory.SetInput("PartSensor", box false)
    ctx.Memory.SetInput("TorqueAlarm", box false)

    runScan()

    ctx.Memory.Get("ConveyorRun") |> should equal (box true)
    ctx.Memory.Get("ClampReady") |> should equal (box false)
    ctx.Memory.Get("PartCount") |> should equal (box 0)
    ctx.Memory.Get("ScrewPulse") |> should equal (box false)
    ctx.Memory.Get("ScrewStart") |> should equal (box false)

    // Simulate elapsed time so the clamp on-delay finishes
    let startTicks = Timebase.nowTicks()
    Context.updateTimerOnWithTimestamp ctx "T_CLAMP" true 2000 startTicks |> ignore
    let finishedTicks = Timebase.addMilliseconds startTicks 2500
    Context.updateTimerOnWithTimestamp ctx "T_CLAMP" true 2000 finishedTicks |> ignore

    runScan()

    ctx.Memory.Get("ClampReady") |> should equal (box true)
    Context.tryGetTimerInfo ctx "T_CLAMP"
    |> Option.map (fun info -> info.Done)
    |> should equal (Some true)

    // Helper to feed one assembled part using the photo sensor pulse
    let feedPart torqueAlarmActive =
        ctx.Memory.SetInput("TorqueAlarm", box torqueAlarmActive)
        ctx.Memory.SetInput("PartSensor", box true)
        runScan()

        ctx.Memory.Get("ScrewPulse") |> should equal (box true)
        if torqueAlarmActive then
            ctx.Memory.Get("ScrewStart") |> should equal (box false)
        else
            ctx.Memory.Get("ScrewStart") |> should equal (box true)

        ctx.Memory.SetInput("PartSensor", box false)
        runScan()

        ctx.Memory.Get("ScrewPulse") |> should equal (box false)
        ctx.Memory.Get("ScrewStart") |> should equal (box false)

    feedPart false
    ctx.Memory.Get("PartCount") |> should equal (box 1)

    feedPart false
    ctx.Memory.Get("PartCount") |> should equal (box 2)

    // Last station detects torque alarm; screw drive should be inhibited but counting still proceeds
    feedPart true
    ctx.Memory.Get("PartCount") |> should equal (box 3)

    ctx.Memory.Get("CycleComplete") |> should equal (box true)

    match Context.tryGetCounterInfo ctx "C_ASSEMBLED" with
    | Some info ->
        info.Done |> should equal true
        info.Count |> should equal 3
    | None -> failwith "Counter state missing"

    // Torque alarm cleared after acknowledgement, outputs remain de-energised without a new part
    ctx.Memory.SetInput("TorqueAlarm", box false)
    runScan()

    ctx.Memory.Get("ScrewStart") |> should equal (box false)
    ctx.Memory.Get("PrevPartCount") |> should equal (box 3)

[<Fact>]
let ``STN1 sequential devices advance and retract in order`` () =
    let ctx = Context.create()
    ctx.State <- ExecutionState.Running

    // Inputs
    ctx.Memory.DeclareInput("Device1.AdvCmd", typeof<bool>)
    ctx.Memory.DeclareInput("Work2.StartReset.Cmd", typeof<bool>)

    // Outputs
    [ "Device1.ADV"; "Device2.ADV"; "Device3.ADV"; "Device4.ADV"
      "Device1.RET"; "Device2.RET"; "Device3.RET"; "Device4.RET"
      "Work1.StartReset"; "Work2.StartReset" ]
    |> List.iter (fun name -> ctx.Memory.DeclareOutput(name, typeof<bool>))

    let program = [
        DsTag.Bool "Device1.ADV" := boolVar "Device1.AdvCmd"
        DsTag.Bool "Device2.ADV" := boolVar "Device1.ADV"
        DsTag.Bool "Device3.ADV" := boolVar "Device2.ADV"
        DsTag.Bool "Device4.ADV" := boolVar "Device3.ADV"

        DsTag.Bool "Device1.RET" := boolVar "Device4.ADV"
        DsTag.Bool "Device2.RET" := boolVar "Device4.ADV"
        DsTag.Bool "Device3.RET" := boolVar "Device4.ADV"
        DsTag.Bool "Device4.RET" :=
            ((boolVar "Device1.RET") ||. (boolVar "Device2.RET")) ||. (boolVar "Device3.RET")

        DsTag.Bool "Work1.StartReset" := boolVar "Work2.StartReset.Cmd"
        DsTag.Bool "Work2.StartReset" := boolVar "Work1.StartReset"
    ]

    let runScan () =
        ctx.State <- ExecutionState.Running
        execList ctx program

    ctx.Memory.SetInput("Device1.AdvCmd", box true)
    ctx.Memory.SetInput("Work2.StartReset.Cmd", box true)
    runScan()

    ctx.Memory.Get("Device4.ADV") |> should equal (box true)
    ctx.Memory.Get("Device4.RET") |> should equal (box true)
    ctx.Memory.Get("Work1.StartReset") |> should equal (box true)
    ctx.Memory.Get("Work2.StartReset") |> should equal (box true)

[<Fact>]
let ``STN2 welding station coordinates robots jigs and pins`` () =
    let ctx = Context.create()
    ctx.State <- ExecutionState.Running

    // Inputs
    ctx.Memory.DeclareInput("CV.GO", typeof<bool>)

    // Outputs
    [ "RBT1.LOAD"; "RBT1.HOME"; "RBT2.WELD"; "RBT2.HOME"
      "PIN.DN"; "PIN.UP"
      "JIG1.ADV"; "JIG2.ADV"; "JIG3.ADV"; "JIG4.ADV"
      "JIG1.RET"; "JIG2.RET"; "JIG3.RET"; "JIG4.RET" ]
    |> List.iter (fun name -> ctx.Memory.DeclareOutput(name, typeof<bool>))

    let andAll exprs =
        match exprs with
        | [] -> failwith "andAll requires expressions"
        | first :: rest -> rest |> List.fold (fun acc e -> acc &&. e) first

    let program = [
        DsTag.Bool "RBT1.LOAD" := boolVar "CV.GO"
        DsTag.Bool "PIN.DN" := boolVar "RBT1.LOAD"

        DsTag.Bool "JIG1.ADV" := (boolVar "RBT1.LOAD") &&. (boolVar "PIN.DN")
        DsTag.Bool "JIG2.ADV" := (boolVar "RBT1.LOAD") &&. (boolVar "PIN.DN")
        DsTag.Bool "JIG3.ADV" := (boolVar "RBT1.LOAD") &&. (boolVar "PIN.DN")
        DsTag.Bool "JIG4.ADV" := (boolVar "RBT1.LOAD") &&. (boolVar "PIN.DN")

        DsTag.Bool "RBT2.WELD" := andAll [ boolVar "JIG1.ADV"; boolVar "JIG2.ADV"; boolVar "JIG3.ADV"; boolVar "JIG4.ADV" ]
        DsTag.Bool "RBT2.HOME" := boolVar "RBT2.WELD"

        DsTag.Bool "JIG1.RET" := boolVar "RBT2.HOME"
        DsTag.Bool "JIG2.RET" := boolVar "RBT2.HOME"
        DsTag.Bool "JIG3.RET" := boolVar "RBT2.HOME"
        DsTag.Bool "JIG4.RET" := boolVar "RBT2.HOME"

        DsTag.Bool "RBT1.HOME" := andAll [ boolVar "JIG1.RET"; boolVar "JIG2.RET"; boolVar "JIG3.RET"; boolVar "JIG4.RET" ]
        DsTag.Bool "PIN.UP" := boolVar "RBT1.HOME"
    ]

    let runScan () =
        ctx.State <- ExecutionState.Running
        StmtEvaluator.execList ctx program

    ctx.Memory.SetInput("CV.GO", box true)
    runScan()

    ctx.Memory.Get("RBT2.WELD") |> should equal (box true)
    ctx.Memory.Get("PIN.UP") |> should equal (box true)
    ctx.Memory.Get("RBT1.HOME") |> should equal (box true)

[<Fact>]
let ``KIT conveyor workcells cascade start resets`` () =
    let ctx = Context.create()
    ctx.State <- ExecutionState.Running

    // Inputs
    ctx.Memory.DeclareInput("CycleStart", typeof<bool>)

    // Outputs (conveyors, cylinders, start/reset flags, completion flags)
    [ "Conveyor1.MOVE"; "Conveyor1.REMOVE"; "1IN_CYL.ADV"; "1IN_CYL.RET"; "KIT.Work1.StartReset"; "KIT.Work1.Complete"
      "Conveyor2.MOVE"; "1st_usb.ADV"; "1st_usb.RET"; "1st_stp.ADV"; "1st_stp.RET"; "KIT.Work2.StartReset"; "KIT.Work2.Complete"
      "Conveyor3.MOVE"; "Conveyor2.REMOVE"; "2nd_usb.ADV"; "2nd_usb.RET"; "2nd_stp.ADV"; "2nd_stp.RET"; "KIT.Work3.StartReset"; "KIT.Work3.Complete"
      "Conveyor4.MOVE"; "Conveyor3.REMOVE"; "3rd_usb.ADV"; "3rd_usb.RET"; "3rd_stp.ADV"; "3rd_stp.RET"; "KIT.Work4.StartReset"; "KIT.Work4.Complete"
      "Conveyor4.REMOVE"; "Conveyor5.MOVE"; "4th_usb.ADV"; "4th_usb.RET"; "4th_stp.ADV"; "4th_stp.RET"; "KIT.Work5.StartReset"; "KIT.Work5.Complete"
      "Conveyor5.REMOVE"; "Conveyor6.MOVE"; "Conveyor6.REMOVE"; "1OUT_CYL.ADV"; "1OUT_CYL.RET"; "KIT.Work6.StartReset"; "KIT.Work6.Complete" ]
    |> List.iter (fun name -> ctx.Memory.DeclareOutput(name, typeof<bool>))

    let program = [
        // Work1
        DsTag.Bool "KIT.Work1.StartReset" := (boolVar "CycleStart") ||. (boolVar "KIT.Work6.Complete")
        DsTag.Bool "Conveyor1.MOVE" := boolVar "KIT.Work1.StartReset"
        DsTag.Bool "Conveyor1.REMOVE" := boolVar "Conveyor1.MOVE"
        DsTag.Bool "1IN_CYL.ADV" := boolVar "Conveyor1.MOVE"
        DsTag.Bool "1IN_CYL.RET" := (boolVar "1IN_CYL.ADV") &&. (boolVar "Conveyor1.REMOVE")
        DsTag.Bool "KIT.Work1.Complete" := boolVar "1IN_CYL.RET"

        // Work2
        DsTag.Bool "KIT.Work2.StartReset" := boolVar "KIT.Work1.Complete"
        DsTag.Bool "Conveyor2.MOVE" := boolVar "KIT.Work2.StartReset"
        DsTag.Bool "1st_usb.ADV" := boolVar "Conveyor2.MOVE"
        DsTag.Bool "1st_usb.RET" := boolVar "1st_usb.ADV"
        DsTag.Bool "1st_stp.ADV" := boolVar "1st_usb.RET"
        DsTag.Bool "1st_stp.RET" := boolVar "1st_stp.ADV"
        DsTag.Bool "KIT.Work2.Complete" := boolVar "1st_stp.RET"

        // Work3
        DsTag.Bool "KIT.Work3.StartReset" := boolVar "KIT.Work2.Complete"
        DsTag.Bool "Conveyor3.MOVE" := boolVar "KIT.Work3.StartReset"
        DsTag.Bool "Conveyor2.REMOVE" := boolVar "KIT.Work2.Complete"
        DsTag.Bool "2nd_usb.ADV" := (boolVar "Conveyor3.MOVE") &&. (boolVar "Conveyor2.REMOVE")
        DsTag.Bool "2nd_usb.RET" := boolVar "2nd_usb.ADV"
        DsTag.Bool "2nd_stp.ADV" := boolVar "2nd_usb.RET"
        DsTag.Bool "2nd_stp.RET" := boolVar "2nd_stp.ADV"
        DsTag.Bool "KIT.Work3.Complete" := boolVar "2nd_stp.RET"

        // Work4
        DsTag.Bool "KIT.Work4.StartReset" := boolVar "KIT.Work3.Complete"
        DsTag.Bool "Conveyor4.MOVE" := boolVar "KIT.Work4.StartReset"
        DsTag.Bool "Conveyor3.REMOVE" := boolVar "KIT.Work3.Complete"
        DsTag.Bool "3rd_usb.ADV" := (boolVar "Conveyor4.MOVE") &&. (boolVar "Conveyor3.REMOVE")
        DsTag.Bool "3rd_usb.RET" := boolVar "3rd_usb.ADV"
        DsTag.Bool "3rd_stp.ADV" := boolVar "3rd_usb.RET"
        DsTag.Bool "3rd_stp.RET" := boolVar "3rd_stp.ADV"
        DsTag.Bool "KIT.Work4.Complete" := boolVar "3rd_stp.RET"

        // Work5
        DsTag.Bool "KIT.Work5.StartReset" := boolVar "KIT.Work4.Complete"
        DsTag.Bool "Conveyor5.MOVE" := boolVar "KIT.Work5.StartReset"
        DsTag.Bool "Conveyor4.REMOVE" := boolVar "KIT.Work4.Complete"
        DsTag.Bool "4th_usb.ADV" := (boolVar "Conveyor5.MOVE") &&. (boolVar "Conveyor4.REMOVE")
        DsTag.Bool "4th_usb.RET" := boolVar "4th_usb.ADV"
        DsTag.Bool "4th_stp.ADV" := boolVar "4th_usb.RET"
        DsTag.Bool "4th_stp.RET" := boolVar "4th_stp.ADV"
        DsTag.Bool "KIT.Work5.Complete" := boolVar "4th_stp.RET"

        // Work6
        DsTag.Bool "KIT.Work6.StartReset" := boolVar "KIT.Work5.Complete"
        DsTag.Bool "Conveyor6.MOVE" := boolVar "KIT.Work6.StartReset"
        DsTag.Bool "Conveyor5.REMOVE" := boolVar "KIT.Work5.Complete"
        DsTag.Bool "Conveyor6.REMOVE" := boolVar "Conveyor6.MOVE"
        DsTag.Bool "1OUT_CYL.ADV" := (boolVar "Conveyor6.MOVE") &&. (boolVar "Conveyor5.REMOVE")
        DsTag.Bool "1OUT_CYL.RET" := (boolVar "1OUT_CYL.ADV") &&. (boolVar "Conveyor6.REMOVE")
        DsTag.Bool "KIT.Work6.Complete" := boolVar "1OUT_CYL.RET"
    ]

    let runScan () =
        ctx.State <- ExecutionState.Running
        StmtEvaluator.execList ctx program

    ctx.Memory.SetInput("CycleStart", box true)
    runScan()

    [ "KIT.Work1.Complete"; "KIT.Work2.Complete"; "KIT.Work3.Complete"
      "KIT.Work4.Complete"; "KIT.Work5.Complete"; "KIT.Work6.Complete" ]
    |> List.iter (fun name -> ctx.Memory.Get(name) |> should equal (box true))

[<Fact>]
let ``Debug work loop toggles between stations`` () =
    let prog = Ev2.Cpu.Debug.WorkLoop.program
    let ctx = Context.create()
    ctx.CycleTime <- 20

    let memory = ctx.Memory
    for (name, dt) in prog.Inputs do memory.DeclareInput(name, dt)
    for (name, dt) in prog.Outputs do memory.DeclareOutput(name, dt)
    for (name, dt) in prog.Locals do memory.DeclareLocal(name, dt)

    let engine = CpuScan.create (prog, Some ctx, Some { ScanConfig.Default with CycleTimeMs = Some ctx.CycleTime }, None, None)
    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    memory.SetInput("Start_Work1", box true)
    Thread.Sleep 40
    memory.SetInput("Start_Work1", box false)

    Thread.Sleep 2000

    cts.Cancel()
    CpuScan.stopAsync(engine).Wait()

    let history name = memory.GetHistory(name, 30)
    let hasValue name expected = history name |> List.exists (fun (_, value, _) -> value = box expected)

    hasValue "Work1_Running" true  |> should equal true
    hasValue "Work1_Running" false |> should equal true
    hasValue "Work2_Running" true  |> should equal true
    hasValue "Work2_Running" false |> should equal true
    hasValue "Work2_StartRequest" true |> should equal true

// ═════════════════════════════════════════════════════════════════
// Phase 2 Enhanced Tests - CpuScan Concurrency & Performance
// ═════════════════════════════════════════════════════════════════

[<Fact>]
let ``CpuScan - Multiple concurrent scans on different engines`` () =
    let createEngine name =
        let prog = { Statement.Program.Name = name; Inputs = []; Outputs = [("Output", typeof<bool>)]; Locals = []; Body = [] }
        let ctx = Context.create()
        ctx.Memory.DeclareOutput("Output", typeof<bool>)
        (CpuScan.create (prog, Some ctx, None, None, None), ctx)

    let (engine1, ctx1) = createEngine "Prog1"
    let (engine2, ctx2) = createEngine "Prog2"
    let (engine3, ctx3) = createEngine "Prog3"

    use cts = new CancellationTokenSource()

    // Start all engines concurrently
    let tasks = [
        CpuScan.start(engine1, Some cts.Token)
        CpuScan.start(engine2, Some cts.Token)
        CpuScan.start(engine3, Some cts.Token)
    ]

    System.Threading.Tasks.Task.WhenAll(tasks).Wait()
    Thread.Sleep 100  // Let them run

    // All should be running
    ctx1.State |> should equal ExecutionState.Running
    ctx2.State |> should equal ExecutionState.Running
    ctx3.State |> should equal ExecutionState.Running

    // Stop all concurrently
    cts.Cancel()
    CpuScan.stopAsync(engine1).Wait()
    CpuScan.stopAsync(engine2).Wait()
    CpuScan.stopAsync(engine3).Wait()

    // All should be stopped
    ctx1.State |> should equal ExecutionState.Stopped
    ctx2.State |> should equal ExecutionState.Stopped
    ctx3.State |> should equal ExecutionState.Stopped

[<Fact>]
let ``CpuScan - Stop while scan is in progress (race condition test)`` () =
    let prog = { Statement.Program.Name = "RaceTest"; Inputs = []; Outputs = [("Counter", typeof<int>)]; Locals = []; Body = [] }
    let ctx = Context.create()
    ctx.Memory.DeclareOutput("Counter", typeof<int>)

    let engine = CpuScan.create (prog, Some ctx, Some { ScanConfig.Default with CycleTimeMs = Some 10 }, None, None)

    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    Thread.Sleep 50  // Let it run for a bit

    // Stop immediately (should not throw or deadlock)
    cts.Cancel()
    let stopTask = CpuScan.stopAsync(engine)
    let completed = stopTask.Wait(5000)  // 5 second timeout

    completed |> should equal true
    ctx.State |> should equal ExecutionState.Stopped

[<Fact>]
let ``CpuScan - Rapid start/stop cycles`` () =
    let prog = { Statement.Program.Name = "RapidCycle"; Inputs = []; Outputs = []; Locals = []; Body = [] }
    let ctx = Context.create()
    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    // Perform 5 rapid start/stop cycles
    for _ in 1..5 do
        use cts = new CancellationTokenSource()
        CpuScan.start(engine, Some cts.Token).Wait()
        Thread.Sleep 10
        cts.Cancel()
        CpuScan.stopAsync(engine).Wait()

    // Engine should still be functional
    ctx.State |> should equal ExecutionState.Stopped

[<Fact>]
let ``CpuScan - Memory updates during concurrent scans`` () =
    let prog = { Statement.Program.Name = "MemTest";
                 Inputs = [("Input", typeof<int>)];
                 Outputs = [("Output", typeof<int>)];
                 Locals = [];
                 Body = [DsTag.Int "Output" := Terminal (DsTag.Int "Input")] }

    let ctx = Context.create()
    ctx.Memory.DeclareInput("Input", typeof<int>)
    ctx.Memory.DeclareOutput("Output", typeof<int>)

    let engine = CpuScan.create (prog, Some ctx, Some { ScanConfig.Default with CycleTimeMs = Some 20 }, None, None)

    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    // Update input value from external thread
    let mutable lastOutput = 0
    for i in 1..10 do
        ctx.Memory.SetInput("Input", box i)
        Thread.Sleep 25
        lastOutput <- ctx.Memory.Get("Output") :?> int

    cts.Cancel()
    CpuScan.stopAsync(engine).Wait()

    // Output should reflect one of the input values (exact value depends on timing)
    lastOutput |> should be (greaterThanOrEqualTo 1)
    lastOutput |> should be (lessThanOrEqualTo 10)

[<Fact>]
let ``CpuScan - Concurrent ScanOnce calls`` () =
    clearVariableRegistry()

    let counterTag = DsTag.Int "Counter"
    let prog = { Statement.Program.Name = "ScanOnceTest";
                 Inputs = [];
                 Outputs = [];
                 Locals = [("Counter", typeof<int>)];
                 Body = [
                     DsTag.Int "Counter" := (Terminal counterTag .+. num 1)
                 ] }

    let ctx = Context.create()
    ctx.State <- ExecutionState.Running
    ctx.Memory.DeclareLocal("Counter", typeof<int>)
    ctx.Memory.Set("Counter", box 0)

    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    // Execute 10 scans concurrently
    let tasks =
        [1..10]
        |> List.map (fun _ -> async { return engine.ScanOnce() })

    tasks |> Async.Parallel |> Async.RunSynchronously |> ignore

    // Counter should be incremented (race conditions mean it won't be exactly 10)
    // We expect at least 1 and at most 10 due to concurrent execution
    let finalCount = ctx.Memory.Get("Counter") :?> int
    finalCount |> should be (greaterThanOrEqualTo 1)
    finalCount |> should be (lessThanOrEqualTo 10)

[<Fact>]
let ``CpuScan - Performance benchmark 1000 scans`` () =
    clearVariableRegistry()

    let prog = { Statement.Program.Name = "PerfTest";
                 Inputs = [("X", typeof<int>); ("Y", typeof<int>)];
                 Outputs = [];
                 Locals = [("Result", typeof<int>)];
                 Body = [DsTag.Int "Result" := (Terminal (DsTag.Int "X") .+. Terminal (DsTag.Int "Y"))] }

    let ctx = Context.create()
    ctx.State <- ExecutionState.Running
    ctx.Memory.DeclareInput("X", typeof<int>)
    ctx.Memory.DeclareInput("Y", typeof<int>)
    ctx.Memory.DeclareLocal("Result", typeof<int>)
    ctx.Memory.SetInput("X", box 5)
    ctx.Memory.SetInput("Y", box 3)

    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    let stopwatch = System.Diagnostics.Stopwatch.StartNew()
    for _ in 1..1000 do
        engine.ScanOnce() |> ignore
    stopwatch.Stop()

    // Should complete 1000 scans reasonably fast (under 5 seconds)
    stopwatch.ElapsedMilliseconds |> should be (lessThan 5000L)

    // Result should be correct
    ctx.Memory.Get("Result") :?> int |> should equal 8

[<Fact>]
let ``CpuScan - State remains consistent across many scans`` () =
    clearVariableRegistry()

    let counterTag = DsTag.Int "Counter"
    let prog = { Statement.Program.Name = "StateTest";
                 Inputs = [];
                 Outputs = [];
                 Locals = [("Counter", typeof<int>)];
                 Body = [DsTag.Int "Counter" := (Terminal counterTag .+. num 1)] }

    let ctx = Context.create()
    ctx.State <- ExecutionState.Running
    ctx.Memory.DeclareLocal("Counter", typeof<int>)
    ctx.Memory.Set("Counter", box 0)

    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    // Execute 100 scans (should increment 100 times)
    for _ in 1..100 do
        engine.ScanOnce() |> ignore

    // After 100 increments, should be 100
    ctx.Memory.Get("Counter") :?> int |> should equal 100

    // One more scan should increment to 101
    engine.ScanOnce() |> ignore
    ctx.Memory.Get("Counter") :?> int |> should equal 101

[<Fact>]
let ``CpuScan - ScanIndex increments correctly`` () =
    let prog = { Statement.Program.Name = "CountTest"; Inputs = []; Outputs = []; Locals = []; Body = [] }
    let ctx = Context.create()
    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    let initialIndex = ctx.ScanIndex

    // Execute 50 scans
    for _ in 1..50 do
        engine.ScanOnce() |> ignore

    // ScanIndex should have increased by 50
    let finalIndex = ctx.ScanIndex
    (int64 (finalIndex - initialIndex)) |> should equal 50L

[<Fact>]
let ``CpuScan - Multiple engines with shared memory (not recommended but should work)`` () =
    clearVariableRegistry()

    let sharedTag = DsTag.Int "SharedVar"
    let ctx = Context.create()
    ctx.State <- ExecutionState.Running
    ctx.Memory.DeclareLocal("SharedVar", typeof<int>)
    ctx.Memory.Set("SharedVar", box 0)

    let prog1 = { Statement.Program.Name = "Writer";
                  Inputs = [];
                  Outputs = [];
                  Locals = [("SharedVar", typeof<int>)];
                  Body = [DsTag.Int "SharedVar" := (Terminal sharedTag .+. num 1)] }

    let prog2 = { Statement.Program.Name = "Reader";
                  Inputs = [];
                  Outputs = [];
                  Locals = [];
                  Body = [] }

    let engine1 = CpuScan.create (prog1, Some ctx, None, None, None)
    let engine2 = CpuScan.create (prog2, Some ctx, None, None, None)

    // Execute scans on both engines
    for _ in 1..10 do
        engine1.ScanOnce() |> ignore
        engine2.ScanOnce() |> ignore

    // SharedVar should be incremented 10 times
    ctx.Memory.Get("SharedVar") :?> int |> should equal 10

[<Fact>]
let ``CpuScan - Execution with zero cycle time (continuous scanning)`` () =
    let scanCounterTag = DsTag.Int "ScanCounter"
    let prog = { Statement.Program.Name = "ContinuousTest";
                 Inputs = [];
                 Outputs = [];
                 Locals = [("ScanCounter", typeof<int>)];
                 Body = [DsTag.Int "ScanCounter" := (Terminal scanCounterTag .+. num 1)] }

    let ctx = Context.create()
    ctx.Memory.DeclareLocal("ScanCounter", typeof<int>)
    ctx.Memory.Set("ScanCounter", box 0)

    // CycleTimeMs = None means continuous scanning (no delay)
    let engine = CpuScan.create (prog, Some ctx, None, None, None)

    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    Thread.Sleep 100  // Let it run continuously for 100ms

    cts.Cancel()
    CpuScan.stopAsync(engine).Wait()

    let scanCount = ctx.Memory.Get("ScanCounter") :?> int

    // Should have executed many scans in 100ms (at least 5)
    scanCount |> should be (greaterThan 5)

[<Fact>]
let ``CpuScan - Execution with very high cycle time`` () =
    let prog = { Statement.Program.Name = "SlowScanTest";
                 Inputs = [];
                 Outputs = [];
                 Locals = [("Counter", typeof<int>)];
                 Body = [DsTag.Int "Counter" := (intVar "Counter" .+. num 1)] }

    let ctx = Context.create()
    ctx.Memory.DeclareLocal("Counter", typeof<int>)
    ctx.Memory.Set("Counter", box 0)

    // Very high cycle time (1 second per scan)
    let engine = CpuScan.create (prog, Some ctx, Some { ScanConfig.Default with CycleTimeMs = Some 1000 }, None, None)

    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    Thread.Sleep 250  // Run for 250ms

    cts.Cancel()
    CpuScan.stopAsync(engine).Wait()

    let scanCount = ctx.Memory.Get("Counter") :?> int

    // Should have executed only 1 scan (or 0 if timing is tight)
    scanCount |> should be (lessThanOrEqualTo 2)

[<Fact>]
let ``CpuScan - Stop after specific number of scans`` () =
    let prog = { Statement.Program.Name = "CountedScanTest";
                 Inputs = [];
                 Outputs = [];
                 Locals = [("Counter", typeof<int>)];
                 Body = [DsTag.Int "Counter" := (intVar "Counter" .+. num 1)] }

    let ctx = Context.create()
    ctx.Memory.DeclareLocal("Counter", typeof<int>)
    ctx.Memory.Set("Counter", box 0)

    let engine = CpuScan.create (prog, Some ctx, Some { ScanConfig.Default with CycleTimeMs = Some 10 }, None, None)

    use cts = new CancellationTokenSource()
    CpuScan.start(engine, Some cts.Token).Wait()

    // Wait for at least 5 scans (5 * 10ms = 50ms + overhead)
    Thread.Sleep 100

    cts.Cancel()
    CpuScan.stopAsync(engine).Wait()

    let scanCount = ctx.Memory.Get("Counter") :?> int

    // Should have executed at least 5 scans
    scanCount |> should be (greaterThanOrEqualTo 5)

do ()
