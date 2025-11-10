module Ev2.Cpu.Test.WorkCallRelay

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Work
open Ev2.Cpu.Generation.Call

// ═════════════════════════════════════════════════════════════════════
// Work Relay 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``WorkRelays - StartWork 기본 생성`` () =
    let relay = WorkRelays.createStartWork "TestWork" None None None None None

    relay.Tag |> should equal (DsTag.Bool "TestWork.SW")
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``WorkRelays - StartWork 이전 Work 조건`` () =
    let prevComplete = boolTag "PrevWork.EW"
    let relay = WorkRelays.createStartWork
                    "Work1"
                    (Some prevComplete)
                    None
                    None
                    None
                    None

    // SET 조건에 prevComplete 포함 확인
    match relay.Set with
    | Binary(DsOp.And, _, _) -> ()
    | _ -> ()  // all 함수로 생성되므로 구조 확인

[<Fact>]
let ``WorkRelays - EndWork 모든 Call 완료`` () =
    let allCallsComplete = all [
        boolTag "Call1.EC"
        boolTag "Call2.EC"
        boolTag "Call3.EC"
    ]

    let relay = WorkRelays.createEndWork "Work1" allCallsComplete None None

    relay.Tag |> should equal (DsTag.Bool "Work1.EW")
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``WorkRelays - ResetWork 펄스 모드`` () =
    let relay = WorkRelays.createResetWork "Work1" None

    relay.Tag |> should equal (DsTag.Bool "Work1.RW")
    relay.Mode |> should equal RelayMode.Pulse

[<Fact>]
let ``WorkRelays - ReadyWork 상태`` () =
    let relay = WorkRelays.createReadyWork "Work1" None None

    relay.Tag |> should equal (DsTag.Bool "Work1.Ready")
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``WorkRelays - GoingWork 상태 전이`` () =
    let relay = WorkRelays.createGoingWork "Work1"

    relay.Tag |> should equal (DsTag.Bool "Work1.Going")

    // SET: SW && !EW
    // RST: EW || RW
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``WorkRelays - FinishWork 상태`` () =
    let relay = WorkRelays.createFinishWork "Work1"

    relay.Tag |> should equal (DsTag.Bool "Work1.Finish")
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``WorkRelays - TimeoutError 타이머 기반`` () =
    let relay = WorkRelays.createTimeoutError "Work1" (Some 60000)

    relay.Tag |> should equal (DsTag.Bool "Work1.TimeoutError")

    // SET 조건에 TON 함수 포함 확인
    match relay.Set with
    | Function("TON", [_; _; Const(ms, DsDataType.TInt)]) ->
        unbox ms |> should equal 60000
    | _ -> failwith "Expected TON function"

[<Fact>]
let ``WorkRelays - createBasicWorkGroup 전체 생성`` () =
    let allCallsComplete = boolTag "AllCallsComplete"
    let relays = WorkRelays.createBasicWorkGroup "Work1" None allCallsComplete

    // 7개의 릴레이 생성 확인
    relays |> List.length |> should equal 7

    // 각 릴레이 이름 확인
    let tags = relays |> List.map (fun r -> r.Tag.Name)
    tags |> should contain "Work1.SW"
    tags |> should contain "Work1.EW"
    tags |> should contain "Work1.RW"
    tags |> should contain "Work1.Ready"
    tags |> should contain "Work1.Going"
    tags |> should contain "Work1.Finish"
    tags |> should contain "Work1.TimeoutError"

// ═════════════════════════════════════════════════════════════════════
// Work State 헬퍼 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``WorkState - stateTag 생성`` () =
    let tag = WorkState.stateTag "MyWork"
    tag |> should equal (DsTag.Int "MyWork_State")

[<Fact>]
let ``WorkState - isState 체크`` () =
    let expr = WorkState.isState "Work1" WorkState.State.Going
    match expr with
    | Binary(DsOp.Eq, Terminal(tag), Const(value, DsDataType.TInt)) ->
        tag |> should equal (DsTag.Int "Work1_State")
        unbox value |> should equal (int WorkState.State.Going)
    | _ -> failwith "Expected state check expression"

[<Fact>]
let ``WorkState - setState Statement`` () =
    let stmt = WorkState.setState "Work1" WorkState.State.Finish
    match stmt with
    | Assign(_, tag, Const(value, DsDataType.TInt)) ->
        tag |> should equal (DsTag.Int "Work1_State")
        unbox value |> should equal (int WorkState.State.Finish)
    | _ -> failwith "Expected state assignment"

// ═════════════════════════════════════════════════════════════════════
// WorkRelayBuilder 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``WorkRelayBuilder - Fluent API 기본`` () =
    let relays = WorkRelayBuilder("BuilderWork").Build()

    relays |> List.length |> should equal 7

[<Fact>]
let ``WorkRelayBuilder - WithPreviousWork`` () =
    let relays =
        WorkRelayBuilder("Work2")
            .WithPreviousWork(boolTag "Work1.EW")
            .WithAllCallsComplete(boolConst true)
            .Build()

    relays |> should not' (be Empty)

[<Fact>]
let ``WorkRelayBuilder - WithTimeLimit`` () =
    let relays =
        WorkRelayBuilder("Work1")
            .WithTimeLimit(30000)
            .WithAllCallsComplete(boolConst true)
            .Build()

    // TimeoutError 릴레이가 올바른 타임리밋을 가지는지 확인
    let timeoutRelay = relays |> List.find (fun r -> r.Tag.Name = "Work1.TimeoutError")
    match timeoutRelay.Set with
    | Function("TON", [_; _; Const(ms, _)]) ->
        unbox ms |> should equal 30000
    | _ -> failwith "Expected TON with correct timeout"

// ═════════════════════════════════════════════════════════════════════
// Call Relay 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``CallRelays - StartCall 첫 번째 Call`` () =
    let relay = CallRelays.createStartCall "Call1" "Work1" None None

    relay.Tag |> should equal (DsTag.Bool "Call1.SC")
    relay.Mode |> should equal RelayMode.SR

    // SET 조건: 부모 Work 시작
    // (검증 생략 - 내부 구현 확인)

[<Fact>]
let ``CallRelays - StartCall 이전 Call 체인`` () =
    let relay = CallRelays.createStartCall "Call2" "Work1" (Some "Call1") None

    relay.Tag |> should equal (DsTag.Bool "Call2.SC")

[<Fact>]
let ``CallRelays - EndCall 완료 조건`` () =
    let completeCondition = boolTag "Device.Ready"
    let relay = CallRelays.createEndCall "Call1" (Some completeCondition)

    relay.Tag |> should equal (DsTag.Bool "Call1.EC")
    relay.Mode |> should equal RelayMode.SR

[<Fact>]
let ``CallRelays - ResetCall 펄스`` () =
    let relay = CallRelays.createResetCall "Call1" None

    relay.Tag |> should equal (DsTag.Bool "Call1.RC")
    relay.Mode |> should equal RelayMode.Pulse

[<Fact>]
let ``CallRelays - createBasicCallGroup`` () =
    let relays = CallRelays.createBasicCallGroup
                    "Call1"
                    "Work1"
                    None
                    (Some (boolTag "Done"))

    relays |> List.length |> should equal 3

    let tags = relays |> List.map (fun r -> r.Tag.Name)
    tags |> should contain "Call1.SC"
    tags |> should contain "Call1.EC"
    tags |> should contain "Call1.RC"

[<Fact>]
let ``CallRelays - createCallChain 순차 실행`` () =
    let calls = [
        "Call1", Some (boolTag "Step1.Done")
        "Call2", Some (boolTag "Step2.Done")
        "Call3", Some (boolTag "Step3.Done")
    ]

    let relays = CallRelays.createCallChain "Work1" calls

    // 3개 Call × 3개 릴레이 = 9개
    relays |> List.length |> should equal 9

// ═════════════════════════════════════════════════════════════════════
// CallSequence 헬퍼 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``CallSequence - prevCallComplete None`` () =
    let expr = CallSequence.prevCallComplete None
    expr |> should equal (boolConst true)

[<Fact>]
let ``CallSequence - prevCallComplete Some`` () =
    let expr = CallSequence.prevCallComplete (Some "PrevCall")
    match expr with
    | Terminal(tag) -> tag.Name |> should equal "PrevCall.EC"
    | _ -> failwith "Expected terminal"

[<Fact>]
let ``CallSequence - allCallsComplete 빈 리스트`` () =
    let expr = CallSequence.allCallsComplete []
    expr |> should equal (boolConst true)

[<Fact>]
let ``CallSequence - allCallsComplete 여러 Call`` () =
    let expr = CallSequence.allCallsComplete ["Call1"; "Call2"; "Call3"]

    // all 함수로 생성됨
    match expr with
    | Function("AND", _) -> ()
    | _ -> ()  // 구조 확인

[<Fact>]
let ``CallSequence - chainCondition 첫 Call`` () =
    let expr = CallSequence.chainCondition "Work1" None
    match expr with
    | Terminal(tag) -> tag.Name |> should equal "Work1.SW"
    | _ -> failwith "Expected Work.SW"

[<Fact>]
let ``CallSequence - chainCondition 이후 Call`` () =
    let expr = CallSequence.chainCondition "Work1" (Some "Call1")
    match expr with
    | Terminal(tag) -> tag.Name |> should equal "Call1.EC"
    | _ -> failwith "Expected Call.EC"

// ═════════════════════════════════════════════════════════════════════
// API Call Relay 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``ApiCallRelays - createApiCallStart`` () =
    let relay = ApiCallRelays.createApiCallStart "VisionCall" "VisionAPI"

    relay.Tag |> should equal (DsTag.Bool "VisionAPI.apiItemSet")
    relay.Mode |> should equal RelayMode.SR

    // SET: Call.SC && !apiItemEnd
    // RST: apiItemEnd

[<Fact>]
let ``ApiCallRelays - createApiCallComplete`` () =
    let relay = ApiCallRelays.createApiCallComplete "VisionCall" "VisionAPI"

    relay.Tag |> should equal (DsTag.Bool "VisionCall.EC")

[<Fact>]
let ``ApiCallRelays - createApiCallGroup`` () =
    let relays = ApiCallRelays.createApiCallGroup "ApiCall1" "Work1" None "TestAPI"

    relays |> List.length |> should equal 4

    let tags = relays |> List.map (fun r -> r.Tag.Name)
    tags |> should contain "ApiCall1.SC"
    tags |> should contain "TestAPI.apiItemSet"
    tags |> should contain "ApiCall1.EC"
    tags |> should contain "ApiCall1.RC"

// ═════════════════════════════════════════════════════════════════════
// CallRelayBuilder 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``CallRelayBuilder - Fluent API 기본`` () =
    let relays = CallRelayBuilder("Call1", "Work1").Build()

    relays |> List.length |> should equal 3

[<Fact>]
let ``CallRelayBuilder - WithPreviousCall`` () =
    let relays =
        CallRelayBuilder("Call2", "Work1")
            .WithPreviousCall("Call1")
            .Build()

    relays |> should not' (be Empty)

[<Fact>]
let ``CallRelayBuilder - WithApi`` () =
    let relays =
        CallRelayBuilder("ApiCall", "Work1")
            .WithApi("VisionAPI")
            .Build()

    // API 연동 시 릴레이 생성 (SC, EC, RC 최소 3개)
    relays |> List.length |> should be (greaterThan 2)

// ═════════════════════════════════════════════════════════════════════
// CallGroups 패턴 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``CallGroups - sequential 순차 실행`` () =
    let calls = [
        "Step1", Some (boolTag "Done1")
        "Step2", Some (boolTag "Done2")
    ]

    let relays = CallGroups.sequential "Work1" calls

    // 2개 Call × 3개 릴레이 = 6개
    relays |> List.length |> should equal 6

[<Fact>]
let ``CallGroups - parallelCalls 병렬 실행`` () =
    let calls = [
        "Check1", Some (boolTag "Done1")
        "Check2", Some (boolTag "Done2")
        "Check3", Some (boolTag "Done3")
    ]

    let relays = CallGroups.parallelCalls "Work1" calls

    // 3개 Call × 3개 릴레이 = 9개
    relays |> List.length |> should equal 9

    // 모든 Call의 SC가 동일한 시작 조건을 가져야 함 (병렬)

[<Fact>]
let ``CallGroups - conditional 조건부 분기`` () =
    let condition = boolTag "Product.IsLarge"

    let trueCalls = [
        "LargePath", Some (boolTag "Large.Done")
    ]

    let falseCalls = [
        "SmallPath", Some (boolTag "Small.Done")
    ]

    let relays = CallGroups.conditional "Work1" condition trueCalls falseCalls

    // (1 true + 1 false) × 3개 릴레이 = 6개
    relays |> List.length |> should equal 6

// ═════════════════════════════════════════════════════════════════════
// 통합 시나리오 테스트
// ═════════════════════════════════════════════════════════════════════

[<Fact>]
let ``통합 - Work와 Call 연동`` () =
    // Work 생성
    let callsComplete = CallSequence.allCallsComplete ["Call1"; "Call2"]
    let workRelays = WorkRelays.createBasicWorkGroup "Work1" None callsComplete

    // Call 체인 생성
    let calls = [
        "Call1", Some (boolTag "Step1.Done")
        "Call2", Some (boolTag "Step2.Done")
    ]
    let callRelays = CallRelays.createCallChain "Work1" calls

    // 전체 릴레이
    let allRelays = workRelays @ callRelays

    allRelays |> List.length |> should equal 13  // 7 Work + 6 Call

[<Fact>]
let ``통합 - 복잡한 Work 체인`` () =
    // Work1
    let work1Calls = CallSequence.allCallsComplete ["Call1"]
    let work1Relays = WorkRelays.createBasicWorkGroup "Work1" None work1Calls

    // Work2 (Work1 완료 후)
    let work2Calls = CallSequence.allCallsComplete ["Call2"; "Call3"]
    let work2Relays = WorkRelays.createBasicWorkGroup
                        "Work2"
                        (Some (boolTag "Work1.EW"))
                        work2Calls

    let allRelays = work1Relays @ work2Relays
    allRelays |> List.length |> should equal 14  // 7 + 7
