module Ev2.Cpu.Test.Work

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.CodeWork

[<Fact>]
let ``WorkAuto - startWork 래치`` () =
    let pattern = WorkAuto.createPattern WorkKind.startWork
    pattern.IsSome |> should equal true

    match pattern with
    | Some relay ->
        relay.Tag.Name |> should equal "startWork"
        relay.Tag.DataType |> should equal typeof<bool>

        match relay.Set with
        | Binary(DsOp.Or, Terminal(req), Terminal(self)) ->
            req.Name |> should equal "startWork_req"
            self |> should equal relay.Tag
        | _ -> failwith "Expected latch set pattern"

        match relay.Reset with
        | Terminal(doneTag) -> doneTag.Name |> should equal "startWork_done"
        | _ -> failwith "Expected done terminal for reset"
    | None -> failwith "Expected relay"

[<Fact>]
let ``WorkStats - Move INC DIV 포함`` () =
    let stats = Stats.create "cycle"

    stats.Length |> should equal 3

    // MOV 사용 (Move → MOV)
    stats |> List.exists (function
        | Command(_, _, Function("MOV", _)) -> true
        | _ -> false) |> should equal true

    // INC 대신 ADD 사용 (Assign 사용)
    stats |> List.exists (function
        | Assign(_, _, Function("ADD", _)) -> true
        | _ -> false) |> should equal true

    // DIV는 Assign으로 변경
    stats |> List.exists (function
        | Assign(_, _, Function("DIV", _)) -> true
        | _ -> false) |> should equal true

[<Fact>]
let ``WorkInterlock - 조건별 fault 태그 생성`` () =
    let conds = [
        "safety", boolTag "safety_ok"
        "door", !!. (boolTag "door_closed")
    ]

    let stmts = Interlock.create "machine" conds
    stmts.Length |> should equal (1 + conds.Length)

    match stmts.Head with
    | Assign(_, tag, expr) ->
        tag.Name |> should equal "machine_interlock_ok"
        expr.InferType() |> should equal typeof<bool>
    | _ -> failwith "Expected interlock assignment"

    stmts.Tail |> List.iter (function
        | Assign(_, tag, _) -> tag.Name |> should startWith "machine_"
        | _ -> failwith "Expected Assign for detail faults")
