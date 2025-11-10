module Ev2.Cpu.Test.CodeBuilder

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.Make.ExpressionGen

[<Fact>]
let ``GenerationUtils - Relay 리스트를 Statement로`` () =
    let relays = [
        Relay.Create(DsTag.Bool "r1", boolVar "cond1", boolExpr false)
        Relay.Create(DsTag.Bool "r2", boolExpr true, boolExpr false)
    ]

    let stmts = GenerationUtils.relaysToStmts relays
    stmts.Length |> should equal relays.Length

    List.zip relays stmts
    |> List.iter (fun (relay, stmt) ->
        match stmt with
        | Assign(0, tag, expr) ->
            tag |> should equal relay.Tag
            expr |> should equal (relay.ToExpr())
        | _ -> failwith "Expected Assign statement")

[<Fact>]
let ``Generation - CounterDown 및 Latch 변환`` () =
    let tag = DsTag.Int "counter"
    let down = boolVar "down"
    let reset = boolVar "reset"

    let relay = Generation.counterDown down reset tag
    relay.Tag |> should equal tag

    match relay.Set with
    | Binary(DsOp.And, cond, Function("CTD", [Const(name, DsDataType.TString); downArg; loadArg; presetArg])) ->
        cond |> should equal down
        // CTD now has 4-arg form: [name; down; load; preset]
        downArg |> should equal down
    | _ -> failwith "Expected CTD counter pattern"

    relay.Reset |> should equal reset

    let relays = [
        Relay.Create(DsTag.Bool "l1", boolVar "s1", boolExpr false)
        Relay.Create(DsTag.Bool "l2", boolExpr true, boolExpr false)
    ]

    let latchStmts = Generation.toLatches relays
    latchStmts.Length |> should equal relays.Length

    List.zip relays latchStmts
    |> List.iter (fun (relay, stmt) ->
        match stmt with
        | Assign(0, tag, expr) ->
            tag |> should equal relay.Tag
            expr |> should equal (relay.ToLatch())
        | _ -> failwith "Expected latch Assign")

[<Fact>]
let ``CodeBuilder - 자동 스텝 배치`` () =
    let builder = CodeBuilder()

    let mkAssign name value =
        Assign(0, DsTag.Bool name, Const(box value, DsDataType.TBool))

    builder.Add(mkAssign "Step1" true)
    builder.AddRange([mkAssign "Step2" false; mkAssign "Step3" true])

    let result = builder.Build()
    result.Length |> should equal 3
    result |> List.map Statement.getStepNumber |> should equal [10; 20; 30]

[<Fact>]
let ``CodeBuilder - 명시적 스텝 이후 자동 스텝`` () =
    let builder = CodeBuilder()

    let manual = Assign(25, DsTag.Bool "Manual", boolExpr true)
    builder.Add(manual)

    let auto = Assign(0, DsTag.Bool "Auto", boolExpr true)
    builder.Add(auto)

    let result = builder.Build()
    result |> List.map Statement.getStepNumber |> should equal [25; 30]
