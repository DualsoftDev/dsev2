# Ev2.Cpu.Generation 사용자 매뉴얼

## 개요

`Ev2.Cpu.Generation`은 PLC 프로그램을 생성하기 위한 고수준 API와 코드 생성기를 제공하는 라이브러리입니다. 복잡한 제어 로직을 간단한 빌더 패턴으로 작성하고, IEC 61131-3 표준의 Structured Text(ST) 코드로 변환할 수 있습니다.

---

## 주요 기능

### 1. Relay 시스템

Relay는 PLC의 기본 제어 단위로, 조건에 따라 출력을 제어합니다.

#### Relay 타입

**기본 Relay**
```fsharp
open Ev2.Cpu.Generation

// 조건부 출력 (IF-THEN)
let relay1 = Relay.create "coil1" (EVar "input1")

// 래치 (SET/RESET)
let latchRelay = Relay.latch "coil2" (EVar "start") (EVar "stop")

// 펄스 (Rising Edge)
let pulseRelay = Relay.pulse "pulse1" (EVar "trigger")

// 타이머 지연
let timerRelay = Relay.timer "delayed" (EVar "enable") 5000  // 5초 지연
```

**고급 Relay 모드**

```fsharp
// SR 래치 (Set-Reset)
let srRelay =
    RelayMode.SR
    |> RelayMode.withSetCondition (EVar "start")
    |> RelayMode.withResetCondition (EVar "stop")
    |> RelayMode.toRelay "output1"

// 펄스 모드 (Rising Edge 트리거)
let pulseMode =
    RelayMode.Pulse
    |> RelayMode.withTriggerCondition (EVar "button")
    |> RelayMode.toRelay "pulse_out"

// One-Shot 모드
let oneShotMode =
    RelayMode.OneShot
    |> RelayMode.withTriggerCondition (EVar "event")
    |> RelayMode.toRelay "oneshot_out"

// 조건부 직접 평가
let conditionalMode =
    RelayMode.Conditional
    |> RelayMode.withCondition (EVar "sensor" .> EInt 100)
    |> RelayMode.toRelay "alarm"
```

**Relay 우선순위**

```fsharp
// Reset 우선 (기본값)
let resetFirst =
    Relay.latch "output" (EVar "set_cond") (EVar "reset_cond")
    |> Relay.withPriority RelayPriority.ResetFirst

// Set 우선
let setFirst =
    Relay.latch "output" (EVar "set_cond") (EVar "reset_cond")
    |> Relay.withPriority RelayPriority.SetFirst

// 동시 발생 시 Off
let simultaneous =
    Relay.latch "output" (EVar "set_cond") (EVar "reset_cond")
    |> Relay.withPriority RelayPriority.SimultaneousOff
```

#### Relay 변환

```fsharp
// Statement 변환
let statements = Relay.toStatements [relay1; latchRelay; pulseRelay]

// 표현식으로 변환
let expr = relay1.ToExpr()  // 조건 표현식

// 래치 형태로 변환
let latchExpr = relay1.ToLatch()  // SET/RESET 문 생성
```

---

### 2. Work 시스템

Work는 작업 단위를 표현하며, 상태 전환을 관리합니다.

#### Work 상태

```fsharp
type WorkState =
    | Idle        // 대기
    | Ready       // 준비
    | Going       // 진행 중
    | Finish      // 완료
    | Error       // 오류
```

#### Work 생성

```fsharp
// 기본 Work 그룹
let workGroup = WorkRelays.createBasicWorkGroup "pump"

// 상태별 Relay
let readyWork = workGroup.ReadyWork    // Ready 상태로 전환
let startWork = workGroup.StartWork    // Going 상태 시작
let goingWork = workGroup.GoingWork    // Going 상태 유지
let endWork = workGroup.EndWork        // Finish 상태로 전환
let finishWork = workGroup.FinishWork  // 완료 처리
let resetWork = workGroup.ResetWork    // 리셋
```

#### Work 빌더 (Fluent API)

```fsharp
// 복잡한 Work 설정
let pumpWork =
    WorkRelayBuilder.create "pump"
    |> WorkRelayBuilder.withPreviousWork (Some "valve")
    |> WorkRelayBuilder.withTimeLimit (Some 10000)  // 10초 제한
    |> WorkRelayBuilder.build

// 조건 추가
let conditionalWork =
    pumpWork.StartWork
    |> Relay.withCondition (EVar "pressure" .> EInt 50)
```

#### Work 통계

```fsharp
// 작업 통계 생성
let stats = WorkStats.create "pump"

// 카운터
let moveCount = stats.MoveCount     // 이동 횟수
let totalTime = stats.TotalTime     // 총 시간
let avgTime = stats.AverageTime     // 평균 시간
```

#### Work 인터록

```fsharp
// 안전 조건
let interlock = WorkInterlock.create "pump" [
    ("low_pressure", EVar "pressure" .< EInt 30)
    ("high_temp", EVar "temperature" .> EInt 80)
]

// 각 조건별 fault 태그 자동 생성
// pump_fault_low_pressure, pump_fault_high_temp
```

---

### 3. Call 시스템

Call은 순차적 또는 병렬 작업 호출을 관리합니다.

#### Call 생성

```fsharp
// 기본 Call 그룹
let callGroup = CallRelays.createBasicCallGroup "process"

// Call Relay
let startCall = callGroup.StartCall
let endCall = callGroup.EndCall
let resetCall = callGroup.ResetCall
```

#### Call 시퀀스

```fsharp
// 여러 Call을 순차 실행
let calls = ["call1"; "call2"; "call3"]
let chainCond = CallSequence.chainCondition calls 0  // 첫 Call 조건

// 이전 Call 완료 조건
let prevComplete = CallSequence.prevCallComplete (Some "call1")

// 모든 Call 완료 확인
let allComplete = CallSequence.allCallsComplete calls
```

#### Call 그룹

```fsharp
// 순차 실행
let sequential = CallGroups.sequential ["step1"; "step2"; "step3"]

// 병렬 실행
let parallel = CallGroups.parallelCalls ["task1"; "task2"]

// 조건부 분기
let conditional = CallGroups.conditional "branch" (EVar "mode" .= EInt 1)
```

#### API Call

```fsharp
// API 호출 그룹
let apiCall = ApiCallRelays.createApiCallGroup "getData"

// API 상태
let apiStart = apiCall.StartCall     // 시작
let apiComplete = apiCall.Complete   // 완료
let apiError = apiCall.Error         // 오류
```

#### Call 빌더

```fsharp
// Fluent API
let apiCall =
    CallRelayBuilder.create "fetchData"
    |> CallRelayBuilder.withPreviousCall (Some "authenticate")
    |> CallRelayBuilder.withApi "REST_API"
    |> CallRelayBuilder.build
```

---

### 4. System 패턴

시스템 레벨의 공통 패턴을 제공합니다.

#### System 상태

```fsharp
type SystemState =
    | Off           // 꺼짐
    | Manual        // 수동 모드
    | Auto          // 자동 모드
    | Emergency     // 비상 정지
```

#### 에러 코드

```fsharp
type ErrorCode =
    | NoError           // 정상
    | Timeout           // 타임아웃
    | CommunicationErr  // 통신 오류
    | HardwareErr       // 하드웨어 오류
    | ConfigErr         // 설정 오류
```

#### System 패턴 생성

```fsharp
// 상태 전환
let statePattern = System.createState "system" SystemState.Auto

// 펄스 생성
let pulse = System.createPulse "heartbeat" 1000  // 1초 주기

// 모니터링
let monitor = System.createMonitor "sensor" (EVar "value" .> EInt 100)

// 안전 인터록
let safety = System.createSafety "machine" [
    EVar "emergency_stop" .= EBool false
    EVar "door_closed" .= EBool true
]

// ON/OFF 제어
let onOff = System.createOnOff "motor"

// 타입별 태그
let tags = System.tagsForType ErrorCode.Timeout
// ["timeout_code"; "timeout_active"; "timeout_ack"]
```

---

### 5. 코드 생성

PLC 프로그램을 ST(Structured Text) 코드로 변환합니다.

#### CodeBuilder

```fsharp
open Ev2.Cpu.Generation

// 코드 빌더 생성
let builder = CodeBuilder.create()

// Relay 추가 (자동 스텝 배치)
let builder =
    builder
    |> CodeBuilder.addRelays [relay1; relay2; relay3]

// 명시적 스텝 지정
let builder =
    builder
    |> CodeBuilder.addRelayAtStep relay4 100

// Statement 추가
let builder =
    builder
    |> CodeBuilder.addStatement (Statement.assign 1 tag expr)

// 생성
let statements = CodeBuilder.build builder
```

#### PLC 코드 생성

```fsharp
open Ev2.Cpu.Generation.Codegen

let library = UserLibrary.create()
// FC, FB 등록...

// Structured Text 생성
let stCode = PLCCodeGen.generateAll library

// 개별 생성
let fcCode = PLCCodeGen.generateFC myFC
let fbCode = PLCCodeGen.generateFB myFB

// TwinCAT 프로젝트 파일 (.TcPOU)
let tcFile = PLCCodeGen.generateTwinCATFile myFC
```

---

### 6. 스코핑 시스템

네임스페이스와 변수 스코프를 관리합니다.

#### NamespaceManager

```fsharp
// 네임스페이스 생성
let ns = NamespaceManager.create "MyProject.Controllers"

// 이름 결합
let fullName = ns.Qualify "PumpControl"  // "MyProject.Controllers.PumpControl"

// 검증
NamespaceManager.isValid "Valid.Namespace"  // true
// NamespaceManager.isValid "Invalid Path!"  // false
```

#### ScopeManager

```fsharp
// FC 변수에 스코프 적용
let scopedFC = ScopeManager.scopeFC "Namespace" myFC

// FC 변수에서 스코프 제거
let unscopedFC = ScopeManager.unscopeFC "Namespace" myFC

// FB 변수에 스코프 적용
let scopedFB = ScopeManager.scopeFB "Namespace" myFB

// FB Static 변수 스코핑
let scopedStatic = ScopeManager.scopeFBStatic "FBName" staticVar

// FB Temp 변수 스코핑
let scopedTemp = ScopeManager.scopeFBTemp "FBName" tempVar

// FB 인스턴스 변수 스코핑
let scopedInstance = ScopeManager.scopeFBInstance "InstanceName" var
```

---

### 7. 예제 라이브러리

실전 예제를 통해 사용법을 배울 수 있습니다.

#### UserFC 예제

```fsharp
open Ev2.Cpu.Generation.Examples

// 산술 함수
let addFC = UserFCExamples.createAddFC()
let multiplyFC = UserFCExamples.createMultiplyFC()
let averageFC = UserFCExamples.createAverageFC()

// 논리 함수
let andFC = UserFCExamples.createAndFC()
let orFC = UserFCExamples.createOrFC()

// 비교 함수
let maxFC = UserFCExamples.createMaxFC()
let minFC = UserFCExamples.createMinFC()
let clampFC = UserFCExamples.createClampFC()
```

#### UserFB 예제

```fsharp
// 타이머/카운터
let tonFB = UserFBExamples.createTON()      // On-Delay Timer
let tofFB = UserFBExamples.createTOF()      // Off-Delay Timer
let ctuFB = UserFBExamples.createCTU()      // Up Counter

// 에지 검출
let rTrigFB = UserFBExamples.createRTRIG()  // Rising Edge
let fTrigFB = UserFBExamples.createFTRIG()  // Falling Edge

// 래치
let srFB = UserFBExamples.createSR()        // Set-Reset
let rsFB = UserFBExamples.createRS()        // Reset-Set
```

---

## 통합 시나리오

### 시나리오 1: 펌프 제어 시스템

```fsharp
open Ev2.Cpu.Generation

// 1. Work 생성
let pumpWork = WorkRelays.createBasicWorkGroup "pump"

// 2. 안전 인터록
let pumpInterlock = WorkInterlock.create "pump" [
    ("low_level", EVar "tank_level" .< EInt 20)
    ("high_pressure", EVar "pressure" .> EInt 100)
]

// 3. 통계
let pumpStats = WorkStats.create "pump"

// 4. Call 시퀀스
let pumpSequence = CallSequence.chainCondition ["pre_check"; "pump"; "post_check"] 0

// 5. 코드 생성
let builder =
    CodeBuilder.create()
    |> CodeBuilder.addRelays [
        pumpWork.ReadyWork
        pumpWork.StartWork
        pumpWork.GoingWork
        pumpWork.EndWork
        pumpWork.FinishWork
    ]
    |> CodeBuilder.addRelays pumpInterlock.FaultRelays

let statements = CodeBuilder.build builder
```

### 시나리오 2: API 통신 시스템

```fsharp
// 1. API Call 그룹
let apiCall = ApiCallRelays.createApiCallGroup "fetchData"

// 2. 순차 실행
let apiSequence = CallGroups.sequential [
    "authenticate"
    "fetchData"
    "processData"
    "sendResult"
]

// 3. 에러 처리
let errorHandler =
    Relay.latch "api_retry" apiCall.Error (apiCall.Complete)
    |> Relay.withPriority RelayPriority.ResetFirst

// 4. 타임아웃
let timeout =
    Relay.timer "api_timeout" apiCall.StartCall 5000
    |> Relay.withCondition (apiCall.Complete .= EBool false)
```

### 시나리오 3: 상태 기계

```fsharp
// 상태 정의
type MachineState =
    | Idle
    | Loading
    | Processing
    | Unloading
    | Error

// 상태 전환 Relay
let toLoading =
    Relay.create "to_loading"
        (EVar "current_state" .= EInt (int Idle) .&& EVar "start_button")

let toProcessing =
    Relay.create "to_processing"
        (EVar "current_state" .= EInt (int Loading) .&& EVar "load_complete")

let toUnloading =
    Relay.create "to_unloading"
        (EVar "current_state" .= EInt (int Processing) .&& EVar "process_done")

let toIdle =
    Relay.create "to_idle"
        (EVar "current_state" .= EInt (int Unloading) .&& EVar "unload_complete")

// 상태 업데이트
let stateUpdate = [
    Statement.command 1 toLoading.Condition
        (ECall "MOV" [EInt (int Loading); EVar "current_state"])

    Statement.command 2 toProcessing.Condition
        (ECall "MOV" [EInt (int Processing); EVar "current_state"])

    Statement.command 3 toUnloading.Condition
        (ECall "MOV" [EInt (int Unloading); EVar "current_state"])

    Statement.command 4 toIdle.Condition
        (ECall "MOV" [EInt (int Idle); EVar "current_state"])
]
```

---

## 모범 사례

### 1. Relay 명명 규칙

```fsharp
// 접두사 사용
let motor1_start = Relay.create "motor1_start" condition
let motor1_stop = Relay.create "motor1_stop" stopCond
let motor1_running = Relay.create "motor1_running" runCond

// Work 상태는 접미사
let pump_ready = pumpWork.ReadyWork
let pump_going = pumpWork.GoingWork
let pump_finish = pumpWork.FinishWork
```

### 2. 에러 처리

```fsharp
// 항상 타임아웃 추가
let workWithTimeout =
    pumpWork.StartWork
    |> Relay.withCondition (
        EVar "enable" .&&
        (EVar "timeout" .= EBool false)
    )

// 에러 복구
let errorRecovery =
    Relay.pulse "error_reset" (EVar "reset_button" .&& EVar "error_active")
```

### 3. 코드 구조화

```fsharp
// 관련 Relay 그룹화
let controlRelays = [
    motor.StartRelay
    motor.StopRelay
    motor.RunningRelay
]

let safetyRelays = [
    interlock.Fault1
    interlock.Fault2
    emergencyStop
]

// 단계별 빌드
let builder =
    CodeBuilder.create()
    |> CodeBuilder.addRelays controlRelays
    |> CodeBuilder.addRelays safetyRelays
```

### 4. 테스트 가능성

```fsharp
// 조건을 변수로 분리
let startCondition =
    EVar "button_pressed" .&&
    EVar "safety_ok" .&&
    (EVar "current_state" .= EInt 0)

let startRelay = Relay.create "start" startCondition

// 테스트 시 조건만 변경 가능
```

---

## API 참조

### 주요 모듈

| 모듈 | 설명 |
|------|------|
| `Relay` | 기본 제어 Relay |
| `RelayMode` | 고급 Relay 모드 |
| `WorkRelays` | Work 시스템 |
| `CallRelays` | Call 시스템 |
| `System` | 시스템 패턴 |
| `CodeBuilder` | 코드 빌더 |
| `PLCCodeGen` | ST 코드 생성 |
| `ScopeManager` | 스코프 관리 |
| `Examples` | 예제 라이브러리 |

### 빌더 패턴

| 빌더 | 용도 |
|------|------|
| `WorkRelayBuilder` | Work Relay 구성 |
| `CallRelayBuilder` | Call Relay 구성 |
| `CodeBuilder` | 프로그램 코드 구성 |

---

## 추가 리소스

- **Ev2.Cpu.Core-사용자매뉴얼.md**: 핵심 타입 및 AST 참조
- **프로젝트 테스트**: `src/UnitTest/cpu/Ev2.Cpu.CodeGen.Tests/` 디렉토리
- **예제 코드**: `src/cpu/Ev2.Cpu.Generation/Examples/` 디렉토리

---

## 버전 정보

- **현재 버전**: 1.0.0
- **대상 프레임워크**: .NET 8.0
- **언어**: F# 8.0

---

## 라이선스

이 프로젝트는 회사 내부 라이선스에 따라 배포됩니다.
