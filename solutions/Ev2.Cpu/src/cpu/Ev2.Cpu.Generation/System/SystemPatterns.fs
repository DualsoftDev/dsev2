namespace Ev2.Cpu.Generation.System

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

/// System 자동 생성 패턴
[<RequireQualifiedAccess>]
module SysAuto =
    
    // 기본 System 패턴
    let createPattern (k: SystemKind) : Relay option =
        let tag = SystemKind.toTag k
        match k with
        // 기본 시스템 상태
        | SystemKind._ON -> 
            Some (SystemRelays.sysRelay k (boolConst true) (boolConst false))
        | SystemKind._OFF -> 
            Some (SystemRelays.sysRelay k (boolConst false) (boolConst true))
        | SystemKind._INIT ->
            Some (SystemRelays.sysLatch k 
                (boolTag "sys_init_req")
                (boolTag "sys_init_done"))
        | SystemKind._SHUTDOWN ->
            Some (SystemRelays.sysLatch k
                (boolTag "sys_shutdown_req")
                (boolTag "sys_shutdown_done"))
        // 모니터
        | SystemKind.pauseMonitor ->
            Some (SystemRelays.sysRelay k (boolTag "pause_state") (~~(boolTag "pause_state")))
        | SystemKind.errorMonitor ->
            Some (SystemRelays.sysRelay k (boolTag "error_state") (~~(boolTag "error_state")))
        | SystemKind.readyMonitor ->
            Some (SystemRelays.sysRelay k (boolTag "ready_state") (~~(boolTag "ready_state")))
        // 안전 및 인터록
        | SystemKind.safetyOk ->
            Some (SystemRelays.sysRelay k
                (all [boolTag "guard_closed"; ~~(boolTag "emg_stop")])
                (any [boolTag "guard_open"; boolTag "emg_stop"]))
        | _ -> None

    
    // 전체 System Relay 생성
    let createAll (custom: SystemKind -> Relay option) =
        EnumEx.Extract<SystemKind>()
        |> Array.choose (fun (value, _) ->
            let kind = enum<SystemKind>(value)
            custom kind <|> createPattern kind)
        |> Array.toList
    
    // Statement로 변환
    let toStmts = List.map Generation.toStmt

/// 시스템 초기화 코드 생성
module SystemInit =
    
    /// 시스템 초기화 시퀀스 생성
    let createInitSequence() : DsStmt list =
        []
        
/// 펄스 패턴
module Pulse =
    
    // 주기 펄스 생성
    let create kind periodMs =
        let tag = SystemKind.toTag kind
        let timerTag = DsTag.Int($"{tag.Name}_tmr")
        let timer = Terminal(timerTag)
        let scan = intTag "scan_ms"

        [
            // 타이머 증가 (Assign 사용: timer := timer + scan)
            Assign(0, timerTag, fn "ADD" [timer; scan])
            // 주기 도달 시 토글 (MOV 사용: tag := NOT tag)
            Command(0, timer >=. intConst periodMs,
                    fn "MOV" [fn "NOT" [Terminal(tag)]; Terminal(tag)])
            // 타이머 리셋 (MOV 사용)
            Command(0, timer >=. intConst periodMs, fn "MOV" [intConst 0; timer])
        ]
    
    // 모든 시스템 펄스
    let all() =
        [
            create SystemKind._T20MS 20
            create SystemKind._T100MS 100
            create SystemKind._T200MS 200
            create SystemKind._T1S 1000
            create SystemKind._T2S 2000
            create SystemKind._T5S 5000
            create SystemKind._T10S 10000
        ] |> List.concat