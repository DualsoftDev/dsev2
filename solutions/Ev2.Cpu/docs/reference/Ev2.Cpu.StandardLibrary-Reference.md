# Ev2.Cpu.StandardLibrary - 완전 참조 가이드

## 개요

Ev2.Cpu.StandardLibrary는 IEC 61131-3 표준을 기반으로 한 산업용 PLC Function Block 및 Function 라이브러리입니다.

**버전**: 1.0.0
**표준 준수**: IEC 61131-3
**총 FB/FC 수**: 22개

### 라이브러리 구성

| 카테고리 | FB/FC 수 | 설명 |
|---------|---------|------|
| Edge Detection | 2 | 신호의 상승/하강 에지 감지 |
| Bistable | 2 | Set-Reset 래치 |
| Timers | 4 | 지연, 펄스, 누적 타이머 |
| Counters | 3 | Up/Down/Bidirectional 카운터 |
| Analog Processing | 3 | 스케일링, 제한, 히스테리시스 |
| Math Functions | 3 | 평균, 최소/최대값 계산 |
| String Manipulation | 5 | 문자열 연결, 추출, 검색 |

---

## 빠른 인덱스

| 분류 | 심볼 | 주요 입출력 | 사용 시나리오 |
|------|------|-------------|---------------|
| Edge Detection | `R_TRIG`, `F_TRIG` | `CLK` ↦ `Q` | 스위치/센서 에지 감지 |
| Bistable | `SR`, `RS` | Set/Reset ↦ Latch | 유지 회로, 토글 제어 |
| Timers | `TON`, `TOF`, `TP`, `TONR` | `IN/PT` ↦ `Q/ET` | 지연, 펄스, 누적 시간 |
| Counters | `CTU`, `CTD`, `CTUD` | Edge ↦ Count | 공정 카운트, 누적량 |
| Analog | `SCALE`, `LIMIT`, `HYSTERESIS` | 값 ↦ 변환 | 센서 스케일링, 히스테리시스 |
| Math | `AVERAGE`, `MIN`, `MAX` | 값 집합 ↦ 결과 | 통계 계산 |
| String | `CONCAT`, `LEFT`, `RIGHT`, `MID`, `FIND` | 문자열 ↦ 변환 | HMI/로그 등 문자열 처리 |

> ⚡ 팁: 빠르게 시그니처만 확인하려면 위 표에서 심볼을 찾고, 자세한 설명은 아래 섹션으로 이동하세요.

---

## 1. Edge Detection (에지 감지)

### 1.1 R_TRIG - Rising Edge Trigger

**타입**: Function Block (FB)
**용도**: 입력 신호의 상승 에지(FALSE → TRUE) 감지

#### 시그니처
```
FB R_TRIG
  VAR_INPUT
    CLK : BOOL      // 입력 신호
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // 상승 에지 감지 시 1 스캔만 TRUE
  END_VAR
END_FB
```

#### 동작
- CLK가 FALSE에서 TRUE로 변할 때, Q가 정확히 1 스캔 동안 TRUE
- 다음 스캔부터 Q는 다시 FALSE
- CLK가 계속 TRUE여도 Q는 FALSE 유지

#### 사용 예제
```fsharp
// F# 코드 생성 예제
let trigger = R_TRIG.create() |> Result.get

// ST 코드 사용 예제
// PROGRAM Main
//   VAR
//     button : BOOL;
//     trigger : R_TRIG;
//     counter : INT := 0;
//   END_VAR
//
//   trigger(CLK := button);
//   IF trigger.Q THEN
//     counter := counter + 1;  // 버튼 눌릴 때마다 카운터 증가
//   END_IF
// END_PROGRAM
```

#### 타이밍 다이어그램
```
CLK:  ┐     ┌─────┐   ┌──
      └─────┘     └───┘
Q:    ┐ ┌─┐       ┌─┐
      └─┘ └───────┘ └───
      (1스캔만)
```

---

### 1.2 F_TRIG - Falling Edge Trigger

**타입**: Function Block (FB)
**용도**: 입력 신호의 하강 에지(TRUE → FALSE) 감지

#### 시그니처
```
FB F_TRIG
  VAR_INPUT
    CLK : BOOL      // 입력 신호
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // 하강 에지 감지 시 1 스캔만 TRUE
  END_VAR
END_FB
```

#### 동작
- CLK가 TRUE에서 FALSE로 변할 때, Q가 1 스캔 동안 TRUE
- R_TRIG와 반대 방향 감지

#### 사용 예제
```fsharp
let trigger = F_TRIG.create() |> Result.get
```

---

## 2. Bistable (쌍안정 래치)

### 2.1 SR - Set-Reset Bistable (Set Priority)

**타입**: Function Block (FB)
**용도**: Set 우선순위를 가지는 양방향 래치

#### 시그니처
```
FB SR
  VAR_INPUT
    S1 : BOOL       // Set input (우선순위 높음)
    R : BOOL        // Reset input
  END_VAR
  VAR_OUTPUT
    Q1 : BOOL       // Output
  END_VAR
END_FB
```

#### 진리표
| S1 | R | Q1 | 설명 |
|----|---|----|------|
| 0  | 0 | Q1 | 이전 상태 유지 |
| 0  | 1 | 0  | Reset |
| 1  | 0 | 1  | Set |
| 1  | 1 | 1  | **Set 우선** (SR의 특징) |

#### 동작 로직
```
Q1 := IF S1 THEN TRUE ELSIF R THEN FALSE ELSE Q1
```

#### 사용 예제
```fsharp
let latch = SR.create() |> Result.get

// 사용 사례: 모터 제어
// START 버튼(S1) → 모터 ON
// STOP 버튼(R) → 모터 OFF
// 동시 입력 시 START 우선
```

---

### 2.2 RS - Reset-Set Bistable (Reset Priority)

**타입**: Function Block (FB)
**용도**: Reset 우선순위를 가지는 양방향 래치

#### 시그니처
```
FB RS
  VAR_INPUT
    S : BOOL        // Set input
    R1 : BOOL       // Reset input (우선순위 높음)
  END_VAR
  VAR_OUTPUT
    Q1 : BOOL       // Output
  END_VAR
END_FB
```

#### 진리표
| S | R1 | Q1 | 설명 |
|---|----|----|------|
| 0 | 0  | Q1 | 이전 상태 유지 |
| 0 | 1  | 0  | Reset |
| 1 | 0  | 1  | Set |
| 1 | 1  | 0  | **Reset 우선** (RS의 특징) |

#### 동작 로직
```
Q1 := IF R1 THEN FALSE ELSIF S THEN TRUE ELSE Q1
```

#### 사용 예제
```fsharp
let latch = RS.create() |> Result.get

// 사용 사례: 비상 정지가 있는 시스템
// START 버튼(S) → 시작
// E-STOP 버튼(R1) → 정지 (우선순위)
// 비상 상황에서 확실한 정지 보장
```

---

## 3. Timers (타이머)

### 3.1 TON - On-Delay Timer

**타입**: Function Block (FB)
**용도**: 입력 신호가 TRUE가 된 후 지정 시간 경과 시 출력 ON

#### 시그니처
```
FB TON
  VAR_INPUT
    IN : BOOL       // Enable input
    PT : INT        // Preset Time (ms)
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output (ET >= PT일 때 TRUE)
    ET : INT        // Elapsed Time (ms)
  END_VAR
END_FB
```

#### 동작
1. IN = FALSE → ET = 0, Q = FALSE (즉시 리셋)
2. IN = TRUE → ET 증가 시작
3. ET >= PT → Q = TRUE
4. ET는 PT로 제한됨

#### 타이밍 다이어그램
```
IN:   ┌───────────┐         ┌────
      └───────────┘         └────
ET:   ┌────────┐            ┌────
     0└───PT───┘0          0└────
Q:          ┌───┐               ┌─
      ──────┘   └───────────────┘
           (PT 경과 후)
```

#### 사용 예제
```fsharp
let timer = TON.create() |> Result.get

// 사용 사례: 모터 시동 지연
// IN = START 버튼
// PT = 3000 (3초)
// Q = 모터 릴레이 (3초 후 ON)
```

---

### 3.2 TOF - Off-Delay Timer

**타입**: Function Block (FB)
**용도**: 입력 신호가 FALSE가 된 후 지정 시간 경과 시 출력 OFF

#### 시그니처
```
FB TOF
  VAR_INPUT
    IN : BOOL       // Input signal
    PT : INT        // Preset Time (ms)
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output
    ET : INT        // Elapsed Time (ms)
  END_VAR
END_FB
```

#### 동작
1. IN = TRUE → Q = TRUE (즉시), ET = 0
2. IN = FALSE → 타이머 시작, Q는 여전히 TRUE
3. ET >= PT → Q = FALSE
4. TON과 반대 동작

#### 타이밍 다이어그램
```
IN:   ┌───────┐             ┌────
      └───────┘             └────
Q:    ┌───────────┐         ┌────
      └───────────┘         └────
ET:           ┌────┐            ┌─
         ─────└PT──┘0───────────└
              (지연 OFF)
```

#### 사용 예제
```fsharp
let timer = TOF.create() |> Result.get

// 사용 사례: 환풍기 지연 정지
// IN = 작업 중 신호
// PT = 60000 (60초)
// 작업 종료 후 1분간 환풍 유지
```

---

### 3.3 TP - Pulse Timer

**타입**: Function Block (FB)
**용도**: 입력 상승 에지에서 지정 시간 동안 펄스 생성

#### 시그니처
```
FB TP
  VAR_INPUT
    IN : BOOL       // Input signal
    PT : INT        // Preset Time (ms)
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output pulse
    ET : INT        // Elapsed Time (ms)
  END_VAR
END_FB
```

#### 동작
1. IN 상승 에지 감지 → Q = TRUE, 타이머 시작
2. ET < PT → Q = TRUE 유지
3. ET >= PT → Q = FALSE
4. 펄스 진행 중 IN 변화는 무시 (펄스 완료까지 지속)

#### 타이밍 다이어그램
```
IN:   ┌───┐ ┌─┐           ┌────
      └───┘ └─┘           └────
Q:    ┌─────┐             ┌─────┐
      └─────┘             └─────┘
      (PT 동안)          (PT 동안)
```

#### 사용 예제
```fsharp
let timer = TP.create() |> Result.get

// 사용 사례: 버튼 누름으로 일정 시간 램프 점등
// IN = 버튼
// PT = 5000 (5초)
// Q = 램프 (버튼 누르면 5초간 점등)
```

---

### 3.4 TONR - Retentive On-Delay Timer

**타입**: Function Block (FB)
**용도**: 누적 시간 측정 (IN이 FALSE여도 시간 유지)

#### 시그니처
```
FB TONR
  VAR_INPUT
    IN : BOOL       // Enable input
    R : BOOL        // Reset input
    PT : INT        // Preset Time (ms)
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output (ET >= PT)
    ET : INT        // Elapsed Time (ms, 누적)
  END_VAR
END_FB
```

#### 동작
1. IN = TRUE → ET 증가
2. IN = FALSE → **ET 유지** (리셋하지 않음, TON과의 차이)
3. ET >= PT → Q = TRUE
4. R = TRUE → ET = 0, Q = FALSE (명시적 리셋 필요)

#### 타이밍 다이어그램
```
IN:   ┌──┐  ┌──┐  ┌──┐
      └──┘  └──┘  └──┘
ET:   ┌┐    ┌┐    ┌────
     0└┘   1└┘   2└────PT
Q:                 ┌────
      ─────────────┘
      (누적 시간)
```

#### 사용 예제
```fsharp
let timer = TONR.create() |> Result.get

// 사용 사례: 기계 누적 운전 시간 측정
// IN = 기계 가동 신호
// R = 리셋 버튼
// PT = 36000000 (10시간)
// 10시간 누적 운전 시 유지보수 알림
```

---

## 4. Counters (카운터)

### 4.1 CTU - Count Up Counter

**타입**: Function Block (FB)
**용도**: 상승 에지마다 증가하는 업 카운터

#### 시그니처
```
FB CTU
  VAR_INPUT
    CU : BOOL       // Count Up input
    R : BOOL        // Reset input
    PV : INT        // Preset Value
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output (CV >= PV)
    CV : INT        // Current Value
  END_VAR
END_FB
```

#### 동작
1. CU 상승 에지 → CV = CV + 1
2. CV >= PV → Q = TRUE
3. R = TRUE → CV = 0, Q = FALSE
4. CV는 PV를 초과하지 않음

#### 사용 예제
```fsharp
let counter = CTU.create() |> Result.get

// 사용 사례: 생산 제품 카운터
// CU = 센서 신호
// PV = 100 (목표 개수)
// Q = 배치 완료 신호
```

---

### 4.2 CTD - Count Down Counter

**타입**: Function Block (FB)
**용도**: 하강 카운터

#### 시그니처
```
FB CTD
  VAR_INPUT
    CD : BOOL       // Count Down input
    LD : BOOL       // Load input
    PV : INT        // Preset Value
  END_VAR
  VAR_OUTPUT
    Q : BOOL        // Output (CV <= 0)
    CV : INT        // Current Value
  END_VAR
END_FB
```

#### 동작
1. LD = TRUE → CV = PV (프리셋 로드)
2. CD 상승 에지 → CV = CV - 1
3. CV <= 0 → Q = TRUE
4. CV는 0 미만으로 감소하지 않음

#### 사용 예제
```fsharp
let counter = CTD.create() |> Result.get

// 사용 사례: 재고 카운트다운
// LD = 재고 보충 신호
// PV = 초기 재고
// CD = 출고 신호
// Q = 재고 부족 경고
```

---

### 4.3 CTUD - Count Up/Down Counter

**타입**: Function Block (FB)
**용도**: 양방향 카운터 (증가/감소)

#### 시그니처
```
FB CTUD
  VAR_INPUT
    CU : BOOL       // Count Up input
    CD : BOOL       // Count Down input
    R : BOOL        // Reset input
    LD : BOOL       // Load input
    PV : INT        // Preset Value
  END_VAR
  VAR_OUTPUT
    QU : BOOL       // Count Up done (CV >= PV)
    QD : BOOL       // Count Down done (CV <= 0)
    CV : INT        // Current Value
  END_VAR
END_FB
```

#### 동작
1. R = TRUE → CV = 0 (최우선)
2. LD = TRUE → CV = PV
3. CU 상승 에지 → CV = CV + 1
4. CD 상승 에지 → CV = CV - 1
5. CV >= PV → QU = TRUE
6. CV <= 0 → QD = TRUE

#### 우선순위
```
R > LD > CU > CD
```

#### 사용 예제
```fsharp
let counter = CTUD.create() |> Result.get

// 사용 사례: 주차장 차량 카운터
// CU = 입구 센서
// CD = 출구 센서
// PV = 최대 주차 대수
// QU = 만차 신호
// QD = 공차 신호
```

---

## 5. Analog Processing (아날로그 처리)

### 5.1 SCALE - Linear Scaling

**타입**: Function (FC)
**용도**: 입력값을 선형적으로 스케일링

#### 시그니처
```
FUNCTION SCALE : DOUBLE
  VAR_INPUT
    IN : DOUBLE         // 입력값
    IN_MIN : DOUBLE     // 입력 최소값
    IN_MAX : DOUBLE     // 입력 최대값
    OUT_MIN : DOUBLE    // 출력 최소값
    OUT_MAX : DOUBLE    // 출력 최대값
  END_VAR
END_FUNCTION
```

#### 수식
```
OUT = OUT_MIN + (IN - IN_MIN) × (OUT_MAX - OUT_MIN) / (IN_MAX - IN_MIN)
```

#### 사용 예제
```fsharp
let scaler = SCALE.create() |> Result.get

// 예: 4-20mA 센서를 0-100%로 변환
// IN = 센서값 (4.0 ~ 20.0 mA)
// IN_MIN = 4.0
// IN_MAX = 20.0
// OUT_MIN = 0.0
// OUT_MAX = 100.0
// 결과: 12mA → 50%
```

---

### 5.2 LIMIT - Value Limiting

**타입**: Function (FC)
**용도**: 값을 지정 범위로 제한

#### 시그니처
```
FUNCTION LIMIT : DOUBLE
  VAR_INPUT
    IN : DOUBLE     // 입력값
    MIN : DOUBLE    // 최소 제한
    MAX : DOUBLE    // 최대 제한
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IF IN < MIN THEN MIN
      ELSIF IN > MAX THEN MAX
      ELSE IN
```

#### 사용 예제
```fsharp
let limiter = LIMIT.create() |> Result.get

// 예: 밸브 개도 제한 (0-100%)
// IN = 제어 출력
// MIN = 0.0
// MAX = 100.0
```

---

### 5.3 HYSTERESIS - Hysteresis Control

**타입**: Function Block (FB)
**용도**: 히스테리시스 제어 (떨림 방지)

#### 시그니처
```
FB HYSTERESIS
  VAR_INPUT
    IN : DOUBLE     // 입력값
    HIGH : DOUBLE   // 상한 임계값 (ON)
    LOW : DOUBLE    // 하한 임계값 (OFF)
  END_VAR
  VAR_OUTPUT
    OUT : BOOL      // 출력 상태
  END_VAR
END_FB
```

#### 동작
```
IN > HIGH  → OUT = TRUE
IN < LOW   → OUT = FALSE
LOW ≤ IN ≤ HIGH → OUT 유지 (이전 상태)
```

#### 타이밍 다이어그램
```
IN:        HIGH ┄┄┄┄┄┄┄┄┄
           ┌─┐     ┌───┐
          ─┘ └─────┘   └──
           LOW ┄┄┄┄┄┄┄┄┄
OUT:         ┌─────┐
        ─────┘     └─────
         (히스테리시스 영역)
```

#### 사용 예제
```fsharp
let hyst = HYSTERESIS.create() |> Result.get

// 예: 온도 제어
// IN = 현재 온도
// HIGH = 25.0°C (냉방 ON)
// LOW = 23.0°C (냉방 OFF)
// 23~25°C 사이에서 상태 유지 → 떨림 방지
```

---

## 6. Math Functions (수학 함수)

### 6.1 AVERAGE - Average of Multiple Inputs

**타입**: Function (FC)
**용도**: 4개 입력값의 평균 계산

#### 시그니처
```
FUNCTION AVERAGE : DOUBLE
  VAR_INPUT
    IN1 : DOUBLE
    IN2 : DOUBLE
    IN3 : DOUBLE
    IN4 : DOUBLE
  END_VAR
END_FUNCTION
```

#### 수식
```
OUT = (IN1 + IN2 + IN3 + IN4) / 4.0
```

#### 사용 예제
```fsharp
let avg = AVERAGE.create() |> Result.get

// 예: 다중 센서 평균
// IN1~IN4 = 4개 온도 센서
// OUT = 평균 온도
```

---

### 6.2 MIN - Minimum Value Selection

**타입**: Function (FC)
**용도**: 4개 입력값 중 최소값 선택

#### 시그니처
```
FUNCTION MIN : DOUBLE
  VAR_INPUT
    IN1 : DOUBLE
    IN2 : DOUBLE
    IN3 : DOUBLE
    IN4 : DOUBLE
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = MIN(MIN(MIN(IN1, IN2), IN3), IN4)
```

#### 사용 예제
```fsharp
let minimum = MIN.create() |> Result.get

// 예: 다중 센서 최소값
// IN1~IN4 = 4개 압력 센서
// OUT = 최소 압력
```

---

### 6.3 MAX - Maximum Value Selection

**타입**: Function (FC)
**용도**: 4개 입력값 중 최대값 선택

#### 시그니처
```
FUNCTION MAX : DOUBLE
  VAR_INPUT
    IN1 : DOUBLE
    IN2 : DOUBLE
    IN3 : DOUBLE
    IN4 : DOUBLE
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = MAX(MAX(MAX(IN1, IN2), IN3), IN4)
```

#### 사용 예제
```fsharp
let maximum = MAX.create() |> Result.get

// 예: 피크 값 추적
// IN1~IN4 = 4개 진동 센서
// OUT = 최대 진동
```

---

## 7. String Manipulation (문자열 처리)

### 7.1 CONCAT - String Concatenation

**타입**: Function (FC)
**용도**: 4개 문자열 연결

#### 시그니처
```
FUNCTION CONCAT : STRING
  VAR_INPUT
    IN1 : STRING
    IN2 : STRING
    IN3 : STRING
    IN4 : STRING
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IN1 + IN2 + IN3 + IN4
```

#### 사용 예제
```fsharp
let concat = CONCAT.create() |> Result.get

// 예: 메시지 조합
// IN1 = "Error: "
// IN2 = "Sensor "
// IN3 = "01"
// IN4 = " fault"
// OUT = "Error: Sensor 01 fault"
```

---

### 7.2 LEFT - Extract Left Substring

**타입**: Function (FC)
**용도**: 문자열 왼쪽에서 지정 길이만큼 추출

#### 시그니처
```
FUNCTION LEFT : STRING
  VAR_INPUT
    IN : STRING     // 입력 문자열
    L : INT         // 추출 길이
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IN[0..L-1]
```

#### 사용 예제
```fsharp
let left = LEFT.create() |> Result.get

// 예: 제품 코드 추출
// IN = "ABC-12345"
// L = 3
// OUT = "ABC"
```

---

### 7.3 RIGHT - Extract Right Substring

**타입**: Function (FC)
**용도**: 문자열 오른쪽에서 지정 길이만큼 추출

#### 시그니처
```
FUNCTION RIGHT : STRING
  VAR_INPUT
    IN : STRING     // 입력 문자열
    L : INT         // 추출 길이
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IN[LEN(IN)-L .. LEN(IN)-1]
```

#### 사용 예제
```fsharp
let right = RIGHT.create() |> Result.get

// 예: 시리얼 번호 추출
// IN = "ABC-12345"
// L = 5
// OUT = "12345"
```

---

### 7.4 MID - Extract Middle Substring

**타입**: Function (FC)
**용도**: 문자열 중간에서 지정 위치와 길이만큼 추출

#### 시그니처
```
FUNCTION MID : STRING
  VAR_INPUT
    IN : STRING     // 입력 문자열
    L : INT         // 추출 길이
    P : INT         // 시작 위치 (0-based)
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IN[P .. P+L-1]
```

#### 사용 예제
```fsharp
let mid = MID.create() |> Result.get

// 예: 날짜 파싱
// IN = "2025-10-26"
// P = 5
// L = 2
// OUT = "10" (월)
```

---

### 7.5 FIND - Find Substring Position

**타입**: Function (FC)
**용도**: 문자열에서 부분 문자열 위치 찾기

#### 시그니처
```
FUNCTION FIND : INT
  VAR_INPUT
    IN1 : STRING    // 검색 대상 문자열
    IN2 : STRING    // 찾을 문자열
  END_VAR
END_FUNCTION
```

#### 동작
```
OUT = IndexOf(IN1, IN2)
// 찾으면: 위치 (0-based)
// 못 찾으면: -1
```

#### 사용 예제
```fsharp
let find = FIND.create() |> Result.get

// 예: 에러 코드 검색
// IN1 = "System OK, Sensor fault"
// IN2 = "fault"
// OUT = 17
```

---

## 8. 사용 방법

### 8.1 라이브러리 초기화

```fsharp
open Ev2.Cpu.StandardLibrary

// 방법 1: 전체 라이브러리 자동 등록
let (successCount, failureCount) = StandardLibraryRegistry.initialize()
printfn $"Registered: {successCount} FBs, Failed: {failureCount}"

// 방법 2: UserLibrary에 수동 등록
let library = UserLibrary()
let results = StandardLibraryRegistry.registerAllTo library
```

### 8.2 개별 FB 생성

```fsharp
// FB 생성
match TON.create() with
| Ok timer ->
    printfn "Timer created successfully"
| Error msg ->
    printfn $"Error: {msg}"

// FC 생성
match SCALE.create() with
| Ok scaler ->
    printfn "Scaler created successfully"
| Error msg ->
    printfn $"Error: {msg}"
```

### 8.3 Validation

```fsharp
match TON.create() with
| Ok fb ->
    match fb.Validate() with
    | Ok () -> printfn "Validation passed"
    | Error msg -> printfn $"Validation failed: {msg}"
| Error msg ->
    printfn $"Creation failed: {msg}"
```

### 8.4 통계 조회

```fsharp
let stats = StandardLibraryRegistry.getStatistics()
printfn $"Total FBs: {stats.TotalFBs}"
printfn $"Timers: {stats.Timers}"
printfn $"Counters: {stats.Counters}"
```

---

## 9. 제약사항 및 주의사항

### 9.1 Tag Registry 관리
- 각 FB/FC 생성 전 `DsTagRegistry.clear()` 호출 권장
- 동일한 파라미터 이름("IN", "OUT" 등)이 다른 타입으로 사용될 수 있음

### 9.2 시간 단위
- 모든 타이머는 밀리초(ms) 단위 사용
- 1초 = 1000ms

### 9.3 String Functions
- 위치 인덱스는 0-based
- 음수 인덱스 미지원

### 9.4 Math Functions
- 현재 4개 입력으로 제한
- 더 많은 입력이 필요하면 중첩 사용

---

## 10. 유지보수 & 확장 가이드

### 10.1 설계 원칙
- 모든 FB/FC는 IEC 61131-3 표준 시그니처·동작을 준수한다.
- 카테고리별 디렉토리로 모듈화하고 각 블록을 독립 파일로 관리한다.
- `FBBuilder`/`FCBuilder`, Expression/Statement 생성기를 재사용해 일관된 DSL을 유지한다.
- 각 구현은 `Validate()`를 제공하고 런타임 변환 및 단위 테스트를 통과해야 한다.

### 10.2 프로젝트 구조
- 소스는 `src/cpu/Ev2.Cpu.StandardLibrary` 하위에 카테고리(EdgeDetection, Timers 등)별로 배치된다.
- `StandardLibraryRegistry.fs` 가 모든 FB/FC를 생성·등록하는 단일 진입점이다.
- 테스트는 `src/UintTest/cpu/Ev2.Cpu.StandardLibrary.Tests` 에 모듈별로 분리되어 있다.

### 10.3 핵심 컴포넌트
- `StandardFunctionOrBlock`/`StandardFunctionBlock` 타입이 모든 표준 항목을 정의한다.
- `createWithClearRegistry`, `createAllStandardFBs`, `registerAllTo`, `initialize`, `getStatistics`, `validateAll` 이 레지스트리 관리의 핵심 함수다.
- 각 카테고리 폴더의 FB/FC는 `builder.AddInput/Output`, `assignAuto`, `Function("IF", ...)` 패턴을 사용한다.

### 10.4 새로운 FB/FC 추가 절차
1. 적절한 카테고리에 새 파일을 생성하고 `FBBuilder` 또는 `FCBuilder` 로 시그니처와 메타데이터를 정의한다.
2. Expression/Statement 생성기를 활용해 로직을 구성하고 `builder.SetDescription` 을 작성한다.
3. `StandardFunctionBlock` 케이스와 `StandardLibraryRegistry.createStandardFB`/`createWithClearRegistry` 를 업데이트한다.
4. 해당 테스트 모듈에 새 시나리오를 작성하고 `validateAll()` 이 성공하는지 확인한다.

### 10.5 테스트 전략
- 카테고리별 테스트 모듈을 유지하며 정상 동작, 경계 조건, 실패 케이스를 모두 포함한다.
- 레지스트리 테스트에서 `DsTagRegistry.clear()` 를 호출해 태그 충돌이 없도록 한다.
- 필요 시 장치 시뮬레이터나 Golden Value 비교를 통해 회귀 테스트를 강화한다.

### 10.6 Validation & Tag Registry
- `Validate()` 결과를 누락 없이 확인하고 실패 시 상세 메시지를 기록한다.
- Tag Registry는 FB/FC 생성 전 초기화하고, 충돌 진단은 로그로 남긴다.
- Validation 오류는 Runtime 통합 전에 모두 해결해야 한다.

### 10.7 문제 해결 & 베스트 프랙티스
- 스코프 충돌, 누락된 입력, 타입 불일치(Error) 발생 시 `Validate()` 및 테스트 로그를 참고한다.
- 코드 변경 후 문서·테스트를 동기화하고, 공통 헬퍼를 적극 재사용하여 중복을 줄인다.
- 새 FB/FC 작성 시 반드시 문서화하고 Registry 통계에 반영한다.

---

## 11. 버전 히스토리

### Version 1.0.0 (2025-10-26)
- 초기 릴리즈
- 22개 표준 FB/FC 구현
- IEC 61131-3 표준 준수
- 완전한 테스트 커버리지 (36 테스트)

---

## 12. 참고 자료

- IEC 61131-3 표준 문서
- [Ev2.Cpu.Generation 사용자 매뉴얼](./Ev2.Cpu.Generation-사용자매뉴얼.md)
- [UserFB/FC 빠른 시작](../guides/manuals/generation/Ev2.Cpu.Generation-사용자매뉴얼.md#빠른-시작)
- [이 문서의 빠른 인덱스](#빠른-인덱스)
- [유지보수 & 확장 가이드](#10-유지보수--확장-가이드)
