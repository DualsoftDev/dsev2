namespace Ev2.Cpu.Generation

open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

/// 워크 패턴 식별자 (sequential enum, not flags)
type WorkKind =
    | startWork   = 11000
    | finishWork  = 11001
    | resetWork   = 11002
    | readyWork   = 11003

[<RequireQualifiedAccess>]
module WorkKind =
    let private enumCache =
        System.Enum.GetValues(typeof<WorkKind>) :?> WorkKind[]
        |> Array.map (fun k -> k, System.Enum.GetName(typeof<WorkKind>, k))

    /// WorkKind → 이름 문자열
    let toName (kind: WorkKind) =
        enumCache
        |> Array.find (fun (k, _) -> k = kind)
        |> snd

    /// WorkKind → DsTag
    let toTag (kind: WorkKind) =
        DsTag.Bool(toName kind)

/// 워크 코드 생성
module CodeWork =

    /// 자동 워크 패턴
    module WorkAuto =

        /// 기본 워크 relay 생성
        let createPattern (kind: WorkKind) : Relay option =
            let tag = WorkKind.toTag kind
            let self = Terminal(tag)
            match kind with
            | WorkKind.startWork ->
                let request = boolTag $"{tag.Name}_req"
                let complete = boolTag $"{tag.Name}_done"
                let setExpr = (request ||. self)
                let resetExpr = complete
                Relay.Create(tag, setExpr, resetExpr) |> Some
            | WorkKind.finishWork ->
                let condition = boolTag $"{tag.Name}_cond"
                Relay.Create(tag, condition, boolConst false) |> Some
            | WorkKind.resetWork ->
                let trigger = boolTag $"{tag.Name}_trigger"
                Relay.Create(tag, trigger, boolConst true) |> Some
            | WorkKind.readyWork ->
                let ready = all [ boolTag "system_ready"; boolTag "permissive_ok" ]
                let reset = any [ boolTag "system_fault"; boolTag "emergency_stop" ]
                Relay.Create(tag, ready, reset) |> Some
            | _ -> None

        /// 모든 워크 패턴 생성 (사용자 정의 우선)
        let createAll (custom: WorkKind -> Relay option) =
            System.Enum.GetValues(typeof<WorkKind>)
            |> Seq.cast<WorkKind>
            |> Seq.choose (fun kind ->
                match custom kind with
                | Some relay -> Some relay
                | None -> createPattern kind)
            |> Seq.toList

        /// Relay → Statement 변환
        let toStmts relays =
            relays |> List.map Generation.toStmt

    /// 워크 통계/계측
    module Stats =
        let create (prefix: string) : DsStmt list =
            let total = DsTag.Double $"{prefix}_time_total"
            let count = DsTag.Int $"{prefix}_count"
            let avg = DsTag.Double $"{prefix}_time_avg"

            [
                Command(0, boolConst true, Function("MOV", [doubleConst 0.0; Terminal(total)]))
                // INC 대신 ADD 사용: count := count + 1
                Assign(5, count, Function("ADD", [Terminal(count); intConst 1]))
                // DIV는 2개 인자만 받고 값 반환: avg := total / count
                Assign(10, avg, Function("DIV", [Terminal(total); Terminal(count)]))
            ]

    /// 워크 인터록 생성
    module Interlock =
        let create (name: string) (conditions: (string * DsExpr) list) : DsStmt list =
            let okTag = DsTag.Bool $"{name}_interlock_ok"
            let okExpr =
                match conditions with
                | [] -> boolConst true
                | _ -> conditions |> List.map snd |> all

            let main = Assign(0, okTag, okExpr)

            let details =
                conditions
                |> List.map (fun (key, expr) ->
                    let faultTag = DsTag.Bool $"{name}_{key}_fault"
                    Assign(0, faultTag, !!. expr))

            main :: details
