namespace Ev2.Cpu.Generation.Examples

open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Work
open Ev2.Cpu.Generation.Call

/// Relay 사용 예제 (RuntimeSpec.md 기반)
module RelayExamples =

    // ═════════════════════════════════════════════════════════════════════
    // 예제 1: 기본 Work 릴레이 생성
    // ═════════════════════════════════════════════════════════════════════

    /// 컨베이어 Work 예제
    let conveyorWorkExample() =
        // 이전 Work 완료 조건
        let prevWorkComplete = boolTag "LoadingWork.EW"

        // 모든 Call 완료 조건
        let allCallsComplete = all [
            boolTag "DetectProduct.EC"
            boolTag "RunConveyor.EC"
            boolTag "ConfirmArrival.EC"
        ]

        // Work 릴레이 그룹 생성
        let relays = WorkRelays.createBasicWorkGroup
                        "ConveyorWork"
                        (Some prevWorkComplete)
                        allCallsComplete

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 2: Call 체인 생성 (순차 실행)
    // ═════════════════════════════════════════════════════════════════════

    /// 로봇 픽업 Call 체인
    let robotPickupChain() =
        let parentWork = "RobotWork"

        // Call 체인 정의 (이름, 완료조건)
        let calls = [
            "OpenGripper", Some (boolTag "Gripper.Opened")
            "MoveToProduct", Some (boolTag "Robot.PositionReached")
            "CloseGripper", Some (boolTag "Gripper.Closed")
            "MoveToTarget", Some (boolTag "Robot.TargetReached")
        ]

        // Call 체인 생성
        let relays = CallRelays.createCallChain parentWork calls

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 3: API 연동 Call
    // ═════════════════════════════════════════════════════════════════════

    /// 비전 검사 API Call
    let visionInspectionCall() =
        let callName = "VisionInspection"
        let parentWork = "InspectionWork"
        let apiName = "VisionAPI"

        // API 연동 Call 그룹 생성
        let relays = ApiCallRelays.createApiCallGroup
                        callName
                        parentWork
                        None  // 첫 번째 Call
                        apiName

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 4: Fluent API를 사용한 Work 릴레이 생성
    // ═════════════════════════════════════════════════════════════════════

    /// WorkRelayBuilder를 사용한 예제
    let fluentWorkExample() =
        let relays =
            WorkRelayBuilder("AssemblyWork")
                .WithPreviousWork(boolTag "PrepWork.EW")
                .WithApiStart(boolTag "MES.StartSignal")
                .WithSafety(boolTag "SafetyDoor.Closed")
                .WithAllCallsComplete(
                    all [
                        boolTag "PickPart.EC"
                        boolTag "AssemblePart.EC"
                        boolTag "InspectAssembly.EC"
                    ])
                .WithTimeLimit(60000)  // 60초 타임아웃
                .Build()

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 5: Call 그룹 패턴 (병렬, 조건부)
    // ═════════════════════════════════════════════════════════════════════

    /// 병렬 Call 그룹 (모든 Call 동시 시작)
    let parallelCallsExample() =
        let parentWork = "ParallelWork"

        let calls = [
            "Check1", Some (boolTag "Check1.Done")
            "Check2", Some (boolTag "Check2.Done")
            "Check3", Some (boolTag "Check3.Done")
        ]

        // 병렬 Call 그룹 생성
        let relays = CallGroups.parallelCalls parentWork calls

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    /// 조건부 분기 Call 그룹
    let conditionalCallsExample() =
        let parentWork = "ConditionalWork"
        let condition = boolTag "Product.IsLarge"

        let trueCalls = [
            "UseLargeGripper", Some (boolTag "LargeGripper.Ready")
            "MoveSlowly", Some (boolTag "SlowMove.Done")
        ]

        let falseCalls = [
            "UseSmallGripper", Some (boolTag "SmallGripper.Ready")
            "MoveNormally", Some (boolTag "NormalMove.Done")
        ]

        // 조건부 분기 Call 그룹 생성
        let relays = CallGroups.conditional
                        parentWork
                        condition
                        trueCalls
                        falseCalls

        // Relay → Statement 변환
        relays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 6: 완전한 Work → Call 계층 구조
    // ═════════════════════════════════════════════════════════════════════

    /// RuntimeSpec.md 6.1 컨베이어 → 로봇 연동 예제
    let conveyorRobotIntegration() =
        // ─────────────────────────────────────────────────────────────
        // 컨베이어 Work
        // ─────────────────────────────────────────────────────────────
        let conveyorCalls = [
            "DetectProduct", Some (boolTag "Sensor.ProductDetected")
            "RunConveyor", Some (boolTag "Conveyor.Running" &&. fn "TON" [boolConst true; intConst 3000])
            "ConfirmArrival", Some (boolTag "Sensor.ProductArrived")
        ]

        let conveyorCallRelays = CallRelays.createCallChain "ConveyorWork" conveyorCalls

        let allConveyorCallsComplete =
            CallSequence.allCallsComplete ["DetectProduct"; "RunConveyor"; "ConfirmArrival"]

        let conveyorWorkRelays =
            WorkRelays.createBasicWorkGroup "ConveyorWork" None allConveyorCallsComplete

        // ─────────────────────────────────────────────────────────────
        // 로봇 Work (컨베이어 완료 후 시작)
        // ─────────────────────────────────────────────────────────────
        let robotCalls = [
            "OpenGripper", Some (boolTag "Gripper.Opened")
            "PickupProduct", Some (boolTag "Gripper.HasProduct")
            "MoveToTarget", Some (boolTag "Robot.AtTarget")
            "PlaceProduct", Some (boolTag "Gripper.Released")
        ]

        let robotCallRelays = CallRelays.createCallChain "RobotWork" robotCalls

        let allRobotCallsComplete =
            CallSequence.allCallsComplete ["OpenGripper"; "PickupProduct"; "MoveToTarget"; "PlaceProduct"]

        let robotWorkRelays =
            WorkRelays.createBasicWorkGroup
                "RobotWork"
                (Some (boolTag "ConveyorWork.EW"))  // 컨베이어 완료 후 시작
                allRobotCallsComplete

        // ─────────────────────────────────────────────────────────────
        // 모든 릴레이 결합
        // ─────────────────────────────────────────────────────────────
        let allRelays =
            conveyorWorkRelays @
            conveyorCallRelays @
            robotWorkRelays @
            robotCallRelays

        // Relay → Statement 변환
        allRelays |> List.map Generation.toLatch

    // ═════════════════════════════════════════════════════════════════════
    // 예제 7: Relay 모드별 동작 테스트
    // ═════════════════════════════════════════════════════════════════════

    /// SR 래치 모드
    let srLatchExample() =
        let relay = Relay.CreateWithMode(
                        DsTag.Bool("TestRelay_SR"),
                        boolTag "SetButton",
                        boolTag "ResetButton",
                        RelayMode.SR)
        Generation.toLatch relay

    /// 펄스 모드
    let pulseExample() =
        let relay = Relay.CreateWithMode(
                        DsTag.Bool("TestRelay_Pulse"),
                        boolTag "TriggerButton",
                        boolConst false,
                        RelayMode.Pulse)
        Generation.toStmt relay

    /// 조건부 모드
    let conditionalExample() =
        let relay = Relay.CreateWithMode(
                        DsTag.Bool("TestRelay_Conditional"),
                        boolTag "Condition1" &&. boolTag "Condition2",
                        boolTag "StopCondition",
                        RelayMode.Conditional)
        Generation.toStmt relay

    // ═════════════════════════════════════════════════════════════════════
    // 예제 8: 우선순위 규칙 테스트
    // ═════════════════════════════════════════════════════════════════════

    /// RST 우선 (안전 우선 - 기본값)
    let resetFirstExample() =
        let relay = Relay.CreateFull(
                        DsTag.Bool("SafetyRelay"),
                        boolTag "StartButton",
                        boolTag "EmergencyStop",
                        RelayMode.SR,
                        RelayPriority.ResetFirst,
                        false)
        Generation.toLatch relay

    /// SET 우선
    let setFirstExample() =
        let relay = Relay.CreateFull(
                        DsTag.Bool("ForceStartRelay"),
                        boolTag "ForceStart",
                        boolTag "NormalStop",
                        RelayMode.SR,
                        RelayPriority.SetFirst,
                        false)
        Generation.toLatch relay

    // ═════════════════════════════════════════════════════════════════════
    // 전체 프로그램 생성 예제
    // ═════════════════════════════════════════════════════════════════════

    /// 완전한 프로그램 생성
    let createCompleteProgram() : Program =
        // 전체 릴레이 수집
        let allStatements = conveyorRobotIntegration()

        // Input/Output/Local 변수 선언
        let inputs = [
            "Sensor.ProductDetected", DsDataType.TBool
            "Sensor.ProductArrived", DsDataType.TBool
            "Gripper.Opened", DsDataType.TBool
            "Gripper.Closed", DsDataType.TBool
            "Gripper.HasProduct", DsDataType.TBool
            "Gripper.Released", DsDataType.TBool
            "Robot.AtTarget", DsDataType.TBool
        ]

        let outputs = [
            "ConveyorWork.SW", DsDataType.TBool
            "ConveyorWork.EW", DsDataType.TBool
            "RobotWork.SW", DsDataType.TBool
            "RobotWork.EW", DsDataType.TBool
        ]

        let locals = [
            "ConveyorWork.Going", DsDataType.TBool
            "ConveyorWork.Finish", DsDataType.TBool
            "RobotWork.Going", DsDataType.TBool
            "RobotWork.Finish", DsDataType.TBool
            "DetectProduct.SC", DsDataType.TBool
            "DetectProduct.EC", DsDataType.TBool
            "RunConveyor.SC", DsDataType.TBool
            "RunConveyor.EC", DsDataType.TBool
            "ConfirmArrival.SC", DsDataType.TBool
            "ConfirmArrival.EC", DsDataType.TBool
            "OpenGripper.SC", DsDataType.TBool
            "OpenGripper.EC", DsDataType.TBool
            "PickupProduct.SC", DsDataType.TBool
            "PickupProduct.EC", DsDataType.TBool
            "MoveToTarget.SC", DsDataType.TBool
            "MoveToTarget.EC", DsDataType.TBool
            "PlaceProduct.SC", DsDataType.TBool
            "PlaceProduct.EC", DsDataType.TBool
        ]

        { Name = "ConveyorRobotSystem"
          Inputs = inputs
          Outputs = outputs
          Locals = locals
          Body = allStatements }
