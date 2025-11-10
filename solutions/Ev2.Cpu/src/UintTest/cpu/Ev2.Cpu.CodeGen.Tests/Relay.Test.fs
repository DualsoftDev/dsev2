module Ev2.Cpu.Test.Relay

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Core.GenerationUtils

[<Fact>]
let ``Relay - 기본 생성`` () =
    let tag = DsTag.Bool "relay1"
    let setExpr = boolTag "start"
    let resetExpr = boolTag "stop"
    
    let relay = Relay.Create(tag, setExpr, resetExpr)
    relay.Tag |> should equal tag
    relay.Set |> should equal setExpr
    relay.Reset |> should equal resetExpr

[<Fact>]
let ``Relay - ToExpr 변환`` () =
    let tag = DsTag.Bool "output"
    let setExpr = boolTag "btn_on"
    let resetExpr = boolTag "btn_off"
    
    let relay = Relay.Create(tag, setExpr, resetExpr)
    let expr = relay.ToExpr()
    
    // Set AND (NOT Reset) 패턴 확인
    match expr with
    | Binary(DsOp.And, s, Unary(DsOp.Not, r)) ->
        s |> should equal setExpr
        r |> should equal resetExpr
    | _ -> failwith "Expected Set AND (NOT Reset) pattern"

[<Fact>]
let ``Relay - ToLatch 변환`` () =
    let tag = DsTag.Bool "latch"
    let setExpr = boolTag "set"
    let resetExpr = boolTag "reset"

    let relay = Relay.Create(tag, setExpr, resetExpr)
    let expr = relay.ToLatch()

    // RST 우선: (NOT Reset) AND (Set OR Self) 패턴 확인
    match expr with
    | Binary(DsOp.And, Unary(DsOp.Not, r), Binary(DsOp.Or, s, Terminal(self))) ->
        s |> should equal setExpr
        r |> should equal resetExpr
        self |> should equal tag
    | _ -> failwith "Expected latch pattern"

[<Fact>]
let ``Relay - Statement 변환`` () =
    let tag = DsTag.Bool "motor"
    let relay = Relay.Create(tag, boolTag "start", boolTag "stop")
    
    let stmt = GenerationUtils.relayToStmt relay
    match stmt with
    | Assign(_, t, expr) ->
        t |> should equal tag
        expr |> should equal (relay.ToExpr())
    | _ -> failwith "Expected Assign statement"

[<Fact>]
let ``Relay - 조건부 Relay`` () =
    let tag = DsTag.Bool "indicator"
    let condition = boolTag "system_ok"
    
    let relay = GenerationUtils.conditionalRelay condition tag
    relay.Set |> should equal condition
    
    // Reset은 NOT condition이어야 함
    match relay.Reset with
    | Unary(DsOp.Not, c) -> c |> should equal condition
    | _ -> failwith "Expected NOT condition for reset"

[<Fact>]
let ``Relay - 래치 Relay`` () =
    let tag = DsTag.Bool "memory"
    let setExpr = boolTag "trigger"
    let resetExpr = boolTag "clear"
    
    let relay = GenerationUtils.latchRelay setExpr resetExpr tag
    
    // Set에 self-holding 로직 포함 확인
    match relay.Set with
    | Binary(DsOp.Or, s, Terminal(self)) ->
        s |> should equal setExpr
        self |> should equal tag
    | _ -> failwith "Expected latch set pattern"
    
    relay.Reset |> should equal resetExpr

[<Fact>]
let ``Relay - 펄스 Relay`` () =
    let tag = DsTag.Bool "pulse"
    let trigger = boolTag "input"
    
    let relay = GenerationUtils.pulseRelay trigger tag
    
    // Set은 Rising edge이어야 함
    match relay.Set with
    | Unary(DsOp.Rising, t) -> t |> should equal trigger
    | _ -> failwith "Expected rising edge for set"
    
    // Reset은 항상 false
    relay.Reset |> should equal (boolConst false)

[<Fact>]
let ``Relay - 타이머 Relay`` () =
    let tag = DsTag.Bool "delayed"
    let start = boolTag "enable"
    let delay = 1000
    
    let relay = GenerationUtils.timerRelay start delay tag

    // Set은 TON 함수 호출
    match relay.Set with
    | Function("TON", [s; _; Const(d, t)]) when t = typeof<int> ->
        s |> should equal start
        unbox d |> should equal delay
    | _ -> failwith "Expected TON function for set"
    
    // Reset은 NOT start
    match relay.Reset with
    | Unary(DsOp.Not, s) -> s |> should equal start
    | _ -> failwith "Expected NOT start for reset"

[<Fact>]
let ``Relay - Helper 함수들`` () =
    let tag = DsTag.Bool "test"
    
    // setWhen
    let r1 = setWhen (boolTag "cond") tag
    r1.Reset |> should equal (boolConst false)
    
    // resetWhen
    let r2 = resetWhen (boolTag "cond") tag
    r2.Set |> should equal (boolConst false)
    
    // setResetWhen
    let r3 = setResetWhen (boolTag "on") (boolTag "off") tag
    match r3.Set, r3.Reset with
    | Terminal(_), Terminal(_) -> ()
    | _ -> failwith "Expected terminal expressions"
    
    // latchWhen
    let r4 = latchWhen (boolTag "set") (boolTag "reset") tag
    match r4.Set with
    | Binary(DsOp.Or, _, Terminal(self)) -> self |> should equal tag
    | _ -> failwith "Expected latch pattern"
