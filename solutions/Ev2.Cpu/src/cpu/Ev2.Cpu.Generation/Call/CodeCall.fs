namespace Ev2.Cpu.Generation

open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

/// 콜 패턴 식별자
[<System.Flags>]
type CallKind =
    | callActive  = 12000
    | callBusy    = 12001
    | callError   = 12002

[<RequireQualifiedAccess>]
module CallKind =
    let private enumCache =
        System.Enum.GetValues(typeof<CallKind>) :?> CallKind[]
        |> Array.map (fun k -> k, System.Enum.GetName(typeof<CallKind>, k))

    let toName (kind: CallKind) =
        enumCache |> Array.find (fun (k, _) -> k = kind) |> snd

    let toTag (kind: CallKind) =
        DsTag.Bool(toName kind)

/// 콜 코드 생성
module CodeCall =

    /// 콜 상태 릴레이
    module CallAuto =

        let createPattern (kind: CallKind) : Relay option =
            let tag = CallKind.toTag kind
            let self = Terminal(tag)
            match kind with
            | CallKind.callActive ->
                let sc = boolTag $"{tag.Name}_SC"
                let rc = boolTag $"{tag.Name}_RC"
                let ec = boolTag $"{tag.Name}_EC"
                // Start command 유지, RC/EC 시 리셋
                let setExpr = (sc ||. self) &&. (!!. rc)
                let resetExpr = ec ||. rc
                Relay.Create(tag, setExpr, resetExpr) |> Some
            | CallKind.callBusy ->
                let busy = boolTag $"{tag.Name}_source"
                Relay.Create(tag, busy, boolConst false) |> Some
            | CallKind.callError ->
                let error = boolTag $"{tag.Name}_flag"
                let clear = boolTag $"{tag.Name}_clear"
                Relay.Create(tag, error, clear) |> Some
            | _ -> None

        let createAll (custom: CallKind -> Relay option) =
            System.Enum.GetValues(typeof<CallKind>)
            |> Seq.cast<CallKind>
            |> Seq.choose (fun kind ->
                match custom kind with
                | Some relay -> Some relay
                | None -> createPattern kind)
            |> Seq.toList

        let toStmts relays =
            relays |> List.map Generation.toStmt

    /// 콜 시퀀스 빌더
    module Sequence =
        let create name trigger completion reset : DsStmt list =
            let toTerminal suffix = Terminal(DsTag.Bool $"{name}_{suffix}")
            let sc = toTerminal "SC"
            let ec = toTerminal "EC"
            let rc = toTerminal "RC"

            [
                // SET 대신 MOV true 사용
                Command(0, trigger, Function("MOV", [boolConst true; sc]))
                Command(5, completion, Function("MOV", [boolConst true; ec]))
                // RST 대신 MOV false 사용
                Command(5, completion, Function("MOV", [boolConst false; sc]))
                Command(10, reset, Function("MOV", [boolConst true; rc]))
                Command(10, reset, Function("MOV", [boolConst false; sc]))
                Command(10, reset, Function("MOV", [boolConst false; ec]))
            ]
