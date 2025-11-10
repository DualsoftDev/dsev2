# 제네릭 타입 시스템 사용 가이드

## 개요

Ev2.Cpu.Core에 Ev2.Gen.FS의 제네릭 타입 시스템이 추가되었습니다. 기존 `DsDataType` enum 시스템과 함께 사용 가능하며, F# .NET 네이티브 타입을 활용한 타입 안전 프로그래밍을 지원합니다.

## 추가된 파일

### Ev2.Cpu.Core
1. **Core/TypeInterfaces.fs** - 제네릭 인터페이스
   - `IExpression<'T>`, `IVariable<'T>`, `ILiteral<'T>`
   - `VarType` enum (Var, VarInput, VarOutput 등)

2. **Core/Variables.fs** - 제네릭 변수 및 리터럴
   - `Literal<'T>` - 타입 안전 상수
   - `Variable<'T>` - 타입 안전 변수
   - `Storage` - Dictionary 기반 변수 저장소

3. **Core/TagsGeneric.fs** - 제네릭 Tag 시스템
   - `Tag<'T>` - 타입 안전 Tag
   - `TagBuilders` 모듈 (bool, int, double, string)

### Ev2.Cpu.Runtime
4. **Engine/MemoryExtensions.fs** - 메모리 제네릭 확장
   - `GetTyped<'T>`, `SetTyped<'T>` - 타입 안전 메모리 접근
   - Tag/Variable 기반 메모리 접근

## 사용 예제

### 1. Variable 사용

```fsharp
open Ev2.Cpu.Core

// Variable 생성
let counter = Variable<int>("Counter", 0)
let enable = Variable<bool>("Enable", true)
let message = Variable<string>("Message", "Hello")

// VariableBuilders 사용
open VariableBuilders

let counter2 = int "Counter2"
let temperature = doubleWith "Temperature" 25.0
```

### 2. Literal 사용

```fsharp
// Literal 생성
let maxValue = Literal<int>(100)
let threshold = Literal<double>(3.14)
let status = Literal<bool>(true)

// 값 접근
let value = maxValue.Value  // int
```

### 3. Tag 사용

```fsharp
// Tag 생성
let counterTag = Tag<int>.Int("Counter")
let enableTag = Tag<bool>.Bool("Enable")

// TagBuilders 사용
open TagBuilders

let tempTag = double "Temperature"
let msgTag = string "Message"
```

### 4. Memory 타입 안전 접근

```fsharp
open Ev2.Cpu.Runtime

let memory = OptimizedMemory()

// 제네릭 변수 선언
memory.DeclareTyped<int>("Counter", MemoryArea.Local, 0)
memory.DeclareTyped<bool>("Enable", MemoryArea.Input, false)

// 타입 안전 Get/Set
let count = memory.GetTyped<int>("Counter")  // int
memory.SetTyped<int>("Counter", count + 1)

// Tag 기반 접근
let tag = Tag<int>.Int("Counter")
let value = memory.GetByTag(tag)
memory.SetByTag(tag, value + 1)

// Variable 기반 접근
let counter = Variable<int>("Counter")
let value = memory.GetByVariable(counter)
memory.SetByVariable(counter, value + 1)

// 안전한 조회 (Option)
match memory.TryGetTyped<int>("UnknownVar") with
| Some value -> printfn "Value: %d" value
| None -> printfn "Variable not found"
```

### 5. Storage 사용

```fsharp
// Storage 생성
let storage = Storage()

// 변수 추가
let counter = Variable<int>("Counter", 0)
storage.AddTyped(counter)

// 타입 안전 조회
match storage.GetTyped<int>("Counter") with
| Some var -> printfn "Counter: %d" var.TValue
| None -> printfn "Not found"

// 모든 int 변수 조회
let intVars = storage.GetAllTyped<int>()
for var in intVars do
    printfn "%s = %d" var.Name var.TValue
```

### 6. DsTag와의 상호 변환

```fsharp
open TagConversion

// DsTag → Tag<'T>
let dsTag = DsTag.Create("Counter", DsDataType.TInt)
match tryFromDsTag<int> dsTag with
| Some tag -> printfn "Converted to Tag<int>"
| None -> printfn "Type mismatch"

// Tag<'T> → DsTag
let tag = Tag<int>.Int("Counter")
let dsTag = toDsTag tag
```

## 주요 특징

### 1. 타입 안전성
```fsharp
// 컴파일 타임 타입 체크
let counter = Variable<int>("Counter", 0)
counter.Value <- 42        // OK
// counter.Value <- "text" // 컴파일 에러!
```

### 2. 기존 시스템과 호환
```fsharp
// DsDataType 시스템 계속 사용 가능
let dsTag = DsTag.Create("Old", DsDataType.TBool)
let dsExpr = DsExpr.Const(42, DsDataType.TInt)

// 새로운 제네릭 시스템도 사용 가능
let newTag = Tag<bool>.Bool("New")
let newVar = Variable<int>("Count", 0)
```

### 3. IntelliSense 지원
```fsharp
let counter = Variable<int>("Counter", 0)
counter.  // IntelliSense에서 Value: int 표시
```

## 마이그레이션 가이드

기존 코드는 수정 없이 계속 작동합니다. 새로운 코드에서 제네릭 타입을 선택적으로 사용하세요.

### Before (기존)
```fsharp
let memory = OptimizedMemory()
memory.DeclareVariable("Counter", DsDataType.TInt, MemoryArea.Local)
let value = memory.Get("Counter") :?> int
memory.Set("Counter", box (value + 1))
```

### After (제네릭)
```fsharp
let memory = OptimizedMemory()
memory.DeclareTyped<int>("Counter", MemoryArea.Local, 0)
let value = memory.GetTyped<int>("Counter")
memory.SetTyped("Counter", value + 1)
```

## 제한사항

1. **DsExpr는 여전히 obj 기반** - AST 구조는 변경되지 않음
2. **평가기는 기존 방식 유지** - ExprEvaluator, StmtEvaluator는 수정 안 됨
3. **제네릭은 선택적** - 기존 코드 수정 불필요

## 참고

- Ev2.Gen.FS의 LS.Interface.fs, LS.Var.fs, LS.Operator.fs 참조
- 제네릭 타입 시스템은 코드 생성 및 정적 분석에 유용합니다
