namespace Ev2.Cpu.Generation.Core

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

/// 코드 생성 빌더
type CodeBuilder() =
    let mutable statements : DsStmt list = []
    let stepGap = 10
    let mutable nextAutoStep = stepGap

    let updateNextAuto stepNumber =
        if stepNumber >= nextAutoStep then
            let next = ((stepNumber / stepGap) + 1) * stepGap
            nextAutoStep <- if next <= stepNumber then stepNumber + stepGap else next

    let stamp (stmt: DsStmt) =
        let stepNumber =
            match stmt with
            | Assign(step, _, _) when step <> 0 ->
                updateNextAuto step
                step
            | Command(step, _, _) when step <> 0 ->
                updateNextAuto step
                step
            | _ ->
                let s = nextAutoStep
                nextAutoStep <- nextAutoStep + stepGap
                s
        Statement.withStep stepNumber stmt

    member _.Add(stmt: DsStmt) =
        statements <- (stamp stmt) :: statements

    member _.AddRange(stmts: DsStmt list) =
        stmts |> List.iter (fun s -> statements <- (stamp s) :: statements)

    member _.AddRelay(relay: Relay) =
        statements <- (GenerationUtils.relayToStmt relay |> stamp) :: statements

    member _.AddRelays(relays: Relay list) =
        relays |> List.iter (fun r -> statements <- (GenerationUtils.relayToStmt r |> stamp) :: statements)

    member _.Build() : DsStmt list =
        List.rev statements

    member _.Clear() =
        statements <- []
        nextAutoStep <- stepGap