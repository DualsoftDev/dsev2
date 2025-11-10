module Ev2.Cpu.Test.Generation

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen
open Ev2.Cpu.Generation.Make.ProgramGen

[<Fact>]
let ``ExpressionGen - 상수와 연산 생성`` () =
    let boolValue = boolExpr true
    match boolValue with
    | Const(v, t) when t = typeof<bool> -> unbox<bool> v |> should equal true
    | _ -> failwith "Expected boolean constant"

    let addExpr = add (intExpr 1) (intExpr 2)
    match addExpr with
    | Binary(DsOp.Add, Const(l, t1), Const(r, t2)) when t1 = typeof<int> && t2 = typeof<int> ->
        unbox<int> l |> should equal 1
        unbox<int> r |> should equal 2
    | _ -> failwith "Expected integer addition"

    let tonExpr = ton "Timer1" (boolVar "start") 500
    match tonExpr with
    | Function("TON", [enable; name; preset]) ->
        enable |> should equal (boolVar "start")
        match name with
        | Const(v, t) when t = typeof<string> -> unbox<string> v |> should equal "Timer1"
        | _ -> failwith "Expected string timer name"
        match preset with
        | Const(v, t) when t = typeof<int> -> unbox<int> v |> should equal 500
        | _ -> failwith "Expected integer preset"
    | _ -> failwith "Expected TON function call"

    let limitExpr = limit (intExpr 0) (intExpr 5) (intExpr 10)
    match limitExpr with
    | Function("LIMIT", [minVal; value; maxVal]) ->
        minVal |> should equal (intExpr 0)
        value |> should equal (intExpr 5)
        maxVal |> should equal (intExpr 10)
    | _ -> failwith "Expected LIMIT function call"

[<Fact>]
let ``StatementGen - 래치와 시퀀스 스텝`` () =
    let latchStmt = latch (boolVar "set") (boolVar "reset") "coil"
    match latchStmt with
    | Assign(0, tag, expr) ->
        tag.Name |> should equal "coil"
        match expr with
        | Binary(DsOp.And, Binary(DsOp.Or, setExpr, Terminal(self)), Unary(DsOp.Not, resetExpr)) ->
            setExpr |> should equal (boolVar "set")
            self |> should equal (DsTag.Bool "coil")
            resetExpr |> should equal (boolVar "reset")
        | _ -> failwith "Expected latch expression pattern"
    | _ -> failwith "Expected Assign statement"

    let actions = [
        assignAt 0 "StepFlag" typeof<bool> (boolExpr true)
        whenAt 5 (boolVar "condition") (Function("NOP", []))
    ]
    let result = sequenceStep 1 (boolVar "advance") 2 "State" actions

    result.Length |> should equal (actions.Length + 1)
    let advanceCommand = List.last result
    match advanceCommand with
    | Command(0, Binary(DsOp.And, guard, cond), Function("MOV", [Const(next, t1); Terminal(target)])) when t1 = typeof<int> ->
        cond |> should equal (boolVar "advance")
        match guard with
        | Binary(DsOp.Eq, Terminal(stateVar), Const(step, t2)) when t2 = typeof<int> ->
            stateVar |> should equal (DsTag.Int "State")
            unbox<int> step |> should equal 1
        | _ -> failwith "Expected guard expression"
        target |> should equal (DsTag.Int "State")
        unbox<int> next |> should equal 2
    | _ -> failwith "Expected advance command"

[<Fact>]
let ``ProgramGen - ProgramBuilder 구성`` () =
    let builder = ProgramBuilder("Unit")
    builder.AddInput("Start", typeof<bool>)
    builder.AddOutput("Ready", typeof<bool>)
    builder.AddLocal("Counter", typeof<int>)

    let stmt1 = assign 5 (DsTag.Bool "Ready") (boolExpr true)
    builder.AddStatement(stmt1)

    let stmt2 = assignAt 0 "InterlockOk" typeof<bool> (boolExpr false)
    let stmt3 = whenAt 3 (boolVar "Start") (Function("NOP", []))
    builder.AddStatements([stmt2; stmt3])

    let relay = Relay.Create(DsTag.Bool "Lamp", boolVar "Start", boolExpr false)
    builder.AddRelay(relay)

    let program = builder.Build()
    program.Name |> should equal "Unit"
    program.Inputs |> should equal [ "Start", typeof<bool> ]
    program.Outputs |> should equal [ "Ready", typeof<bool> ]
    program.Locals |> should equal [ "Counter", typeof<int> ]

    program.Body.Length |> should equal 4

    let bodySteps = program.Body |> List.map Statement.getStepNumber
    bodySteps |> should equal [5; 0; 3; 0]

    match List.last program.Body with
    | Assign(_, tag, expr) ->
        tag |> should equal (DsTag.Bool "Lamp")
        match expr with
        | Binary(DsOp.And, Terminal(source), Unary(DsOp.Not, Const(resetVal, t))) when t = typeof<bool> ->
            source |> should equal (DsTag.Bool "Start")
            unbox<bool> resetVal |> should equal false
        | _ -> failwith "Expected relay expression"
    | _ -> failwith "Expected Assign for relay"

[<Fact>]
let ``ProgramGen - 상태 기계 프로그램`` () =
    let states = [
        "Idle", [ assignAuto "IdleOutput" typeof<bool> (boolExpr true) ]
        "Run",  [ assignAuto "RunOutput" typeof<bool> (boolExpr false) ]
    ]

    let program = createStateMachineProgram "Machine" states

    program.Locals |> should equal [
        "State", typeof<int>
        "NextState", typeof<int>
        "Idle_Active", typeof<bool>
        "Run_Active", typeof<bool>
    ]

    program.Body.Length |> should equal 5

    match program.Body.Head with
    | Assign(0, tag, Binary(DsOp.Eq, Terminal(stateVar), Const(idx, t))) when t = typeof<int> ->
        tag |> should equal (DsTag.Bool "Idle_Active")
        stateVar |> should equal (DsTag.Int "State")
        unbox<int> idx |> should equal 0
    | _ -> failwith "Expected Idle state activation"

    match List.item 2 program.Body with
    | Assign(0, tag, Binary(DsOp.Eq, Terminal(stateVar), Const(idx, t))) when t = typeof<int> ->
        tag |> should equal (DsTag.Bool "Run_Active")
        stateVar |> should equal (DsTag.Int "State")
        unbox<int> idx |> should equal 1
    | _ -> failwith "Expected Run state activation"

    match List.last program.Body with
    | Assign(0, tag, Terminal(source)) ->
        tag |> should equal (DsTag.Int "State")
        source |> should equal (DsTag.Int "NextState")
    | _ -> failwith "Expected state update assignment"
