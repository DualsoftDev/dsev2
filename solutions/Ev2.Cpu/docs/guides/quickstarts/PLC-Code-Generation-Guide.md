# PLC 코드 생성 가이드

## 개요

Ev2.Cpu.Generation은 **UserFB/FC를 실제 PLC 코드로 자동 생성**합니다.

- **표준 준수**: IEC 61131-3 Structured Text (ST) 형식
- **PLC 호환**: TwinCAT, Codesys, Siemens 등 대부분의 PLC 지원
- **즉시 배포**: 생성된 코드를 PLC 프로젝트에 바로 복사하여 사용 가능

---

## 빠른 시작

> **중복 줄이기 안내**  
> 빌더 사용법과 FC/FB 생성 기본기는 [Ev2.Cpu.Generation 사용자 매뉴얼](./Ev2.Cpu.Generation-사용자매뉴얼.md#빠른-시작)에서 통합 관리합니다. 이 가이드는 코드 생성 파이프라인과 프로젝트 출력에 집중합니다.

---

## 전체 프로젝트 생성

### 프로젝트 구조

여러 FB/FC를 하나의 프로젝트로 묶어 생성할 수 있습니다.

```fsharp
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Codegen.PLCCodeGen

// 1. 레지스트리 생성
let registry = UserFBRegistry()

// 2. FC 등록
registry.RegisterFC(createCelsiusToFahrenheitFC())
registry.RegisterFC(createLinearScaleFC())

// 3. FB 등록
registry.RegisterFB(createHysteresisFB())
registry.RegisterFB(createMotorControlFB())
registry.RegisterFB(createSequence3StepFB())

// 4. 인스턴스 생성 및 등록
let motor1 = createFBInstance "Motor1" (createMotorControlFB())
let motor2 = createFBInstance "Motor2" (createMotorControlFB())
registry.RegisterInstance(motor1)
registry.RegisterInstance(motor2)

// 5. 프로젝트 코드 생성
let projectCode = generatePLCProject registry "IndustrialAutomation"

printfn "%s" projectCode
```

---

## 파일로 저장

### Structured Text 파일 저장

```fsharp
open System.IO

// 출력 디렉토리
let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "PLCOutput")
if not (Directory.Exists(outputDir)) then
    Directory.CreateDirectory(outputDir) |> ignore

// 프로젝트 저장
savePLCProject outputDir "MyProject" registry

// 결과: PLCOutput/MyProject.st
```

### TwinCAT 프로젝트 파일 생성

```fsharp
// TwinCAT .TcPOU 파일 생성
saveTwinCATProject outputDir registry

// 결과:
//   PLCOutput/FC_CelsiusToFahrenheit.TcPOU
//   PLCOutput/FB_MotorControl.TcPOU
//   PLCOutput/FB_Hysteresis.TcPOU
//   ...
```

---

## 실전 예제

### 예제 1: 온도 제어 시스템

```fsharp
let registry = UserFBRegistry()

// 1. 온도 변환 FC
registry.RegisterFC(createCelsiusToFahrenheitFC())

// 2. 히스테리시스 제어 FB
registry.RegisterFB(createHysteresisFB())

// 3. 경보 FB 생성
let alarmFB = FBBuilder("TemperatureAlarm")
alarmFB.AddInput("temperature", DsDataType.TDouble)
alarmFB.AddInput("highAlarm", DsDataType.TDouble)
alarmFB.AddInput("lowAlarm", DsDataType.TDouble)
alarmFB.AddOutput("highAlarmActive", DsDataType.TBool)
alarmFB.AddOutput("lowAlarmActive", DsDataType.TBool)

let temp = Terminal(DsTag.Double("temperature"))
let high = Terminal(DsTag.Double("highAlarm"))
let low = Terminal(DsTag.Double("lowAlarm"))

alarmFB.AddStatement(assignAuto "highAlarmActive" DsDataType.TBool (gt temp high))
alarmFB.AddStatement(assignAuto "lowAlarmActive" DsDataType.TBool (lt temp low))

registry.RegisterFB(alarmFB.Build())

// 4. 인스턴스 생성
let heaterCtrl = createFBInstance "HeaterControl" (createHysteresisFB())
let coolerCtrl = createFBInstance "CoolerControl" (createHysteresisFB())
let tempAlarm = createFBInstance "TempAlarm" (alarmFB.Build())

registry.RegisterInstance(heaterCtrl)
registry.RegisterInstance(coolerCtrl)
registry.RegisterInstance(tempAlarm)

// 5. 프로젝트 저장
let outputDir = "C:\\PLCProjects\\TemperatureControl"
savePLCProject outputDir "TemperatureControl" registry
saveTwinCATProject outputDir registry

printfn "온도 제어 시스템 생성 완료!"
```

**생성된 파일:**
```
TemperatureControl/
  ├─ TemperatureControl.st          # 전체 프로젝트 (ST)
  ├─ FC_CelsiusToFahrenheit.TcPOU   # TwinCAT FC
  ├─ FB_Hysteresis.TcPOU             # TwinCAT FB
  └─ FB_TemperatureAlarm.TcPOU       # TwinCAT FB
```

### 예제 2: 컨베이어 시스템

```fsharp
let registry = UserFBRegistry()

// 1. 모터 제어 FB
registry.RegisterFB(createMotorControlFB())

// 2. 카운터 FB
let counterFB = FBBuilder("ProductCounter")
counterFB.AddInput("trigger", DsDataType.TBool)
counterFB.AddInput("reset", DsDataType.TBool)
counterFB.AddInput("preset", DsDataType.TInt)
counterFB.AddOutput("count", DsDataType.TInt)
counterFB.AddOutput("done", DsDataType.TBool)
counterFB.AddStaticWithInit("currentCount", DsDataType.TInt, box 0)

// 로직 추가...
registry.RegisterFB(counterFB.Build())

// 3. 인스턴스
let conveyorMotor = createFBInstance "ConveyorMotor" (createMotorControlFB())
let productCounter = createFBInstance "ProductCounter" (counterFB.Build())

registry.RegisterInstance(conveyorMotor)
registry.RegisterInstance(productCounter)

// 4. 저장
savePLCProject "C:\\PLCProjects\\Conveyor" "ConveyorSystem" registry
```

---

## PLC에 배포하기

### 1. TwinCAT에 배포

1. TwinCAT XAE를 엽니다
2. PLC 프로젝트를 생성합니다
3. **POUs** 폴더에 생성된 `.TcPOU` 파일을 추가합니다
   - `Add Existing Item...` 선택
   - 생성된 `.TcPOU` 파일 선택
4. 메인 프로그램(MAIN)에서 인스턴스를 선언하고 호출합니다:

```
PROGRAM MAIN
VAR
    Motor1 : FB_MotorControl;
    Motor2 : FB_MotorControl;
    TempAlarm : FB_TemperatureAlarm;
END_VAR

// 모터 1 호출
Motor1(
    start := startButton1,
    stop := stopButton1,
    emergency := emergencyStop,
    overload := overload1
);

// 출력 사용
IF Motor1.running THEN
    // 모터 1이 작동 중
END_IF;
```

5. 빌드하고 PLC에 다운로드합니다

### 2. Codesys에 배포

1. Codesys를 엽니다
2. 프로젝트를 생성합니다
3. **Application** → **Add Object** → **POU**
4. 생성된 `.st` 파일의 내용을 복사하여 붙여넣습니다
5. 메인 프로그램에서 인스턴스를 선언하고 사용합니다

### 3. Siemens TIA Portal에 배포

1. TIA Portal을 엽니다
2. PLC 프로그램 블록에 **Function** 또는 **Function Block** 추가
3. 생성된 ST 코드를 복사하여 SCL 에디터에 붙여넣습니다
4. 컴파일하고 다운로드합니다

---

## 생성 옵션

### 데이터 타입 매핑

| Ev2 타입 | PLC ST 타입 |
|----------|-------------|
| `DsDataType.TBool` | `BOOL` |
| `DsDataType.TInt` | `INT` |
| `DsDataType.TDouble` | `REAL` |
| `DsDataType.TString` | `STRING` |

### 연산자 매핑

| Ev2 연산자 | PLC ST 연산자 |
|-----------|--------------|
| `add` | `+` |
| `sub` | `-` |
| `mul` | `*` |
| `div` | `/` |
| `and'` | `AND` |
| `or'` | `OR` |
| `not'` | `NOT` |
| `eq` | `=` |
| `ne` | `<>` |
| `gt` | `>` |
| `ge` | `>=` |
| `lt` | `<` |
| `le` | `<=` |
| `rising` | `R_TRIG()` |
| `falling` | `F_TRIG()` |

---

## 주의사항 및 제한사항

### 1. 지원되는 기능

✅ **FC (Function)**: 모든 수식과 계산
✅ **FB (Function Block)**: Static 변수, 명령문, 릴레이 로직
✅ **파라미터**: VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT
✅ **연산자**: 산술, 논리, 비교 연산
✅ **타이머/카운터**: TON, TOF, TP, CTU, CTD

### 2. 제한사항

⚠️ **복잡한 제어 구조**: `FOR`, `WHILE` 루프는 수동으로 추가 필요
⚠️ **고급 PLC 기능**: OSCAT 라이브러리, 사용자 정의 타입은 별도 구현 필요
⚠️ **PLC별 차이**: 일부 PLC는 함수 이름이나 타입에 제약이 있을 수 있음

### 3. 베스트 프랙티스

✅ **이름 규칙**: PLC 표준 명명 규칙 준수 (영문자, 숫자, 언더스코어만 사용)
✅ **주석 추가**: `SetDescription()` 사용하여 문서화
✅ **테스트**: 생성된 코드를 PLC 시뮬레이터에서 먼저 테스트
✅ **버전 관리**: 생성된 PLC 코드를 Git 등에서 관리

---

## API 참조

### 코드 생성 함수

| 함수 | 설명 |
|------|------|
| `generateFC(fc)` | FC를 ST 코드로 변환 |
| `generateFB(fb)` | FB를 ST 코드로 변환 |
| `generatePLCProject(registry, name)` | 전체 프로젝트 생성 |
| `generateTwinCATFile(fc)` | TwinCAT .TcPOU 파일 생성 (FC) |
| `generateTwinCATFileForFB(fb)` | TwinCAT .TcPOU 파일 생성 (FB) |

### 파일 저장 함수

| 함수 | 설명 |
|------|------|
| `savePLCFile(path, content)` | 파일 저장 |
| `savePLCProject(dir, name, registry)` | 프로젝트 .st 파일 저장 |
| `saveTwinCATProject(dir, registry)` | TwinCAT .TcPOU 파일들 저장 |

---

## 예제 모음

전체 예제는 다음 파일에서 확인할 수 있습니다:
- `/src/cpu/Ev2.Cpu.Generation/Examples/PLCDeploymentExamples.fs`

예제 실행:
```fsharp
open Ev2.Cpu.Generation.Examples.PLCDeploymentExamples

// 모든 예제 실행
runAllExamples()

// 개별 예제 실행
example1_GenerateSingleFC()
example2_GenerateSingleFB()
example3_GenerateFullProject()
example6_TemperatureControlSystem()
example7_ConveyorSystem()
```

---

## 다음 단계

1. ✅ **UserFB/FC 설계**: 재사용 가능한 블록 만들기
2. ✅ **코드 생성**: PLC ST 형식으로 자동 변환
3. 🔄 **PLC 배포**: TwinCAT/Codesys에 배포
4. 🔄 **테스트**: 시뮬레이터 또는 실제 PLC에서 검증
5. 🔄 **유지보수**: 변경사항 반영 및 재배포

---

## 문제 해결

### Q: 생성된 코드가 컴파일되지 않습니다

**A:** PLC별 문법 차이가 있을 수 있습니다:
- TwinCAT: 대부분 표준 준수
- Codesys: 일부 함수 이름 변경 필요 (예: `R_TRIG` → `R_TRIG_1`)
- Siemens: SCL 문법으로 일부 수정 필요

### Q: Static 변수가 초기화되지 않습니다

**A:** FB 인스턴스를 처음 선언할 때 초기화됩니다. PLC 리셋 시에도 유지되므로 수동 초기화가 필요한 경우 RESET 로직을 추가하세요.

### Q: 타이머가 작동하지 않습니다

**A:** 타이머는 시간 단위가 밀리초(ms)입니다. PLC에 따라 시간 리터럴 형식이 다를 수 있습니다 (예: `T#5s`, `5000ms`).

---

## 요약

✅ **자동 생성**: UserFB/FC → PLC ST 코드
✅ **표준 준수**: IEC 61131-3
✅ **PLC 호환**: TwinCAT, Codesys, Siemens 지원
✅ **즉시 배포**: 생성된 코드를 바로 PLC에 사용
✅ **생산성 향상**: 수동 코딩 대비 10배 이상 빠름

더 자세한 정보는 `Ev2.Cpu.Generation-사용자매뉴얼.md`의 **빠른 시작** 섹션을 참고하세요!
