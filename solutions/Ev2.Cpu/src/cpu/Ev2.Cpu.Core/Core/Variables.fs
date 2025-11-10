namespace Ev2.Cpu.Core

open System
open System.Collections.Generic

// ─────────────────────────────────────────────────────────────────────
// 제네릭 변수 및 리터럴 구현 (Generic Variable & Literal Implementation)
// ─────────────────────────────────────────────────────────────────────
// Ev2.Gen.FS의 변수 시스템을 Ev2.Cpu.Core에 통합
// F# .NET 네이티브 타입을 사용한 타입 안전 변수 관리
// ─────────────────────────────────────────────────────────────────────

/// <summary>제네릭 리터럴 (상수값)</summary>
/// <remarks>
/// 컴파일 시점에 타입이 결정되는 상수값
/// 예: Literal&lt;int&gt;(42), Literal&lt;bool&gt;(true)
/// </remarks>
type Literal<'T>(value:'T) =
    member x.DataType = typeof<'T>
    member val Value = value with get, set

    interface ILiteral<'T> with
        member x.DataType = x.DataType
        member x.Value
            with get() = box x.Value
            and set(v:obj) = x.Value <- (v :?> 'T)
        member x.TValue = x.Value

/// <summary>변수 기본 클래스 (추상)</summary>
/// <remarks>
/// Variable과 다른 변수 타입들의 공통 기능 제공
/// 이름, 타입, 초기값 등 메타데이터 관리
/// </remarks>
[<AbstractClass>]
type VarBase<'T>(name:string, ?varType:VarType, ?initValue:'T) =
    member x.Name = name
    member x.DataType = typeof<'T>
    member x.VarType = defaultArg varType VarType.Var
    member x.InitValue = initValue
    member val Comment = null:string with get, set

    interface IVariable<'T> with
        member x.Name = x.Name
        member x.DataType = x.DataType
        member x.VarType = x.VarType
        member x.Value
            with get() = failwith "Abstract VarBase - Value not implemented"
            and set v = failwith "Abstract VarBase - Value not implemented"
        member x.TValue = failwith "Abstract VarBase - TValue not implemented"

    interface IInitValueProvider with
        member x.InitValue = x.InitValue |> Option.map box

/// <summary>제네릭 변수</summary>
/// <remarks>
/// 런타임에 값을 읽고 쓸 수 있는 변수
/// 예: Variable&lt;int&gt;("Counter", 0), Variable&lt;string&gt;("Message")
/// </remarks>
type Variable<'T>(name:string, ?value:'T, ?varType:VarType) =
    inherit VarBase<'T>(name, ?varType=varType)

    /// 매개변수 없는 생성자 (디폴트 이름)
    new() = Variable<'T>(null)

    /// 현재 값 (읽기/쓰기 가능)
    member val Value = defaultArg value Unchecked.defaultof<'T> with get, set

    interface IVariable<'T> with
        member x.Value
            with get() = box x.Value
            and set(v:obj) = x.Value <- (v :?> 'T)
        member x.TValue = x.Value

    /// Retain 메모리 플래그 (전원 차단 시 값 유지)
    member val Retain = false with get, set

    /// PLC 메모리 주소 (옵션)
    member val Address = null:string with get, set

    /// HMI 노출 플래그
    member val Hmi = false with get, set

    /// EIP/OPC UA 노출 플래그
    member val Eip = false with get, set

/// <summary>변수 저장소 (딕셔너리)</summary>
/// <remarks>
/// 변수 이름을 키로 하는 IVariable 컬렉션
/// 대소문자 구분 없음 (OrdinalIgnoreCase)
/// </remarks>
type Storage() =
    inherit Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)

    /// <summary>변수 시퀀스로부터 Storage 생성</summary>
    static member Create(variables:IVariable seq) =
        let storage = Storage()
        for var in variables do
            storage.Add(var.Name, var)
        storage

/// 변수 빌더 헬퍼
module VariableBuilders =
    /// bool 변수 생성
    let bool name = Variable<bool>(name)

    /// int 변수 생성
    let int name = Variable<int>(name)

    /// double 변수 생성
    let double name = Variable<double>(name)

    /// string 변수 생성
    let string name = Variable<string>(name)

    /// 초기값을 가진 bool 변수
    let boolWith name value = Variable<bool>(name, value)

    /// 초기값을 가진 int 변수
    let intWith name value = Variable<int>(name, value)

    /// 초기값을 가진 double 변수
    let doubleWith name value = Variable<double>(name, value)

    /// 초기값을 가진 string 변수
    let stringWith name value = Variable<string>(name, value)
