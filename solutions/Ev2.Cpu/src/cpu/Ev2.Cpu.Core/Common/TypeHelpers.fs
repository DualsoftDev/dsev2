namespace Ev2.Cpu.Core.Common

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Type Helpers and Pattern Matching Utilities
// ═════════════════════════════════════════════════════════════════════════════
// Runtime 함수들에서 반복적으로 나타나는 타입 매칭 패턴을 추상화합니다.
// TypeConverter는 타입 변환을, TypeHelpers는 타입 매칭 패턴을 제공합니다.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// 부동소수점 비교를 위한 엡실론 값
/// </summary>
module Constants =
    let Epsilon = 1e-10

/// <summary>
/// 두 값의 타입을 분석한 결과
/// </summary>
type BinaryTypeMatch =
    | BothInt of int * int
    | BothDouble of float * float
    | BothBool of bool * bool
    | BothString of string * string
    | IntAndDouble of int * float
    | DoubleAndInt of float * int
    | Incompatible of obj * obj

/// <summary>
/// 단일 값의 타입을 분석한 결과
/// </summary>
type UnaryTypeMatch =
    | MatchInt of int
    | MatchDouble of float
    | MatchBool of bool
    | MatchString of string
    | MatchNull
    | MatchOther of obj

/// <summary>
/// 이항 연산을 위한 타입 매칭 유틸리티
/// </summary>
module BinaryTypeMatcher =

    /// <summary>
    /// 두 값의 타입을 분석하여 BinaryTypeMatch 반환
    /// </summary>
    let analyze (a: obj) (b: obj) : BinaryTypeMatch =
        match a, b with
        | (:? int as i1), (:? int as i2) -> BothInt (i1, i2)
        | (:? float as d1), (:? float as d2) -> BothDouble (d1, d2)
        | (:? bool as b1), (:? bool as b2) -> BothBool (b1, b2)
        | (:? string as s1), (:? string as s2) -> BothString (s1, s2)
        | (:? int as i), (:? float as d) -> IntAndDouble (i, d)
        | (:? float as d), (:? int as i) -> DoubleAndInt (d, i)
        | _ -> Incompatible (a, b)

    /// <summary>
    /// 수치 타입 매칭 (int, double만)
    /// </summary>
    let analyzeNumeric (a: obj) (b: obj) : BinaryTypeMatch option =
        match analyze a b with
        | BothInt _ as m -> Some m
        | BothDouble _ as m -> Some m
        | IntAndDouble _ as m -> Some m
        | DoubleAndInt _ as m -> Some m
        | _ -> None

    /// <summary>
    /// 수치 값 추출 (공통 타입으로 변환)
    /// </summary>
    let extractNumeric (a: obj) (b: obj) : (float * float) option =
        match analyze a b with
        | BothInt (i1, i2) -> Some (float i1, float i2)
        | BothDouble (d1, d2) -> Some (d1, d2)
        | IntAndDouble (i, d) -> Some (float i, d)
        | DoubleAndInt (d, i) -> Some (d, float i)
        | _ -> None

    /// <summary>
    /// 비교 가능한 값으로 변환 (string 포함)
    /// </summary>
    let extractComparable (a: obj) (b: obj) : Choice<(float * float), (string * string), (bool * bool)> option =
        match analyze a b with
        | BothInt (i1, i2) -> Some (Choice1Of3 (float i1, float i2))
        | BothDouble (d1, d2) -> Some (Choice1Of3 (d1, d2))
        | IntAndDouble (i, d) -> Some (Choice1Of3 (float i, d))
        | DoubleAndInt (d, i) -> Some (Choice1Of3 (d, float i))
        | BothString (s1, s2) -> Some (Choice2Of3 (s1, s2))
        | BothBool (b1, b2) -> Some (Choice3Of3 (b1, b2))
        | _ -> None

/// <summary>
/// 단항 연산을 위한 타입 매칭 유틸리티
/// </summary>
module UnaryTypeMatcher =

    /// <summary>
    /// 값의 타입을 분석하여 UnaryTypeMatch 반환
    /// </summary>
    let analyze (value: obj) : UnaryTypeMatch =
        if isNull value then MatchNull
        else
            match value with
            | :? int as i -> MatchInt i
            | :? float as d -> MatchDouble d
            | :? bool as b -> MatchBool b
            | :? string as s -> MatchString s
            | other -> MatchOther other

    /// <summary>
    /// 수치 값 추출 (int, double만)
    /// </summary>
    let extractNumeric (value: obj) : float option =
        match analyze value with
        | MatchInt i -> Some (float i)
        | MatchDouble d -> Some d
        | _ -> None

    /// <summary>
    /// int 값 추출 시도
    /// </summary>
    let tryGetInt (value: obj) : int option =
        match analyze value with
        | MatchInt i -> Some i
        | _ -> None

    /// <summary>
    /// double 값 추출 시도
    /// </summary>
    let tryGetDouble (value: obj) : float option =
        match analyze value with
        | MatchDouble d -> Some d
        | _ -> None

    /// <summary>
    /// bool 값 추출 시도
    /// </summary>
    let tryGetBool (value: obj) : bool option =
        match analyze value with
        | MatchBool b -> Some b
        | _ -> None

    /// <summary>
    /// string 값 추출 시도
    /// </summary>
    let tryGetString (value: obj) : string option =
        match analyze value with
        | MatchString s -> Some s
        | _ -> None

/// <summary>
/// 이항 산술 연산을 위한 고차 함수들
/// ComparisonFunctions, ArithmeticFunctions에서 반복되는 패턴 제거용
/// </summary>
module BinaryOperators =

    /// <summary>
    /// 수치 이항 연산 헬퍼 (int 우선, 필요시 double로 승격)
    /// </summary>
    let applyNumeric
        (intOp: int -> int -> 'T)       // int 연산
        (doubleOp: float -> float -> 'T) // double 연산
        (a: obj)
        (b: obj) : 'T option =
        match BinaryTypeMatcher.analyze a b with
        | BothInt (i1, i2) -> Some (intOp i1 i2)
        | BothDouble (d1, d2) -> Some (doubleOp d1 d2)
        | IntAndDouble (i, d) -> Some (doubleOp (float i) d)
        | DoubleAndInt (d, i) -> Some (doubleOp d (float i))
        | _ -> None

    /// <summary>
    /// 수치 이항 연산 헬퍼 (항상 double로 변환)
    /// </summary>
    let applyNumericAsDouble
        (op: float -> float -> 'T)
        (a: obj)
        (b: obj) : 'T option =
        BinaryTypeMatcher.extractNumeric a b
        |> Option.map (fun (d1, d2) -> op d1 d2)

    /// <summary>
    /// 수치 이항 연산 (박싱된 결과 반환)
    /// </summary>
    let applyNumericBoxed
        (intOp: int -> int -> obj)
        (doubleOp: float -> float -> obj)
        (a: obj)
        (b: obj) : obj option =
        applyNumeric intOp doubleOp a b

/// <summary>
/// 비교 연산을 위한 고차 함수들
/// </summary>
module ComparisonOperators =

    /// <summary>
    /// 수치 비교 (int/double 자동 승격)
    /// </summary>
    let compareNumeric
        (op: float -> float -> bool)
        (a: obj)
        (b: obj) : bool option =
        BinaryTypeMatcher.extractNumeric a b
        |> Option.map (fun (d1, d2) -> op d1 d2)

    /// <summary>
    /// 문자열 비교
    /// </summary>
    let compareString
        (op: string -> string -> bool)
        (a: obj)
        (b: obj) : bool option =
        match BinaryTypeMatcher.analyze a b with
        | BothString (s1, s2) -> Some (op s1 s2)
        | _ -> None

    /// <summary>
    /// 동등성 비교 (모든 타입 지원, float는 epsilon 사용)
    /// </summary>
    let equals (a: obj) (b: obj) : bool =
        if isNull a && isNull b then true
        elif isNull a || isNull b then false
        else
            match BinaryTypeMatcher.analyze a b with
            | BothInt (i1, i2) -> i1 = i2
            | BothDouble (d1, d2) -> Math.Abs(d1 - d2) < Constants.Epsilon
            | BothBool (b1, b2) -> b1 = b2
            | BothString (s1, s2) -> s1 = s2
            | IntAndDouble (i, d) -> Math.Abs(float i - d) < Constants.Epsilon
            | DoubleAndInt (d, i) -> Math.Abs(d - float i) < Constants.Epsilon
            | Incompatible _ -> false

    /// <summary>
    /// 대소 비교 (수치, 문자열 지원)
    /// </summary>
    let lessThan (a: obj) (b: obj) : bool =
        match BinaryTypeMatcher.analyze a b with
        | BothInt (i1, i2) -> i1 < i2
        | BothDouble (d1, d2) -> d1 < d2
        | IntAndDouble (i, d) -> float i < d
        | DoubleAndInt (d, i) -> d < float i
        | BothString (s1, s2) -> s1 < s2
        | _ -> failwithf "Cannot compare ( < ) between %A and %A" a b

/// <summary>
/// 타입 강제 변환 (Coercion) 유틸리티
/// </summary>
module TypeCoercion =

    /// <summary>
    /// 두 값을 공통 타입으로 강제 변환 (수치 타입만)
    /// int + double → double + double
    /// </summary>
    let coerceToCommonNumeric (a: obj) (b: obj) : (obj * obj) option =
        match BinaryTypeMatcher.analyze a b with
        | BothInt _ -> Some (a, b)
        | BothDouble _ -> Some (a, b)
        | IntAndDouble (i, d) -> Some (box (float i), box d)
        | DoubleAndInt (d, i) -> Some (box d, box (float i))
        | _ -> None

    /// <summary>
    /// 수치 타입을 double로 강제 변환
    /// </summary>
    let toDouble (value: obj) : float option =
        match UnaryTypeMatcher.analyze value with
        | MatchInt i -> Some (float i)
        | MatchDouble d -> Some d
        | _ -> None

    /// <summary>
    /// 수치 타입을 int로 강제 변환 (double은 반올림)
    /// </summary>
    let toInt (value: obj) : int option =
        match UnaryTypeMatcher.analyze value with
        | MatchInt i -> Some i
        | MatchDouble d -> Some (int (Math.Round d))
        | _ -> None

/// <summary>
/// Null 안전 연산 유틸리티
/// </summary>
module NullSafe =

    /// <summary>
    /// Null 체크를 포함한 이항 연산
    /// </summary>
    let apply2 (f: obj -> obj -> 'T) (defaultValue: 'T) (a: obj) (b: obj) : 'T =
        if isNull a || isNull b then defaultValue
        else f a b

    /// <summary>
    /// Null 체크를 포함한 단항 연산
    /// </summary>
    let apply1 (f: obj -> 'T) (defaultValue: 'T) (value: obj) : 'T =
        if isNull value then defaultValue
        else f value

    /// <summary>
    /// Null을 기본값으로 대체
    /// </summary>
    let withDefault (defaultValue: obj) (value: obj) : obj =
        if isNull value then defaultValue else value

/// <summary>
/// 기본 타입 검사 유틸리티
/// </summary>
module TypeChecking =

    /// <summary>
    /// 값이 특정 .NET 타입인지 검증
    /// </summary>
    let asType<'T> (name: string) (value: obj) : Result<'T, string> =
        match value with
        | :? 'T as typed -> Ok typed
        | _ ->
            let expectedType = typeof<'T>.Name
            let actualType = if isNull value then "null" else value.GetType().Name
            Error (sprintf "%s must be of type %s but got %s" name expectedType actualType)

    /// <summary>
    /// 값이 수치 타입인지 검증
    /// </summary>
    let requireNumeric (value: obj) : Result<obj, string> =
        match UnaryTypeMatcher.analyze value with
        | MatchInt _ | MatchDouble _ -> Ok value
        | _ ->
            let typeName = if isNull value then "null" else value.GetType().Name
            Error (sprintf "Expected numeric value but got %s" typeName)

    /// <summary>
    /// 값이 null이 아닌지 검증
    /// </summary>
    let requireNonNull (name: string) (value: obj) : Result<obj, string> =
        if isNull value then
            Error (sprintf "%s cannot be null" name)
        else
            Ok value
