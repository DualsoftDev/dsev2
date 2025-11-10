module Ev2.Cpu.Test.Call

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.CodeCall

[<Fact>]
let ``CallAuto - callActive 패턴`` () =
    let pattern = CallAuto.createPattern CallKind.callActive
    pattern.IsSome |> should equal true

    match pattern with
    | Some relay ->
        relay.Tag.Name |> should equal "callActive"

        match relay.Set with
        | Binary(DsOp.And, Binary(DsOp.Or, sc, Terminal(self)), Unary(DsOp.Not, rc)) ->
            match sc with
            | Terminal(tag) -> tag.Name |> should equal "callActive_SC"
            | _ -> failwith "Expected SC terminal"
            self |> should equal relay.Tag
            match rc with
            | Terminal(tag) -> tag.Name |> should equal "callActive_RC"
            | _ -> failwith "Expected RC terminal"
        | _ -> failwith "Expected latch pattern with RC guard"

        match relay.Reset with
        | Binary(DsOp.Or, Terminal(ec), Terminal(rc)) ->
            ec.Name |> should equal "callActive_EC"
            rc.Name |> should equal "callActive_RC"
        | _ -> failwith "Expected EC/RC reset expression"
    | None -> failwith "Expected relay"

[<Fact>]
let ``CallSequence - SET RST 흐름`` () =
    let trigger = boolTag "loader_start"
    let doneSig = boolTag "loader_done"
    let resetSig = boolTag "loader_abort"

    let seq = Sequence.create "loader" trigger doneSig resetSig
    seq.Length |> should equal 6

    // SET 대신 MOV true 확인
    let hasSet target =
        seq |> List.exists (function
            | Command(_, _, Function("MOV", [Const(v, DsDataType.TBool); Terminal(tag)]))
                when tag.Name = target && unbox v = true -> true
            | _ -> false)

    // RST 대신 MOV false 확인
    let hasRst target =
        seq |> List.exists (function
            | Command(_, _, Function("MOV", [Const(v, DsDataType.TBool); Terminal(tag)]))
                when tag.Name = target && unbox v = false -> true
            | _ -> false)

    hasSet "loader_SC" |> should equal true
    hasSet "loader_EC" |> should equal true
    hasSet "loader_RC" |> should equal true
    hasRst "loader_SC" |> should equal true
    hasRst "loader_EC" |> should equal true
