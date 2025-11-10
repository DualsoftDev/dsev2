module Ev2.Cpu.Test.RelayAdvanced

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core

// ═════════════════════════════════════════════════════════════════════
// Relay 모드 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``RelayMode - SR 모드 기본 동작`` () =
    let tag = DsTag.Bool "sr_relay"
    let relay = Relay.CreateWithMode(tag, boolTag "set", boolTag "reset", RelayMode.SR)

    relay.Mode |> should equal RelayMode.SR

    // SR 모드는 ToModeExpr()가 ToLatch()와 같아야 함
    let modeExpr = relay.ToModeExpr()
    let latchExpr = relay.ToLatch()
    modeExpr |> should equal latchExpr

[<Fact>]
let ``RelayMode - Pulse 모드 Rising Edge`` () =
    let tag = DsTag.Bool "pulse_relay"
    let trigger = boolTag "trigger"
    let relay = Relay.CreateWithMode(tag, trigger, boolConst false, RelayMode.Pulse)

    relay.Mode |> should equal RelayMode.Pulse

    // Pulse 모드는 Rising edge 감지
    let expr = relay.ToModeExpr()
    match expr with
    | Unary(DsOp.Rising, t) -> t |> should equal trigger
    | _ -> failwith "Expected rising edge"

[<Fact>]
let ``RelayMode - OneShot 모드 Rising Edge`` () =
    let tag = DsTag.Bool "oneshot_relay"
    let trigger = boolTag "button"
    let relay = Relay.CreateWithMode(tag, trigger, boolConst false, RelayMode.OneShot)

    relay.Mode |> should equal RelayMode.OneShot

    // OneShot도 Rising edge
    let expr = relay.ToModeExpr()
    match expr with
    | Unary(DsOp.Rising, t) -> t |> should equal trigger
    | _ -> failwith "Expected rising edge"

[<Fact>]
let ``RelayMode - Conditional 모드 직접 평가`` () =
    let tag = DsTag.Bool "cond_relay"
    let setExpr = boolTag "start" &&. boolTag "ready"
    let resetExpr = boolTag "stop"
    let relay = Relay.CreateWithMode(tag, setExpr, resetExpr, RelayMode.Conditional)

    relay.Mode |> should equal RelayMode.Conditional

    // Conditional 모드는 ToExpr()와 같음
    let modeExpr = relay.ToModeExpr()
    let toExpr = relay.ToExpr()
    modeExpr |> should equal toExpr

// ═════════════════════════════════════════════════════════════════════
// Relay 우선순위 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``RelayPriority - ResetFirst 기본 동작`` () =
    let tag = DsTag.Bool "safety_relay"
    let relay = Relay.CreateFull(
                    tag,
                    boolTag "start",
                    boolTag "emergency",
                    RelayMode.SR,
                    RelayPriority.ResetFirst,
                    false)

    relay.Priority |> should equal RelayPriority.ResetFirst

    // ResetFirst: (!RESET) && (SET || self)
    let expr = relay.ToLatch()
    match expr with
    | Binary(DsOp.And, Unary(DsOp.Not, _), Binary(DsOp.Or, _, Terminal(_))) -> ()
    | _ -> failwith "Expected ResetFirst pattern"

[<Fact>]
let ``RelayPriority - SetFirst 우선`` () =
    let tag = DsTag.Bool "force_relay"
    let relay = Relay.CreateFull(
                    tag,
                    boolTag "force_start",
                    boolTag "normal_stop",
                    RelayMode.SR,
                    RelayPriority.SetFirst,
                    false)

    relay.Priority |> should equal RelayPriority.SetFirst

    // SetFirst: SET || (self && !RESET)
    let expr = relay.ToLatch()
    match expr with
    | Binary(DsOp.Or, _, Binary(DsOp.And, Terminal(_), Unary(DsOp.Not, _))) -> ()
    | _ -> failwith "Expected SetFirst pattern"

[<Fact>]
let ``RelayPriority - SimultaneousOff 동시 발생`` () =
    let tag = DsTag.Bool "sim_relay"
    let relay = Relay.CreateFull(
                    tag,
                    boolTag "set",
                    boolTag "reset",
                    RelayMode.SR,
                    RelayPriority.SimultaneousOff,
                    false)

    relay.Priority |> should equal RelayPriority.SimultaneousOff

    // SimultaneousOff: (SET && !RESET) || (self && !SET && !RESET)
    let expr = relay.ToLatch()
    // 표현식이 OR 연산을 포함하는지만 확인
    match expr with
    | Binary(DsOp.Or, _, _) -> ()
    | _ -> failwith "Expected SimultaneousOff pattern"

// ═════════════════════════════════════════════════════════════════════
// Relay 초기값 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``Relay - DefaultValue false`` () =
    let relay = Relay.Create(DsTag.Bool "relay", boolTag "a", boolTag "b")
    relay.DefaultValue |> should equal false

[<Fact>]
let ``Relay - DefaultValue true 설정`` () =
    let relay = Relay.CreateFull(
                    DsTag.Bool "default_on",
                    boolTag "set",
                    boolTag "reset",
                    RelayMode.SR,
                    RelayPriority.ResetFirst,
                    true)
    relay.DefaultValue |> should equal true

// ═════════════════════════════════════════════════════════════════════
// Relay 복합 시나리오 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``Relay - 복합 조건 SR 래치`` () =
    let tag = DsTag.Bool "complex_latch"
    let setConditions = all [
        boolTag "sensor1"
        boolTag "sensor2"
        !!. (boolTag "error")
    ]
    let resetConditions = any [
        boolTag "stop"
        boolTag "emergency"
    ]

    let relay = Relay.Create(tag, setConditions, resetConditions)
    let expr = relay.ToLatch()

    // 표현식이 올바르게 구성되었는지 확인
    match expr with
    | Binary(DsOp.And, Unary(DsOp.Not, _), Binary(DsOp.Or, _, Terminal(self))) ->
        self |> should equal tag
    | _ -> failwith "Expected complex latch pattern"

[<Fact>]
let ``Relay - 중첩 조건`` () =
    let tag = DsTag.Bool "nested_relay"
    let innerCondition = boolTag "temp_ok" &&. boolTag "pressure_ok"
    let outerCondition = innerCondition &&. boolTag "flow_ok"

    let relay = Relay.Create(tag, outerCondition, boolTag "alarm")
    relay.Set |> should equal outerCondition

[<Fact>]
let ``Relay - 타이머 기반 지연`` () =
    let tag = DsTag.Bool "delayed_start"
    let condition = boolTag "start_button"
    let timerExpr = fn "TON" [condition; strConst "test_timer"; intConst 3000]

    let relay = Relay.Create(tag, timerExpr, boolTag "cancel")

    match relay.Set with
    | Function("TON", [_; _; Const(delay, DsDataType.TInt)]) ->
        unbox delay |> should equal 3000
    | _ -> failwith "Expected TON function"

[<Fact>]
let ``Relay - 카운터 기반 트리거`` () =
    let tag = DsTag.Bool "count_reached"
    let countExpr = fn "CTU" [boolTag "pulse"; intConst 10]

    let relay = Relay.Create(tag, countExpr, boolTag "reset")

    match relay.Set with
    | Function("CTU", _) -> ()
    | _ -> failwith "Expected CTU function"

// ═════════════════════════════════════════════════════════════════════
// Relay 에지 케이스 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``Relay - 상수 Set 조건`` () =
    let relay = Relay.Create(DsTag.Bool "always_on", boolConst true, boolConst false)
    relay.Set |> should equal (boolConst true)
    relay.Reset |> should equal (boolConst false)

[<Fact>]
let ``Relay - 상수 Reset 조건`` () =
    let relay = Relay.Create(DsTag.Bool "always_off", boolConst false, boolConst true)
    relay.Set |> should equal (boolConst false)
    relay.Reset |> should equal (boolConst true)

[<Fact>]
let ``Relay - 동일 변수 Set/Reset`` () =
    let sameVar = boolTag "toggle"
    let relay = Relay.Create(DsTag.Bool "toggle_relay", sameVar, sameVar)

    // 동일 변수가 Set과 Reset에 사용될 수 있음
    relay.Set |> should equal sameVar
    relay.Reset |> should equal sameVar

[<Fact>]
let ``Relay - 복잡한 논리식`` () =
    let tag = DsTag.Bool "logic_relay"
    let setExpr = (boolTag "a" &&. boolTag "b") ||. (boolTag "c" &&. (!!. (boolTag "d")))
    let resetExpr = boolTag "e" ||. (boolTag "f" &&. boolTag "g")

    let relay = Relay.Create(tag, setExpr, resetExpr)

    // 복잡한 표현식이 보존되는지 확인
    relay.Set |> should equal setExpr
    relay.Reset |> should equal resetExpr

// ═════════════════════════════════════════════════════════════════════
// Relay 변환 메서드 일관성 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``Relay - ToExpr vs ToLatch 차이`` () =
    let tag = DsTag.Bool "compare_relay"
    let relay = Relay.Create(tag, boolTag "set", boolTag "reset")

    let toExpr = relay.ToExpr()
    let toLatch = relay.ToLatch()

    // ToExpr는 self를 포함하지 않음
    // ToLatch는 self를 포함함
    toExpr |> should not' (equal toLatch)

[<Fact>]
let ``Relay - ToPulse 일관성`` () =
    let relay1 = Relay.Create(DsTag.Bool "p1", boolTag "trigger", boolConst false)
    let relay2 = Relay.CreateWithMode(
                    DsTag.Bool "p2",
                    boolTag "trigger",
                    boolConst false,
                    RelayMode.Pulse)

    // ToPulse()와 Pulse 모드의 ToModeExpr()는 같아야 함
    relay1.ToPulse() |> should equal (relay2.ToModeExpr())

[<Fact>]
let ``Relay - 모든 모드 ToModeExpr 호출 가능`` () =
    let tag = DsTag.Bool "mode_test"
    let setExpr = boolTag "set"
    let resetExpr = boolTag "reset"

    let modes = [
        RelayMode.SR
        RelayMode.Pulse
        RelayMode.OneShot
        RelayMode.Conditional
    ]

    for mode in modes do
        let relay = Relay.CreateWithMode(tag, setExpr, resetExpr, mode)
        let expr = relay.ToModeExpr()

        // 모든 모드가 표현식을 생성할 수 있어야 함
        expr |> should not' (be Null)
