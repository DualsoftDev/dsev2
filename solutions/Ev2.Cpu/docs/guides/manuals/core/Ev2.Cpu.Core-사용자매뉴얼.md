# Ev2.Cpu.Core 사용자 매뉴얼

## 개요

`Ev2.Cpu.Core`는 IEC 61131-3 표준 기반 PLC 프로그래밍 언어의 핵심 타입과 연산자를 정의하는 기반 라이브러리입니다. 런타임 엔진과 코드 생성기가 공통으로 사용하는 추상 구문 트리(AST), 데이터 타입, 연산자 시스템을 제공합니다.

---

## 주요 기능

### 1. 데이터 타입 시스템 (`DsDataType`)

PLC에서 사용하는 4가지 기본 데이터 타입을 제공합니다.

#### 지원 타입

```fsharp
type DsDataType =
    | TBool     // 논리값 (TRUE/FALSE)
    | TInt      // 32비트 정수
    | TDouble   // 64비트 실수
    | TString   // 문자열
```

#### 주요 메서드

- **`DotNetType`**: PLC 타입을 .NET 타입으로 매핑
- **`DefaultValue`**: 타입별 기본값 반환 (Bool: false, Int: 0, Double: 0.0, String: "")
- **`Validate(value)`**: 런타임 값의 타입 검증
- **`IsCompatibleWith(other)`**: 타입 호환성 검사 (Int → Double 자동 승격 지원)
- **`IsNumeric`**: 산술 연산 가능 여부 확인

#### 사용 예제

```fsharp
open Ev2.Cpu.Core

// 타입 생성 및 기본값
let intType = DsDataType.TInt
let defaultVal = intType.DefaultValue  // box 0

// .NET 타입에서 변환
let typeFromObj = DsDataType.OfType(typeof<bool>)  // TBool

// 타입 호환성 검사
let compatible = TDouble.IsCompatibleWith(TInt)  // true (정수→실수 승격)

// 값 검증
let validated = TInt.Validate(box 42)  // OK
// let invalid = TInt.Validate(box "text")  // 예외 발생
```

---

### 2. 연산자 시스템 (`DsOp`)

IEC 61131-3 표준 연산자를 체계적으로 정의합니다.

#### 연산자 분류

**산술 연산자**
- `Add`, `Sub`, `Mul`, `Div`, `Mod`

**비교 연산자**
- `EQ` (같음), `NE` (다름), `LT` (작음), `LE` (작거나 같음), `GT` (큼), `GE` (크거나 같음)

**논리 연산자**
- `And`, `Or`, `Xor`, `Not`

**에지 검출**
- `RisingEdge` (상승 에지), `FallingEdge` (하강 에지)

**특수 연산**
- `Move` (데이터 이동)

#### 연산자 속성

```fsharp
// 연산자 분류 확인
DsOp.IsArithmetic DsOp.Add  // true
DsOp.IsLogical DsOp.And     // true
DsOp.IsComparison DsOp.EQ   // true
DsOp.IsEdge DsOp.RisingEdge // true

// 단항/이항 구분
DsOp.IsUnary DsOp.Not       // true
DsOp.IsBinary DsOp.Add      // true

// 우선순위
DsOp.Priority DsOp.Mul      // 4 (곱셈)
DsOp.Priority DsOp.Add      // 3 (덧셈, 낮은 우선순위)
```

#### 타입 검증

```fsharp
open Ev2.Cpu.Core

// 산술 연산자는 수치 타입만 허용
Operators.validateForTypes DsOp.Add TInt TInt     // OK
Operators.validateForTypes DsOp.Add TDouble TInt  // OK (승격)
// Operators.validateForTypes DsOp.Add TBool TInt  // Error

// 논리 연산자는 Bool 타입만
Operators.validateForTypes DsOp.And TBool TBool   // OK
// Operators.validateForTypes DsOp.And TInt TInt   // Error

// 비교 연산자는 결과가 항상 Bool
Operators.validateForTypes DsOp.GT TInt TDouble   // OK, 결과는 TBool
```

---

### 3. 표현식 AST (`DsExpr`)

PLC 표현식을 표현하는 추상 구문 트리입니다.

#### 표현식 타입

```fsharp
type DsExpr =
    | EConst of value: obj * dataType: DsDataType
    | EVar of name: string * dataType: DsDataType option
    | ETerminal of tag: DsTag
    | EUnary of op: DsOp * expr: DsExpr
    | EBinary of left: DsExpr * op: DsOp * right: DsExpr
    | ECall of funcName: string * args: DsExpr list
    | EMeta of tag: string * metadata: Map<string, obj>
```

#### 표현식 빌더

```fsharp
open Ev2.Cpu.Core

// 상수
let constInt = DsExpr.eInt 42
let constBool = DsExpr.eBool true
let constDouble = DsExpr.eDouble 3.14
let constString = DsExpr.eString "Hello"

// 변수
let varExpr = DsExpr.eVar "temperature" (Some TDouble)

// 산술 연산
let sum = DsExpr.eBinary (DsExpr.eInt 10) DsOp.Add (DsExpr.eInt 20)
// 또는 연산자 오버로드 사용
let sum' = DsExpr.eInt 10 .+ DsExpr.eInt 20

// 복합 표현식: (a + b) * 2
let complex = (DsExpr.eVar "a" None .+ DsExpr.eVar "b" None) .* DsExpr.eInt 2

// 함수 호출
let funcCall = DsExpr.eCall "SQRT" [DsExpr.eDouble 25.0]
```

#### 표현식 분석

```fsharp
// 변수 추출
let vars = expr.GetVariables()  // 표현식에서 사용된 모든 변수명

// 함수 호출 추출
let funcs = expr.GetFunctionCalls()  // 사용된 함수명 목록

// 복잡도 계산
let depth = ExprAnalysis.depth expr        // 트리 깊이
let complexity = ExprAnalysis.complexity expr  // 노드 총 개수

// 상수 표현식 판정
let isConst = ExprAnalysis.isConstant expr  // true if 모든 노드가 상수

// 타입 추론
let inferredType = expr.InferType()  // Option<DsDataType>
```

#### 텍스트 변환

```fsharp
// 표현식을 텍스트로 (우선순위 고려)
let text = expr.ToText()
// 예: "((a + b) * 2)"

// 검증
match expr.Validate() with
| Ok () -> printfn "Valid expression"
| Error msg -> printfn "Invalid: %s" msg
```

---

### 4. 문장 AST (`DsStmt`)

PLC 프로그램의 실행 문장을 표현합니다.

#### 문장 타입

```fsharp
type DsStmt =
    | Assign of step: int * target: DsTag * value: DsExpr
    | Command of step: int * condition: DsExpr * action: DsExpr
```

#### Assign 문장

변수에 값을 할당합니다.

```fsharp
open Ev2.Cpu.Core

// output := input + 10
let assignStmt =
    Statement.assign
        1                                    // 스텝 번호
        (DsTag.create "output" TDouble)      // 대상 변수
        (DsExpr.eVar "input" None .+ DsExpr.eInt 10)  // 표현식
```

#### Command 문장

조건이 참일 때 액션을 실행합니다.

```fsharp
// IF temp > 100 THEN alarm := TRUE
let commandStmt =
    Statement.command
        2                                    // 스텝 번호
        (DsExpr.eVar "temp" None .> DsExpr.eInt 100)  // 조건
        (DsExpr.eCall "MOV" [DsExpr.eBool true; DsExpr.eVar "alarm" None])  // 액션
```

#### 문장 분석

```fsharp
// 참조 변수 추출 (읽기 + 쓰기)
let refs = Statement.getReferencedVariables stmt

// 텍스트 변환
let text = stmt.ToText()
// 예: "1: output := (input + 10)"
//     "2: IF (temp > 100) THEN MOV(TRUE, alarm)"
```

---

### 5. 사용자 정의 함수/블록 (UserDefined)

사용자가 정의한 함수(FC)와 함수 블록(FB)을 관리합니다.

#### UserFC (Function)

입력을 받아 출력을 반환하는 순수 함수입니다.

```fsharp
open Ev2.Cpu.Core.UserDefined

// FC 빌더 사용
let addFC =
    UserFC.builder "ADD_TWO"
    |> UserFC.withInput "a" TInt None
    |> UserFC.withInput "b" TInt None
    |> UserFC.withOutput "result" TInt
    |> UserFC.withBody [
        Statement.assign 1
            (DsTag.create "result" TInt)
            (DsExpr.eVar "a" None .+ DsExpr.eVar "b" None)
    ]
    |> UserFC.build

// 검증
match addFC.Validate() with
| Ok () -> printfn "FC is valid"
| Error err -> printfn "Validation failed: %s" (err.Format())
```

#### UserFB (Function Block)

내부 상태(Static 변수)를 가진 함수 블록입니다.

```fsharp
// FB 빌더 사용
let counterFB =
    UserFB.builder "COUNTER"
    |> UserFB.withInput "increment" TBool None
    |> UserFB.withInput "reset" TBool None
    |> UserFB.withOutput "count" TInt
    |> UserFB.withStatic "currentCount" TInt (Some (box 0))  // 내부 상태
    |> UserFB.withBody [
        // IF reset THEN currentCount := 0
        Statement.command 1
            (DsExpr.eVar "reset" None)
            (DsExpr.eCall "MOV" [DsExpr.eInt 0; DsExpr.eVar "currentCount" None])

        // IF increment THEN currentCount := currentCount + 1
        Statement.command 2
            (DsExpr.eVar "increment" None)
            (DsExpr.eCall "MOV" [
                DsExpr.eVar "currentCount" None .+ DsExpr.eInt 1
                DsExpr.eVar "currentCount" None
            ])

        // count := currentCount
        Statement.assign 3
            (DsTag.create "count" TInt)
            (DsExpr.eVar "currentCount" None)
    ]
    |> UserFB.build

// FB 인스턴스 생성
let counterInstance = FBInstance.create counterFB "myCounter"
```

#### UserLibrary

FC/FB를 등록하고 관리하는 레지스트리입니다.

```fsharp
let library = UserLibrary.create()

// FC 등록
match library.RegisterFC(addFC) with
| Ok () -> printfn "FC registered"
| Error err -> printfn "Registration failed: %s" (err.Format())

// FB 등록
match library.RegisterFB(counterFB) with
| Ok () -> printfn "FB registered"
| Error err -> printfn "Registration failed"

// 인스턴스 등록
match library.RegisterInstance(counterInstance) with
| Ok () -> printfn "Instance registered"
| Error err -> printfn "Registration failed"

// 조회
let fc = library.GetFC("ADD_TWO")           // Option<UserFC>
let fb = library.GetFB("COUNTER")           // Option<UserFB>
let inst = library.GetInstance("myCounter") // Option<FBInstance>

// 목록 조회
let allFCs = library.GetAllFCs()
let allFBs = library.GetAllFBs()
let allInstances = library.GetAllInstances()
```

---

### 6. 프로그램 구조 (Statement.Program)

PLC 프로그램의 전체 구조를 정의합니다.

```fsharp
type Program = {
    Name: string
    Body: DsStmt list
    Description: string option
}

// 프로그램 생성
let program = {
    Name = "MainProgram"
    Body = [
        Statement.assign 1 (DsTag.create "output1" TInt) (DsExpr.eInt 100)
        Statement.command 2
            (DsExpr.eVar "input1" None .> DsExpr.eInt 50)
            (DsExpr.eCall "SET" [DsExpr.eVar "alarm" None])
    ]
    Description = Some "Main PLC control program"
}
```

---

## 타입 변환 유틸리티

### TypeConverter

다양한 타입 간 변환을 제공합니다.

```fsharp
open Ev2.Cpu.Core

// obj → 특정 타입 변환
let boolVal = TypeConverter.toBool (box "true")      // true
let intVal = TypeConverter.toInt (box "42")          // 42
let doubleVal = TypeConverter.toDouble (box 3.14)    // 3.14
let strVal = TypeConverter.toString (box 123)        // "123"

// 안전한 변환 (Result 타입 반환)
match TypeConverter.tryConvert TDouble (box 42) with
| Ok converted -> printfn "Converted: %A" converted
| Error msg -> printfn "Conversion failed: %s" msg

// 범용 변환
let result = TypeConverter.convert TDouble (box 100)  // box 100.0
```

---

## 검증 시스템

### TypeValidation

타입 및 값 검증 기능을 제공합니다.

```fsharp
open Ev2.Cpu.Core

// null 검사
TypeValidation.checkNull value "paramName"

// 타입 검사
TypeValidation.checkType value TInt

// 범위 검사 (Int)
TypeValidation.checkRange 42 "value"  // -2^31 ~ 2^31-1

// 범위 검사 (Double, NaN/Infinity 거부)
TypeValidation.checkRange 3.14 "value"

// 스코프 경로 검증
TypeValidation.validateScopePath "Namespace.SubNamespace"  // OK
// TypeValidation.validateScopePath "Invalid Path!"  // 예외
```

---

## 에러 처리

### UserDefinitionError

사용자 정의 함수/블록의 에러를 표현합니다.

```fsharp
type UserDefinitionError = {
    ErrorCode: string
    Message: string
    Context: string list
}

// 에러 생성
let error =
    UserDefinitionError.create
        "FC.Input.Missing"
        "Function has no input parameters"
        ["ADD_TWO"; "inputs"]

// 에러 포맷팅
let formatted = error.Format()
// "[FC.Input.Missing] Function has no input parameters (Context: ADD_TWO > inputs)"
```

---

## 모범 사례

### 1. 타입 안전성 확보

```fsharp
// 항상 타입 검증 수행
let validateExpression expr =
    match expr.Validate() with
    | Ok () ->
        match expr.InferType() with
        | Some typ -> Ok typ
        | None -> Error "Cannot infer type"
    | Error msg -> Error msg
```

### 2. 변수 명명 규칙

```fsharp
// PLC 표준 규칙 준수
let inputVar = DsTag.create "I:Sensor1" TBool      // 입력
let outputVar = DsTag.create "O:Valve1" TBool      // 출력
let internalVar = DsTag.create "V:Counter" TInt    // 내부
```

### 3. 표현식 빌더 활용

```fsharp
// 연산자 오버로드로 가독성 향상
let condition =
    (DsExpr.eVar "temp" None .> DsExpr.eDouble 100.0)
    .&& (DsExpr.eVar "pressure" None .< DsExpr.eDouble 50.0)

// 함수 호출로 복잡한 로직 캡슐화
let avgExpr = DsExpr.eCall "AVERAGE" [
    DsExpr.eVar "sensor1" None
    DsExpr.eVar "sensor2" None
    DsExpr.eVar "sensor3" None
]
```

### 4. 에러 처리 패턴

```fsharp
// Result 타입 사용
let processFC fc =
    fc.Validate()
    |> Result.bind (fun () -> library.RegisterFC(fc))
    |> Result.map (fun () -> sprintf "FC '%s' registered" fc.Name)

match processFC myFC with
| Ok msg -> printfn "Success: %s" msg
| Error err -> printfn "Error: %s" (err.Format())
```

---

## API 참조

### 주요 모듈

| 모듈 | 설명 |
|------|------|
| `Ev2.Cpu.Core` | 핵심 타입 및 연산자 |
| `Ev2.Cpu.Core.UserDefined` | 사용자 정의 함수/블록 |
| `Ev2.Cpu.Core.Statement` | 프로그램 문장 |

### 주요 타입

| 타입 | 용도 |
|------|------|
| `DsDataType` | PLC 데이터 타입 |
| `DsOp` | 연산자 |
| `DsExpr` | 표현식 AST |
| `DsStmt` | 문장 AST |
| `DsTag` | 변수 태그 |
| `UserFC` | 사용자 함수 |
| `UserFB` | 사용자 함수 블록 |
| `FBInstance` | FB 인스턴스 |
| `UserLibrary` | FC/FB 레지스트리 |
| `Program` | PLC 프로그램 |

---

## 추가 리소스

- **CPU 스팩문서.md**: 전체 시스템 스펙 및 런타임 동작 설명
- **IEC 61131-3 표준**: PLC 프로그래밍 언어 국제 표준
- **프로젝트 테스트**: `src/UnitTest/cpu/Ev2.Cpu.Core.Tests/` 디렉토리 참조

---

## 버전 정보

- **현재 버전**: 1.0.0
- **대상 프레임워크**: .NET 8.0
- **언어**: F# 8.0

---

## 라이선스

이 프로젝트는 회사 내부 라이선스에 따라 배포됩니다.
