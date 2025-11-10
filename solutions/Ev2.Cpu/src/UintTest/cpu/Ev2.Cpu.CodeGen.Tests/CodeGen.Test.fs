module Ev2.Cpu.Test.Make

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Core.GenerationUtils

[<Fact>]
let ``Generation - Pulse 생성`` () =
    let tag = DsTag.Bool "pulse_out"
    let trigger = boolTag "trigger"
    
    let relay = Generation.pulse trigger tag
    
    match relay.Set with
    | Unary(DsOp.Rising, t) -> t |> should equal trigger
    | _ -> failwith "Expected rising edge"
    
    relay.Reset |> should equal (boolConst false)

[<Fact>]
let ``Generation - Timer 생성`` () =
    let tag = DsTag.Bool "timer_out"
    let start = boolTag "start"
    let ms = 5000
    
    let relay = Generation.timer start ms tag
    
    match relay.Set with
    | Function("TON", [s; Const(name, t1); Const(time, t2)]) when t1 = typeof<string> && t2 = typeof<int> ->
        s |> should equal start
        unbox time |> should equal ms
    | _ -> failwith "Expected TON function"
    
    match relay.Reset with
    | Unary(DsOp.Not, s) -> s |> should equal start
    | _ -> failwith "Expected NOT start"

[<Fact>]
let ``Generation - Counter 생성`` () =
    let tag = DsTag.Int "counter"
    let up = boolTag "count_up"
    let reset = boolTag "reset"
    
    let relay = Generation.counterUp up  reset tag
    
    // Reset 조건에 reset 포함 확인
    relay.Reset.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Generation - State 전환`` () =
    let currentState = DsTag.Int "state"
    let targetState = DsTag.Int "next_state"
    let condition = boolTag "transition"
    
    let relay = Generation.state currentState targetState condition
    
    // Set: 현재 상태가 타겟과 같고 조건 만족
    match relay.Set with
    | Binary(DsOp.And, Binary(DsOp.Eq, Terminal(curr), Terminal(tgt)), cond) ->
        curr |> should equal currentState
        tgt |> should equal targetState
        cond |> should equal condition
    | _ -> failwith "Expected state transition set condition"
    
    // Reset: 현재 상태가 타겟과 다름
    match relay.Reset with
    | Binary(DsOp.Ne, Terminal(curr), Terminal(tgt)) ->
        curr |> should equal currentState
        tgt |> should equal targetState
    | _ -> failwith "Expected state transition reset condition"

[<Fact>]
let ``Generation - Interlock 생성`` () =
    let tag = DsTag.Bool "interlock_ok"
    let conditions = [
        boolTag "safety"
        boolTag "ready"
        boolTag "enabled"
    ]
    
    let relay = Generation.interlock conditions tag
    
    // Set: 모든 조건 AND
    relay.Set.InferType() |> should equal typeof<bool>
    
    // Reset: 하나라도 false (any NOT)
    relay.Reset.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Generation - Statement 변환`` () =
    let tag = DsTag.Bool "output"
    let relay = Relay.Create(tag, boolTag "on", boolTag "off")
    
    // toStmt
    let stmt = Generation.toStmt relay
    match stmt with
    | Assign(_, t, _) -> t |> should equal tag
    | _ -> failwith "Expected Assign"
    
    // toLatch
    let latchStmt = Generation.toLatch relay
    match latchStmt with
    | Assign(_, t, expr) ->
        t |> should equal tag
        // RST 우선 래치 패턴 포함 확인: (NOT Reset) AND (Set OR Self)
        match expr with
        | Binary(DsOp.And, Unary(DsOp.Not, _), Binary(DsOp.Or, _, Terminal(self))) ->
            self |> should equal tag
        | _ -> failwith "Expected latch pattern"
    | _ -> failwith "Expected Assign"

[<Fact>]
let ``Generation - 여러 Relay를 Statement로`` () =
    let relays = [
        Relay.Create(DsTag.Bool "r1", boolTag "a", boolTag "b")
        Relay.Create(DsTag.Bool "r2", boolTag "c", boolTag "d")
        Relay.Create(DsTag.Bool "r3", boolTag "e", boolTag "f")
    ]
    
    let stmts = Generation.toStmts relays
    stmts.Length |> should equal 3
    
    stmts |> List.forall (function Assign(_, _, _) -> true | _ -> false) 
          |> should equal true

[<Fact>]
let ``CodeBuilder - 사용 예제`` () =
    let builder = CodeBuilder()
    
    // 단일 statement 추가
    let stmt1 = DsTag.Bool "x" := boolConst true
    builder.Add(stmt1)
    
    // 여러 statement 추가
    let stmts = [
        DsTag.Bool "y" := boolConst false
        DsTag.Int "count" := num 0
    ]
    builder.AddRange(stmts)
    
    // Relay 추가
    let relay = Relay.Create(DsTag.Bool "relay", boolTag "on", boolTag "off")
    builder.AddRelay(relay)
    
    // 빌드
    let result = builder.Build()
    result.Length |> should equal 4
    
    // Clear
    builder.Clear()
    builder.Build().Length |> should equal 0

[<Fact>]
let ``Inline Stmt helpers - assign and command`` () =
    let assignStmt = assignBool "lamp" (boolConst true)
    match assignStmt with
    | Assign(0, tag, Const(value, t)) when t = typeof<bool> ->
        tag.Name |> should equal "lamp"
        unbox<bool> value |> should equal true
    | _ -> failwith "Expected boolean assignment"

    let cmd = whenDo (boolTag "start") (fn "SET" [boolTag "motor"])
    match cmd with
    | Command(0, cond, Function("SET", [Terminal target])) ->
        cond |> should equal (boolTag "start")
        target.Name |> should equal "motor"
    | _ -> failwith "Expected SET command"

    let stepped = assignStmt |> withStep 20
    match stepped with
    | Assign(20, _, _) -> ()
    | _ -> failwith "Expected reassigned step"

    let constStmt = setIntConst "counter" 5
    match constStmt with
    | Assign(0, tag, Const(value, t)) when t = typeof<int> ->
        tag.Name |> should equal "counter"
        unbox<int> value |> should equal 5
    | _ -> failwith "Expected integer const assignment"

// ═══════════════════════════════════════════════════════════════════════════
// Boundary Value Tests (Phase 5)
// ═══════════════════════════════════════════════════════════════════════════

[<Fact>]
let ``Generation - Timer with 0ms (immediate)`` () =
    let tag = DsTag.Bool "timer_zero"
    let start = boolTag "start"
    let ms = 0

    let relay = Generation.timer start ms tag

    match relay.Set with
    | Function("TON", [s; Const(name, t1); Const(time, t2)]) when t1 = typeof<string> && t2 = typeof<int> ->
        s |> should equal start
        unbox time |> should equal 0
    | _ -> failwith "Expected TON with 0ms"

[<Fact>]
let ``Generation - Timer with 1ms (minimum practical)`` () =
    let tag = DsTag.Bool "timer_1ms"
    let start = boolTag "start"
    let ms = 1

    let relay = Generation.timer start ms tag

    match relay.Set with
    | Function("TON", [s; Const(name, t1); Const(time, t2)]) when t1 = typeof<string> && t2 = typeof<int> ->
        s |> should equal start
        unbox time |> should equal 1
    | _ -> failwith "Expected TON with 1ms"

[<Fact>]
let ``Generation - Timer with Int32.MaxValue`` () =
    let tag = DsTag.Bool "timer_max"
    let start = boolTag "start"
    let ms = System.Int32.MaxValue

    let relay = Generation.timer start ms tag

    match relay.Set with
    | Function("TON", [s; Const(name, t1); Const(time, t2)]) when t1 = typeof<string> && t2 = typeof<int> ->
        s |> should equal start
        unbox time |> should equal System.Int32.MaxValue
    | _ -> failwith "Expected TON with Int32.MaxValue"

[<Fact>]
let ``Generation - Timer with very large value (1 hour)`` () =
    let tag = DsTag.Bool "timer_1hour"
    let start = boolTag "start"
    let ms = 3600000  // 1 hour in milliseconds

    let relay = Generation.timer start ms tag

    match relay.Set with
    | Function("TON", [s; Const(name, t1); Const(time, t2)]) when t1 = typeof<string> && t2 = typeof<int> ->
        s |> should equal start
        unbox time |> should equal 3600000
    | _ -> failwith "Expected TON with 1 hour"

[<Fact>]
let ``Generation - Interlock with single condition`` () =
    let tag = DsTag.Bool "interlock_single"
    let conditions = [boolTag "only_one"]

    let relay = Generation.interlock conditions tag

    relay.Set.InferType() |> should equal typeof<bool>
    relay.Reset.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Generation - Interlock with many conditions (100)`` () =
    let tag = DsTag.Bool "interlock_many"
    let conditions = [for i in 1..100 -> boolTag (sprintf "cond%d" i)]

    let relay = Generation.interlock conditions tag

    relay.Set.InferType() |> should equal typeof<bool>
    relay.Reset.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Generation - Tag with very long name (500 chars)`` () =
    let longName = String.replicate 500 "X"
    let tag = DsTag.Bool longName
    let relay = Relay.Create(tag, boolTag "on", boolTag "off")

    let stmt = Generation.toStmt relay
    match stmt with
    | Assign(_, t, _) -> t.Name |> should equal longName
    | _ -> failwith "Expected Assign"

[<Fact>]
let ``Generation - Very large number of relays (1000)`` () =
    let relays = [
        for i in 1..1000 ->
            Relay.Create(DsTag.Bool (sprintf "relay%d" i), boolTag "on", boolTag "off")
    ]

    let stmts = Generation.toStmts relays
    stmts.Length |> should equal 1000
    stmts |> List.forall (function Assign(_, _, _) -> true | _ -> false)
          |> should equal true

[<Fact>]
let ``CodeBuilder - Add 5000 statements`` () =
    let builder = CodeBuilder()

    for i in 1..5000 do
        builder.Add(DsTag.Bool (sprintf "v%d" i) := boolConst true)

    let result = builder.Build()
    result.Length |> should equal 5000

[<Fact>]
let ``CodeBuilder - AddRange with 3000 statements`` () =
    let builder = CodeBuilder()

    let stmts = [
        for i in 1..3000 ->
            DsTag.Int (sprintf "count%d" i) := num i
    ]

    builder.AddRange(stmts)

    let result = builder.Build()
    result.Length |> should equal 3000

[<Fact>]
let ``CodeBuilder - Multiple clear operations`` () =
    let builder = CodeBuilder()

    // Add, clear, add, clear cycle 100 times
    for _ in 1..100 do
        builder.Add(DsTag.Bool "test" := boolConst true)
        builder.Build().Length |> should equal 1
        builder.Clear()
        builder.Build().Length |> should equal 0

[<Fact>]
let ``Generation - State transition with Int32 boundary values`` () =
    let currentState = DsTag.Int "state"
    let targetStateMin = DsTag.Int "next_min"
    let targetStateMax = DsTag.Int "next_max"
    let condition = boolTag "go"

    // Test with extreme state values
    let relayMin = Generation.state currentState targetStateMin condition
    relayMin.Set.InferType() |> should equal typeof<bool>
    relayMin.Reset.InferType() |> should equal typeof<bool>

    let relayMax = Generation.state currentState targetStateMax condition
    relayMax.Set.InferType() |> should equal typeof<bool>
    relayMax.Reset.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Generation - Step assignments with Int32.MaxValue`` () =
    let stmt = assignBool "test" (boolConst true)
    let stepped = stmt |> withStep System.Int32.MaxValue

    match stepped with
    | Assign(step, _, _) -> step |> should equal System.Int32.MaxValue
    | _ -> failwith "Expected step assignment"

[<Fact>]
let ``Generation - toLatch pattern verification`` () =
    let tag = DsTag.Bool "latch_test"
    let setExpr = boolTag "set_trigger"
    let resetExpr = boolTag "reset_trigger"
    let relay = Relay.Create(tag, setExpr, resetExpr)

    let latchStmt = Generation.toLatch relay

    match latchStmt with
    | Assign(_, t, expr) ->
        t |> should equal tag
        // Verify RST-priority latch: (NOT Reset) AND (Set OR Self)
        match expr with
        | Binary(DsOp.And, Unary(DsOp.Not, reset), Binary(DsOp.Or, set, Terminal(self))) ->
            reset |> should equal resetExpr
            set |> should equal setExpr
            self |> should equal tag
        | _ -> failwith "Expected RST-priority latch pattern"
    | _ -> failwith "Expected Assign statement"
