namespace Ev2.Cpu.Runtime

open System
open System.Threading
open System.Globalization
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime

// ========================================================
// 공통: 엔진/컨텍스트 초기화 & 시나리오용 도우미
// ========================================================
module Harness =
    let mkEngine (prog: Statement.Program, cycleMs) =
        let ctx = Context.create()
        ctx.CycleTime <- cycleMs
        let eng = CpuScan.create (prog, Some ctx, None, None, None)
        ctx, eng

    let start (eng: CpuScanEngine, token: CancellationToken) =
        CpuScan.start (eng, Some token) |> ignore

    let stop (eng: CpuScanEngine) =
        CpuScan.stopAsync eng |> ignore

    let sleep(ms:int) = Thread.Sleep ms
    
    let logSnapshot label (ctx: ExecutionContext) =
        printfn "[%s] %s" label (ctx.Memory.SnapshotText())


// ────────────────────────────────────────────────────────
// ① Conveyor (컨베이어) — 래치/알람/산술
// ────────────────────────────────────────────────────────
module ConveyorDemo =
    open Statement
    open Harness

    // 프로그램 시작 시 변수 레지스트리 초기화
    let initializeVariables () =
        clearVariableRegistry()
        
        // 입력 변수들을 먼저 레지스트리에 등록
        ignore (DsTag.Bool "Start")
        ignore (DsTag.Bool "Stop") 
        ignore (DsTag.Bool "EStop")
        ignore (DsTag.Double "Temp")
        ignore (DsTag.Double "Threshold")
        ignore (DsTag.Double "Scale")
        
        // 출력 변수들을 레지스트리에 등록
        ignore (DsTag.Bool "Motor")
        ignore (DsTag.Bool "Alarm") 
        ignore (DsTag.Double "Speed")

    let program : Program =
        initializeVariables()
        
        { Name = "Conveyor"
          Inputs =
            [ "Start",     typeof<bool>
              "Stop",      typeof<bool>
              "EStop",     typeof<bool>
              "Temp",      typeof<double>
              "Threshold", typeof<double>
              "Scale",     typeof<double> ]
          Outputs =
            [ "Motor", typeof<bool>
              "Alarm", typeof<bool>
              "Speed", typeof<double> ]
          Locals  = []
          Body =
            [ // 모터 래치 - 동일한 이름의 변수는 같은 객체 사용
              let startVar = boolVar "Start"      // 첫 번째 사용
              let stopVar = boolVar "Stop"        // 첫 번째 사용
              let estopVar = boolVar "EStop"      // 첫 번째 사용
              let tempVar = dblVar "Temp"         // 첫 번째 사용
              let thresholdVar = dblVar "Threshold" // 첫 번째 사용
              let scaleVar = dblVar "Scale"       // 첫 번째 사용
              let motorTag = DsTag.Bool "Motor"   // 첫 번째 사용
              let alarmTag = DsTag.Bool "Alarm"   // 첫 번째 사용
              let speedTag = DsTag.Double "Speed" // 첫 번째 사용
              
              let sets   = (startVar &&. (!!. estopVar) &&. (!!. stopVar))
              let resets = (stopVar ||. estopVar)
              (sets, resets) ==| "Motor"

              // 알람: 온도 초과 또는 EStop - 이미 등록된 변수 재사용
              alarmTag := ((tempVar >>. thresholdVar) ||. estopVar)

              // 속도: ON일 때만 Temp * Scale - 이미 등록된 변수 재사용
              (boolVar "Motor", mul [ tempVar; scaleVar ]) -~> "Speed" ] }

    let run () =
        let ctx, eng = mkEngine(program, 100)
        let m = ctx.Memory

        // 초기값 설정 - 레지스트리에서 동일한 변수 사용
        m.DeclareInput("Start", typeof<bool>);     m.SetInput("Start",     box false)
        m.DeclareInput("Stop", typeof<bool>);      m.SetInput("Stop",      box false)
        m.DeclareInput("EStop", typeof<bool>);     m.SetInput("EStop",     box false)
        m.DeclareInput("Temp", typeof<double>);    m.SetInput("Temp",      box 25.0)
        m.DeclareInput("Threshold", typeof<double>); m.SetInput("Threshold", box 60.0)
        m.DeclareInput("Scale", typeof<double>);   m.SetInput("Scale",     box 0.1)

        m.DeclareOutput("Motor", typeof<bool>);  m.Set("Motor", TypeHelpers.getDefaultValue typeof<bool>)
        m.DeclareOutput("Alarm", typeof<bool>);  m.Set("Alarm", TypeHelpers.getDefaultValue typeof<bool>)
        m.DeclareOutput("Speed", typeof<double>);m.Set("Speed", TypeHelpers.getDefaultValue typeof<double>)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        printfn "\n--- (A) Start ON ---"
        m.SetInput("Start", box true); sleep 300; logSnapshot "A" ctx

        printfn "\n--- (B) Temp 70.0 ---"
        m.SetInput("Temp", box 70.0);  sleep 300; logSnapshot "B" ctx

        printfn "\n--- (C) Stop ON ---"
        m.SetInput("Stop", box true);  sleep 300; logSnapshot "C" ctx

        printfn "\n--- (D) EStop ON ---"
        m.SetInput("EStop", box true); sleep 300; logSnapshot "D" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ────────────────────────────────────────────────────────
// ② Traffic Light (신호등) — 상태+타이머(PLC TON)
//     STATE: 0=RED, 1=GREEN, 2=YELLOW
// ────────────────────────────────────────────────────────
module TrafficLightDemo =
    open Statement
    open Harness

    let initializeVariables () =
        clearVariableRegistry()
        
        // 입력 변수 등록
        ignore (DsTag.Bool "Start")
        ignore (DsTag.Bool "EStop")
        ignore (DsTag.Int "T_RED")
        ignore (DsTag.Int "T_GREEN")  
        ignore (DsTag.Int "T_YELLOW")
        
        // 출력 변수 등록
        ignore (DsTag.Bool "RED")
        ignore (DsTag.Bool "YELLOW")
        ignore (DsTag.Bool "GREEN")
        
        // 로컬 변수 등록
        ignore (DsTag.Int "STATE")

    let program : Program =
        initializeVariables()
        
        // 변수들을 한 번만 생성하여 재사용
        let startVar = boolVar "Start"
        let estopVar = boolVar "EStop"
        let stateVar = intVar "STATE"
        let redTag = DsTag.Bool "RED"
        let yellowTag = DsTag.Bool "YELLOW"  
        let greenTag = DsTag.Bool "GREEN"
        let stateTag = DsTag.Int "STATE"
        
        let st n = (stateVar ==. num n)
        let active = (startVar &&. (!!. estopVar))

        { Name = "TrafficLight"
          Inputs =
            [ "Start",    typeof<bool>
              "EStop",    typeof<bool>
              "T_RED",    typeof<int>
              "T_GREEN",  typeof<int>
              "T_YELLOW", typeof<int> ]
          Outputs =
            [ "RED",    typeof<bool>
              "YELLOW", typeof<bool>
              "GREEN",  typeof<bool> ]
          Locals  = [ "STATE", typeof<int> ]
          Body =
              [
                // 안전 우선: 비활성 시 모두 OFF
                ((!!. active), Const(box false, typeof<bool>)) -~> "RED"
                ((!!. active), Const(box false, typeof<bool>)) -~> "YELLOW"
                ((!!. active), Const(box false, typeof<bool>)) -~> "GREEN"

                // 상태별 출력 - 이미 등록된 변수 재사용
                redTag := (active &&. st 0)
                greenTag := (active &&. st 1)
                yellowTag := (active &&. st 2)

                // 상태 전이
                // CRITICAL FIX (DEFECT-020-4): Updated TON to 3-arg form (enable, name, preset)
                // Previous 2-arg form is deprecated and causes runtime errors
                (active &&. st 0 &&. call "TON" [ Const(box true, typeof<bool>); str "TL_RED"    ; intVar "T_RED"    ])
                  --> (call "MOV" [ num 1; strVar "STATE" ])

                (active &&. st 1 &&. call "TON" [ Const(box true, typeof<bool>); str "TL_GREEN"  ; intVar "T_GREEN"  ])
                  --> (call "MOV" [ num 2; strVar "STATE" ])

                (active &&. st 2 &&. call "TON" [ Const(box true, typeof<bool>); str "TL_YELLOW" ; intVar "T_YELLOW" ])
                  --> (call "MOV" [ num 0; strVar "STATE" ])

                // EStop 시 즉시 상태 초기화
                estopVar --> (call "MOV" [ num 0; strVar "STATE" ])
              ]
              }

    let run () =
        let ctx, eng = mkEngine(program, 100)
        let m = ctx.Memory

        // 선언/초기화
        for (n,t) in program.Inputs  do m.DeclareInput(n,t)
        for (n,t) in program.Outputs do m.DeclareOutput(n,t)
        for (n,t) in program.Locals  do m.DeclareLocal(n,t)

        m.SetInput("Start", box true)
        m.SetInput("EStop", box false)
        m.SetInput("T_RED", box 1200)
        m.SetInput("T_GREEN", box 1200)
        m.SetInput("T_YELLOW", box 700)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        printfn "\n[Run] normal 6s"; sleep 6000; logSnapshot "6s" ctx
        printfn "\n[Action] EStop ON"; m.SetInput("EStop", box true); sleep 800; logSnapshot "EStop" ctx
        printfn "[Action] EStop OFF + Start ON"; m.SetInput("EStop", box false); m.SetInput("Start", box true)
        sleep 2500; logSnapshot "Resume" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ────────────────────────────────────────────────────────
// ③ Tank Level — 비교/경보/스케일링
//     IN: Level, Set, Enable
//     OUT: Valve(ON if Level<Set && Enable), AlarmHi(Level>Set+5)
// ────────────────────────────────────────────────────────
module TankLevelDemo =
    open Statement
    open Harness

    let initializeVariables () =
        clearVariableRegistry()
        
        // 변수 등록
        ignore (DsTag.Bool "Enable")
        ignore (DsTag.Double "Level")
        ignore (DsTag.Double "Set")
        ignore (DsTag.Bool "Valve")
        ignore (DsTag.Bool "AlarmHi")

    let program : Program =
        initializeVariables()
        
        // 변수들을 한 번만 생성
        let enableVar = boolVar "Enable"
        let levelVar = dblVar "Level"
        let setVar = dblVar "Set"
        let valveTag = DsTag.Bool "Valve"
        let alarmTag = DsTag.Bool "AlarmHi"
        
        { Name = "TankLevel"
          Inputs  = [ "Enable", typeof<bool>; "Level", typeof<double>; "Set", typeof<double> ]
          Outputs = [ "Valve", typeof<bool>;  "AlarmHi", typeof<bool> ]
          Locals  = []
          Body =
            [ valveTag   := (enableVar &&. (levelVar <<. setVar))
              alarmTag := (levelVar >=. add [ setVar; dbl 5.0 ]) ] }

    let run () =
        let ctx, eng = mkEngine(program, 100)
        let m = ctx.Memory

        for (n,t) in program.Inputs  do m.DeclareInput(n,t)
        for (n,t) in program.Outputs do m.DeclareOutput(n,t)

        m.SetInput("Enable", box true)
        m.SetInput("Set", box 50.0)
        m.SetInput("Level", box 40.0)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        sleep 300; logSnapshot "Level=40" ctx
        m.SetInput("Level", box 55.0); sleep 300; logSnapshot "Level=55" ctx
        m.SetInput("Enable", box false); sleep 300; logSnapshot "Disabled" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ────────────────────────────────────────────────────────
// ④ Flasher — TOF로 깜빡이 (Blink 주기)
//     IN: BlinkEn, Period(ms)
//     OUT: Lamp
// ────────────────────────────────────────────────────────
module FlasherDemo =
    open Statement
    open Harness

    let initializeVariables () =
        clearVariableRegistry()
        
        ignore (DsTag.Bool "BlinkEn")
        ignore (DsTag.Int "Period")
        ignore (DsTag.Bool "Lamp")

    let program : Program =
        initializeVariables()
        
        let lampTag = DsTag.Bool "Lamp"
        let periodVar = intVar "Period"
        
        { Name="Flasher"
          Inputs  = [ "BlinkEn", typeof<bool>; "Period", typeof<int> ]
          Outputs = [ "Lamp", typeof<bool> ]
          Locals  = []
          Body =
            [ // 주기 절반마다 true → Lamp := 해당 신호
              lampTag := call "TON" [ str "BLINK"; periodVar ]
            ] }

    let run () =
        let ctx, eng = mkEngine(program, 100)
        let m = ctx.Memory
        for (n,t) in program.Inputs  do m.DeclareInput(n,t)
        for (n,t) in program.Outputs do m.DeclareOutput(n,t)

        m.SetInput("BlinkEn", box true)   // (현재 TON 구현은 enable=true 내장)
        m.SetInput("Period",  box 500)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        for i in 1..6 do sleep 300; logSnapshot $"tick{i}" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ────────────────────────────────────────────────────────
// ⑤ Counter — 상승엣지 카운트 (CTU)
//     IN: Pulse, Preset
//     OUT: Done, Count(변수로 관찰)
// ────────────────────────────────────────────────────────
module CounterDemo =
    open Statement
    open Harness

    let initializeVariables () =
        clearVariableRegistry()
        
        ignore (DsTag.Bool "Pulse")
        ignore (DsTag.Int "Preset")
        ignore (DsTag.Bool "Done")
        ignore (DsTag.Int "Count")

    let program : Program =
        initializeVariables()
        
        let pulseVar = boolVar "Pulse"
        let presetVar = intVar "Preset"
        let countTag = DsTag.Int "Count"
        let countVar = intVar "Count" 
        let doneTag = DsTag.Bool "Done"
        
        { Name="Counter"
          Inputs  = [ "Pulse", typeof<bool>; "Preset", typeof<int> ]
          Outputs = [ "Done", typeof<bool> ]
          Locals  = [ "Count", typeof<int> ]
          Body =
            [ // 상승에지 카운트 - 3-인자 버전 사용 (counter_name, enable, preset)
              countTag := call "CTU" [ str "C1"; pulseVar; presetVar ]
              doneTag := (countVar >=. presetVar) ] }

    let run () =
        let ctx, eng = mkEngine(program, 50)
        let m = ctx.Memory

        for (n,t) in program.Inputs  do m.DeclareInput(n,t)
        for (n,t) in program.Outputs do m.DeclareOutput(n,t)
        for (n,t) in program.Locals  do m.DeclareLocal(n,t)

        m.SetInput("Preset", box 3)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        // 펄스 5번
        for i in 1..5 do
            m.SetInput("Pulse", box true);  sleep 60
            m.SetInput("Pulse", box false); sleep 60
            logSnapshot $"pulse{i}" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ────────────────────────────────────────────────────────
// ⑥ WorkflowSequence — Work1 ↔ W1 StartReset 양방향 제어
//     Work1: Device1.ADV → Device1.RET → Device1_ADV_1 → Device1_RET_1
//     W1: Device2.RET → Device2.ADV → Device2_RET_1 → Device2_ADV_1 → Device2.RET
// ────────────────────────────────────────────────────────
module WorkflowSequenceDemo =
    open Statement
    open Harness

    let initializeVariables () =
        clearVariableRegistry()
        
        // 입력 변수 등록
        ignore (DsTag.Bool "StartReset_Work1")
        ignore (DsTag.Bool "StartReset_W1")
        ignore (DsTag.Bool "Enable")
        
        // 출력 변수 등록
        ignore (DsTag.Bool "Work1_Active")
        ignore (DsTag.Bool "W1_Active")
        ignore (DsTag.Bool "Device1_ADV")
        ignore (DsTag.Bool "Device1_RET")
        ignore (DsTag.Bool "Device2_ADV")
        ignore (DsTag.Bool "Device2_RET")
        
        // 로컬 변수 등록
        ignore (DsTag.Int "Work1_State")
        ignore (DsTag.Int "W1_State")
        ignore (DsTag.Bool "Work1_Running")
        ignore (DsTag.Bool "W1_Running")
        ignore (DsTag.Bool "Work1_Complete")
        ignore (DsTag.Bool "W1_Complete")

    let program : Program =
        initializeVariables()
        
        // 변수들을 한 번만 생성하여 재사용
        let work1ActiveTag = DsTag.Bool "Work1_Active"
        let w1ActiveTag = DsTag.Bool "W1_Active"  
        let work1StateVar = intVar "Work1_State"
        let w1StateVar = intVar "W1_State"
        let startWork1Var = boolVar "StartReset_Work1"
        let startW1Var = boolVar "StartReset_W1"
        let enableVar = boolVar "Enable"
        let work1CompleteTag = DsTag.Bool "Work1_Complete"
        let w1CompleteTag = DsTag.Bool "W1_Complete"
        let device1AdvTag = DsTag.Bool "Device1_ADV"
        let device1RetTag = DsTag.Bool "Device1_RET"
        let device2AdvTag = DsTag.Bool "Device2_ADV"
        let device2RetTag = DsTag.Bool "Device2_RET"
        
        let work1Active = boolVar "Work1_Active"
        let w1Active = boolVar "W1_Active"
        
        { Name = "WorkflowSequence"
          Inputs =
            [ "StartReset_Work1", typeof<bool>
              "StartReset_W1", typeof<bool>
              "Enable", typeof<bool> ]
          Outputs =
            [ "Work1_Active", typeof<bool>
              "W1_Active", typeof<bool>
              "Device1_ADV", typeof<bool>
              "Device1_RET", typeof<bool>
              "Device2_ADV", typeof<bool>
              "Device2_RET", typeof<bool> ]
          Locals  =
            [ "Work1_State", typeof<int>
              "W1_State", typeof<int>
              "Work1_Running", typeof<bool>
              "W1_Running", typeof<bool>
              "Work1_Complete", typeof<bool>
              "W1_Complete", typeof<bool> ]
          Body =
            [
              // Work1 Running 플래그 제어
              (startWork1Var &&. (work1StateVar ==. num 0) &&. (!!. (boolVar "Work1_Running")))
                --> (call "MOV" [ Const(box true, typeof<bool>); strVar "Work1_Running" ])
              
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 0)) 
                --> (call "MOV" [ num 1; strVar "Work1_State" ])
              
              // Work1 상태 전이 - Running일 때만 진행
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 1) &&. call "TON" [ str "W1_DEV1_ADV"; num 1000 ]) 
                --> (call "MOV" [ num 2; strVar "Work1_State" ])
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 2) &&. call "TON" [ str "W1_DEV1_RET"; num 1000 ]) 
                --> (call "MOV" [ num 3; strVar "Work1_State" ])
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 3) &&. call "TON" [ str "W1_DEV1_ADV_1"; num 1000 ]) 
                --> (call "MOV" [ num 4; strVar "Work1_State" ])
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 4) &&. call "TON" [ str "W1_DEV1_RET_1"; num 1000 ]) 
                --> (call "MOV" [ num 0; strVar "Work1_State" ])
              
              // Work1 완료 시 Running 플래그 해제
              (boolVar "Work1_Running" &&. (work1StateVar ==. num 0) &&. call "TON" [ str "W1_COMPLETE_DELAY"; num 100 ])
                --> (call "MOV" [ Const(box false, typeof<bool>); strVar "Work1_Running" ])

              // W1 Running 플래그 제어
              (startW1Var &&. (w1StateVar ==. num 0) &&. (!!. (boolVar "W1_Running")))
                --> (call "MOV" [ Const(box true, typeof<bool>); strVar "W1_Running" ])
              
              (boolVar "W1_Running" &&. (w1StateVar ==. num 0)) 
                --> (call "MOV" [ num 1; strVar "W1_State" ])
              
              // W1 상태 전이 - Running일 때만 진행
              (boolVar "W1_Running" &&. (w1StateVar ==. num 1) &&. call "TON" [ str "W1_DEV2_RET"; num 1000 ]) 
                --> (call "MOV" [ num 2; strVar "W1_State" ])
              (boolVar "W1_Running" &&. (w1StateVar ==. num 2) &&. call "TON" [ str "W1_DEV2_ADV"; num 1000 ]) 
                --> (call "MOV" [ num 3; strVar "W1_State" ])
              (boolVar "W1_Running" &&. (w1StateVar ==. num 3) &&. call "TON" [ str "W1_DEV2_RET_1"; num 1000 ]) 
                --> (call "MOV" [ num 4; strVar "W1_State" ])
              (boolVar "W1_Running" &&. (w1StateVar ==. num 4) &&. call "TON" [ str "W1_DEV2_ADV_1"; num 1000 ]) 
                --> (call "MOV" [ num 5; strVar "W1_State" ])
              (boolVar "W1_Running" &&. (w1StateVar ==. num 5) &&. call "TON" [ str "W1_DEV2_RET_END"; num 1000 ]) 
                --> (call "MOV" [ num 0; strVar "W1_State" ])
              
              // W1 완료 시 Running 플래그 해제
              (boolVar "W1_Running" &&. (w1StateVar ==. num 0) &&. call "TON" [ str "W1_W1_COMPLETE_DELAY"; num 100 ])
                --> (call "MOV" [ Const(box false, typeof<bool>); strVar "W1_Running" ])

              // 워크플로우 활성화 상태 - Running 플래그 기반
              work1ActiveTag := boolVar "Work1_Running"
              w1ActiveTag := boolVar "W1_Running"

              // 완료 플래그
              work1CompleteTag := (boolVar "Work1_Running" &&. (work1StateVar ==. num 4) &&. call "TON" [ str "W1_DEV1_RET_1"; num 1000 ])
              w1CompleteTag := (boolVar "W1_Running" &&. (w1StateVar ==. num 5) &&. call "TON" [ str "W1_DEV2_RET_END"; num 1000 ])

              // 양방향 StartReset 제어
              (boolVar "Work1_Complete") --> (call "MOV" [ Const(box true, typeof<bool>); strVar "StartReset_W1" ])
              (boolVar "W1_Complete") --> (call "MOV" [ Const(box true, typeof<bool>); strVar "StartReset_Work1" ])

              // 디바이스 출력 매핑
              device1AdvTag := (boolVar "Work1_Running" &&. ((work1StateVar ==. num 1) ||. (work1StateVar ==. num 3)))
              device1RetTag := (boolVar "Work1_Running" &&. ((work1StateVar ==. num 2) ||. (work1StateVar ==. num 4)))
              device2AdvTag := (boolVar "W1_Running" &&. ((w1StateVar ==. num 2) ||. (w1StateVar ==. num 4)))
              device2RetTag := (boolVar "W1_Running" &&. ((w1StateVar ==. num 1) ||. (w1StateVar ==. num 3) ||. (w1StateVar ==. num 5)))
            ]
        }

    let run () =
        let ctx, eng = mkEngine(program, 100)
        let m = ctx.Memory

        // 변수 선언
        for (n,t) in program.Inputs  do m.DeclareInput(n,t)
        for (n,t) in program.Outputs do m.DeclareOutput(n,t)
        for (n,t) in program.Locals  do m.DeclareLocal(n,t)

        // 초기화
        m.SetInput("Enable", box true)
        m.SetInput("StartReset_Work1", box false)
        m.SetInput("StartReset_W1", box false)

        use cts = new CancellationTokenSource()
        start(eng, cts.Token)

        printfn "\n--- Starting Work1 Sequence ---"
        m.SetInput("StartReset_Work1", box true); sleep 500; logSnapshot "Work1 Started" ctx
        m.SetInput("StartReset_Work1", box false)

        // Work1 시퀀스 실행 (Device1: ADV → RET → ADV_1 → RET_1)
        for i in 1..5 do
            sleep 1200; logSnapshot $"Work1 Step {i}" ctx

        // W1이 자동 시작되어야 함
        printfn "\n--- W1 should auto-start after Work1 completion ---"
        for i in 1..6 do
            sleep 1200; logSnapshot $"W1 Step {i}" ctx

        cts.Cancel(); stop eng
        logSnapshot "Final" ctx


// ========================================================
// 변수 레지스트리 상태 확인 유틸리티
// ========================================================
module VariableRegistryUtils =
    let printRegistryStatus(title: string) =
        printfn "\n=== %s ===" title
        let vars = getAllRegisteredVariables()
        if List.isEmpty vars then
            printfn "Registry is empty"
        else
            vars |> List.iter (fun v -> printfn "  %s" (v.ToString()))
        printfn "Total variables: %d" vars.Length


// ========================================================
// 실행 선택: 아래 중 하나만 주석 해제해서 실행하세요
// ========================================================
module WhichDemo =
    //let run = CounterDemo.run   // ← 기본 실행
    let run =    FlasherDemo.run
    // let run = TankLevelDemo.run
    // let run = TrafficLightDemo.run
    // let run = ConveyorDemo.run
    // let run = WorkflowSequenceDemo.run

module Program =
    [<EntryPoint>]
    let main _ =
        // 실행 전 레지스트리 상태 확인
        VariableRegistryUtils.printRegistryStatus("Before Demo")
        
        //WhichDemo.run()
        CounterDemo.run()
        FlasherDemo.run()
        TankLevelDemo.run()
        TrafficLightDemo.run()
        ConveyorDemo.run()
        WorkflowSequenceDemo.run()
        
        // 실행 후 레지스트리 상태 확인
        VariableRegistryUtils.printRegistryStatus("After Demo")
        0
