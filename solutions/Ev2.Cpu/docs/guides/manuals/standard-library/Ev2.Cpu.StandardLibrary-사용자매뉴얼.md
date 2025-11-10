# Ev2.Cpu.StandardLibrary 사용자 매뉴얼

## 개요

`Ev2.Cpu.StandardLibrary`는 IEC 61131-3 표준 함수 블록(FB)과 함수(FC)를 제공하는 라이브러리입니다. 타이머, 카운터, 에지 검출, 바이스테이블 래치, 수학 함수, 문자열 처리, 아날로그 신호 처리 등 PLC 프로그래밍에 필수적인 표준 함수를 포함합니다.

---

## 주요 기능

### 1. 타이머 (Timers)

IEC 61131-3 표준 타이머 함수 블록을 제공합니다.

#### TON - On-Delay Timer

설정 시간 후에 출력이 켜지는 타이머입니다.

**파라미터**
- **입력 (IN)**
  - `IN` (Bool): 타이머 활성화
  - `PT` (Int): 프리셋 시간 (ms)

- **출력 (OUT)**
  - `Q` (Bool): 타이머 완료 신호
  - `ET` (Double): 경과 시간 (ms)

- **내부 상태 (Static)**
  - `startTime` (Double): 시작 시각
  - `lastIN` (Bool): 이전 입력 상태

**동작**
```
IN ────┐          ┌──────
       └──────────┘

Q  ─────────┐     ┌──────
            └─────┘
            ◄─PT─►
```

**사용 예제**

```fsharp
open Ev2.Cpu.StandardLibrary

// FB 생성
let tonFB = Timers.TON()

// 인스턴스 생성
let timerInst = FBInstance.create tonFB "delayTimer"

// UserLibrary에 등록
let library = UserLibrary.create()
library.RegisterFB(tonFB) |> ignore
library.RegisterInstance(timerInst) |> ignore

// 프로그램에서 사용
// delayTimer.IN := start_button
// delayTimer.PT := 3000  // 3초
// delayTimer()
// motor := delayTimer.Q
```

#### TOF - Off-Delay Timer

입력이 꺼진 후 설정 시간 후에 출력이 꺼지는 타이머입니다.

**동작**
```
IN ────┐          ┌──────
       └──────────┘

Q  ────┘          └──────
       ◄─PT─►
```

**파라미터**: TON과 동일

#### TP - Pulse Timer

입력이 켜지면 설정 시간 동안 펄스를 출력합니다.

**동작**
```
IN ────┐     ┌────────┐
       └─────┘        └──

Q  ────┐     ┌────────┐
       └─────┘        └──
       ◄─PT─►         ◄─PT─►
```

**파라미터**: TON과 동일

#### TONR - Retentive On-Delay Timer

리셋하기 전까지 시간을 누적하는 타이머입니다.

**파라미터**
- **입력 (IN)**
  - `IN` (Bool): 타이머 활성화
  - `R` (Bool): 리셋 신호
  - `PT` (Int): 프리셋 시간 (ms)

- **출력 (OUT)**
  - `Q` (Bool): 타이머 완료 신호
  - `ET` (Double): 누적 경과 시간 (ms)

- **내부 상태 (Static)**
  - `accumulated` (Double): 누적 시간
  - `startTime` (Double): 시작 시각
  - `lastIN` (Bool): 이전 입력 상태

**사용 예제**

```fsharp
// 누적 운전 시간 측정
// tonrInst.IN := motor_running
// tonrInst.R := reset_button
// tonrInst.PT := 36000000  // 10시간 (ms)
// tonrInst()
// maintenance_due := tonrInst.Q  // 10시간 누적 시 정비 필요
```

---

### 2. 카운터 (Counters)

IEC 61131-3 표준 카운터 함수 블록입니다.

#### CTU - Up Counter

펄스를 카운트하여 설정값에 도달하면 출력합니다.

**파라미터**
- **입력 (IN)**
  - `CU` (Bool): 카운트 업 신호 (상승 에지)
  - `R` (Bool): 리셋 신호
  - `PV` (Int): 프리셋 값

- **출력 (OUT)**
  - `Q` (Bool): 카운트 >= PV
  - `CV` (Int): 현재 카운트 값

- **내부 상태 (Static)**
  - `count` (Int): 내부 카운터
  - `lastCU` (Bool): 이전 CU 상태

**사용 예제**

```fsharp
// 제품 카운팅
// ctuInst.CU := product_sensor
// ctuInst.R := reset_button
// ctuInst.PV := 100  // 100개
// ctuInst()
// batch_complete := ctuInst.Q
// current_count := ctuInst.CV
```

#### CTD - Down Counter

카운트를 감소하여 0에 도달하면 출력합니다.

**파라미터**
- **입력 (IN)**
  - `CD` (Bool): 카운트 다운 신호 (상승 에지)
  - `LD` (Bool): 로드 신호 (PV로 초기화)
  - `PV` (Int): 프리셋 값

- **출력 (OUT)**
  - `Q` (Bool): 카운트 <= 0
  - `CV` (Int): 현재 카운트 값

- **내부 상태 (Static)**
  - `count` (Int): 내부 카운터
  - `lastCD` (Bool): 이전 CD 상태
  - `lastLD` (Bool): 이전 LD 상태

#### CTUD - Up-Down Counter

업/다운 카운팅을 모두 지원하는 카운터입니다.

**파라미터**
- **입력 (IN)**
  - `CU` (Bool): 카운트 업
  - `CD` (Bool): 카운트 다운
  - `R` (Bool): 리셋
  - `LD` (Bool): 로드
  - `PV` (Int): 프리셋 값

- **출력 (OUT)**
  - `QU` (Bool): 카운트 >= PV
  - `QD` (Bool): 카운트 <= 0
  - `CV` (Int): 현재 카운트 값

- **내부 상태 (Static)**
  - `count` (Int): 내부 카운터
  - `lastCU`, `lastCD`, `lastLD` (Bool): 이전 상태

**사용 예제**

```fsharp
// 양방향 이동 카운터
// ctudInst.CU := forward_sensor
// ctudInst.CD := backward_sensor
// ctudInst.R := reset
// ctudInst.PV := 10
// ctudInst()
// at_max := ctudInst.QU
// at_min := ctudInst.QD
// position := ctudInst.CV
```

---

### 3. 에지 검출 (Edge Detection)

신호의 상승/하강 에지를 검출합니다.

#### R_TRIG - Rising Edge Trigger

입력 신호의 상승 에지(OFF → ON)를 검출합니다.

**파라미터**
- **입력 (IN)**
  - `CLK` (Bool): 입력 신호

- **출력 (OUT)**
  - `Q` (Bool): 상승 에지 검출 시 1 스캔 동안 TRUE

- **내부 상태 (Static)**
  - `lastCLK` (Bool): 이전 CLK 상태

**사용 예제**

```fsharp
// 버튼 클릭 검출
// rTrigInst.CLK := button
// rTrigInst()
// on_button_click := rTrigInst.Q  // 1 스캔 동안만 TRUE
```

#### F_TRIG - Falling Edge Trigger

입력 신호의 하강 에지(ON → OFF)를 검출합니다.

**파라미터**: R_TRIG와 동일

**사용 예제**

```fsharp
// 센서 이탈 검출
// fTrigInst.CLK := sensor
// fTrigInst()
// on_sensor_leave := fTrigInst.Q
```

---

### 4. 바이스테이블 (Bistable)

Set/Reset 래치 함수 블록입니다.

#### SR - Set (Dominant) Bistable

Set 신호가 우선인 래치입니다.

**파라미터**
- **입력 (IN)**
  - `S1` (Bool): Set 신호
  - `R` (Bool): Reset 신호

- **출력 (OUT)**
  - `Q1` (Bool): 출력 상태

**동작**: S1과 R이 동시에 TRUE이면 Set 우선 (Q1 = TRUE)

**사용 예제**

```fsharp
// 비상 정지 래치
// srInst.S1 := emergency_button
// srInst.R := reset_button
// srInst()
// emergency_stop := srInst.Q1
```

#### RS - Reset (Dominant) Bistable

Reset 신호가 우선인 래치입니다.

**파라미터**
- **입력 (IN)**
  - `S` (Bool): Set 신호
  - `R1` (Bool): Reset 신호 (우선)

- **출력 (OUT)**
  - `Q1` (Bool): 출력 상태

**동작**: S와 R1이 동시에 TRUE이면 Reset 우선 (Q1 = FALSE)

---

### 5. 수학 함수 (Math)

수학 계산 함수들을 제공합니다.

#### MAX - Maximum

여러 값 중 최댓값을 반환합니다.

**파라미터**
- **입력 (IN)**
  - `values` (가변 인자): 비교할 값들

- **출력 (OUT)**
  - `result` (Double): 최댓값

**사용 예제**

```fsharp
// result := MAX(sensor1, sensor2, sensor3)
```

#### MIN - Minimum

여러 값 중 최솟값을 반환합니다.

**파라미터**: MAX와 동일

#### AVERAGE - Average

여러 값의 평균을 계산합니다.

**파라미터**
- **입력 (IN)**
  - `values` (가변 인자): 값들

- **출력 (OUT)**
  - `result` (Double): 평균값

**사용 예제**

```fsharp
// avg_temp := AVERAGE(temp1, temp2, temp3, temp4)
```

---

### 6. 문자열 함수 (String)

문자열 처리 함수들을 제공합니다.

#### CONCAT - Concatenate

문자열들을 연결합니다.

**파라미터**
- **입력 (IN)**
  - `str1`, `str2`, ... (가변 인자): 연결할 문자열들

- **출력 (OUT)**
  - `result` (String): 연결된 문자열

**사용 예제**

```fsharp
// message := CONCAT("Temperature: ", TO_STRING(temp), " °C")
```

#### LEFT - Left Substring

문자열의 왼쪽에서 n개 문자를 추출합니다.

**파라미터**
- **입력 (IN)**
  - `str` (String): 원본 문자열
  - `length` (Int): 추출할 문자 개수

- **출력 (OUT)**
  - `result` (String): 추출된 문자열

**사용 예제**

```fsharp
// prefix := LEFT("ABCDEF", 3)  // "ABC"
```

#### RIGHT - Right Substring

문자열의 오른쪽에서 n개 문자를 추출합니다.

**파라미터**: LEFT와 동일

#### MID - Middle Substring

문자열의 중간에서 부분 문자열을 추출합니다.

**파라미터**
- **입력 (IN)**
  - `str` (String): 원본 문자열
  - `start` (Int): 시작 위치 (0부터)
  - `length` (Int): 추출할 길이

- **출력 (OUT)**
  - `result` (String): 추출된 문자열

**사용 예제**

```fsharp
// mid := MID("ABCDEFGH", 2, 4)  // "CDEF"
```

#### FIND - Find Substring

부분 문자열의 위치를 찾습니다.

**파라미터**
- **입력 (IN)**
  - `str` (String): 검색 대상 문자열
  - `pattern` (String): 찾을 문자열

- **출력 (OUT)**
  - `result` (Int): 위치 (0부터, 없으면 -1)

**사용 예제**

```fsharp
// pos := FIND("Hello World", "World")  // 6
```

---

### 7. 아날로그 신호 처리 (Analog)

아날로그 신호를 처리하는 함수 블록입니다.

#### HYSTERESIS - Hysteresis

히스테리시스를 가진 비교기입니다.

**파라미터**
- **입력 (IN)**
  - `input` (Double): 입력 신호
  - `high` (Double): 상한 임계값
  - `low` (Double): 하한 임계값

- **출력 (OUT)**
  - `Q` (Bool): 출력 상태

- **내부 상태 (Static)**
  - `state` (Bool): 현재 상태

**동작**
```
Input
  ↑
high ─────────────────────
        ↑ ON      ↑ ON
        ├─────────┤
low  ────┼─────────┼──────
          ↓ OFF     ↓ OFF

Q     ───┐       ┌─┐     ┌
         └───────┘ └─────┘
```

**사용 예제**

```fsharp
// 온도 제어 (히스테리시스로 떨림 방지)
// hystInst.input := temperature
// hystInst.high := 25.0  // 상한 25도
// hystInst.low := 23.0   // 하한 23도
// hystInst()
// cooling_on := hystInst.Q
```

---

## 표준 라이브러리 레지스트리

모든 표준 함수를 한 번에 등록할 수 있습니다.

### StandardLibraryRegistry

```fsharp
open Ev2.Cpu.StandardLibrary

// UserLibrary 생성
let library = UserLibrary.create()

// 표준 라이브러리 전체 등록
StandardLibraryRegistry.registerAll library

// 개별 등록
StandardLibraryRegistry.registerTimers library
StandardLibraryRegistry.registerCounters library
StandardLibraryRegistry.registerEdgeDetection library
StandardLibraryRegistry.registerBistable library
StandardLibraryRegistry.registerMath library
StandardLibraryRegistry.registerString library
StandardLibraryRegistry.registerAnalog library

// 통계 조회
let stats = StandardLibraryRegistry.getStatistics library
printfn "Total FBs: %d" stats.TotalFBCount
printfn "Total FCs: %d" stats.TotalFCCount

// 카테고리별 통계
printfn "Timers: %d" stats.TimerCount
printfn "Counters: %d" stats.CounterCount
printfn "Edge Detection: %d" stats.EdgeDetectionCount
```

---

## 사용 예제

### 예제 1: 타이머 기반 시퀀스 제어

```fsharp
open Ev2.Cpu.StandardLibrary
open Ev2.Cpu.Core

// 1. 라이브러리 생성 및 등록
let library = UserLibrary.create()
StandardLibraryRegistry.registerAll library

// 2. 타이머 인스턴스 생성
let tonFB = Timers.TON()
let timer1Inst = FBInstance.create tonFB "timer1"
let timer2Inst = FBInstance.create tonFB "timer2"
let timer3Inst = FBInstance.create tonFB "timer3"

library.RegisterFB(tonFB) |> ignore
library.RegisterInstance(timer1Inst) |> ignore
library.RegisterInstance(timer2Inst) |> ignore
library.RegisterInstance(timer3Inst) |> ignore

// 3. 프로그램 생성
let program = {
    Name = "SequenceControl"
    Body = [
        // Step 1: 밸브 열기 (2초 후)
        // timer1.IN := start
        // timer1.PT := 2000
        // timer1()
        // valve_open := timer1.Q

        // Step 2: 펌프 작동 (5초 후)
        // timer2.IN := timer1.Q
        // timer2.PT := 5000
        // timer2()
        // pump_on := timer2.Q

        // Step 3: 밸브 닫기 (3초 후)
        // timer3.IN := timer2.Q
        // timer3.PT := 3000
        // timer3()
        // valve_close := timer3.Q
    ]
    Description = Some "Timer-based sequence control"
}
```

### 예제 2: 카운터 기반 배치 제어

```fsharp
// 1. 카운터 인스턴스
let ctuFB = Counters.CTU()
let productCounter = FBInstance.create ctuFB "productCounter"

library.RegisterFB(ctuFB) |> ignore
library.RegisterInstance(productCounter) |> ignore

// 2. 에지 검출 (센서 노이즈 제거)
let rTrigFB = EdgeDetection.R_TRIG()
let sensorEdge = FBInstance.create rTrigFB "sensorEdge"

library.RegisterFB(rTrigFB) |> ignore
library.RegisterInstance(sensorEdge) |> ignore

// 3. 프로그램
let batchProgram = {
    Name = "BatchControl"
    Body = [
        // 에지 검출
        // sensorEdge.CLK := product_sensor
        // sensorEdge()

        // 카운팅
        // productCounter.CU := sensorEdge.Q
        // productCounter.R := reset_button
        // productCounter.PV := 100
        // productCounter()

        // 배치 완료 확인
        // batch_complete := productCounter.Q
        // current_count := productCounter.CV

        // 배치 완료 시 자동 리셋
        // IF productCounter.Q THEN
        //     reset_button := TRUE
        // END_IF
    ]
    Description = Some "Batch counter control"
}
```

### 예제 3: 온도 제어 시스템

```fsharp
// 1. 히스테리시스 인스턴스
let hystFB = Analog.HYSTERESIS()
let tempControl = FBInstance.create hystFB "tempControl"

library.RegisterFB(hystFB) |> ignore
library.RegisterInstance(tempControl) |> ignore

// 2. 타이머 (냉각 지연)
let tofFB = Timers.TOF()
let coolingDelay = FBInstance.create tofFB "coolingDelay"

library.RegisterFB(tofFB) |> ignore
library.RegisterInstance(coolingDelay) |> ignore

// 3. 프로그램
let tempProgram = {
    Name = "TemperatureControl"
    Body = [
        // 온도 히스테리시스 제어
        // tempControl.input := current_temperature
        // tempControl.high := 25.0
        // tempControl.low := 23.0
        // tempControl()

        // 냉각 지연 (즉시 ON/OFF 방지)
        // coolingDelay.IN := tempControl.Q
        // coolingDelay.PT := 2000  // 2초 지연
        // coolingDelay()

        // 냉각 팬 제어
        // cooling_fan := coolingDelay.Q
    ]
    Description = Some "Temperature control with hysteresis"
}
```

### 예제 4: 비상 정지 시스템

```fsharp
// 1. SR 래치 (Set 우선)
let srFB = Bistable.SR()
let emergencyLatch = FBInstance.create srFB "emergencyLatch"

library.RegisterFB(srFB) |> ignore
library.RegisterInstance(emergencyLatch) |> ignore

// 2. 펄스 타이머 (리셋 확인)
let tpFB = Timers.TP()
let resetConfirm = FBInstance.create tpFB "resetConfirm"

library.RegisterFB(tpFB) |> ignore
library.RegisterInstance(resetConfirm) |> ignore

// 3. 프로그램
let safetyProgram = {
    Name = "EmergencyStopSystem"
    Body = [
        // 비상 정지 래치
        // emergencyLatch.S1 := emergency_button OR safety_fault
        // emergencyLatch.R := reset_button AND safety_ok
        // emergencyLatch()

        // 리셋 확인 펄스
        // resetConfirm.IN := emergencyLatch.R
        // resetConfirm.PT := 3000  // 3초 확인
        // resetConfirm()

        // 시스템 정지
        // system_stopped := emergencyLatch.Q1
        // reset_confirmed := resetConfirm.Q
    ]
    Description = Some "Emergency stop system with reset confirmation"
}
```

---

## 통합 예제: 자동 생산 라인

전체 표준 라이브러리를 활용한 자동 생산 라인 예제입니다.

```fsharp
open Ev2.Cpu.StandardLibrary
open Ev2.Cpu.Runtime

// 1. 라이브러리 설정
let library = UserLibrary.create()
StandardLibraryRegistry.registerAll library

// 2. FB 인스턴스 생성
let instances = [
    // 타이머
    ("conveyor_delay", Timers.TON())
    ("quality_check_timer", Timers.TON())
    ("reject_pulse", Timers.TP())

    // 카운터
    ("product_counter", Counters.CTU())
    ("reject_counter", Counters.CTU())

    // 에지 검출
    ("product_detected", EdgeDetection.R_TRIG())
    ("quality_checked", EdgeDetection.R_TRIG())

    // 비상 정지
    ("emergency_latch", Bistable.SR())
]

for (name, fb) in instances do
    let inst = FBInstance.create fb name
    library.RegisterFB(fb) |> ignore
    library.RegisterInstance(inst) |> ignore

// 3. 런타임 컨텍스트
let ctx = Context.create()

// 변수 선언
ctx.Memory.DeclareInput("start_button", TBool)
ctx.Memory.DeclareInput("stop_button", TBool)
ctx.Memory.DeclareInput("emergency_button", TBool)
ctx.Memory.DeclareInput("product_sensor", TBool)
ctx.Memory.DeclareInput("quality_ok", TBool)

ctx.Memory.DeclareOutput("conveyor_motor", TBool)
ctx.Memory.DeclareOutput("reject_gate", TBool)
ctx.Memory.DeclareOutput("alarm", TBool)

ctx.Memory.DeclareInternal("system_running", TBool)
ctx.Memory.DeclareInternal("current_count", TInt, retain = true)
ctx.Memory.DeclareInternal("reject_count", TInt, retain = true)

// 4. 프로그램
let productionProgram = {
    Name = "AutoProductionLine"
    Body = [
        // 비상 정지 시스템
        // emergency_latch.S1 := emergency_button
        // emergency_latch.R := start_button AND NOT emergency_button
        // emergency_latch()
        // alarm := emergency_latch.Q1

        // 시스템 실행 조건
        // system_running := start_button AND NOT emergency_latch.Q1

        // 제품 감지 (에지)
        // product_detected.CLK := product_sensor
        // product_detected()

        // 제품 카운팅
        // product_counter.CU := product_detected.Q
        // product_counter.R := stop_button
        // product_counter.PV := 1000
        // product_counter()
        // current_count := product_counter.CV

        // 품질 검사 지연
        // quality_check_timer.IN := product_detected.Q
        // quality_check_timer.PT := 2000  // 2초 후 검사
        // quality_check_timer()

        // 품질 불량 감지
        // quality_checked.CLK := quality_check_timer.Q
        // quality_checked()

        // 불량품 카운팅
        // reject_counter.CU := quality_checked.Q AND NOT quality_ok
        // reject_counter.R := stop_button
        // reject_counter()
        // reject_count := reject_counter.CV

        // 불량품 배출 펄스
        // reject_pulse.IN := quality_checked.Q AND NOT quality_ok
        // reject_pulse.PT := 500  // 0.5초
        // reject_pulse()
        // reject_gate := reject_pulse.Q

        // 컨베이어 제어
        // conveyor_motor := system_running AND NOT product_counter.Q
    ]
    Description = Some "Automated production line control"
}

// 5. 실행
let engine = CpuScan.createDefault productionProgram

// 입력 시뮬레이션
ctx.Memory.SetInput("start_button", box true)
ctx.Memory.SetInput("product_sensor", box true)
ctx.Memory.SetInput("quality_ok", box true)

// 스캔 실행
engine.ScanOnce() |> ignore

// 출력 확인
let conveyorState = ctx.Memory.Get("conveyor_motor")
let currentCount = ctx.Memory.Get("current_count")
printfn "Conveyor: %b, Count: %d" (unbox conveyorState) (unbox currentCount)
```

---

## 모범 사례

### 1. FB 인스턴스 명명

```fsharp
// 기능_역할 패턴
let instances = [
    ("conveyor_timer", Timers.TON())
    ("pump_delay", Timers.TOF())
    ("product_counter", Counters.CTU())
    ("sensor_edge", EdgeDetection.R_TRIG())
]

// 명확하고 일관성 있는 이름 사용
```

### 2. 프리셋 값 관리

```fsharp
// 상수로 정의하여 유지보수 용이
let CONVEYOR_DELAY_MS = 2000
let QUALITY_CHECK_TIME_MS = 3000
let MAX_PRODUCT_COUNT = 1000

// timer.PT := CONVEYOR_DELAY_MS
```

### 3. 에지 검출 활용

```fsharp
// 센서 노이즈 제거 및 정확한 카운팅
// sensor_edge.CLK := sensor_raw
// sensor_edge()
// product_counter.CU := sensor_edge.Q  // 에지만 카운트
```

### 4. 히스테리시스로 떨림 방지

```fsharp
// ON/OFF 반복 방지
// hyst.high := setpoint + 1.0
// hyst.low := setpoint - 1.0
// 충분한 간격 확보
```

### 5. Retain 변수 사용

```fsharp
// 전원 재투입 시에도 카운트 유지
ctx.Memory.DeclareInternal("total_count", TInt, retain = true)
```

---

## API 참조

### 주요 모듈

| 모듈 | 설명 |
|------|------|
| `Timers` | 타이머 FB (TON, TOF, TP, TONR) |
| `Counters` | 카운터 FB (CTU, CTD, CTUD) |
| `EdgeDetection` | 에지 검출 FB (R_TRIG, F_TRIG) |
| `Bistable` | 래치 FB (SR, RS) |
| `Math` | 수학 함수 (MAX, MIN, AVERAGE) |
| `String` | 문자열 함수 (CONCAT, LEFT, RIGHT, MID, FIND) |
| `Analog` | 아날로그 처리 (HYSTERESIS) |
| `StandardLibraryRegistry` | 표준 라이브러리 레지스트리 |

### 표준 FB 목록

**타이머**
- `TON`: On-Delay Timer
- `TOF`: Off-Delay Timer
- `TP`: Pulse Timer
- `TONR`: Retentive On-Delay Timer

**카운터**
- `CTU`: Up Counter
- `CTD`: Down Counter
- `CTUD`: Up-Down Counter

**에지 검출**
- `R_TRIG`: Rising Edge Trigger
- `F_TRIG`: Falling Edge Trigger

**래치**
- `SR`: Set (Dominant) Bistable
- `RS`: Reset (Dominant) Bistable

**아날로그**
- `HYSTERESIS`: Hysteresis Comparator

---

## 추가 리소스

- **IEC 61131-3 표준**: 표준 함수 블록 스펙
- **Ev2.Cpu.Core-사용자매뉴얼.md**: UserFC/UserFB 생성 방법
- **Ev2.Cpu.Runtime-사용자매뉴얼.md** (`docs/guides/manuals/runtime/Ev2.Cpu.Runtime-사용자매뉴얼.md`): FB 인스턴스 실행 방법
- **프로젝트 테스트**: `src/UnitTest/cpu/Ev2.Cpu.StandardLibrary.Tests/` 디렉토리

---

## 버전 정보

- **현재 버전**: 1.0.0
- **대상 프레임워크**: .NET 8.0
- **언어**: F# 8.0
- **표준 준수**: IEC 61131-3

---

## 라이선스

이 프로젝트는 회사 내부 라이선스에 따라 배포됩니다.
