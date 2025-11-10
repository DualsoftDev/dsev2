# Ev2.Cpu.Generation 사용자 매뉴얼

Ev2.Cpu.Generation은 DSL 기반으로 PLC 제어 로직을 설계하고, 사용자 정의 FC/FB를 손쉽게 구성·배포할 수 있는 빌더 모듈 모음입니다. 이 문서는 중복되던 **신규/구버전 매뉴얼을 통합**해 가장 중요한 사용 패턴만 정리했습니다.

## 📚 목차
1. [개요](#개요)
2. [빠른 시작](#빠른-시작)
3. [빌더 기본 개념](#빌더-기본-개념)
4. [표현식 · 명령문 작성하기](#표현식--명령문-작성하기)
5. [프로그램 생성과 PLC 코드 출력](#프로그램-생성과-plc-코드-출력)
6. [UserLibrary와 배포 준비](#userlibrary와-배포-준비)
7. [⭐ 런타임 중 코드 수정 (런중라이트)](#-런타임-중-코드-수정-런중라이트)
8. [참고 자료](#참고-자료)

---

## 개요
- **주요 모듈**
  - `Ev2.Cpu.Generation.Core` : DSL 타입 및 공통 헬퍼
  - `Ev2.Cpu.Generation.Make.*` : Expression/Statement/Program/UserFB 빌더
  - `Ev2.Cpu.Generation.Codegen.*` : Structured Text(ST) 코드 생성기
- **타겟 프로젝트** : .NET 8, F# 8
- **권장 워크플로우**
  1. Expression/Statement 헬퍼로 로직 작성
  2. FC/FB 빌더로 재사용 가능한 블록 구성
  3. ProgramBuilder 또는 CodeGen으로 PLC 코드 생성
  4. UserLibrary에 등록 후 런타임에 배포

---

## 빠른 시작

### 1) Function (FC) 만들기
```fsharp
open Ev2.Cpu.Generation.Make.UserFBGen

let createCelsiusToFahrenheit() =
    let builder = FCBuilder("CelsiusToFahrenheit")
    builder.AddInput("celsius", DsDataType.TDouble)
    builder.AddOutput("fahrenheit", DsDataType.TDouble)

    let body =
        ExpressionGen.add
            (ExpressionGen.mul (ExpressionGen.doubleVar "celsius") (ExpressionGen.doubleExpr 1.8))
            (ExpressionGen.doubleExpr 32.0)

    builder.SetBody(body)
    builder.Build()
```

### 2) Function Block (FB) 만들기
```fsharp
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

let createMotorControl() =
    let builder = FBBuilder("MotorControl")
    builder.AddInput("start", DsDataType.TBool)
    builder.AddInput("stop", DsDataType.TBool)
    builder.AddInput("emergency", DsDataType.TBool)
    builder.AddOutput("running", DsDataType.TBool)
    builder.AddOutput("fault", DsDataType.TBool)

    builder.AddStaticWithInit("latchedRun", DsDataType.TBool, box false)

    let runSet =
        Function("IF", [
            boolVar "emergency"
            boolExpr false
            Function("IF", [
                boolVar "start"
                boolExpr true
                boolVar "latchedRun"
            ])
        ])

    builder.AddStatement(assignAuto "latchedRun" DsDataType.TBool runSet)
    builder.AddStatement(assignAuto "running" DsDataType.TBool (boolVar "latchedRun"))
    builder.AddStatement(assignAuto "fault" DsDataType.TBool (boolVar "emergency"))
    builder.Build()
```

> 📌 `Build()` 는 `Result<UserFC,_>` / `Result<UserFB,_>` 를 반환합니다. 실패 시 `err.Format()` 으로 상세 메시지를 확인하세요.

---

## 빌더 기본 개념

### 지원 데이터 타입
| 타입 | 설명 | 예시 |
|------|------|------|
| `TBool` | PLC 불리언 | `true`, `false` |
| `TInt` | 32비트 정수 | `0`, `100` |
| `TDouble` | 배정밀도 실수 | `3.14`, `-42.0` |
| `TString` | 문자열 | `"Hello"` |

### FCBuilder 핵심 순서
1. `AddInput` / `AddOutput` 로 시그니처 정의  
2. `ExpressionGen` 을 이용해 본문(`DsExpr`) 작성  
3. `SetBody` 후 `Build()` 호출

### FBBuilder 핵심 순서
1. 입력(`AddInput`), 출력(`AddOutput`), 입출력(`AddInOut`) 선언  
2. 상태가 필요한 변수는 `AddStatic` / `AddTemp` 사용  
3. `StatementGen` 혹은 직접 `Assign`, `Command` 를 추가  
4. (선택) `SetDescription` 으로 메타데이터 기록  
5. `Build()` 로 결과 생성

---

## 표현식 · 명령문 작성하기

### ExpressionGen 요약
```fsharp
open Ev2.Cpu.Generation.Make.ExpressionGen

let risingEdge = rising (boolVar "StartSignal")
let elapsed =
    Function("IF", [
        boolVar "Running"
        Function("TON", [ boolVar "Running"; stringExpr "WorkTimer"; intExpr 5000 ])
        intExpr 0
    ])
```
- 상수: `boolExpr`, `intExpr`, `doubleExpr`, `stringExpr`
- 변수: `boolVar`, `intVar`, `doubleVar`, `stringVar`
- 산술/논리: `add`, `sub`, `mul`, `div`, `and'`, `or'`, `not'`
- PLC 함수: `Function("TON", [...])`, `Function("CTU", [...])`

### StatementGen 요약
```fsharp
open Ev2.Cpu.Generation.Make.StatementGen

let statements = [
    assignAuto "Running" DsDataType.TBool (boolVar "StartButton")
    when' (boolVar "ResetButton") (mov (boolExpr false) (DsTag.Bool "Running"))
]
```
- `assignAt` / `assignAuto` : 변수 할당
- `whenAt` / `when'` : 조건부 명령
- `startTimer`, `countUp` 등 고수준 헬퍼 제공

### Relay & Generation Utils
```fsharp
open Ev2.Cpu.Generation.Core

let latch =
    Relay.CreateWithMode(
        DsTag.Bool "Work.Running",
        ExpressionGen.boolVar "Work.SW",
        ExpressionGen.boolVar "Work.RW",
        RelayMode.SR)
```
- Relay 는 자기유지(SR), 펄스, 조건부 등 다양한 모드를 지원
- `GenerationUtils.relayToStmt` 로 Statement 변환 가능

---

## 프로그램 생성과 PLC 코드 출력
```fsharp
open Ev2.Cpu.Generation.Make.ProgramGen

let buildProgram fb =
    let builder = ProgramBuilder("MainProgram")
    builder.AddInput("SystemStart", DsDataType.TBool)
    builder.AddLocal("State", DsDataType.TInt)
    builder.AddStatement(assignAuto "State" DsDataType.TInt (intExpr 0))
    builder.AddStatement(StatementGen.when' (boolVar "SystemStart") (mov (boolExpr true) (DsTag.Bool "Motor.run")))
    builder.Build()
```

PLC 코드 출력은 `Ev2.Cpu.Generation.Codegen.PLCCodeGen` 을 이용합니다.
```fsharp
open Ev2.Cpu.Generation.Codegen.PLCCodeGen

let plcCode =
    match createMotorControl() with
    | Ok fb -> generateFB fb
    | Error err -> failwith (err.Format())
```

> 💡 보다 상세한 코드 생성 시나리오는 `docs/guides/quickstarts/PLC-Code-Generation-Guide.md` 를 참고하세요.

---

## UserLibrary와 배포 준비

```fsharp
open Ev2.Cpu.Core.UserDefined

let library = UserLibrary()

let registerStandardLibrary () =
    Ev2.Cpu.StandardLibrary.StandardLibraryRegistry.registerAllTo library
    |> ignore

let registerCustomBlocks () =
    match createCelsiusToFahrenheit() with
    | Ok fc -> library.RegisterFC(fc) |> ignore
    | Error err -> failwith (err.Format())

    match createMotorControl() with
    | Ok fb -> library.RegisterFB(fb) |> ignore
    | Error err -> failwith (err.Format())
```

- `RegisterFC` / `RegisterFB` 는 충돌 시 `Error UserDefinitionError` 를 반환
- `UserLibrary.GetFC/FB` 로 등록된 객체를 조회하고, `GetAllFCs/FBs` 로 리스트를 확인할 수 있습니다.
- 표준 FB/FC 는 `Ev2.Cpu.StandardLibrary.StandardLibraryRegistry.initialize()` 로 일괄 등록 가능

---

## ⭐ 런타임 중 코드 수정 (런중라이트)

Ev2.Cpu.Generation에서 만든 FC/FB는 런타임에서도 안전하게 교체할 수 있습니다. 핵심은 `RuntimeUpdateManager` 와 `UpdateRequest` 를 사용하는 것입니다.

```fsharp
open Ev2.Cpu.Runtime
open Ev2.Cpu.Core.UserDefined

let updateProgram (ctx: ExecutionContext) (library: UserLibrary) =
    let updateMgr = RuntimeUpdateManager(ctx, library, None)

    let newFc =
        match createCelsiusToFahrenheit() with
        | Ok fc -> fc
        | Error err -> failwith (err.Format())

    // 1. 사용자 FC 업데이트
    updateMgr.EnqueueUpdate(UpdateRequest.updateUserFC(newFc, validate = true))

    // 2. 프로그램 본문 교체 (예시)
    let newBody =
        [
            StatementGen.assignAuto "Result" DsDataType.TDouble
                (ExpressionGen.call "CelsiusToFahrenheit" [ ExpressionGen.doubleVar "Sensor.Temp" ])
        ]

    updateMgr.EnqueueUpdate(UpdateRequest.updateProgramBody(newBody, validate = true))

    // 3. 적용
    match updateMgr.ProcessPendingUpdates() with
    | [] -> ()
    | results ->
        let failures =
            results
            |> List.choose (function
                | UpdateResult.Success _ -> None
                | other -> Some (other.Format()))
        if not failures.IsEmpty then
            failwithf "런타임 업데이트 실패\n%s" (String.concat "\n" failures)
```

**주의 사항**
1. 검증(`validate = true`)을 켜면 UserDefinitionValidation을 자동 수행합니다.
2. 실패 시 `UpdateResult.RolledBack` 가 돌아오며, `RuntimeUpdateManager.Rollback()` 으로 즉시 복구할 수 있습니다.
3. 고빈도 업데이트는 스캔 시간에 영향을 줄 수 있으므로 배치 단위(`UpdateRequest.batch`)로 묶어 처리하세요.

---

## 참고 자료
- 구조 및 아키텍처 : `docs/concepts/ARCHITECTURE.md`
- 상세 사양 : `docs/specs/codegen/Ev2.Cpu.CodeGen.md`
- PLC 코드 출력 가이드 : `docs/guides/quickstarts/PLC-Code-Generation-Guide.md`
- 표준 라이브러리 참조 : `docs/reference/Ev2.Cpu.StandardLibrary-Reference.md`
- 런타임 업데이트 테스트 : `src/UintTest/cpu/Ev2.Cpu.Runtime.Tests/RuntimeUpdate.Tests.fs`

---

필요한 내용만 남기기 위해 중복되던 문단을 모두 정리했습니다. 추가로 다루고 싶은 항목이 있다면 `guides/` 디렉터리에 새 섹션으로 확장해 주세요.
