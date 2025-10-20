# POU 타입 확장 전략

## 결론: 하이브리드 접근법 추천

두 방식의 장점을 결합한 **하이브리드 접근법**을 권장합니다.

## 추천 방안: Discriminated Union + Type Extension

```fsharp
// 1. 기본 DU 타입 유지
type LdElement =
    | Contact of LdContact
    | Coil of LdCoil
    | FunctionBlock of LdFunctionBlock
    // 확장 케이스를 직접 추가
    | CompareContact of LdCompareContact
    | Timer of LdTimer
    | Counter of LdCounter
    | MathBlock of LdMathBlock
    // 향후 확장용 (플러그인)
    | Custom of ILdCustomElement
```

### 장점:
1. ✅ **단일 타입**: PouBody와 PouBodyEx 구분 불필요
2. ✅ **패턴 매칭**: F#의 강력한 기능 활용
3. ✅ **확장성**: Custom 케이스로 플러그인 지원
4. ✅ **타입 안전**: 컴파일 타임 검증
5. ✅ **간결함**: 중복 최소화

### 단점:
1. ❌ **핵심 파일 수정**: 확장 시 IR.POUs.fs 수정 필요
2. ❌ **재컴파일**: 새 케이스 추가 시 전체 재컴파일

---

## 구현 방법

### Option 1: 모든 케이스를 Main 타입에 포함 (추천)

```fsharp
// IR.POUs.fs - 하나의 파일에 모두
type LdElement =
    // 기본 (항상 지원)
    | Contact of LdContact
    | Coil of LdCoil
    | FunctionBlock of LdFunctionBlock

    // 확장 (선택적 지원, 하지만 타입에 포함)
    | CompareContact of LdCompareContact
    | Timer of LdTimer
    | Counter of LdCounter
    | MathBlock of LdMathBlock

    // 플러그인 확장용
    | CustomElement of obj * string  // (data, typeName)
```

**장점:**
- 단일 타입으로 관리
- 패턴 매칭 완전 활용
- 확장이 일급 시민

**단점:**
- 핵심 파일이 커짐
- 모든 확장이 코어에 포함

### Option 2: Interface를 통한 플러그인 시스템

```fsharp
// 핵심 타입
type LdElement =
    | Contact of LdContact
    | Coil of LdCoil
    | FunctionBlock of LdFunctionBlock
    | Extension of ILdElement  // 확장 인터페이스

// 확장 인터페이스
type ILdElement =
    abstract member Id : string
    abstract member ElementType : string
    abstract member ToJson : unit -> string
    abstract member Validate : unit -> Result<unit, string>

// 플러그인에서 구현
type LdTimerExtension(id, timerType, instance, pt) =
    interface ILdElement with
        member _.Id = id
        member _.ElementType = "Timer"
        member _.ToJson() = ...
        member _.Validate() = ...
```

**장점:**
- 핵심 코드 수정 불필요
- 진정한 플러그인 시스템
- 확장 독립적 배포 가능

**단점:**
- 패턴 매칭 제한적
- 타입 안전성 감소
- 복잡도 증가

### Option 3: Active Patterns로 추상화

```fsharp
// 기본 DU
type LdElement =
    | Contact of LdContact
    | Coil of LdCoil
    | Timer of LdTimer
    | Custom of obj

// Active Pattern으로 추상화
let (|InputElement|OutputElement|LogicElement|) element =
    match element with
    | Contact _ -> InputElement
    | Coil _ -> OutputElement
    | Timer _ | Custom _ -> LogicElement

// 사용
match element with
| InputElement -> "Input processing"
| OutputElement -> "Output processing"
| LogicElement -> "Logic processing"
```

**장점:**
- 여러 케이스를 논리적으로 그룹화
- 기존 패턴 매칭 유지
- 추상화 레벨 조정 가능

**단점:**
- Active Pattern 복잡도
- 디버깅 어려움

---

## 실전 추천: Option 1 변형

현재 프로젝트에 가장 적합한 방식:

```fsharp
// IR.POUs.fs
namespace Ev2.Gen

type LdElement =
    // Core elements (필수)
    | Contact of LdContact
    | Coil of LdCoil
    | FunctionBlock of LdFunctionBlock

    // Extended elements (일반적으로 사용)
    | CompareContact of LdCompareContact
    | Timer of LdTimer
    | Counter of LdCounter
    | MathBlock of LdMathBlock

    // 향후를 위한 확장 포트
    member this.AsCustom() =
        match this with
        | Contact _ | Coil _ | FunctionBlock _ -> None
        | CompareContact c -> Some ("CompareContact", box c)
        | Timer t -> Some ("Timer", box t)
        | Counter c -> Some ("Counter", box c)
        | MathBlock m -> Some ("MathBlock", box m)
```

### 이유:

1. **PLC 코드 생성의 특성**
   - 확장이 자주 일어나지 않음
   - IEC 61131-3 표준은 안정적
   - 새 element 타입은 드물게 추가

2. **타입 안전성 우선**
   - 컴파일 타임 검증 중요
   - 런타임 에러 최소화
   - 명확한 계약

3. **개발 생산성**
   - 패턴 매칭으로 간결한 코드
   - IDE 지원 (IntelliSense)
   - 리팩토링 용이

4. **JSON 직렬화**
   - DU는 자동 태그 생성
   - 타입 정보 보존
   - 역직렬화 간단

---

## 마이그레이션 계획

현재 코드를 Option 1으로 변경하려면:

1. `IR.POUs.Extended.fs` 삭제
2. `IR.POUs.fs`에 확장 케이스 추가
3. `PouBodyEx` 제거, `PouBody` 하나로 통합
4. 예제 코드 수정

이렇게 하면:
- 더 간단한 API
- 더 나은 타입 안전성
- F# 관용구에 부합
- 유지보수 용이

---

## C# Interop 고려사항

C#에서 사용하려면:

```csharp
// C#에서의 사용
var element = ldElement as LdElement.Contact;
if (element != null)
{
    var contact = element.Item;
    Console.WriteLine(contact.VarName);
}

// 또는 pattern matching (C# 7+)
switch (ldElement)
{
    case LdElement.Contact c:
        Console.WriteLine(c.Item.VarName);
        break;
    case LdElement.Timer t:
        Console.WriteLine(t.Item.Instance);
        break;
}
```

DU가 C#에서도 충분히 사용 가능합니다.
