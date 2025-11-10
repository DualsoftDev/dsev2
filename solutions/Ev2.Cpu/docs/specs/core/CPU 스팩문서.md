# DS 시스템 내부 릴레이 로직 사양서


## 📋 목차
1. [개요](#1-개요)
2. [기호 및 약어 정의](#2-기호-및-약어-정의)
3. [릴레이 로직 구조](#3-릴레이-로직-구조)
4. [상세 릴레이 사양](#4-상세-릴레이-사양)
5. [타이밍 다이어그램](#5-타이밍-다이어그램)
6. [적용 예시](#6-적용-예시)

---

## 1. 개요

### 1.1 목적
본 문서는 DS(DualSoft) 시스템의 내부 릴레이 로직을 정의하여, 시스템 개발자와 유지보수 담당자가 일관된 방식으로 자동화 로직을 구현할 수 있도록 합니다.

### 1.2 적용 범위
- 산업 자동화 시스템 (FA: Factory Automation)
- PLC 기반 제어 시스템
- 디지털 트윈 시뮬레이션 환경

### 1.3 기본 원칙
- **계층적 구조**: System → Flow → Work → Call
- **상태 기반 제어**: FSM(Finite State Machine) 적용
- **자기유지 회로**: SR(Set-Reset) 래치 기본
- **안전 우선**: 에러 및 비상정지 최우선 처리

### 1.4 상태를 기반 동작

DS 시스템은 4가지 기본 상태를 기반으로 동작합니다:
- **R (Ready)**: 준비 완료, 다음 동작 대기
- **G (Going)**: 작업 진행 중
- **F (Finish)**: 작업 완료
- **H (Homing)**: 원점 복귀 중

---

## 2. 기호 및 약어 정의

### 2.1 릴레이 타입 약어

| 약어 | 전체 명칭 | 용도 | 비고 |
|:---:|:---------|:-----|:-----|
| **SW** | Start Work | Work 시작 신호 | 작업 단위 시작 |
| **EW** | End Work | Work 종료 신호 | 모든 Call 완료 시 |
| **RW** | Reset Work | Work 리셋 신호 | 초기화/홈복귀 |
| **SC** | Start Call | Call 시작 신호 | 세부 동작 시작 |
| **EC** | End Call | Call 종료 신호 | API 동작 완료 |
| **RC** | Reset Call | Call 리셋 신호 | 1-cycle 펄스 |


### 2.2 논리 연산 기호

| 우선순위 | 기호 | 의미 | 예시 | 설명 |
|:-------:|:---:|:-----|:-----|:-----|
| 1 | `()` | 괄호 | `(A && B)` | 최우선 평가 |
| 2 | `!` | NOT | `!A` | 논리 부정 |
| 3 | `↑` | Rising Edge | `A↑` | 0→1 변화 감지 |
| 3 | `↓` | Falling Edge | `A↓` | 1→0 변화 감지 |
| 4 | `&&` | AND | `A && B` | 논리곱 |
| 5 | `\|\|` | OR | `A \|\| B` | 논리합 |
| 6 | `∀` | For All | `∀Call.EC` | 모든 조건 만족 |
| 6 | `∃` | Exists | `∃Call.Error` | 하나라도 존재 |

**규칙 R2.1**: 괄호가 없는 경우 위 우선순위에 따라 평가됨
**규칙 R2.2**: 복잡한 조건은 반드시 괄호로 명시적 그룹화

### 2.3 릴레이 회로 표기법

```
SR(SET := 세트조건, RST := 리셋조건)
TON(IN := 입력조건, PT := 지연시간)
Pulse(TRIGGER := 트리거조건)
```

---

## 3. 릴레이 로직 구조

### 3.1 계층 구조도

```
┌────────────────────────────────────┐
│            System Level            │
│  ┌─────────────────────────────┐   │
│  │         Flow Level          │   │
│  │  ┌───────────────────────┐  │   │
│  │  │      Work Level       │  │   │
│  │  │  ┌─────────────────┐  │  │   │
│  │  │  │   Call Level    │  │  │   │
│  │  │  └─────────────────┘  │  │   │
│  │  └───────────────────────┘  │   │
│  └─────────────────────────────┘   │
└────────────────────────────────────┘
```

### 3.2 상태 전이 모델

#### Work 상태 전이
```
┌────────┐  SW=1   ┌────────┐  EW=1   ┌────────┐
│ Ready  │ ──────> │ Going  │ ──────> │ Finish │
└────────┘         └────────┘         └────────┘
    ↑                                     │
    │              ┌────────┐             │ RW=1
    └───────────── │ Homing │ <───────────┘
                   └────────┘
```

| 단계 | 현재 상태 | SET 조건 (Trigger) | 전이/주요 액션 | RST 조건 |
| --- | --- | --- | --- | --- |
| ① Ready → Going | Ready | `Work.SW` (선행 Work 완료 ∧ 보조조건 ∧ 안전조건 ∧ 원점 확인) | `Work.State := Going`, 타이머·카운터 초기화, 장치 준비 신호 ON | `Work.RW` ∨ `Work.Error` ∨ `System.Emergency` |
| ② Going 유지 | Going | `Work.SW` 유지 ∧ `!Work.EW` | 생산 시퀀스 실행, TON/CTU 등 진행 | `Work.RW` ∨ `Work.Error` ∨ `System.Emergency` |
| ③ Going → Finish | Going | `Work.EW` (모든 Call 완료 ∧ 에러 없음) | `Work.State := Finish`, 완료 신호 ON | `Work.RW` |
| ④ Finish → Ready | Finish | `Work.RW` 또는 상위 Flow 재시작 | 내부 플래그 Reset, Ready 진입 | - |
| ⑤ Any → Homing | Ready/Going/Finish | 수동 Home 명령 또는 초기 시퀀스 | `Work.State := Homing`, 장비 원점 복귀 수행 | Homing 완료 시 Ready |

---

## 4. 릴레이 사양

### 4.1 Work 제어 릴레이

#### 🔧 W-01: Work 시작 릴레이 (SW)

**목적**: Work 실행을 시작하는 메인 트리거 신호

**로직 정의**:
```
Work.SW = SR(
    SET := (선행조건 AND 보조조건 AND 안전조건),
    RST := (리셋조건 OR 에러조건)
)
```

**상세 조건**:
- **SET 조건**:
  - ✅ 모든 선행 Work 완료 (`∀PrevWork.EW`)
  - ✅ 하나의 외부 API 시작 신호 (`∃ApiDef.PS`)
  - ✅ 강제 시작 버튼 (`HMI.ForceStart`)
  - ✅ 보조 조건 만족 (`Work.Aux`)
  - ✅ 원점 확인 (`Work.OG`)

- **RST 조건**:
  - ❌ Work 리셋 신호 (`Work.RW`)
  - ❌ 플로우 운전상태 벗어남 (`!Flow.Drive`)


#### 🔧 W-02: Work 종료 릴레이 (EW)

**목적**: Work 내 모든 작업 완료를 나타내는 신호

**로직 정의**:
```
Work.EW = SR(
    SET := (모든Call완료 AND 시작됨 AND 에러없음),
    RST := (리셋)
)
```

**상세 조건**:
- **SET 조건**:
  - ✅ 모든 내부 Call 완료 (`∀Call.EC`)
  - ✅ Work 진행 중 상태 (`Work.Going`)
  - ✅ 에러 없음 (`!Work.Error`)

- **RST 조건**:
  - ❌ Work 리셋 (`Work.RW`)

### 4.2 Call 제어 릴레이

#### 🔧 C-01: Call 시작 릴레이 (SC)

**목적**: Call 단위 작업 시작 신호

**로직 정의**:
```
Call.SC = SR(
    SET := (이전Call완료 OR 부모Work시작) AND 보조조건,
    RST := (Call리셋 OR Call종료)
)
```

**시퀀스 다이어그램**:
```
ParentWork    Call-1      Call-2      Call-3
    │           │           │           │
    ├──SW──────>│           │           │
    │           ├──SC──────>│           │
    │           │           │           │
    │           ├──EC──────>│           │
    │           │           ├──SC──────>│
    │           │           │           │
    │           │           ├──EC──────>│
    │           │           │           ├──SC──>
```

### 4.3 API 연동 릴레이

#### 🔧 A-01: API 호출 시작 (apiItemSet)

**목적**: 외부 시스템에 API 호출을 트리거

**로직 정의**:
```
apiItemSet = (Call.SC AND !apiItemEnd)
```

**통신 플로우**:
```
[Local System]              [Remote System]
     │                            │
     ├──apiItemSet──────────────>│
     │                            ├──Processing...
     │                            │
     │<─────────apiItemEnd────────┤
     │                            │
```

### 4.4 시스템 제어 릴레이

#### 🔧 S-01: 자동/수동 모드 전환

**자동 모드**:
```
System.AutoMode = SR(
    SET := HMI.AutoButton,
    RST := (HMI.ManualButton OR System.Emergency)
)
```

**수동 모드**:
```
System.ManualMode = SR(
    SET := (HMI.ManualButton OR !System.AutoMode),
    RST := HMI.AutoButton
)
```

### 4.5 에러 처리 릴레이

#### 🔧 E-01: 타임아웃 에러

**목적**: 작업 시간 초과 감지

**로직 정의**:
```
Work.TimeoutTimer = TON(
    IN := (Work.Going AND !Work.EW),
    PT := Work.TimeLimit
)

Work.TimeoutError = SR(
    SET := Work.TimeoutTimer.DN,
    RST := System.ClearButton
)
```

**파라미터**:
- `TimeLimit`: 기본 30초 (설정 가능)
- `ClearButton`: 2초 이상 유지 필요

---

## 5. 타이밍 다이어그램

### 5.1 정상 동작 시퀀스

```
시간(t)    0   1   2   3   4   5   6   7   8   9
─────────────────────────────────────────────────
Work.SW    0   1   1   1   1   1   0   0   0   0
Call1.SC   0   0   1   1   0   0   0   0   0   0
Call1.EC   0   0   0   0   1   0   0   0   0   0
Call2.SC   0   0   0   0   1   1   0   0   0   0
Call2.EC   0   0   0   0   0   0   1   0   0   0
Work.EW    0   0   0   0   0   0   1   1   0   0
Work.RW    0   0   0   0   0   0   0   0   1   0
```

### 5.2 에러 발생 시퀀스

```
시간(t)    0   1   2   3   4   5   6   7   8   9
─────────────────────────────────────────────────
Work.SW    0   1   1   1   1   1   1   1   0   0
Call1.SC   0   0   1   1   1   1   1   1   0   0
Timeout    0   0   0   0   0   0   0   1   1   1
Error      0   0   0   0   0   0   0   1   1   0
Clear      0   0   0   0   0   0   0   0   0   1
```

---

## 6. 적용 예시

### 6.1 컨베이어 → 로봇 연동

```
[컨베이어 Work]
├── Call1: 제품 감지
├── Call2: 컨베이어 구동
└── Call3: 도착 확인
        ↓ (EW 신호)
[로봇 Work]
├── Call1: 그리퍼 열기
├── Call2: 제품 픽업
├── Call3: 이동
└── Call4: 제품 배치
```

### 6.2 병렬 처리 패턴

```
[분기 Work]
    ├──────┬──────┐
    ↓      ↓      ↓
[작업A] [작업B] [작업C]
    ↓      ↓      ↓
    └──────┴──────┘
[병합 Work]
```

**병합 조건**:
```
MergeWork.SW = SR(
    SET := (WorkA.EW AND WorkB.EW AND WorkC.EW),
    RST := MergeWork.RW
)
```

---


# 📚 릴레이 규칙 문서
---

## 📋 목차
1. [개요](#1-개요)
2. [기호 및 약어 정의](#2-기호-및-약어-정의)
3. [릴레이 로직 기본 규칙](#3-릴레이-로직-기본-규칙)
4. [상태 기계(FSM) 사양](#4-상태-기계-fsm-사양)
5. [Work 릴레이 그룹](#5-work-릴레이-그룹)
6. [Call 릴레이 그룹](#6-call-릴레이-그룹)
7. [API 연동 릴레이 그룹](#7-api-연동-릴레이-그룹)
8. [에러 및 비상 처리](#8-에러-및-비상-처리)
9. [타이밍 및 동기화](#9-타이밍-및-동기화)
10. [변수명 매핑 테이블](#10-변수명-매핑-테이블)

---

## 1. 개요

### 1.1 목적
본 문서는 DS(DualSoft) 시스템의 내부 릴레이 로직을 명확하게 정의하여, 시스템 개발자와 유지보수 담당자가 일관된 방식으로 자동화 로직을 구현할 수 있도록 합니다.


---

## 2. 기호 및 약어 정의

### 2.1 릴레이 타입 정의

| 타입 | 설명 | 동작 | 리셋 방식 |
|:----:|:-----|:-----|:---------|
| **SR** | Set-Reset Latch | 상태 유지 | 명시적 RST 필요 |
| **Pulse** | One-Shot | 1스캔 유지 | 자동 리셋 |
| **TON** | Timer On Delay | 지연 후 ON | IN=OFF 시 리셋 |
| **TOF** | Timer Off Delay | 지연 후 OFF | IN=ON 시 리셋 |

---

## 3. 릴레이 로직 기본 규칙

### 3.1 SET/RST 우선순위 규칙

**규칙 R3.1: 동시 SET/RST 처리**
```
IF (SET_Condition == TRUE) AND (RST_Condition == TRUE) THEN
    Priority = RST  // RST가 우선 (안전 우선 원칙)
    Output = OFF
END IF
```


### 3.2 릴레이 선언 표준 형식

```
RelayName := RelayType(
    SET := (조건1 && 조건2) || 조건3,  // 괄호로 명확히
    RST := 조건4 || 조건5,
    DEFAULT := OFF,                      // 초기값
)
```

### 3.3 OFF 조건 명확화

**규칙 R3.3: RST = OFF의 의미**
```
RST := OFF     // 자동 리셋 없음, SET 유지
RST := _OFF    // System._OFF (항상 FALSE)
RST := 1scan   // 다음 스캔에 자동 리셋
```

---

## 4. 상태 기계(FSM) 사양

### 4.1 상태 배타성 보장

**규칙 R4.1: One-Hot 인코딩 강제**
```
// 컴파일 시점 검증
ASSERT: (R + G + F + H) == 1

// 런타임 검증
StateCheck := CASE
    WHEN (R && !G && !F && !H) => Valid_R
    WHEN (!R && G && !F && !H) => Valid_G
    WHEN (!R && !G && F && !H) => Valid_F
    WHEN (!R && !G && !F && H) => Valid_H
    ELSE => ERROR_MultiState
END CASE
```

### 4.2 초기화 및 복구 규칙

**규칙 R4.2: 전원 투입 시퀀스**
```
PowerOn_Sequence:
    1. All_States := OFF
    2. H := SET          // Homing 먼저
    3. Wait(OG)          // 원점 대기
    4. R := SET, H := RST // Ready 전환
```

**규칙 R4.3: 비상 복구 시퀀스**
```
Emergency_Recovery:
    1. Save_Current_State
    2. All_G := RST      // 모든 진행 중단
    3. Wait(Clear)       // 클리어 대기
    4. Goto_Homing       // H 상태로
```

---

## 5. Work 릴레이 그룹

### 5.1 Work.SW (Start Work)

```
Work.SW := SR(
    SET := ((∀SourceWork.EW || ∃ApiDef.PS || Work.SF) 
            && Work.Aux && Work.OG)
            && !Work.Error && !System.Emergency,
    RST := Work.RW || Work.Error || System.Emergency,
    DEFAULT := OFF,
    PRIORITY := RST
)
```

**조건 설명**:
- **SET 우선순위**: `(((소스완료 OR API시작 OR 강제) AND 보조) AND 원점) AND 에러없음`
- **RST 우선순위**: `리셋 OR 에러 OR 비상`

### 5.2 Work.EW (End Work)

```
Work.EW := SR(
    SET := (Work.GG && (∀Call.EC) && !Work.Error)
           && Script.Done && Time.Done && Motion.Done,
    RST := (Work.RW && (∀Call.State == OFF))
           || (!∃Call && Work.RW),
    DEFAULT := OFF,
    PRIORITY := RST
)
```

**특수 처리**:
```
// Optional 조건 처리
Script.Done := Work.Script.Exists ? Script.Complete : TRUE
Time.Done := Work.Time.Exists ? Timer.DN : TRUE
Motion.Done := Work.Motion.Exists ? Motion.Complete : TRUE
```

---

## 6. Call 릴레이 그룹

### 6.1 병렬 Call 처리 규칙

**규칙 R6.1: Head/Tail Call 구분**
```
// Head Call: Work 시작과 동시 실행
HeadCall[i].SC := SR(
    SET := Work.RR && Call[i].IsHead && !Work.Error,
    RST := Call[i].EC || Call[i].RC,
    PRIORITY := RST
)

// Tail Call: DAG 선행 조건 확인
TailCall[j].SC := SR(
    SET := (∀Predecessor[j].EC) && !Work.Error,
    RST := Call[j].EC || Call[j].RC,
    PRIORITY := RST
)
```

### 6.2 Disabled Call 처리

**규칙 R6.2: 비활성 Call 즉시 종료**
```
IF Call.Disabled THEN
    Call.SC := Pulse(Work.G && Work.RR)
    Call.EC := Call.SC  // 동일 스캔 종료
    Call.PS := OFF
    Call.PE := OFF
END IF
```

---

## 7. API 연동 릴레이 그룹

### 7.1 통신 타임아웃 처리

**규칙 R7.1: API 타임아웃 및 재시도**
```
ApiTimeout := TON(
    IN := apiDefSet && !apiDefEnd,
    PT := API_TIMEOUT_MS  // 기본 5000ms
)

ApiRetry := CTU(
    CU := ApiTimeout.DN && RetryEnable,
    RESET := apiDefEnd || MaxRetryReached,
    PV := MAX_RETRY_COUNT  // 기본 3회
)

ApiError := SR(
    SET := ApiRetry.Q,  // 최대 재시도 도달
    RST := System.ClearButton,
    PRIORITY := RST
)
```

---

## 8. 에러 및 비상 처리

### 8.1 에러 전파 규칙

**규칙 R8.1: 에러 계층 전파**
```
// Bottom-Up 전파
Call.Error => Work.Error => Flow.Error => System.Error

// 에러 시 동작 중단
IF Work.Error THEN
    Work.G := RST       // 진행 중단
    ∀Call.SC := BLOCK  // 새 Call 차단
    Running_Calls := OPTION(Continue|Stop)  // 설정 의존
END IF
```

### 8.2 비상정지 처리

**규칙 R8.2: Emergency 우선순위**
```
Priority_Level:
    1. System.Emergency  // 최우선
    2. Flow.Error
    3. Work.Error
    4. Call.Error
    5. Normal Operation  // 최하위

Emergency_Action:
    - All Work.G := RST
    - All Call.SC := BLOCK
    - All ActionOut := SAFE_POSITION
    - Wait(System.ClearButton && Safety.OK)
```

---

## 9. 타이밍 및 동기화

### 9.1 스캔 동기화 규칙

**규칙 R9.1: 실행 순서 보장**
```
Scan_Order:
    1. Input_Read        // 물리 입력
    2. Emergency_Check   // 비상 체크
    3. Error_Check      // 에러 체크
    4. State_Update     // FSM 상태
    5. Logic_Execute    // 릴레이 로직
    6. Output_Write     // 물리 출력
```

### 9.2 라이징/폴링 엣지 처리

**규칙 R9.2: 엣지 감지 구현**
```
// 라이징 엣지
Signal_Rising := Signal && !Signal_Prev
Signal_Prev := Signal  // 스캔 종료 시 갱신

// 폴링 엣지
Signal_Falling := !Signal && Signal_Prev
```

---

## 10. 변수명 매핑 테이블

### 10.1 Work 릴레이 매핑

| 문서 표기 | 코드 변수명 | TagKind Enum | 타입 | 설명 |
|:---------|:-----------|:------------|:-----|:-----|
| Work.SW | work_start | WorkTag.SW | SR | 시작 릴레이 |
| Work.EW | work_end | WorkTag.EW | SR | 종료 릴레이 |
| Work.RW | work_reset | WorkTag.RW | SR | 리셋 릴레이 |
| Work.R | work_ready | WorkTag.R | SR | Ready 상태 |
| Work.G | work_going | WorkTag.G | SR | Going 상태 |
| Work.F | work_finish | WorkTag.F | SR | Finish 상태 |
| Work.H | work_homing | WorkTag.H | SR | Homing 상태 |
| Work.OG | work_origin | WorkTag.OG | SR | 원점 센서 |
| Work.Error | work_error | WorkTag.ERR | SR | 에러 플래그 |
| Work.GG | work_g_guard | WorkTag.GG | Pulse | G 1스캔 지연 |

### 10.2 Call 릴레이 매핑

| 문서 표기 | 코드 변수명 | TagKind Enum | 타입 | 설명 |
|:---------|:-----------|:------------|:-----|:-----|
| Call.SC | call_start | CallTag.SC | SR | 시작 릴레이 |
| Call.EC | call_end | CallTag.EC | SR | 종료 릴레이 |
| Call.RC | call_reset | CallTag.RC | Pulse | 리셋 펄스 |
| Call.PS | plan_start | CallTag.PS | SR | Plan 시작 |
| Call.PE | plan_end | CallTag.PE | SR | Plan 종료 |
| Call.Disabled | call_disabled | CallTag.DIS | Bool | 비활성 플래그 |

### 10.3 API 릴레이 매핑

| 문서 표기 | 코드 변수명 | TagKind Enum | 타입 | 설명 |
|:---------|:-----------|:------------|:-----|:-----|
| apiDefSet | api_def_set | ApiTag.SET | SR | API 호출 시작 |
| apiDefEnd | api_def_end | ApiTag.END | SR | API 호출 종료 |
| actionOut | action_out | ApiTag.OUT | Mixed | 물리 출력 |
| actionIn | action_in | ApiTag.IN | Mixed | 물리 입력 |

---

## 11. 검증 및 테스트

### 11.1 컴파일 시점 검증

```
COMPILE_CHECK:
    - FSM One-Hot 검증
    - DAG 순환 참조 체크
    - 변수명 중복 체크
    - 괄호 매칭 검증
```

### 11.2 런타임 검증

```
RUNTIME_CHECK:
    - 상태 동시 활성 감지
    - 타임아웃 모니터링
    - 통신 에러 감지
    - 메모리 오버플로우 방지
```

---

## 12. 구현 예제

### 12.1 정상 동작 시퀀스

```fsharp
// F# 구현 예제
let workStartRelay = 
    SR(
        SET = (allSourceComplete || apiStart || forceStart) 
              && auxCondition && originOK 
              && not workError && not emergency,
        RST = workReset || workError || emergency,
        DEFAULT = false,
        PRIORITY = RST
    )
```

### 12.2 에러 처리 예제

```fsharp
// 에러 발생 시 처리
match workState with
| Going when errorDetected ->
    workState <- Error
    allCalls |> List.iter (fun c -> c.Block())
    actionOut <- SafePosition
| _ -> ()
```


## 부록 A: 약어 사전

| 약어 | 전체 명칭 | 설명 |
|:-----|:---------|:-----|
| DS | DualSoft | 시스템 명칭 |
| FSM | Finite State Machine | 유한 상태 기계 |
| DAG | Directed Acyclic Graph | 방향성 비순환 그래프 |
| SR | Set-Reset | 자기유지 릴레이 |
| HMI | Human Machine Interface | 사용자 인터페이스 |
| PLC | Programmable Logic Controller | 프로그래머블 로직 컨트롤러 |

---

## 부록 B: 에러 코드 정의

| 코드 | 설명 | 처리 방법 |
|:-----|:-----|:---------|
| E001 | FSM 다중 상태 활성 | 즉시 Emergency Stop |
| E002 | API 타임아웃 | 재시도 후 에러 처리 |
| E003 | DAG 순환 참조 | 컴파일 에러 |
| E004 | 메모리 오버플로우 | 시스템 리셋 |
| E005 | 통신 에러 | 재연결 시도 |


# 📚 상세 로직
## 1. 상태 릴레이 그룹

### R001~R004. Work 상태 릴레이 (R/G/F/H - FSM State)

**목적**: Work의 4가지 상태(Ready/Going/Finish/Homing)를 FSM으로 관리

**로직 정의**:
```
// R004. Ready 상태
Work.R := SR(
    SET := Work.OG && !Work.G && !Work.Error,
    RST := Work.SW
)

// R005. Going 상태  
Work.G := SR(
    SET := Work.SW && Work.R,
    RST := Work.EW || Work.RW || Work.Error
)

// R006. Finish 상태
Work.F := SR(
    SET := Work.EW && Work.G,
    RST := Work.RW↑
)

// R007. Homing 상태
Work.H := SR(
    SET := Work.RW && Work.F,
    RST := Work.OG || System.Reset
)
```

**상태 전이 다이어그램**:
```
        ┌─────────────────────────────────────────┐
        │                                         │
        ▼                                         │
    ┌───────┐  Work.SW   ┌───────┐  Work.EW  ┌───────┐
    │   R   │ ─────────> │   G   │ ────────> │   F   │
    │ Ready │            │ Going │           │Finish │
    └───────┘            └───────┘           └───────┘
        ▲                                         │
        │                ┌───────┐               │
        │ Work.OG        │   H   │  Work.RW      │
        └────────────────│Homing │<──────────────┘
                         └───────┘
```

**상세 설명**:

**R (Ready) - 준비 상태**:
- **SET 조건**: 
  - `Work.OG`: 원점 위치 확인
  - `!Work.G`: 진행 중이 아님
  - `!Work.Error`: 에러 없음
- **RST 조건**: `Work.SW` (작업 시작)
- **의미**: 다음 작업 시작 가능한 대기 상태

**G (Going) - 진행 상태**:
- **SET 조건**:
  - `Work.SW`: 시작 신호
  - `Work.R`: Ready 상태에서만 시작
- **RST 조건**:
  - `Work.EW`: 정상 종료
  - `Work.RW`: 강제 리셋
  - `Work.Error`: 에러 발생
- **의미**: 작업 실행 중

**F (Finish) - 완료 상태**:
- **SET 조건**:
  - `Work.EW`: 종료 신호
  - `Work.G`: Going 상태에서만 종료
- **RST 조건**: `Work.RW↑` (리셋 라이징)
- **의미**: 작업 완료, 다음 동작 대기

**H (Homing) - 복귀 상태**:
- **SET 조건**:
  - `Work.RW`: 리셋 신호
  - `Work.F`: Finish 상태에서만 홈복귀
- **RST 조건**:
  - `Work.OG`: 원점 도달
  - `System.Reset`: 시스템 리셋
- **의미**: 원점 복귀 진행 중

**통합 타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10 t11
         │   │   │   │   │   │   │   │   │   │   │   │
Work.OG  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲_______╱▔▔▔▔▔
                                              
Work.R   ▔▔▔▔╲_______________________________╱▔▔▔▔▔
         
Work.SW  ____╱▔╲_____________________________________
             
Work.G   ________╱▔▔▔▔▔▔▔╲___________________________
                         
Work.EW  ________________╱▔╲_________________________
                         
Work.F   ____________________╱▔▔▔▔╲__________________
                                  
Work.RW  ______________________╱▔╲___________________
                                  
Work.H   __________________________╱▔▔▔╲_____________

상태:    [R][   G(Going)   ][F(Fin)][H(Home)][R(Ready)]
시간:    t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10 t11

이벤트:      ↑SW          ↑EW    ↑RW     ↑OG
전이:        R→G           G→F    F→H     H→R
```

**상태 전이 규칙**:
```
// 정상 순환
R → G: Work.SW (시작)
G → F: Work.EW (종료)  
F → H: Work.RW (리셋)
H → R: Work.OG (원점)

// 비정상 전이
G → H: Work.RW && Work.Error (에러 리셋)
G → R: System.Reset (시스템 리셋)
Any → R: System.PowerOn (전원 투입)
```

**FSM 일관성 보장**:
```
// 상태 배타성 (한 번에 하나의 상태만)
Assert: (R + G + F + H) == 1

// 전이 조건 명확성
Transition(R→G): Work.R && Work.SW
Transition(G→F): Work.G && Work.EW
Transition(F→H): Work.F && Work.RW
Transition(H→R): Work.H && Work.OG
```

**에러 처리**:
```
// 에러 발생 시 상태 처리
if (Work.Error) {
    if (Work.G) → Work.G = RESET  // Going 중단
    if (System.AutoMode) → Wait for Clear
    if (System.ManualMode) → Allow Manual Reset
}
```

**적용 예시**:
- **컨베이어**: R(대기) → G(이송) → F(도착) → H(복귀) → R(대기)
- **로봇팔**: R(홈) → G(작업) → F(완료) → H(홈복귀) → R(홈)
- **가공기**: R(준비) → G(가공) → F(완료) → H(청소) → R(준비)

**주의사항**:
- **상태 일관성**: 항상 하나의 상태만 활성화
- **전이 원자성**: 상태 전이는 1스캔 내 완료
- **초기 상태**: 전원 투입 시 H→R 시퀀스로 시작
- **에러 복구**: Error 상태에서는 Clear 후 H→R로 복구


## 2. Work 릴레이 그룹

### R005. Work 시작 릴레이 (SW - Start Work)

**목적**: Work 실행을 시작하여 R(Ready) → G(Going) 상태 전이

**로직 정의**:
```
Work.SW := SR(
    SET := (∀SourceWork.EW || ∃ApiDef.PS || Work.SF) && Work.Aux && Work.OG,
    RST := Work.RW
)
```

**상세 설명**:
- **SET 조건**:
  - `∀SourceWork.EW`: 모든 선행 Work가 F(Finish) 상태
  - `∃ApiDef.PS`: 외부 시스템에서 시작 신호 수신
  - `Work.SF`: HMI에서 강제 시작 버튼
  - `Work.Aux`: 안전/인터록 조건 만족
  - `Work.OG`: 원점 위치 확인 (R 상태 보증)
  
- **RST 조건**:
  - `Work.RW`: Work 리셋으로 H(Homing) 진입

**상태 전이**: `R → G`

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9
         │   │   │   │   │   │   │   │   │   │
Work.R   ▔▔▔▔▔▔▔▔▔▔▔▔╲_______________╱▔▔▔▔▔▔
                      
Source.EW ____________╱▔▔▔╲_____________________
                      ↑
Work.Aux ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                      
Work.OG  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                      ↓
Work.SW  _____________╱▔▔▔▔▔▔╲__________________
                      
Work.G   _________________╱▔▔▔▔▔▔╲______________
                                  ↓
Work.RW  ______________________╱▔▔╲______________

상태:     [ Ready ][ Going  ][H][ Ready ]
```

**시퀀스 설명**:
- `t0-t2`: Work.R 상태 유지 (준비 완료)
- `t3`: SourceWork.EW 신호 ON → Work.SW SET 조건 만족
- `t4`: Work.SW ON → Work.G 상태 전이 (R→G)
- `t6`: Work.RW 신호 → Work.SW RST, Work.G 종료
- `t7`: Homing 상태 (H)
- `t8`: Work.OG 재확인 → Work.R 상태 복귀

**적용 예시**:
- 컨베이어가 제품을 이송 완료(SourceWork.EW) 후 로봇팔 작업 시작(Work.SW)
- 외부 MES 시스템에서 작업 지시(ApiDef.PS) 수신 시 가공 작업 시작
- 작업자가 수동으로 시작 버튼(Work.SF) 누를 때 검사 공정 시작





### R006. Work 종료 릴레이 (EW - End Work)

**목적**: Work 내 모든 작업 완료 시 G(Going) → F(Finish) 상태 전이

**로직 정의**:
```
Work.EW := SR(
    SET := (Work.GG && ∀Call.EC && Script.Done && Time.Done && Motion.Done) || Work.ForceFinish,
    RST := (Work.RW && ∀Call.Off) || (!∃Call && Work.RW)
)
```

**상세 설명**:
- **SET 조건**:
  - `Work.GG`: Work 실행 중 유지 릴레이 (Going 상태 1스캔 지연)
  - `∀Call.EC`: 모든 내부 Call 완료 (ET Contact 확인)
  - `Script.Done`: 스크립트 실행 완료 (있는 경우)
  - `Time.Done`: 시간 조건 완료 (있는 경우)
  - `Motion.Done`: 모션 동작 완료 (있는 경우)
  - `Work.ForceFinish`: 강제 종료 (ONP && Manual Mode)

- **RST 조건**:
  - `Work.RW && ∀Call.Off`: 모든 Call이 OFF 상태일 때만 리셋 수행
  - `!∃Call && Work.RW`: Call이 없는 경우 즉시 리셋

**상태 전이**: `G → F`

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Work.G   ____╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲_______________________
         
Work.GG  ________╱▔▔▔▔▔▔▔▔▔▔▔╲_______________________
                 ↑(1스캔 지연)
                 
Call[1].EC ______╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲__________________
         
Call[2].EC __________╱▔▔▔▔▔▔▔▔▔▔▔▔╲__________________
         
Call[3].EC ______________╱▔▔▔▔▔▔▔▔╲__________________
                         ↓
Work.EW  ________________╱▔▔▔▔▔▔▔▔▔╲_________________
                         
Work.F   ____________________╱▔▔▔▔▔▔▔╲_______________
                                     
Work.RW  ________________________╱▔▔▔╲_______________
                                     ↑
Call.AllOff ____________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                                 ↑(모든 Call 초기화 확인)

상태:    [R][  Going (G)  ][ Finish (F) ][H][R]
```

**실행 순서 (중요)**:
1. **ET (End Tag) 설정**: 종료 조건 만족 시 즉시 SET
2. **GG (Going Guard) 설정**: G 상태를 1스캔 유지하여 안정적 전이
3. **Call 초기화 확인**: RW 신호 시 모든 Call OFF 확인 후 리셋

```
// 수식 실행 순서 (순서 중요!)
Step 1: (SET, RST) ==| (Work.ET)    // 종료 태그 먼저 설정
Step 2: (Work.G, OFF) --| (Work.GG)  // Going 상태 1스캔 유지
```

**특수 조건 처리**:
```
// Script가 있는 경우
if (Work.Script.Exists) 
    Condition += Script.Relay
else 
    Condition += Always_ON

// Time 조건이 있는 경우  
if (Work.Time.Exists)
    Condition += Time.Relay
else
    Condition += Always_ON

// Motion이 있는 경우
if (Work.Motion.Exists)
    Condition += Motion.Relay  
else
    Condition += Always_ON
```

**Call 초기화 확인 로직**:
```
// Call이 있는 경우 - 모든 Call OFF 확인
if (∃Call) {
    CoinAllOff := ∀Call.State == OFF
    RST := Work.RW && CoinAllOff
}
// Call이 없는 경우 - 즉시 리셋
else {
    RST := Work.RW
}
```

**강제 종료 조건**:
```
ForceFinish := ONP && Flow.ManualMode
// ONP: One Pulse (HMI 강제 종료 버튼)
// Flow.ManualMode: 수동 운전 모드
```

**적용 예시**:
- 로봇팔의 3개 동작(Call)이 모두 완료되면 Work 종료
- 가공 작업 중 설정 시간 도달 시 자동 종료
- 수동 모드에서 작업자가 강제 종료 버튼으로 즉시 종료

**주의사항**:
- **H/S(Half Step) 동기화**: Call 초기화 완료 확인 후 리셋으로 스캔 동기 보장
- GG 릴레이는 반드시 ET 설정 후 실행 (Full Scan Step 제어 보장)
- Script, Time, Motion은 옵션이며, 없을 경우 항상 ON으로 처리
- 강제 종료는 수동 모드에서만 가능


### R007. Work 리셋 릴레이 (RW - Reset Work)

**목적**: Work를 초기화하여 F(Finish) → H(Homing) 상태 전이

**로직 정의**:
```
Work.RW := SR(
    SET := (∃ResetCausal.Going && Work.ET) || 
           FallingPlans || 
           ManualReset || 
           OriginReset,
    RST := Work.R
)
```

**상세 설명**:

- **SET 조건 (병렬 처리)**:

  1. **인과 리셋 (Causal Reset)**:
     - `∃ResetCausal.Going && Work.ET`
     - `∃ResetCausal.Going`: 리셋 소스 중 하나라도 Going 상태
     - `Work.ET`: 현재 Work가 종료 상태일 때만
     - Arrow 연결된 다음 Work의 Going 신호가 현재 Work 리셋 트리거

  2. **API 리셋 (Falling Plans)**:
     - `FallingPlans`: 외부 시스템의 ApiDef 리셋 신호

  3. **수동 리셋 (Manual Reset)**:
     - `ManualReset := RFP && Flow.ManualMode`
     - `RFP`: Reset Force Pulse (HMI 리셋 버튼)

  4. **원점 리셋 (Origin Reset)**:
     - `(OB || OA) && Flow.ManualMode && !OG`

- **RST 조건**:
  - `Work.R`: Work가 Ready 상태로 복귀 완료

**상태 전이**: `F → H → R`

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Work.F   ▔▔▔▔▔▔▔▔▔▔▔▔╲________________________________
Work.ET  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲____________________________
                         
NextWork.Going ______╱▔▔▔▔▔▔▔╲________________________
                     ↑(리셋 트리거)
                     
Work.RW  ________________╱▔▔▔▔▔╲______________________
                         ↑(모든 조건 병렬 평가)
                         
Work.H   ____________________╱▔▔▔▔╲___________________
                                  
Work.OG  ________________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                                  
Work.R   __________________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔

상태:    [ Finish (F) ][ Homing ][ Ready (R) ]
```

**스캔 기반 동작 원리**:
```
// DS 스캔 제어 - 우선순위 없이 병렬 평가
Every Scan: {
    // 모든 조건 동시 평가 (OR 조건)
    Condition1 = (∃ResetCausal.Going && Work.ET)
    Condition2 = FallingPlans  
    Condition3 = (RFP && Flow.ManualMode)
    Condition4 = ((OB || OA) && Flow.ManualMode && !OG)
    
    // SET 조건 - 하나라도 참이면 SET
    if (Condition1 || Condition2 || Condition3 || Condition4) {
        Work.RW = SET
    }
    
    // RST 조건
    if (Work.R) {
        Work.RW = RESET
    }
}
```

**PC 이벤트 제어 모드**:
```
// 이벤트 기반 동작 (PC 제어)
OnEvent: {
    // 각 조건이 독립적으로 이벤트 트리거
    Event1: ResetCausal.Going↑ → Check Work.ET → SET RW
    Event2: FallingPlans↑ → SET RW
    Event3: RFP↑ && ManualMode → SET RW
    Event4: (OB↑ || OA↑) && ManualMode && !OG → SET RW
}
```

**리셋 트리거 메커니즘**:
```
// Arrow Reset Signal (Going 레벨 감지)
ResetSignal := {
    Source: NextWork.Going || ParallelWork.Going
    Type: Level Signal (Going 상태 지속)
    Trigger: Work.ET && ResetSignal
    Effect: Current Work Reset
}
```

**병렬/순차 리셋 예시**:
```
[순차 구조]
Work_A (F) ← Reset ── Work_B (G)
                      ↑ Work_B.Going이 Work_A 리셋

[병렬 구조]
Work_A (F) ← Reset ┐
Work_B (F) ← Reset ├── Work_C (G)
Work_D (F) ← Reset ┘
                      ↑ Work_C.Going이 모든 병렬 Work 리셋
```

**스캔 지연이 필요한 특수 케이스**:
```
// GG 릴레이 사용 예 (꼭 필요한 경우만)
Work.GG := Delay(Work.G, 1scan)  // 1스캔 지연

// 사용 목적:
// 1. Full Scan Step 제어 보장
// 2. ET 설정 후 안정적 상태 전이
// 3. H/S(Half Step) 동기화
```

**적용 예시**:
- **정상 흐름**: 로봇팔 작업 완료(ET) → 컨베이어 시작(Going) → 로봇팔 리셋
- **병렬 리셋**: 병합 Work 시작(Going) → 모든 선행 병렬 Work 리셋
- **수동 리셋**: 작업자가 리셋 버튼으로 즉시 초기화
- **원점 리셋**: 원점 위치 이탈 시 원점 복귀 실행

**주의사항**:
- **스캔 제어**: 모든 조건이 매 스캔마다 병렬 평가 (우선순위 없음)
- **이벤트 제어**: PC 제어 시 각 조건이 독립적 이벤트로 동작
- **GG 사용 최소화**: 꼭 필요한 경우(상태 전이 안정성)에만 스캔 지연 사용
- Work.ET 확인으로 완료 상태에서만 정상 리셋 (데이터 정합성)


## 3. Call 릴레이 그룹

### R008. Call 시작 릴레이 (SC - Start Call)

**목적**: Work 내부의 Call 시작 제어 (Head Call과 Tail Call 구분 처리)

**로직 정의**:
```
// Head Call (첫 번째 Call)
HeadCall.SC := SR(
    SET := !Work.Error && Work.G && Flow.AutoMode && Work.RR,
    RST := Call.ET || Call.RT
)

// Tail Call (후속 Call)
TailCall.SC := SR(
    SET := !Work.Error && Work.G && Flow.AutoMode && PrevCall.Finish,
    RST := Call.ET || Call.RT
)
```

**상세 설명**:

**공통 시작 조건**:
- `!Work.Error`: Work 에러 없음
- `Work.G`: Work가 Going 상태
- `Flow.AutoMode`: 자동 운전 모드

**Head Call (초기 Call)**:
- **추가 SET 조건**:
  - `Work.RR`: Real Reset 릴레이 (Work 초기화 완료)
- **의미**: Work 시작 시 첫 번째로 실행되는 Call

**Tail Call (연쇄 Call)**:
- **추가 SET 조건**:
  - `PrevCall.Finish`: 이전 Call의 DAG 인과 완료
  - `GetStartDAGAndCausals()`: 선행 Call 완료 신호
- **의미**: 선행 Call 완료 후 순차적으로 실행

**공통 RST 조건**:
- `Call.ET`: Call End Tag (종료)
- `Call.RT`: Call Reset Tag (리셋)

**Call 체인 구조**:
```
Work.G ──┬──> HeadCall[1].SC ──> Call[1].ET ──┐
         │                                     │
         └──> HeadCall[2].SC ──> Call[2].ET ──┼──> TailCall[3].SC
                                               │
         └──> HeadCall[3].SC ──> Call[3].ET ──┘
         
Legend:
- HeadCall: Work.RR 조건으로 시작
- TailCall: 선행 Call 완료 조건으로 시작
```

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Work.G   ____╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲________
Work.RR  ____╱▔▔▔╲____________________________________
Flow.AOP ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔

Head[1].SC __╱▔▔▔▔╲__________________________________
Head[1].ET ______╱▔╲__________________________________

Head[2].SC __╱▔▔▔▔▔▔╲________________________________
Head[2].ET __________╱▔╲______________________________
                      ↓
Tail[3].SC __________╱▔▔▔▔╲__________________________
Tail[3].ET ______________╱▔╲__________________________
                          ↓
Tail[4].SC ______________╱▔▔▔▔╲______________________
Tail[4].ET __________________╱▔╲______________________

Call Flow: [Head1,2 병렬] → [Tail3 순차] → [Tail4 순차]
```

**DAG (Directed Acyclic Graph) 처리**:
```
// Call Graph 구조
Graph.Inits = [HeadCall1, HeadCall2, ...]  // 초기 Call들
Graph.Vertices = [All Calls]               // 모든 Call
Graph.Tails = Vertices - Inits            // 후속 Call들

// 시작 조건 분기
for (Call in AllCalls) {
    if (Call in HeadCalls) {
        StartCondition = BaseCondition && Work.RR
    } else {
        StartCondition = BaseCondition && GetStartDAGAndCausals()
    }
}
```

**병렬/순차 실행 패턴**:
```
[병렬 Head Calls]
Work.RR ──┬──> Call_A.SC
          ├──> Call_B.SC  
          └──> Call_C.SC
          (동시 시작)

[순차 Tail Calls]
Call_A.ET ──> Call_D.SC
Call_D.ET ──> Call_E.SC
(순차 진행)

[병합 후 진행]
Call_B.ET ──┐
Call_C.ET ──┼──> Call_F.SC
Call_E.ET ──┘
(모두 완료 후 시작)
```

**에러 처리**:
```
// Work 에러 시 모든 Call 중단
if (Work.Error) {
    All Call.SC = BLOCK  // 새로운 Call 시작 차단
    Running Calls = Continue or Stop (설정에 따라)
}
```

**수동 모드 처리**:
```
// 수동 모드에서는 Flow.AutoMode 조건 제외
if (System.ManualMode) {
    StartCondition = !Work.Error && Work.G && Manual.StartButton
}
```

**적용 예시**:
- **로봇 작업**: 
  - Head: [그리퍼열기, 위치이동] 동시 실행
  - Tail: 픽업 → 이동 → 배치 순차 실행
  
- **가공 작업**:
  - Head: [척킹, 쿨런트ON] 동시 실행  
  - Tail: 황삭 → 정삭 → 측정 순차 실행

**주의사항**:
- **RR 신호**: Head Call은 Work.RR 신호로 동기 시작
- **DAG 무결성**: 순환 참조 방지, 인과 관계 명확화
- **ET/RT 처리**: Call 종료/리셋 시 즉시 SC 해제
- **병렬 안전성**: 동시 실행 Call 간 자원 충돌 방지

### R009. Call 종료 릴레이 (EC - End Call)

**목적**: Call 작업 완료 조건 확인 및 종료 신호 생성

**로직 정의**:
```
// Disabled Call (비활성 Call)
DisabledCall.EC := SR(
    SET := Call.SC && Work.G,
    RST := Call.RT
)

// Normal Call (정상 Call)
NormalCall.EC := SR(
    SET := Call.SC && Work.G && Call.EndAction && !Work.Error,
    RST := Call.RT
)
```

**상세 설명**:

**Disabled Call (비활성화된 Call)**:
- **SET 조건**:
  - `Call.SC`: Call 시작됨
  - `Work.G`: Work가 Going 상태
- **RST 조건**: `Call.RT` (Call 리셋)
- **의미**: 비활성화된 Call은 시작과 동시에 즉시 종료

**Normal Call (정상 Call)**:
- **SET 조건**:
  - `Call.SC`: Call 시작됨
  - `Work.G`: Work가 Going 상태
  - `Call.EndAction`: Call 종료 조건 만족
  - `!Work.Error`: Work 에러 없음
- **RST 조건**: `Call.RT` (Call 리셋)
- **의미**: 종료 조건 만족 시 Call 완료

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Work.G   ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Work.Error ___________________________________________

[Normal Call]
Call.SC  ____╱▔▔▔▔▔▔▔▔╲______________________________
EndAction ____________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Call.EC  ____________╱▔▔▔▔╲__________________________
                     ↑(SC && EndAction && !Error)

[Disabled Call]  
Call.SC  ____╱▔▔▔╲____________________________________
Call.EC  ____╱▔▔▔╲____________________________________
             ↑(즉시 종료)

[Reset 발생]
Call.RT  ________________________╱▔╲__________________
Call.EC  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲____________________
```

**Call 종료 조건 (EndAction)**:
```
// Call 레벨 EndAction 구성 (2가지만)
Call.EndAction := {
    1. ApiCall 완료: apiItemEnd && actionIn
    2. Sensor 조건: SensorCondition.Met
}
```

**센서 조건 상세**:
```
// 모든 센서 타입을 통합 처리
SensorCondition := {
    Digital: DI.On || DI.Off (설정값)
    Analog: AI.Value >= Threshold (범위 조건)
    Counter: Counter.Value >= Target (카운터 센서)
    External: External.Signal (외부 신호/ACK)
}
```

**특수 Call 처리**:
```
// 1. Disabled Call (비활성)
if (Call.Disabled) {
    // 시작과 동시에 종료 (Pass-through)
    EC = SC && Work.G
}

// 2. Sensor Wait Call
if (Call.HasSensorCondition) {
    // 센서 조건 만족 시 즉시 종료 (대기 없음)
    EC = SC && Work.G && SensorCondition && !Error
}

// 3. API Call
if (Call.HasApiCall) {
    // API 응답 수신 시 종료
    EC = SC && Work.G && apiItemEnd && actionIn && !Error
}
```

**즉시 종료 패턴 (Coin Flip)**:
```
// "무조건 센서 맞으면 기다리지 않기"
CoinFlip Pattern:
├─> Call.SC 발생
├─> SensorCondition 이미 만족 상태
└─> Call.EC 즉시 SET (대기 없음)

예시:
- 위치 센서가 이미 ON → 이동 Call 즉시 완료
- 압력값이 이미 범위 내 → 압력 Call 즉시 완료
- 카운터가 이미 도달 → 카운트 Call 즉시 완료
```

**Call 종료 플로우**:
```
[API Call 플로우]
SC(시작) → ApiCall 실행 → actionIn(응답) → EC(종료)

[센서 Call 플로우]
SC(시작) → Sensor 확인 → 조건만족 → EC(종료)

[즉시 종료]
SC(시작) → Condition(이미만족) → EC(즉시종료)

[비활성 Call]
SC(시작) → EC(즉시종료)
```

**병렬 Call 종료 처리**:
```
Work.G ──┬──> Call_A.SC ──> ApiCall ──> Call_A.EC
         ├──> Call_B.SC ──> (Disabled) ──> Call_B.EC
         └──> Call_C.SC ──> 센서대기 ──> Call_C.EC
         
// 각 Call은 독립적으로 종료
// Work.EW는 모든 Call.EC 완료 후 SET
```

**에러 처리**:
```
// Work 에러 시
if (Work.Error) {
    Call.EC = BLOCK  // 종료 차단
    // Error Clear 후 재시도 또는 Skip
}

// Call 개별 타임아웃
if (Call.WaitTime > MaxTime) {
    Call.EC = BLOCK
    Call.TimeoutFlag = SET
}
```

**적용 예시**:

**API Call**:
- 로봇 이동: SC → ApiCall(이동명령) → actionIn(완료) → EC
- 데이터 요청: SC → ApiCall(요청) → apiItemEnd(응답) → EC

**센서 Call (다양한 타입)**:
- 디지털: SC → DI.ProductSensor.On → EC
- 아날로그: SC → AI.Temperature > 100 → EC
- 카운터: SC → Counter.ProductCount >= 10 → EC
- 외부신호: SC → External.ConveyorReady → EC

**Disabled Call**:
- 스킵 설정: SC → EC (즉시)
- 테스트 제외: SC → EC (즉시)

**즉시 종료 Call**:
- 센서 확인: SC → 이미ON → EC (대기없음)
- 조건 체크: SC → 조건만족 → EC (즉시)

**주의사항**:
- **Call 단순성**: Call은 API 호출과 센서 조건에만 집중
- **센서 통합**: 모든 센서 타입을 SensorCondition으로 통합 처리
- **즉시 종료 안전성**: Coin Flip 패턴 시 상태 일관성 확인
- **에러 시 동작**: Work.Error 시 새로운 EC 차단





### R010. Call 리셋 릴레이 (RC - Reset Call)

**목적**: Work 리셋 시 모든 내부 Call을 초기화

**로직 정의**:
```
Call.RC := SR(
    SET := ParentWork.RT,
    RST := Call.R
)
```

**상세 설명**:

**SET 조건**:
- `ParentWork.RT`: 부모 Work의 리셋 태그
- Work가 리셋되면 모든 하위 Call도 연쇄 리셋

**RST 조건**:
- `Call.R`: Call이 Ready 상태로 복귀 완료

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Work.F   ▔▔▔▔▔▔▔▔▔▔▔▔╲________________________________
Work.RT  ____________╱▔▔▔╲____________________________
                     ↑(Work 리셋 신호)
                     
Call[1].RT __________╱▔▔▔╲____________________________
Call[2].RT __________╱▔▔▔╲____________________________
Call[3].RT __________╱▔▔▔╲____________________________
                     ↑(모든 Call 동시 리셋)
                     
Call[1].R ________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Call[2].R ________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Call[3].R ________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                         ↑(Call Ready 상태 복귀)

상태:    [Work Reset] → [Call Reset] → [Call Ready]
```

**리셋 전파 메커니즘**:
```
Work.RT (트리거)
    │
    ├──> Call[1].RT ──> Call[1] 초기화 ──> Call[1].R
    ├──> Call[2].RT ──> Call[2] 초기화 ──> Call[2].R
    └──> Call[3].RT ──> Call[3] 초기화 ──> Call[3].R
    
// 모든 Call이 동시에 리셋 신호 수신
// 각 Call은 독립적으로 초기화 수행
```

**Call 그래프 처리**:
```
// Work 내부 Call 구조
Real.Graph.Vertices = [Call1, Call2, Call3, ...]

// 리셋 로직
for (Call in Real.Graph.Vertices) {
    Call.RT = SR(
        SET := Real.V.RT,  // 부모 Work 리셋
        RST := Call.R      // Call Ready 복귀
    )
}
```

**리셋 동작 순서**:
```
1. Work.RW 발생 (Work 리셋 시작)
2. Work.RT SET (Work 리셋 태그)
3. 모든 Call.RT SET (Call 리셋 전파)
4. 각 Call 초기화 수행
5. Call.R 도달 (Ready 상태)
6. Call.RT RESET
7. Work 초기화 계속
```

**병렬 Call 리셋**:
```
Work.RT ──┬──> Call_A.RT ──> 초기화 ──> Call_A.R
          ├──> Call_B.RT ──> 초기화 ──> Call_B.R
          └──> Call_C.RT ──> 초기화 ──> Call_C.R
          
// 모든 Call 동시 리셋
// 각각 독립적으로 Ready 도달
```

**순차 Call 리셋**:
```
Work.RT ──> Call_1.RT ──> Call_1.R
       ──> Call_2.RT ──> Call_2.R
       ──> Call_3.RT ──> Call_3.R

// 시퀀스와 무관하게 모두 동시 리셋
// DAG 구조 무시하고 일괄 초기화
```

**특수 상황 처리**:
```
// Going 상태 Call 리셋
if (Call.G && Work.RT) {
    Call.RT = SET      // 즉시 리셋
    Call.G = RESET     // Going 중단
    // 진행 중인 작업 정리
}

// Disabled Call도 리셋
if (Call.Disabled && Work.RT) {
    Call.RT = SET      // 리셋 신호는 전달
    Call.R = SET       // 즉시 Ready
}
```

**에러 상황 리셋**:
```
// Work 에러로 인한 리셋
if (Work.Error && Work.RT) {
    All Call.RT = SET
    // 에러 상태 Call도 강제 리셋
}

// Call 개별 에러는 무시
if (Call.Error && Work.RT) {
    Call.RT = SET      // 에러 무시하고 리셋
    Call.Error = RESET // 에러 플래그 클리어
}
```

**적용 예시**:

**정상 리셋**:
- 작업 완료 후: Work.F → Work.RT → All Call.RT
- 다음 사이클: NextWork 시작 → PrevWork.RT → All Call.RT

**강제 리셋**:
- 수동 리셋: Manual.Reset → Work.RT → All Call.RT
- 시스템 리셋: System.Reset → Work.RT → All Call.RT

**에러 리셋**:
- 타임아웃: Work.Timeout → Work.RT → All Call.RT
- 비상정지: Emergency → Work.RT → All Call.RT

**주의사항**:
- **동시 리셋**: 모든 Call이 동시에 리셋 신호 수신
- **독립 초기화**: 각 Call은 독립적으로 초기화 수행
- **상태 무관**: Call의 현재 상태(G/F/H)와 무관하게 리셋
- **에러 클리어**: 리셋 시 Call 에러 상태도 함께 클리어



## 4. API 연동 릴레이 그룹

### R011. API 호출 시작 (apiDefSet)

**목적**: Call에서 외부 시스템(ApiDef)으로 API 호출 트리거

**로직 정의**:
```
ApiDefSet := SR(
    SET := ∃Call.PS,
    RST := ∃Call.End
)
```

**상세 설명**:

- **SET 조건**: 
  - `∃Call.PS`: 연결된 Call 중 하나라도 Plan Set(시작) 신호
  - 여러 Call이 OR 조건으로 통합
  
- **RST 조건**: 
  - `∃Call.End`: 연결된 Call 중 하나라도 종료 신호
  - Call 종료 시 API 호출 해제

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │

Call.PS  ____╱▔╲______________________________________
Call.End ____________╱▔╲______________________________
ApiDefSet ___╱▔▔▔▔▔▔╲________________________________
             ↑SET    ↑RST
```

---

### R012. API 호출 종료 (apiDefEnd)

**목적**: 외부 시스템(ApiDef)으로부터 응답 수신 확인

**로직 정의**:
```
ApiDefEnd := SR(
    SET := ApiDef.RxET,
    RST := ApiSystem.OFF
)
```

**상세 설명**:

**SET 조건**:
- `ApiDef.RxET`: 외부 시스템의 수신 종료 태그
- ApiDef가 처리 완료하고 응답 신호 전송

**RST 조건**:
- `ApiSystem.OFF`: API 시스템 OFF 신호
- 시스템 리셋 또는 초기화 시

**API 통신 플로우**:
```
[Local System]              [Remote System]
     │                            │
     ├──apiDefSet───────────────>│ (PS)
     │                            │
     │                     ApiDef 처리
     │                            │
     │<─────────RxET──────────────┤
     │                            │
     └──apiDefEnd = ON            │
```

**다중 Call 처리**:
```
// 여러 Call이 하나의 ApiDef를 공유하는 경우
Calls = [Call1, Call2, Call3]

// OR 조건으로 통합
pStart = Call1.PS || Call2.PS || Call3.PS
pEnd = Call1.End || Call2.End || Call3.End

// API 호출
ApiDefSet = SR(SET := pStart, RST := pEnd)
```

**ApiDef 연동 로직 구현**:
```
// A1_ApiDefSet 구현
ApiDefManager.A1_ApiDefSet(calls) {
    pStart = ∃Call.PS  // 모든 Call의 PS를 OR
    pEnd = ∃Call.End    // 모든 Call의 End를 OR
    
    ApiDefSet = SR(
        SET := pStart,
        RST := pEnd
    )
}

// A2_ApiDefEnd 구현  
ApiDefManager.A2_ApiDefEnd() {
    ApiDefEnd = SR(
        SET := ApiDef.RxET,
        RST := ApiSystem.OFF
    )
}
```

**전체 ApiDef 연동 시퀀스**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9
         │   │   │   │   │   │   │   │   │   │
Call.SC  ____╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲____________________
Call.PS  ________╱▔╲______________________________
                 ↓
apiDefSet _______╱▔▔▔▔▔▔▔▔▔▔▔╲___________________
                 ↓
[API 전송] ──────────────>
                         ↓
[ApiDef 처리]            ████
                             ↓
ApiDef.RxET _________________╱▔╲__________________
                             ↓
apiDefEnd ___________________╱▔▔▔▔▔╲______________
                                   ↓
actionIn ____________________╱▔▔▔▔▔▔▔╲____________
                                     ↓
Call.EC  ________________________╱▔╲______________

시퀀스: SC → PS → apiDefSet → ApiDef처리 → RxET → 
        apiDefEnd → actionIn → EC
```

**에러 처리**:
```
// API 타임아웃
if (apiDefSet && !apiDefEnd && Timeout) {
    ApiError = SET
    // 재시도 또는 스킵 로직
}

// 통신 에러
if (ApiComm.Error) {
    apiDefEnd = Force OFF
    ApiError = SET
}
```

**적용 예시**:

**일반 ApiDef 호출**:
- PLC 통신: Call → apiDefSet → PLC 처리 → apiDefEnd
- 로봇 제어: Call → apiDefSet → 로봇 동작 → apiDefEnd
- 데이터베이스: Call → apiDefSet → DB 쿼리 → apiDefEnd

**다중 Call 연동**:
```
3개 Call이 1개 ApiDef 공유:
Call1.PS ─┐
Call2.PS ─┼─OR─> apiDefSet → ApiDef
Call3.PS ─┘

Call1.End ─┐
Call2.End ─┼─OR─> apiDefSet RST
Call3.End ─┘
```

**병렬 ApiDef 호출**:
```
Work.G ──┬──> Call_A ──> ApiDef_1
         ├──> Call_B ──> ApiDef_2
         └──> Call_C ──> ApiDef_3
         
// 각 ApiDef는 독립적으로 동작
// apiDefSet/End 쌍으로 관리
```

**ApiDef 계층 구조**:
```
System A                    System B
  Work                        ApiDef
   └─Call ──apiDefSet──>      └─Work
                                └─Call ──apiDefSet──> System C
                                                        ApiDef
```

**주의사항**:
- **원자성**: apiDefSet과 apiDefEnd는 쌍으로 동작
- **OR 통합**: 여러 Call 신호는 OR 조건으로 통합
- **독립성**: 각 ApiDef는 독립적인 Set/End 관리
- **에러 복구**: 통신 에러 시 적절한 타임아웃 및 재시도


### R013. 물리 출력 신호 (actionOut)

**목적**: ApiDef 호출 후 실제 물리 장치로 제어 신호 출력

**로직 정의**:
```
// 디지털 출력
DigitalActionOut := SR(
    SET := Call.PS && !Call.End,
    RST := MutualReset || Emergency || Pause
)

// 아날로그 출력
AnalogActionOut := 
    if (SimMode || NoInput) 
        Value @ Call.ET
    else 
        Value @ (Call.PS && Call.PE)
```

**상세 설명**:

#### 디지털 출력 (Digital Output)

**Normal Action 타입**:
```
ActionOut := SR(
    SET := Call.PS && !Call.End,
    RST := OFF
)
```

**Push Action 타입** (상호 리셋):
```
PushActionOut := SR(
    SET := Call.PS && !Call.End,
    RST := MutualResetCoins.PS  // 다른 Call의 시작이 리셋
)
```

**Emergency/Pause 처리**:
```
// Emergency와 Pause 조합에 따른 동작
EmergencyAction = {
    Target=ON:  SET += Flow.Emergency
    Target=OFF: RST += Flow.Emergency
}

PauseAction = {
    Target=ON:  SET += Flow.Pause
    Target=OFF: RST += Flow.Pause
}
```

#### 아날로그 출력 (Analog Output)

**값 출력 조건**:
```
// 시뮬레이션 모드 또는 입력 태그 없음
if (SimMode || InTag == NULL) {
    OutTag = Value @ Call.ET  // 종료 시 값 출력
} else {
    OutTag = Value @ (Call.PS && Call.PE)  // 시작과 종료 조건
}

// 1사이클 후 기본값 복귀
FallingEdge := !Call.PS (1 scan delay)
if (FallingEdge && InTag != NULL) {
    OutTag = DefaultValue
}
```

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
[디지털 출력]
Call.PS  ____╱▔╲______________________________________
Call.End ________________╱▔╲__________________________
ActionOut ___╱▔▔▔▔▔▔▔▔▔▔╲____________________________
             ↑SET        ↑RST

[아날로그 출력 - Normal]
Call.PS  ____╱▔╲______________________________________
Call.PE  ________╱▔╲__________________________________
Value    ________[100]________________________________
ActionOut _______[100]╱▔╲_____________________________

[아날로그 출력 - SimMode]
Call.ET  ________________╱▔╲__________________________
Value    ________________[100]________________________
ActionOut _______________[100]╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
```

#### Emergency/Pause 매트릭스

```
상황별 출력 제어:
┌─────────┬─────────┬─────────┬──────────────┐
│ Emg     │ Pause   │ Target  │ 출력 동작     │
├─────────┼─────────┼─────────┼──────────────┤
│ ON/ON   │ ON/ON   │ ON/ON   │ SET+=E||P    │
│ ON/ON   │ ON/OFF  │ ON/OFF  │ SET+=E,RST+=P│
│ ON/OFF  │ ON/ON   │ OFF/ON  │ SET+=P,RST+=E│
│ ON/OFF  │ ON/OFF  │ OFF/OFF │ RST+=E||P    │
└─────────┴─────────┴─────────┴──────────────┘
```

#### Push Type (상호 배타적 출력)

```
// 실린더 같은 단동 액추에이터
Call_A.Out ──┬─> 전진
Call_B.Out ──┴─> 후진

// 상호 리셋 로직
Call_A.ActionOut := SR(
    SET := Call_A.PS,
    RST := Call_B.PS  // B 시작이 A 리셋
)

Call_B.ActionOut := SR(
    SET := Call_B.PS,
    RST := Call_A.PS  // A 시작이 B 리셋
)
```

#### 구현 코드 매핑

```fsharp
// 디지털 출력 생성
getStatementTypeDigital(set, td, callActionType) {
    if (callActionType == Push) {
        // MutualResetCoins 사용
        RST = MutualResetCoins.PS
    } else {
        // Normal Action
        RST = OFF
    }
    
    // Emergency/Pause 조합 처리
    ProcessEmergencyPause(set, rst)
}

// 아날로그 출력 생성
getStatementTypeAnalog(sets, td, call, valueParam) {
    Value = valueParam.Out.WriteValue
    
    if (SimMode || InTag == NULL) {
        // 관찰 없으면 출력 유지
        Output @ Call.ET
    } else {
        // PS && PE 조건
        Output @ (Call.PS && Call.PE)
        
        // 1사이클 후 기본값
        FallingEdge → DefaultValue
    }
}
```

#### 적용 예시

**디지털 출력**:
- 솔레노이드 밸브: ON/OFF 제어
- 램프/부저: 상태 표시
- 모터 구동: Start/Stop

**아날로그 출력**:
- 서보 위치: 0~100mm 지령
- 유량 제어: 0~100% 개도
- 온도 설정: 0~200°C

**Push Type**:
- 양방향 실린더: 전진/후진
- 3위치 밸브: 상/중/하
- 토글 스위치: A/B 선택

**Emergency 처리**:
```
비상정지 시:
- 안전 위치로 이동
- 모터 정지
- 밸브 차단
```

#### 주의사항

- **출력 타이밍**: PS && !End로 안정적 출력
- **상호 배타**: Push Type은 MutualReset 필수
- **아날로그 복귀**: 1사이클 후 기본값 복귀
- **Emergency 우선**: 안전 동작 최우선 처리
- **시뮬레이션**: SimMode에서는 관찰 없이 출력 유지


### R014. 물리 입력 확인 (actionIn)

**목적**: 물리 장치로부터 동작 완료 신호 확인

**로직 정의**:
```
// 입력이 있는 경우
ActionIn := ∀TaskDef.InExpr

// 입력이 없는 경우  
ActionIn := None (항상 참으로 간주)
```

**상세 설명**:

#### ActionIn 표현식 생성

**기본 구조**:
```
Call.ActionInExpr = {
    // 입력이 있는 모든 TaskDef 수집
    InExprs = TaskDefs.Where(ExistInput)
                      .Select(GetInExpr)
    
    // AND 조건으로 통합
    if (InExprs.Any()) 
        return ∀InExprs  // 모든 입력 조건 만족
    else 
        return None      // 입력 없음 (확인 불필요)
}
```

#### 입력 표현식 생성 (GetInExpr)

**조건별 처리**:
```
GetInExpr(ValueParam, DevTag, System) {
    // 태그가 없으면 OFF (항상 거짓)
    if (DevTag == NULL) 
        return System.OFF
    
    // 기본값이면 태그 값 그대로
    if (ValueParam.IsDefaultValue)
        return DevTag.Expression
    
    // Bool 타입
    if (ValueParam.DataType == BOOL) {
        if (ValueParam.Value == true)
            return DevTag.Expression      // 정논리
        else
            return !DevTag.Expression     // 역논리
    }
    
    // 아날로그 타입 (비교)
    else {
        return DevTag.Expression == ValueParam
    }
}
```

#### 타이밍 차트

```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
[디지털 입력]
ActionOut ___╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲________________
Physical  ___________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
InTag     _______________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
ActionIn  _______________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                         ↑(물리 응답 확인)

[아날로그 입력]
ActionOut ___╱▔▔▔▔▔▔▔▔▔▔╲____________________________
SetValue  ___[100]____________________________________
InTag     ___[0]___[50]___[100]______________________
ActionIn  _________________╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                           ↑(값 도달)

[입력 없음]
ActionOut ___╱▔▔▔▔▔▔▔▔▔▔╲____________________________
ActionIn  = None (확인 불필요)
```

#### 입력 타입별 처리

**디지털 입력**:
```
// 정논리 (Value = true)
if (Sensor.ON) 
    ActionIn = DI.Sensor

// 역논리 (Value = false)  
if (Sensor.OFF)
    ActionIn = !DI.Sensor
```

**아날로그 입력**:
```
// 값 비교
if (Temperature >= 100)
    ActionIn = (AI.Temp >= 100)

// 범위 비교
if (50 <= Pressure <= 100)
    ActionIn = (AI.Press >= 50 && AI.Press <= 100)
```

**복수 입력 AND 조건**:
```
Call.TaskDefs = [TaskDef1, TaskDef2, TaskDef3]

ActionIn = TaskDef1.InExpr && 
           TaskDef2.InExpr && 
           TaskDef3.InExpr
           
// 모든 조건 만족 시 ActionIn = true
```

#### 특수 케이스 처리

**입력 태그 없음**:
```
if (InTag == NULL) {
    // 피드백 확인 불가
    // SimMode 또는 가상 장치
    ActionIn = System.OFF (항상 거짓)
}
```

**기본값 사용**:
```
if (ValueParam.IsDefaultValue) {
    // 파라미터 지정 없음
    // 태그 값 그대로 사용
    ActionIn = Tag.Expression
}
```

**타임아웃 처리**:
```
// ActionIn이 일정 시간 내 만족 안되면
if (WaitTime > MaxTime && !ActionIn) {
    Call.Timeout = true
    // 강제 진행 또는 에러 처리
}
```

#### 구현 예시

**실린더 동작 확인**:
```
// 전진 완료 확인
ActionOut: DO.CylinderForward = ON
ActionIn: DI.ForwardSensor = ON

// 후진 완료 확인  
ActionOut: DO.CylinderBackward = ON
ActionIn: DI.BackwardSensor = ON
```

**서보 위치 확인**:
```
// 위치 100mm 도달
ActionOut: AO.ServoPosition = 100
ActionIn: AI.CurrentPosition >= 99 && <= 101
```

**복합 조건**:
```
// 그리퍼 픽업 완료
ActionOut: DO.GripperClose = ON
ActionIn: DI.GripperClosed = ON && 
          DI.PartDetected = ON &&
          AI.GripForce >= 50
```

#### 시스템 통합

```
Call 실행 흐름:
1. Call.SC (시작)
2. apiDefSet (API 호출)
3. actionOut (물리 출력)
4. actionIn (응답 대기) ← 현재 단계
5. apiDefEnd (API 종료)
6. Call.EC (완료)

// ActionIn 만족 시 Call 종료 가능
Call.EC = Call.SC && apiDefEnd && actionIn
```

#### 적용 예시

**센서 확인**:
- 위치 센서: 실린더/모터 도달 확인
- 압력 센서: 공압/유압 확인
- 온도 센서: 가열/냉각 완료

**값 확인**:
- 서보 위치: 목표 위치 도달
- 유량계: 설정 유량 도달
- 중량 센서: 목표 중량 도달

**무확인 동작**:
- 경광등: 출력만 (입력 없음)
- 부저: 출력만 (입력 없음)
- 표시등: 출력만 (입력 없음)

#### 주의사항

- **AND 조건**: 모든 입력 조건이 만족되어야 ActionIn 성립
- **NULL 처리**: 입력 태그 없으면 OFF 반환
- **타입 매칭**: Bool/Analog 타입에 따른 적절한 비교
- **타임아웃**: 무한 대기 방지를 위한 시간 제한
- **시뮬레이션**: SimMode에서는 ActionIn 체크 생략 가능


### R015. API Plan 시작 (apiCallStart)

**목적**: Call에서 ApiDef로 전송할 Plan 시작 신호 생성

**로직 정의**:
```
// 활성 Call
Call.PS := SR(
    SET := ((Manual && StartPoint) || 
            (Manual && ForceStart) || 
            (Auto && SC && PreCondition && Work.G)) 
           && Safety && !MutualReset,
    RST := Call.End || !Flow.Ready
)

// 비활성 Call (Disabled)
DisabledCall.PS := SR(
    SET := OFF,
    RST := Call.End || !Flow.Ready
)
```

**상세 설명**:

#### SET 조건 구성

**수동 모드 경로**:
1. `Manual && StartPoint`: 수동 모드에서 시작점 조건
2. `Manual && ForceStart`: 수동 모드에서 강제 시작 (SFP)

**자동 모드 경로**:
3. `Auto && SC && PreCondition && Work.G`: 
   - `Auto`: Flow 자동 운전 (d_st)
   - `SC`: Call 시작 릴레이 (ST)
   - `PreCondition`: 사전 조건 (AutoPreExpr)
   - `Work.G`: 부모 Work가 Going 상태

**공통 조건**:
- `Safety`: 안전 조건 만족 (SafetyExpr)
- `!MutualReset`: 상호 배타 Call 미실행

#### RST 조건
- `Call.End`: Call 종료 신호
- `!Flow.Ready`: Flow 준비 상태 해제

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │
Flow.Auto ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Work.G   ____╱▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔╲________________
Call.SC  ________╱▔▔▔▔▔▔╲_____________________________
PreCond  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
Safety   ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
!Mutual  ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔
                 ↓
Call.PS  ________╱▔▔▔▔▔▔╲_____________________________
                         ↑
Call.End ________________╱▔╲__________________________
```

#### 상호 배타 처리 (MutualReset)

```
// Push Type Call의 경우
MutualResetCoins = [Call_A, Call_B]

Call_A.PS := SR(
    SET := Conditions && !Call_B.PS,
    RST := Call_A.End || Call_B.PS
)

Call_B.PS := SR(
    SET := Conditions && !Call_A.PS,
    RST := Call_B.End || Call_A.PS
)
```

---

#### R016. API Plan 종료 (apiCallEnd - PE)

**목적**: ApiDef 처리 완료 확인 신호

**로직 정의**:
```
// Job Call (여러 TaskDef 포함)
JobCall.PE := ∀TaskDef.ApiDefEnd

// 일반 Call
NormalCall.PE := Call.PS
```

**상세 설명**:

#### Job Call 처리
```
if (Call.IsJob) {
    // 모든 TaskDef의 ApiDefEnd를 AND
    PE = TaskDef[1].ApiDefEnd && 
         TaskDef[2].ApiDefEnd && 
         TaskDef[3].ApiDefEnd
}
```

#### 일반 Call 처리
```
else {
    // PS 신호를 그대로 PE로 전달
    PE = Call.PS
}
```

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │

[Job Call - 복수 TaskDef]
Call.PS  ____╱▔▔▔▔▔▔╲_________________________________
TD1.End  ____________╱▔╲_______________________________
TD2.End  ______________╱▔╲_____________________________
TD3.End  ________________╱▔╲___________________________
Call.PE  ________________╱▔╲___________________________
                         ↑(모든 TaskDef 완료)

[Normal Call - 단일]
Call.PS  ____╱▔▔▔▔▔▔╲_________________________________
Call.PE  ____╱▔▔▔▔▔▔╲_________________________________
             ↑(PS와 동일)
```

#### 전체 API 흐름

```
Call 실행 시퀀스:
1. Call.SC (시작 릴레이)
   ↓
2. Call.PS (Plan 시작) ← R015
   ↓
3. apiDefSet (API 호출)
   ↓
4. actionOut (물리 출력)
   ↓
5. actionIn (물리 응답)
   ↓
6. apiDefEnd (API 종료)
   ↓
7. Call.PE (Plan 종료) ← R016
   ↓
8. Call.EC (종료 릴레이)
```

#### 모드별 동작

**자동 모드**:
```
Auto Mode:
- Flow.d_st = ON (Drive 상태)
- Call.SC 발생
- PreCondition 확인
- Work.G 확인
→ Call.PS SET
```

**수동 모드**:
```
Manual Mode:
- Flow.mop = ON (Manual 모드)
- StartPoint 또는 ForceStart
- Safety 확인
→ Call.PS SET
```

**Disabled Call**:
```
Disabled:
- PS 항상 OFF
- PE는 정상 동작
- 실제 API 호출 없음
```

#### 적용 예시

**일반 Call**:
```
로봇 이동 Call:
PS → ApiDef(이동명령) → 로봇동작 → PE
```

**Job Call (복합 작업)**:
```
조립 작업 Call:
PS → TaskDef1(픽업) → ApiDefEnd1
   → TaskDef2(이동) → ApiDefEnd2  
   → TaskDef3(조립) → ApiDefEnd3
   → PE (모두 완료)
```

**상호 배타 Call**:
```
실린더 전진/후진:
전진.PS → 후진.PS 차단
후진.PS → 전진.PS 차단
```

#### 주의사항

- **안전 조건**: Safety 조건 항상 확인
- **상호 배타**: MutualReset으로 충돌 방지
- **Flow 상태**: Ready 해제 시 즉시 PS 리셋
- **Job 동기화**: 모든 TaskDef 완료 대기
- **Disabled 처리**: 비활성 Call도 정상 시퀀스 유지


### R016. API Plan 종료 (apiCallEnd - PE)

**목적**: ApiDef 처리 완료 확인 및 Call 종료 준비 신호

**로직 정의**:
```
// Job Call (여러 TaskDef 포함)
JobCall.PE := SR(
    SET := ∀TaskDef.ApiItemEnd,
    RST := OFF
)

// 일반 Call
NormalCall.PE := SR(
    SET := Call.PS,
    RST := OFF
)
```

**상세 설명**:

#### Call 타입별 처리

**Job Call (복합 작업)**:
- **SET 조건**: 
  - `∀TaskDef.ApiItemEnd`: 모든 TaskDef의 API 종료 신호 AND
  - 모든 하위 작업이 완료되어야 PE 활성화
- **RST 조건**: `OFF` (항상 거짓 - 자동 리셋 없음)

**Normal Call (단일 작업)**:
- **SET 조건**: 
  - `Call.PS`: Plan 시작 신호를 그대로 사용
  - PS가 활성화되면 PE도 즉시 활성화
- **RST 조건**: `OFF` (항상 거짓)

**타이밍 차트**:
```
         t0  t1  t2  t3  t4  t5  t6  t7  t8  t9  t10
         │   │   │   │   │   │   │   │   │   │   │

[Job Call - 복수 TaskDef]
Call.PS  ____╱▔▔▔▔▔▔╲_________________________________
TD1.ApiEnd __________╱▔▔▔▔▔╲__________________________
TD2.ApiEnd ____________╱▔▔▔▔▔╲________________________
TD3.ApiEnd ______________╱▔▔▔▔▔╲______________________
                              ↓
Call.PE  __________________╱▔▔▔╲______________________
                           ↑(모든 TaskDef 완료)

[Normal Call - 단일]
Call.PS  ____╱▔▔▔▔▔▔╲_________________________________
Call.PE  ____╱▔▔▔▔▔▔╲_________________________________
             ↑(PS와 동시)
```

#### Job Call 상세 동작

```
Job Structure:
Call (Job)
├── TaskDef1 → ApiItem1 → ApiItemEnd1
├── TaskDef2 → ApiItem2 → ApiItemEnd2
└── TaskDef3 → ApiItem3 → ApiItemEnd3

PE 조건:
PE = ApiItemEnd1 && ApiItemEnd2 && ApiItemEnd3
```

#### Normal Call 상세 동작

```
Normal Structure:
Call → PS → (Direct) → PE

// PS 신호가 PE로 직접 전달
// 별도의 API 종료 대기 없음
```

#### PE 릴레이 특성

**래치 동작**:
```
// 한 번 SET되면 유지
PE := SR(
    SET := Condition,
    RST := OFF  // 리셋 없음
)

// Call 종료 시까지 유지
// Call.EC 또는 Call.RT에 의해 간접 클리어
```

#### 전체 Call 실행 흐름에서 PE 역할

```
Call 실행 시퀀스:
1. Call.SC (시작)
   ↓
2. Call.PS (Plan 시작)
   ↓
3. apiItemSet (API 호출)
   ↓
4. actionOut (물리 출력)
   ↓
5. actionIn (물리 응답)
   ↓
6. apiItemEnd (API 종료)
   ↓
7. Call.PE (Plan 종료) ← 현재 단계
   ↓
8. Call.EC (Call 종료)
```

#### PE 사용 목적

**동기화 포인트**:
```
// PE는 API 처리 완료 확인점
// 다음 동작 진행 가능 신호

if (Call.PE) {
    // API 처리 완료
    // 다음 Call 시작 가능
    // Work 종료 조건 확인
}
```

**Call 종료 조건**:
```
// Call.EC 조건에 PE 포함
Call.EC = Call.SC && Call.PE && actionIn && !Error
                      ↑
                 PE 확인 필수
```

#### 구현 코드 분석

```fsharp
member v.C2_CallPlanEnd() =
    let call = v.Vertex.GetPureCall()
    
    // Job이면 모든 TaskDef 종료 대기
    let sets = 
        if call.IsJob then
            call.TargetJob.TaskDefs
                .Select(fun d -> d.ApiItem.ApiItemEnd)
                .ToAnd()  // 모든 조건 AND
        else
            v.PS.Expr  // Normal은 PS 그대로
    
    let rsts = v._off.Expr  // 리셋 없음
    
    (sets, rsts) --| (v.PE, getFuncName())
```

#### 적용 예시

**Job Call 예시**:
```
픽앤플레이스 Job:
1. TaskDef1: 그리퍼 열기 → ApiItemEnd1
2. TaskDef2: Z축 하강 → ApiItemEnd2  
3. TaskDef3: 그리퍼 닫기 → ApiItemEnd3
→ PE = End1 && End2 && End3
```

**Normal Call 예시**:
```
단순 이동 Call:
PS(이동시작) → PE(즉시) → 실제이동 → actionIn → EC
```

**병렬 Job 처리**:
```
병렬 검사 Job:
├── 비전검사 → ApiItemEnd1 ─┐
├── 중량검사 → ApiItemEnd2 ─┼─AND→ PE
└── 치수검사 → ApiItemEnd3 ─┘
```

#### 주의사항

- **리셋 없음**: PE는 OFF로 리셋 (자동 해제 없음)
- **Job 동기화**: 모든 TaskDef 완료 필수
- **Normal 즉시성**: Normal Call은 PS=PE
- **종료 조건**: PE는 Call.EC의 필수 조건
- **상태 유지**: Call 종료까지 PE 상태 유지
---

## 7. 구현 매핑 & 테스트 구조

### 7.1 코드 레이어 매핑
| 레이어 | 핵심 모듈 경로 | 역할 요약 |
| --- | --- | --- |
| **Core** | `src/Ev2.Cpu.Core/Core/*.fs`, `src/Ev2.Cpu.Core/Ast/*.fs`, `src/Ev2.Cpu.Core/Parsing/Parser.fs`, `src/Ev2.Cpu.Core/Struct/*.fs` | 타입 시스템(`DataType`), AST(`DsExpr`, `DsStmt`), 연산자/파서 정의 등 런타임 전 계층이 공유하는 도메인 모델. |
| **CodeGen** | `src/Ev2.Cpu.CodeGen/CodeGen/*.fs` | System/Flow/Work/Call 패턴을 IEC‑61131 Structured Text로 생성. `CodeWork.fs`, `CodeFlow.fs`, `CodeSystem.fs`가 본 문서의 릴레이 사양을 F# DSL로 구현. |
| **Runtime** | `src/Ev2.Cpu.Runtime/Runtime/*.fs`, `src/Ev2.Cpu.Runtime/CpuScan.fs`, `src/Ev2.Cpu.Runtime/CpuTest.fs` | `Context.create`, `Memory`, `ExprEvaluator`, `StmtEvaluator`가 사양을 실제 스캔 루프와 타이머/카운터 동작에 매핑. `CpuTest.fs`는 문서 예시 시나리오(컨베이어/트래픽 라이트 등)를 실행 가능한 스크립트로 제공. |

### 7.2 테스트 프로젝트 구성
| 테스트 프로젝트 | 주요 커버리지 | 비고 |
| --- | --- | --- |
| `src/Ev2.Cpu.Core.Tests` | Core 타입/연산, AST DSL, 문서의 논리/비교/상태 빌더 검증 | 121개 테스트. `Expression.Test.fs`, `Statement.Test.fs`가 SR/TON 패턴을 단위 검증. |
| `src/Ev2.Cpu.CodeGen.Tests` | Work/Flow/System DSL과 CodeGen 패턴, 릴레이 변환 | 33개 테스트. `Relay.Test.fs`, `WorkFlow.Test.fs`가 본 사양의 릴레이/병렬 패턴을 확인. |
| `src/Ev2.Cpu.Runtime.Tests` | 실행 컨텍스트, 스캔 루프, 런타임 기본 시나리오 | 12개 테스트. `Runtime.Execution.Test.fs`는 `Context.create` + `StmtEvaluator.exec`를 통해 실제 메모리 동작을 검증. |

모든 테스트는 `~/.dotnet/dotnet test src/dsev2cpu.sln` 으로 일괄 실행되며, 위 사양의 규칙이 리그레션 없이 유지되는지 검증합니다.

### 7.3 구현 시 주의 사항
- **SR 래치 & TON 규칙**은 `CodeCommon` DSL과 `Runtime/StmtEvaluator.fs` 에서 동일한 조건으로 유지해야 합니다. 수정 시 Core DSL → CodeGen → Runtime → Tests 를 순차적으로 업데이트하십시오.
- **타이머/카운터**는 `Runtime/Context.fs` 내부 `updateTimerOn/Off`, `updateCounter*` 함수가 단일 근거입니다. 문서의 `Work.TimeoutTimer` 로직을 변경할 때는 이 함수들과 `Runtime.Execution.Test.fs`를 함께 수정하십시오.
- **워크플로 병합/분기**는 `CodeFlow.fs` 및 대응 테스트(`WorkFlow.Test.fs`)를 통해 검증되므로, 사양과 구현의 괴리를 줄이기 위해 문서 ↔ 코드 ↔ 테스트를 항상 동일한 변경 단위로 관리합니다.
