namespace Ev2.Cpu.Core

open System

// ─────────────────────────────────────────────────────────────────────
// 제네릭 타입 인터페이스 (Generic Type Interfaces)
// ─────────────────────────────────────────────────────────────────────
// Ev2.Gen.FS의 타입 시스템을 Ev2.Cpu.Core에 통합
// 기존 DsDataType 시스템과 함께 사용 가능하도록 설계
// ─────────────────────────────────────────────────────────────────────

/// 타입 정보를 가진 객체
[<AllowNullLiteral>]
type IWithType =
    abstract DataType : Type

/// 값을 가진 객체 (obj 타입)
[<AllowNullLiteral>]
type IWithValue =
    abstract Value : obj with get, set

/// 표현식 기본 인터페이스
[<AllowNullLiteral>]
type IExpression =
    inherit IWithType
    inherit IWithValue

/// 타입이 지정된 값
[<AllowNullLiteral>]
type TValue<'T> =
    abstract TValue : 'T

/// 제네릭 표현식
[<AllowNullLiteral>]
type IExpression<'T> =
    inherit IExpression
    inherit TValue<'T>

/// 터미널 (기본값)
[<AllowNullLiteral>]
type ITerminal =
    inherit IExpression

/// 제네릭 터미널
[<AllowNullLiteral>]
type ITerminal<'T> =
    inherit ITerminal
    inherit IExpression<'T>

/// 변수 종류
type VarType =
    | VarUndefined
    | Var
    | VarConstant
    | VarInput
    | VarOutput
    | VarInOut
    | VarReturn
    | VarExternal
    | VarExternalConstant
    | VarGlobal
    | VarGlobalConstant

/// 변수 인터페이스
[<AllowNullLiteral>]
type IVariable =
    inherit ITerminal
    abstract Name : string
    abstract VarType : VarType

/// 제네릭 변수 인터페이스
[<AllowNullLiteral>]
type IVariable<'T> =
    inherit IVariable
    inherit ITerminal<'T>

/// 리터럴 인터페이스
[<AllowNullLiteral>]
type ILiteral =
    inherit ITerminal

/// 제네릭 리터럴 인터페이스
[<AllowNullLiteral>]
type ILiteral<'T> =
    inherit ILiteral
    inherit ITerminal<'T>

/// 초기값 제공자
[<AllowNullLiteral>]
type IInitValueProvider =
    abstract InitValue : obj option

/// 스토리지 인터페이스
[<AllowNullLiteral>]
type IStorage =
    abstract IVariables : IVariable[]
