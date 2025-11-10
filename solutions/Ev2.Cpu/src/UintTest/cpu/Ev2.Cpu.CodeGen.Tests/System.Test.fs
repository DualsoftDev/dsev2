module Ev2.Cpu.Test.System

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.System
open Ev2.Cpu.Generation.System.SystemRelays
open Ev2.Cpu.Generation.Core

[<Fact>]
let ``System - 기본 패턴 생성`` () =
    let initPattern = SysAuto.createPattern SystemKind._INIT
    initPattern.IsSome |> should equal true
    
    match initPattern with
    | Some relay ->
        relay.Tag.Name |> should equal "_INIT"
        relay.Tag.DataType |> should equal typeof<bool>
    | None -> failwith "Expected relay"

[<Fact>]
let ``System - ON/OFF 상태`` () =
    let onPattern = SysAuto.createPattern SystemKind._ON
    let offPattern = SysAuto.createPattern SystemKind._OFF
    
    match onPattern with
    | Some relay ->
        relay.Set |> should equal (boolConst true)
        relay.Reset |> should equal (boolConst false)
    | None -> failwith "Expected ON pattern"
    
    match offPattern with
    | Some relay ->
        relay.Set |> should equal (boolConst false)
        relay.Reset |> should equal (boolConst true)
    | None -> failwith "Expected OFF pattern"

[<Fact>]
let ``System - 모니터 패턴`` () =
    let pauseMonitor = SysAuto.createPattern SystemKind.pauseMonitor
    let errorMonitor = SysAuto.createPattern SystemKind.errorMonitor
    
    pauseMonitor.IsSome |> should equal true
    errorMonitor.IsSome |> should equal true
    
    match pauseMonitor with
    | Some relay ->
        relay.Tag.Name |> should equal "pauseMonitor"
        // 모니터는 상태를 따라감
        match relay.Set with
        | Terminal(tag) -> tag.Name |> should equal "pause_state"
        | _ -> failwith "Expected pause_state terminal"
    | None -> failwith "Expected monitor"

[<Fact>]
let ``System - Safety 인터록`` () =
    let safetyPattern = SysAuto.createPattern SystemKind.safetyOk
    
    match safetyPattern with
    | Some relay ->
        relay.Tag.Name |> should equal "safetyOk"
        // Set: guard_closed AND NOT emg_stop
        match relay.Set with
        | Binary(DsOp.And, Terminal(guard), Unary(DsOp.Not, Terminal(emg))) ->
            guard.Name |> should equal "guard_closed"
            emg.Name |> should equal "emg_stop"
        | _ -> failwith "Expected safety set pattern"
        // Reset: guard_open OR emg_stop
        match relay.Reset with
        | Binary(DsOp.Or, Terminal(guard), Terminal(emg)) ->
            guard.Name |> should equal "guard_open"
            emg.Name |> should equal "emg_stop"
        | _ -> failwith "Expected safety reset pattern"
    | None -> failwith "Expected safety pattern"

[<Fact>]
let ``System - Pulse 생성`` () =
    let pulse = Pulse.create SystemKind._T1S 1000

    pulse |> should not' (be Empty)

    // 타이머 증가 (Assign 사용)
    pulse |> List.exists (function
        | Assign(_, _, Function("ADD", _)) -> true
        | _ -> false) |> should equal true

    // 토글 (MOV with NOT 사용)
    pulse |> List.exists (function
        | Command(_, _, Function("MOV", [Function("NOT", _); _])) -> true
        | _ -> false) |> should equal true

    // 리셋 (MOV 사용)
    pulse |> List.exists (function
        | Command(_, _, Function("MOV", [Const(v, t); _])) when t = typeof<int> && unbox v = 0 -> true
        | _ -> false) |> should equal true

[<Fact>]
let ``System - 모든 펄스 생성`` () =
    let pulses = Pulse.all()
    
    pulses |> should not' (be Empty)
    
    // 각 펄스 주기별로 3개의 statement (증가, 토글, 리셋)
    // 7개 펄스 종류 * 3 = 21개
    pulses.Length |> should be (greaterThan 15)

[<Fact>]
let ``System - 타입별 태그`` () =
    // Bool 타입
    let boolTag = SystemKind.toTag SystemKind._ON
    boolTag.DataType |> should equal typeof<bool>
    
    // Int 타입 (날짜/시간)
    let intTag1 = SystemKind.toTag SystemKind.datetime_yy
    intTag1.DataType |> should equal typeof<int>
    
    let intTag2 = SystemKind.toTag SystemKind.cpuLoad
    intTag2.DataType |> should equal typeof<int>
    
    // Double 타입
    let doubleTag = SystemKind.toTag SystemKind.tempData
    doubleTag.DataType |> should equal typeof<double>

[<Fact>]
let ``System - 코드 생성`` () =
    let systemCode = generateSystemCode "TestSystem"
    
    systemCode.Name |> should equal "TestSystem"
    systemCode.Relays |> should not' (be Empty)

[<Fact>]
let ``SystemState - Enum 값`` () =
    SystemState.Idle |> int |> should equal 0
    SystemState.Ready |> int |> should equal 1
    SystemState.Running |> int |> should equal 2
    SystemState.Paused |> int |> should equal 3
    SystemState.Error |> int |> should equal 4
    SystemState.Emergency |> int |> should equal 5
    SystemState.Maintenance |> int |> should equal 6

[<Fact>]
let ``ErrorCode - Enum 값`` () =
    ErrorCode.NoError |> int |> should equal 0
    ErrorCode.TimeoutError |> int |> should equal 1
    ErrorCode.CommunicationError |> int |> should equal 2
    ErrorCode.HardwareError |> int |> should equal 3
    ErrorCode.SoftwareError |> int |> should equal 4
    ErrorCode.OperatorError |> int |> should equal 5
    ErrorCode.SafetyError |> int |> should equal 6
