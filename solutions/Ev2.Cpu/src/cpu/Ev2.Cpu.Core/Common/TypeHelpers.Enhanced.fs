namespace Ev2.Cpu.Core

open System
open System.Globalization

// ═════════════════════════════════════════════════════════════════════
// 제네릭 타입 헬퍼 (Generic Type Helpers)
// ═════════════════════════════════════════════════════════════════════
// DsDataType enum을 System.Type으로 대체하기 위한 헬퍼 함수들
// 기존 DsDataType의 모든 기능을 System.Type 기반으로 제공
// ═════════════════════════════════════════════════════════════════════

/// <summary>타입 헬퍼 유틸리티</summary>
/// <remarks>
/// DsDataType을 System.Type으로 대체하기 위한 헬퍼 함수 제공
/// 지원 타입: bool, int, double, string (4개 기본 타입)
/// </remarks>
module TypeHelpers =

    // ─────────────────────────────────────────────────────────────────
    // 타입 검증 및 변환
    // ─────────────────────────────────────────────────────────────────

    /// <summary>지원되는 타입인지 검사</summary>
    let isSupportedType (t: Type) : bool =
        t = typeof<bool> || t = typeof<int> || t = typeof<double> || t = typeof<string>

    /// <summary>타입이 숫자 타입인지 검사 (산술 연산 가능)</summary>
    let isNumericType (t: Type) : bool =
        t = typeof<int> || t = typeof<double>

    /// <summary>타입 기본값 반환</summary>
    let getDefaultValue (t: Type) : obj =
        if t = typeof<bool> then box false
        elif t = typeof<int> then box 0
        elif t = typeof<double> then box 0.0
        elif t = typeof<string> then box ""
        else invalidArg "t" (sprintf "Unsupported type: %s" t.FullName)

    /// <summary>문자열에서 타입 파싱</summary>
    let tryParseTypeName (name: string) : Type option =
        if String.IsNullOrWhiteSpace name then None
        else
            match name.Trim().ToLowerInvariant() with
            | "bool" | "boolean" | "system.boolean" -> Some typeof<bool>
            | "int" | "int32" | "system.int32" -> Some typeof<int>
            | "double" | "float" | "real" | "system.double" -> Some typeof<double>
            | "string" | "text" | "system.string" -> Some typeof<string>
            | _ -> None

    /// <summary>문자열에서 타입 파싱 (실패 시 예외)</summary>
    let parseTypeName (name: string) : Type =
        match tryParseTypeName name with
        | Some t -> t
        | None -> invalidArg "name" (sprintf "Unsupported type name: %s" name)

    // ─────────────────────────────────────────────────────────────────
    // 타입 호환성 및 변환
    // ─────────────────────────────────────────────────────────────────

    /// <summary>타입 호환성 검사 (자동 타입 승격 규칙)</summary>
    /// <remarks>
    /// - 동일 타입: 항상 호환
    /// - Int → Double: 승격 허용
    /// - 기타: 호환 불가
    /// </remarks>
    let areTypesCompatible (target: Type) (source: Type) : bool =
        if target = source then true
        elif target = typeof<double> && source = typeof<int> then true  // int → double 승격
        else false

    /// <summary>값의 타입 검증 (타입 안전성 보장)</summary>
    /// <param name="expectedType">기대하는 타입</param>
    /// <param name="value">검증할 값</param>
    /// <returns>검증된 값 (필요 시 타입 변환)</returns>
    let validateType (expectedType: Type) (value: obj) : obj =
        if isNull value then
            invalidArg "value" "Value cannot be null"
        let actualType = value.GetType()
        if actualType = expectedType then
            value
        // Int → Double 승격
        elif actualType = typeof<int> && expectedType = typeof<double> then
            box (double (unbox<int> value))
        else
            failwithf "Type mismatch: expected %s but got %s" expectedType.FullName actualType.FullName

    // ─────────────────────────────────────────────────────────────────
    // 타입 변환 (TypeConverter 모듈 대체)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>객체를 bool로 변환</summary>
    let toBool (value: obj) : bool =
        if isNull value then false
        else
            match value with
            | :? bool as b -> b
            | :? int as i -> i <> 0
            | :? double as d -> not (Double.IsNaN d) && d <> 0.0
            | :? single as f -> not (Single.IsNaN f) && f <> 0.0f
            | :? string as s ->
                match Boolean.TryParse(s) with
                | true, b -> b
                | _ ->
                    let normalized = s.Trim().ToLower()
                    match normalized with
                    | "0" | "off" | "no" | "false" | "" -> false
                    | "1" | "on" | "yes" | "true" -> true
                    | _ -> not (String.IsNullOrWhiteSpace s)
            | _ -> failwithf "Cannot convert %s to bool" (value.GetType().Name)

    /// <summary>객체를 int로 변환</summary>
    let toInt (value: obj) : int =
        if isNull value then 0
        else
            match value with
            | :? int as i -> i
            | :? bool as b -> if b then 1 else 0
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then
                    failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then
                    failwith "Double value exceeds Int32 range"
                else int d  // Truncate toward zero (IEC 61131-3)
            | :? single as f ->
                let d = float f
                if Double.IsNaN d || Double.IsInfinity d then
                    failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then
                    failwith "Single value exceeds Int32 range"
                else int d
            | :? string as s ->
                match Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid integer: %s" s
            | _ -> failwithf "Cannot convert %s to int" (value.GetType().Name)

    /// <summary>객체를 double로 변환</summary>
    let toDouble (value: obj) : double =
        if isNull value then 0.0
        else
            match value with
            | :? double as d -> d
            | :? single as f -> float f
            | :? int as i -> float i
            | :? bool as b -> if b then 1.0 else 0.0
            | :? string as s ->
                match Double.TryParse(s, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture) with
                | true, d -> d
                | _ -> failwithf "Invalid double: %s" s
            | _ -> failwithf "Cannot convert %s to double" (value.GetType().Name)

    /// <summary>객체를 string으로 변환</summary>
    let toString (value: obj) : string =
        if isNull value then String.Empty
        else
            match value with
            | :? string as s -> s
            | :? bool as b -> if b then "True" else "False"
            | :? int as i -> string i
            | :? double as d -> d.ToString(CultureInfo.InvariantCulture)
            | :? single as f -> (float f).ToString(CultureInfo.InvariantCulture)
            | _ -> value.ToString()

    /// <summary>목표 타입으로 변환</summary>
    let convertToType (targetType: Type) (value: obj) : obj =
        if targetType = typeof<bool> then box (toBool value)
        elif targetType = typeof<int> then box (toInt value)
        elif targetType = typeof<double> then box (toDouble value)
        elif targetType = typeof<string> then box (toString value)
        else failwithf "Unsupported target type: %s" targetType.FullName

    /// <summary>목표 타입으로 변환 시도</summary>
    let tryConvertToType (targetType: Type) (value: obj) : obj option =
        try Some (convertToType targetType value)
        with _ -> None

    /// <summary>Try 변환 함수들</summary>
    let tryToBool (value: obj) : bool option =
        try Some (toBool value) with _ -> None

    let tryToInt (value: obj) : int option =
        try Some (toInt value) with _ -> None

    let tryToDouble (value: obj) : double option =
        try Some (toDouble value) with _ -> None

    let tryToString (value: obj) : string option =
        try Some (toString value) with _ -> None

    // ─────────────────────────────────────────────────────────────────
    // 타입 검증 (TypeValidator 모듈 대체)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>null 값 검증</summary>
    let checkNull (value: obj) (context: string) : obj =
        if isNull value then
            invalidArg "value" (sprintf "Null value in context: %s" context)
        else value

    /// <summary>타입 일치 검증</summary>
    let checkType (expectedType: Type) (value: obj) : obj =
        if isNull value then
            invalidArg "value" (sprintf "Null value for type %O" expectedType)
        else
            let actualType = value.GetType()
            if areTypesCompatible expectedType actualType then value
            else failwithf "Type mismatch: expected %O but got %O" expectedType actualType

    /// <summary>숫자 범위 검증</summary>
    let checkRange (typ: Type) (value: obj) : obj =
        if typ = typeof<int> then
            let i = toInt value
            if i >= Int32.MinValue && i <= Int32.MaxValue then box i
            else failwith "Int32 out of range"
        elif typ = typeof<double> then
            let d = toDouble value
            if Double.IsNaN(d) || Double.IsInfinity(d) then
                failwith "Invalid double value (NaN or Infinity)"
            else box d
        else value

    /// <summary>문자열 검증</summary>
    let validateString (value: obj) (allowEmpty: bool) : obj =
        if isNull value then
            if allowEmpty then box String.Empty
            else failwith "null string value"
        else
            match value with
            | :? string as s ->
                if not allowEmpty && String.IsNullOrWhiteSpace(s) then
                    failwith "Empty string not allowed"
                else value
            | _ ->
                failwithf "type mismatch: expected String, got %s" (value.GetType().Name)

    // ─────────────────────────────────────────────────────────────────
    // 편의 함수
    // ─────────────────────────────────────────────────────────────────

    /// <summary>타입 이름을 사람이 읽기 쉬운 형태로 반환</summary>
    let getTypeName (t: Type) : string =
        if t = typeof<bool> then "Bool"
        elif t = typeof<int> then "Int"
        elif t = typeof<double> then "Double"
        elif t = typeof<string> then "String"
        else t.Name
