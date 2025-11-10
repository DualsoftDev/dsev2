namespace Ev2.Cpu.Core.Common

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Common Validation Infrastructure
// ═════════════════════════════════════════════════════════════════════════════
// 모든 모듈에서 사용할 수 있는 공통 검증 로직을 제공합니다.
// Identifier 검증, 타입 검증, 범위 검증 등의 반복적인 패턴을 추상화합니다.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// IEC 61131-3 표준 예약어 목록
/// </summary>
module ReservedKeywords =
    let Keywords : Set<string> =
        Set.ofList [
            // Control structures
            "IF"; "THEN"; "ELSE"; "END_IF"; "ELSIF"
            "FOR"; "TO"; "BY"; "DO"; "END_FOR"
            "WHILE"; "END_WHILE"
            "REPEAT"; "UNTIL"; "END_REPEAT"
            "CASE"; "OF"; "END_CASE"
            "RETURN"; "EXIT"

            // Variable declarations
            "VAR"; "END_VAR"
            "VAR_INPUT"; "VAR_OUTPUT"; "VAR_IN_OUT"
            "VAR_TEMP"; "VAR_GLOBAL"; "VAR_EXTERNAL"
            "VAR_ACCESS"; "CONSTANT"; "RETAIN"; "NON_RETAIN"

            // Program organization units
            "FUNCTION"; "END_FUNCTION"
            "FUNCTION_BLOCK"; "END_FUNCTION_BLOCK"
            "PROGRAM"; "END_PROGRAM"
            "TYPE"; "END_TYPE"
            "STRUCT"; "END_STRUCT"

            // Data types
            "BOOL"; "SINT"; "INT"; "DINT"; "LINT"
            "USINT"; "UINT"; "UDINT"; "ULINT"
            "REAL"; "LREAL"
            "TIME"; "DATE"; "TIME_OF_DAY"; "TOD"
            "DATE_AND_TIME"; "DT"
            "STRING"; "WSTRING"
            "ARRAY"; "POINTER"; "REFERENCE"

            // Literals and constants
            "TRUE"; "FALSE"; "NULL"

            // Operators
            "AND"; "OR"; "NOT"; "XOR"
            "MOD"

            // Others
            "AT"; "WITH"; "THIS"; "SUPER"
        ]

    /// <summary>문자열이 예약어인지 확인 (대소문자 무시)</summary>
    let isReserved (name: string) =
        if String.IsNullOrWhiteSpace name then false
        else Set.contains (name.ToUpperInvariant()) Keywords

/// <summary>
/// 식별자(Identifier) 검증 유틸리티
/// PLC 변수명, 함수명, 블록명 등의 명명 규칙 검증
/// </summary>
module IdentifierValidation =

    /// <summary>
    /// 식별자가 유효한지 확인
    /// 규칙: 첫 글자는 문자 또는 '_', 나머지는 문자/숫자/'_'
    /// </summary>
    let isValid (name: string) : bool =
        if String.IsNullOrWhiteSpace name then false
        else
            let firstChar = name.[0]
            let isFirstValid = Char.IsLetter(firstChar) || firstChar = '_'
            let isRestValid =
                name
                |> Seq.skip 1
                |> Seq.forall (fun c -> Char.IsLetterOrDigit(c) || c = '_')
            isFirstValid && isRestValid

    /// <summary>
    /// 식별자 검증 (Result 반환)
    /// </summary>
    let validate (kind: string) (name: string) : Result<unit, string> =
        if String.IsNullOrWhiteSpace name then
            Error (sprintf "%s name cannot be empty" kind)
        elif not (isValid name) then
            Error (sprintf "%s name '%s' is not a valid identifier" kind name)
        elif ReservedKeywords.isReserved name then
            Error (sprintf "%s name '%s' is a reserved keyword" kind name)
        else
            Ok ()

    /// <summary>
    /// 식별자 검증 (StructuredError 반환)
    /// </summary>
    let validateStructured (kind: string) (name: string) : Result<unit, StructuredError> =
        let normalized = if String.IsNullOrWhiteSpace name then "<empty>" else name
        if String.IsNullOrWhiteSpace name then
            StructuredError.createWithPath
                (sprintf "%s.NameEmpty" kind)
                (sprintf "%s name cannot be empty" kind)
                ["name"; normalized]
            |> Error
        elif not (isValid name) then
            StructuredError.createWithPath
                (sprintf "%s.NameInvalid" kind)
                (sprintf "%s name '%s' is not a valid identifier" kind name)
                ["name"; normalized]
            |> Error
        elif ReservedKeywords.isReserved name then
            StructuredError.createWithPath
                (sprintf "%s.NameReserved" kind)
                (sprintf "%s name '%s' is a reserved keyword" kind name)
                ["name"; normalized]
            |> Error
        else
            Ok ()

    /// <summary>
    /// 식별자 리스트가 모두 유효한지 검증
    /// </summary>
    let validateMany (kind: string) (names: string list) : Result<unit, string list> =
        let errors =
            names
            |> List.choose (fun name ->
                match validate kind name with
                | Ok () -> None
                | Error e -> Some e)
        if List.isEmpty errors then Ok ()
        else Error errors

    /// <summary>
    /// 식별자 리스트에 중복이 없는지 검증
    /// </summary>
    let validateUnique (kind: string) (names: string list) : Result<unit, string> =
        let duplicates =
            names
            |> List.groupBy id
            |> List.filter (fun (_, instances) -> List.length instances > 1)
            |> List.map fst

        if List.isEmpty duplicates then Ok ()
        else Error (sprintf "Duplicate %s names found: %s" kind (String.concat ", " duplicates))

/// <summary>
/// 범위 검증 유틸리티
/// </summary>
module RangeValidation =

    /// <summary>
    /// 값이 범위 내에 있는지 검증
    /// </summary>
    let validateRange<'T when 'T : comparison>
        (min: 'T)
        (max: 'T)
        (value: 'T)
        (name: string) : Result<unit, string> =
        if value < min || value > max then
            Error (sprintf "%s must be between %A and %A, got %A" name min max value)
        else
            Ok ()

    /// <summary>
    /// 값이 최소값 이상인지 검증
    /// </summary>
    let validateMin<'T when 'T : comparison>
        (min: 'T)
        (value: 'T)
        (name: string) : Result<unit, string> =
        if value < min then
            Error (sprintf "%s must be at least %A, got %A" name min value)
        else
            Ok ()

    /// <summary>
    /// 값이 최대값 이하인지 검증
    /// </summary>
    let validateMax<'T when 'T : comparison>
        (max: 'T)
        (value: 'T)
        (name: string) : Result<unit, string> =
        if value > max then
            Error (sprintf "%s must be at most %A, got %A" name max value)
        else
            Ok ()

    /// <summary>
    /// 값이 양수인지 검증
    /// </summary>
    let validatePositive
        (value: int)
        (name: string) : Result<unit, string> =
        validateMin 1 value name

    /// <summary>
    /// 값이 음이 아닌지 검증
    /// </summary>
    let validateNonNegative
        (value: int)
        (name: string) : Result<unit, string> =
        validateMin 0 value name

/// <summary>
/// 문자열 검증 유틸리티
/// </summary>
module StringValidation =

    /// <summary>
    /// 문자열이 비어있지 않은지 검증
    /// </summary>
    let validateNotEmpty (name: string) (value: string) : Result<unit, string> =
        if String.IsNullOrWhiteSpace value then
            Error (sprintf "%s cannot be empty" name)
        else
            Ok ()

    /// <summary>
    /// 문자열 길이가 범위 내에 있는지 검증
    /// </summary>
    let validateLength
        (minLen: int)
        (maxLen: int)
        (name: string)
        (value: string) : Result<unit, string> =
        if isNull value then
            Error (sprintf "%s cannot be null" name)
        elif value.Length < minLen then
            Error (sprintf "%s must be at least %d characters, got %d" name minLen value.Length)
        elif value.Length > maxLen then
            Error (sprintf "%s must be at most %d characters, got %d" name maxLen value.Length)
        else
            Ok ()

    /// <summary>
    /// 문자열이 패턴과 일치하는지 검증
    /// </summary>
    let validatePattern
        (pattern: string)
        (name: string)
        (value: string) : Result<unit, string> =
        if isNull value then
            Error (sprintf "%s cannot be null" name)
        else
            let regex = System.Text.RegularExpressions.Regex(pattern)
            if regex.IsMatch(value) then Ok ()
            else Error (sprintf "%s does not match required pattern" name)

/// <summary>
/// 컬렉션 검증 유틸리티
/// </summary>
module CollectionValidation =

    /// <summary>
    /// 리스트가 비어있지 않은지 검증
    /// </summary>
    let validateNotEmpty<'T> (name: string) (list: 'T list) : Result<unit, string> =
        if List.isEmpty list then
            Error (sprintf "%s cannot be empty" name)
        else
            Ok ()

    /// <summary>
    /// 리스트 크기가 범위 내에 있는지 검증
    /// </summary>
    let validateCount<'T>
        (minCount: int)
        (maxCount: int)
        (name: string)
        (list: 'T list) : Result<unit, string> =
        let count = List.length list
        if count < minCount then
            Error (sprintf "%s must have at least %d items, got %d" name minCount count)
        elif count > maxCount then
            Error (sprintf "%s must have at most %d items, got %d" name maxCount count)
        else
            Ok ()

    /// <summary>
    /// 리스트의 각 요소에 검증 함수 적용
    /// </summary>
    let validateEach<'T>
        (validator: 'T -> Result<unit, string>)
        (list: 'T list) : Result<unit, string list> =
        let errors =
            list
            |> List.mapi (fun i item ->
                match validator item with
                | Ok () -> None
                | Error e -> Some (sprintf "[%d]: %s" i e))
            |> List.choose id

        if List.isEmpty errors then Ok ()
        else Error errors

/// <summary>
/// 복합 검증 유틸리티
/// 여러 검증을 조합하여 실행
/// </summary>
module CompositeValidation =

    /// <summary>
    /// 여러 검증을 순차 실행 (첫 번째 실패 시 중단)
    /// </summary>
    let validateAll (validations: (unit -> Result<unit, 'E>) list) : Result<unit, 'E> =
        let rec loop remaining =
            match remaining with
            | [] -> Ok ()
            | v :: rest ->
                match v () with
                | Ok () -> loop rest
                | Error e -> Error e
        loop validations

    /// <summary>
    /// 여러 검증을 모두 실행 (모든 에러 수집)
    /// </summary>
    let validateAllCollectErrors (validations: (unit -> Result<unit, 'E>) list) : Result<unit, 'E list> =
        let errors =
            validations
            |> List.choose (fun v ->
                match v () with
                | Ok () -> None
                | Error e -> Some e)

        if List.isEmpty errors then Ok ()
        else Error errors

    /// <summary>
    /// 여러 검증을 실행하고 ValidationResult로 반환
    /// </summary>
    let validateAllAsValidationResult
        (validations: (unit -> Result<unit, 'E>) list) : ValidationResult<unit, 'E> =
        let errors =
            validations
            |> List.choose (fun v ->
                match v () with
                | Ok () -> None
                | Error e -> Some e)

        if List.isEmpty errors then Valid ()
        else Invalid errors

/// <summary>
/// 타입 검증 유틸리티
/// </summary>
module TypeValidation =

    /// <summary>
    /// 값이 특정 .NET 타입인지 검증
    /// </summary>
    let validateType<'T> (name: string) (value: obj) : Result<'T, string> =
        match value with
        | :? 'T as typed -> Ok typed
        | _ ->
            let expectedType = typeof<'T>.Name
            let actualType = if isNull value then "null" else value.GetType().Name
            Error (sprintf "%s must be of type %s but got %s" name expectedType actualType)

    /// <summary>
    /// 값이 수치 타입(Int 또는 Double)인지 검증
    /// </summary>
    let validateNumericType (name: string) (value: obj) : Result<obj, string> =
        if isNull value then
            Error (sprintf "%s cannot be null" name)
        else
            match value with
            | :? int | :? float -> Ok value
            | _ -> Error (sprintf "%s must be a numeric type (int or double)" name)

/// <summary>
/// 빌더 패턴을 위한 검증 헬퍼
/// </summary>
module ValidationBuilder =

    type ValidationBuilder() =
        member _.Return(x) = Ok x
        member _.ReturnFrom(x) = x
        member _.Bind(x, f) = Result.bind f x
        member _.Zero() = Ok ()

    /// <summary>
    /// 검증 빌더 인스턴스
    /// </summary>
    let validation = ValidationBuilder()

    /// <example>
    /// validation {
    ///     do! validateNotEmpty "name" name
    ///     do! validateRange 0 100 value "value"
    ///     return ()
    /// }
    /// </example>
