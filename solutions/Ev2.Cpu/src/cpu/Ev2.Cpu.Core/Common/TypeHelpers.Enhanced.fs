namespace Ev2.Cpu.Core

open System
open System.Globalization

// ═════════════════════════════════════════════════════════════════════
// 제네릭 타입 헬퍼 (Generic Type Helpers) - Extended to All Primitive Types
// ═════════════════════════════════════════════════════════════════════
// .NET 프리미티브 타입 시스템 완전 지원
// 11개 타입: bool, sbyte, byte, short, ushort, int, uint, long, ulong, double, string
// IEC 61131-3 PLC 타입 매핑 지원
// ═════════════════════════════════════════════════════════════════════

/// <summary>타입 헬퍼 유틸리티</summary>
/// <remarks>
/// 지원 타입 (11개):
/// - Boolean: bool
/// - Signed Integers: sbyte(Int8), short(Int16), int(Int32), long(Int64)
/// - Unsigned Integers: byte(UInt8), ushort(UInt16), uint(UInt32), ulong(UInt64)
/// - Floating Point: double
/// - Text: string
/// </remarks>
module TypeHelpers =

    // ─────────────────────────────────────────────────────────────────
    // 타입 분류 및 검증
    // ─────────────────────────────────────────────────────────────────

    /// <summary>지원되는 타입인지 검사 (11개 타입)</summary>
    let isSupportedType (t: Type) : bool =
        t = typeof<bool> ||
        t = typeof<sbyte> || t = typeof<byte> ||
        t = typeof<int16> || t = typeof<uint16> ||
        t = typeof<int> || t = typeof<uint> ||
        t = typeof<int64> || t = typeof<uint64> ||
        t = typeof<double> || t = typeof<string>

    /// <summary>정수 타입인지 검사 (8개 정수 타입)</summary>
    let isIntegerType (t: Type) : bool =
        t = typeof<sbyte> || t = typeof<byte> ||
        t = typeof<int16> || t = typeof<uint16> ||
        t = typeof<int> || t = typeof<uint> ||
        t = typeof<int64> || t = typeof<uint64>

    /// <summary>부호있는 정수 타입인지 검사</summary>
    let isSignedIntegerType (t: Type) : bool =
        t = typeof<sbyte> || t = typeof<int16> || t = typeof<int> || t = typeof<int64>

    /// <summary>부호없는 정수 타입인지 검사</summary>
    let isUnsignedIntegerType (t: Type) : bool =
        t = typeof<byte> || t = typeof<uint16> || t = typeof<uint> || t = typeof<uint64>

    /// <summary>숫자 타입인지 검사 (산술 연산 가능 - 정수 + double)</summary>
    let isNumericType (t: Type) : bool =
        isIntegerType t || t = typeof<double>

    /// <summary>타입 크기 반환 (바이트 단위)</summary>
    let getTypeSize (t: Type) : int =
        if t = typeof<bool> then 1
        elif t = typeof<sbyte> || t = typeof<byte> then 1
        elif t = typeof<int16> || t = typeof<uint16> then 2
        elif t = typeof<int> || t = typeof<uint> then 4
        elif t = typeof<int64> || t = typeof<uint64> then 8
        elif t = typeof<double> then 8
        elif t = typeof<string> then 0  // 가변 크기
        else invalidArg "t" (sprintf "Unsupported type: %s" t.FullName)

    // ─────────────────────────────────────────────────────────────────
    // 타입 기본값
    // ─────────────────────────────────────────────────────────────────

    /// <summary>타입 기본값 반환</summary>
    let getDefaultValue (t: Type) : obj =
        if t = typeof<bool> then box false
        elif t = typeof<sbyte> then box 0y
        elif t = typeof<byte> then box 0uy
        elif t = typeof<int16> then box 0s
        elif t = typeof<uint16> then box 0us
        elif t = typeof<int> then box 0
        elif t = typeof<uint> then box 0u
        elif t = typeof<int64> then box 0L
        elif t = typeof<uint64> then box 0UL
        elif t = typeof<double> then box 0.0
        elif t = typeof<string> then box ""
        else invalidArg "t" (sprintf "Unsupported type: %s" t.FullName)

    // ─────────────────────────────────────────────────────────────────
    // 타입 파싱 (.NET 및 IEC 61131-3 PLC 타입명 지원)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>문자열에서 타입 파싱 (.NET 타입명 + PLC 타입명)</summary>
    let tryParseTypeName (name: string) : Type option =
        if String.IsNullOrWhiteSpace name then None
        else
            match name.Trim().ToLowerInvariant() with
            // Boolean
            | "bool" | "boolean" | "system.boolean" -> Some typeof<bool>

            // Signed integers
            | "sbyte" | "int8" | "system.sbyte" | "sint" -> Some typeof<sbyte>
            | "short" | "int16" | "system.int16" | "int" -> Some typeof<int16>  // PLC INT = 16-bit
            | "int32" | "system.int32" | "dint" -> Some typeof<int>  // PLC DINT = 32-bit
            | "long" | "int64" | "system.int64" | "lint" -> Some typeof<int64>

            // Unsigned integers
            | "byte" | "uint8" | "system.byte" | "usint" -> Some typeof<byte>
            | "ushort" | "uint16" | "system.uint16" | "uint" | "word" -> Some typeof<uint16>
            | "uint32" | "system.uint32" | "udint" | "dword" -> Some typeof<uint>
            | "ulong" | "uint64" | "system.uint64" | "ulint" | "lword" -> Some typeof<uint64>

            // Floating point
            | "double" | "system.double" | "lreal" | "real64" -> Some typeof<double>
            | "float" | "real" -> Some typeof<double>  // 호환성: float/real → double

            // String
            | "string" | "text" | "system.string" -> Some typeof<string>

            | _ -> None

    /// <summary>문자열에서 타입 파싱 (실패 시 예외)</summary>
    let parseTypeName (name: string) : Type =
        match tryParseTypeName name with
        | Some t -> t
        | None -> invalidArg "name" (sprintf "Unsupported type name: %s" name)

    let areTypesCompatible (target: Type) (source: Type) : bool =
        if target = source then true
        else
            // Signed integer promotions
            if target = typeof<int16> && source = typeof<sbyte> then true
            elif target = typeof<int> && (source = typeof<sbyte> || source = typeof<int16>) then true
            elif target = typeof<int64> && (source = typeof<sbyte> || source = typeof<int16> || source = typeof<int>) then true

            // Unsigned integer promotions1
            elif target = typeof<uint16> && source = typeof<byte> then true
            elif target = typeof<uint> && (source = typeof<byte> || source = typeof<uint16>) then true
            elif target = typeof<uint64> && (source = typeof<byte> || source = typeof<uint16> || source = typeof<uint>) then true

            // Mixed: unsigned → signed (safe when target is 2x larger)
            elif target = typeof<int16> && source = typeof<byte> then true  // byte(0-255) fits in short(-32768~32767)
            elif target = typeof<int> && (source = typeof<byte> || source = typeof<uint16>) then true
            elif target = typeof<int64> && (source = typeof<byte> || source = typeof<uint16> || source = typeof<uint>) then true

            // All integers → double (may lose precision for large integers)
            elif target = typeof<double> && isIntegerType source then true

            else false

    /// <summary>값의 타입 검증 및 자동 승격</summary>
    let validateType (expectedType: Type) (value: obj) : obj =
        if isNull value then
            invalidArg "value" "Value cannot be null"
        let actualType = value.GetType()
        if actualType = expectedType then
            value
        elif areTypesCompatible expectedType actualType then
            // 자동 타입 승격 수행
            match expectedType with
            | t when t = typeof<int16> -> box (Convert.ToInt16 value)
            | t when t = typeof<uint16> -> box (Convert.ToUInt16 value)
            | t when t = typeof<int> -> box (Convert.ToInt32 value)
            | t when t = typeof<uint> -> box (Convert.ToUInt32 value)
            | t when t = typeof<int64> -> box (Convert.ToInt64 value)
            | t when t = typeof<uint64> -> box (Convert.ToUInt64 value)
            | t when t = typeof<double> -> box (Convert.ToDouble value)
            | _ -> failwithf "Cannot promote %s to %s" actualType.FullName expectedType.FullName
        else
            failwithf "Type mismatch: expecte1d %s but got %s" expectedType.FullName actualType.FullName

    // ─────────────────────────────────────────────────────────────────
    // 타입 변환 함수들 (11개 타입 지원)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>객체를 bool로 변환</summary>
    let toBool (value: obj) : bool =
        if isNull value then false
        else
            match value with
            | :? bool as b -> b
            | :? sbyte as i -> i <> 0y
            | :? byte as i -> i <> 0uy
            | :? int16 as i -> i <> 0s
            | :? uint16 as i -> i <> 0us
            | :? int as i -> i <> 0
            | :? uint as i -> i <> 0u
            | :? int64 as i -> i <> 0L
            | :? uint64 as i -> i <> 0UL
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

    /// <summary>객체를 sbyte로 변환 (IEC 61131-3 truncation toward zero)</summary>
    let toSByte (value: obj) : sbyte =
        if isNull value then 0y
        else
            match value with
            | :? sbyte as i -> i
            | :? byte as i -> if i <= byte SByte.MaxValue then sbyte i else failwith "Byte value exceeds SByte range"
            | :? bool as b -> if b then 1y else 0y
            | :? int16 as i ->
                if i >= int16 SByte.MinValue && i <= int16 SByte.MaxValue then sbyte i
                else failwith "Int16 value exceeds SByte range"
            | :? int as i ->
                if i >= int SByte.MinValue && i <= int SByte.MaxValue then sbyte i
                else failwith "Int32 value exceeds SByte range"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to SByte"
                elif d > float SByte.MaxValue || d < float SByte.MinValue then failwith "Double value exceeds SByte range"
                else sbyte d
            | :? string as s ->
                match SByte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid sbyte: %s" s
            | _ -> failwithf "Cannot convert %s to sbyte" (value.GetType().Name)

    /// <summary>객체를 byte로 변환</summary>
    let toByte (value: obj) : byte =
        if isNull value then 0uy
        else
            match value with
            | :? byte as i -> i
            | :? sbyte as i -> if i >= 0y then byte i else failwith "SByte value is negative"
            | :? bool as b -> if b then 1uy else 0uy
            | :? uint16 as i -> if i <= uint16 Byte.MaxValue then byte i else failwith "UInt16 value exceeds Byte range"
            | :? int as i ->
                if i >= 0 && i <= int Byte.MaxValue then byte i
                else failwith "Int32 value exceeds Byte range"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to Byte"
                elif d > float Byte.MaxValue || d < 0.0 then failwith "Double value exceeds Byte range"
                else byte d
            | :? string as s ->
                match Byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid byte: %s" s
            | _ -> failwithf "Cannot convert %s to byte" (value.GetType().Name)

    /// <summary>객체를 short로 변환</summary>
    let toShort (value: obj) : int16 =
        if isNull value then 0s
        else
            match value with
            | :? int16 as i -> i
            | :? sbyte as i -> int16 i
            | :? byte as i -> int16 i
            | :? bool as b -> if b then 1s else 0s
            | :? int as i ->
                if i >= int Int16.MinValue && i <= int Int16.MaxValue then int16 i
                else failwith "Int32 value exceeds Int16 range"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to Int16"
                elif d > float Int16.MaxValue || d < float Int16.MinValue then failwith "Double value exceeds Int16 range"
                else int16 d
            | :? string as s ->
                match Int16.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid short: %s" s
            | _ -> failwithf "Cannot convert %s to short" (value.GetType().Name)

    /// <summary>객체를 ushort로 변환</summary>
    let toUShort (value: obj) : uint16 =
        if isNull value then 0us
        else
            match value with
            | :? uint16 as i -> i
            | :? byte as i -> uint16 i
            | :? bool as b -> if b then 1us else 0us
            | :? uint as i -> if i <= uint32 UInt16.MaxValue then uint16 i else failwith "UInt32 value exceeds UInt16 range"
            | :? int as i ->
                if i >= 0 && i <= int UInt16.MaxValue then uint16 i
                else failwith "Int32 value exceeds UInt16 range"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to UInt16"
                elif d > float UInt16.MaxValue || d < 0.0 then failwith "Double value exceeds UInt16 range"
                else uint16 d
            | :? string as s ->
                match UInt16.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid ushort: %s" s
            | _ -> failwithf "Cannot convert %s to ushort" (value.GetType().Name)

    /// <summary>객체를 int로 변환</summary>
    let toInt (value: obj) : int =
        if isNull value then 0
        else
            match value with
            | :? int as i -> i
            | :? sbyte as i -> int i
            | :? byte as i -> int i
            | :? int16 as i -> int i
            | :? uint16 as i -> int i
            | :? bool as b -> if b then 1 else 0
            | :? int64 as i ->
                if i >= int64 Int32.MinValue && i <= int64 Int32.MaxValue then int i
                else failwith "Int64 value exceeds Int32 range"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then failwith "Double value exceeds Int32 range"
                else int d  // Truncate toward zero (IEC 61131-3)
            | :? single as f ->
                let d = float f
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to Int32"
                elif d > float Int32.MaxValue || d < float Int32.MinValue then failwith "Single value exceeds Int32 range"
                else int d
            | :? string as s ->
                match Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid integer: %s" s
            | _ -> failwithf "Cannot convert %s to int" (value.GetType().Name)

    /// <summary>객체를 uint로 변환</summary>
    let toUInt (value: obj) : uint32 =
        if isNull value then 0u
        else
            match value with
            | :? uint as i -> i
            | :? byte as i -> uint32 i
            | :? uint16 as i -> uint32 i
            | :? bool as b -> if b then 1u else 0u
            | :? uint64 as i -> if i <= uint64 UInt32.MaxValue then uint32 i else failwith "UInt64 value exceeds UInt32 range"
            | :? int as i -> if i >= 0 then uint32 i else failwith "Int32 value is negative"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to UInt32"
                elif d > float UInt32.MaxValue || d < 0.0 then failwith "Double value exceeds UInt32 range"
                else uint32 d
            | :? string as s ->
                match UInt32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid uint: %s" s
            | _ -> failwithf "Cannot convert %s to uint" (value.GetType().Name)

    /// <summary>객체를 long로 변환</summary>
    let toLong (value: obj) : int64 =
        if isNull value then 0L
        else
            match value with
            | :? int64 as i -> i
            | :? sbyte as i -> int64 i
            | :? byte as i -> int64 i
            | :? int16 as i -> int64 i
            | :? uint16 as i -> int64 i
            | :? int as i -> int64 i
            | :? uint as i -> int64 i
            | :? bool as b -> if b then 1L else 0L
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to Int64"
                elif d > float Int64.MaxValue || d < float Int64.MinValue then failwith "Double value exceeds Int64 range"
                else int64 d
            | :? string as s ->
                match Int64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid long: %s" s
            | _ -> failwithf "Cannot convert %s to long" (value.GetType().Name)

    /// <summary>객체를 ulong로 변환</summary>
    let toULong (value: obj) : uint64 =
        if isNull value then 0UL
        else
            match value with
            | :? uint64 as i -> i
            | :? byte as i -> uint64 i
            | :? uint16 as i -> uint64 i
            | :? uint as i -> uint64 i
            | :? bool as b -> if b then 1UL else 0UL
            | :? int as i -> if i >= 0 then uint64 i else failwith "Int32 value is negative"
            | :? int64 as i -> if i >= 0L then uint64 i else failwith "Int64 value is negative"
            | :? double as d ->
                if Double.IsNaN d || Double.IsInfinity d then failwith "Cannot convert NaN or Infinity to UInt64"
                elif d > float UInt64.MaxValue || d < 0.0 then failwith "Double value exceeds UInt64 range"
                else uint64 d
            | :? string as s ->
                match UInt64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, i -> i
                | _ -> failwithf "Invalid ulong: %s" s
            | _ -> failwithf "Cannot convert %s to ulong" (value.GetType().Name)

    /// <summary>객체를 double로 변환</summary>
    let toDouble (value: obj) : double =
        if isNull value then 0.0
        else
            match value with
            | :? double as d -> d
            | :? single as f -> float f
            | :? sbyte as i -> float i
            | :? byte as i -> float i
            | :? int16 as i -> float i
            | :? uint16 as i -> float i
            | :? int as i -> float i
            | :? uint as i -> float i
            | :? int64 as i -> float i  // 정밀도 손실 가능
            | :? uint64 as i -> float i  // 정밀도 손실 가능
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
            | :? sbyte as i -> string i
            | :? byte as i -> string i
            | :? int16 as i -> string i
            | :? uint16 as i -> string i
            | :? int as i -> string i
            | :? uint as i -> string i
            | :? int64 as i -> string i
            | :? uint64 as i -> string i
            | :? double as d -> d.ToString(CultureInfo.InvariantCulture)
            | :? single as f -> (float f).ToString(CultureInfo.InvariantCulture)
            | _ -> value.ToString()

    // ─────────────────────────────────────────────────────────────────
    // 범용 변환 함수
    // ─────────────────────────────────────────────────────────────────

    /// <summary>목표 타입으로 변환 (11개 타입 지원)</summary>
    let convertToType (targetType: Type) (value: obj) : obj =
        if targetType = typeof<bool> then box (toBool value)
        elif targetType = typeof<sbyte> then box (toSByte value)
        elif targetType = typeof<byte> then box (toByte value)
        elif targetType = typeof<int16> then box (toShort value)
        elif targetType = typeof<uint16> then box (toUShort value)
        elif targetType = typeof<int> then box (toInt value)
        elif targetType = typeof<uint> then box (toUInt value)
        elif targetType = typeof<int64> then box (toLong value)
        elif targetType = typeof<uint64> then box (toULong value)
        elif targetType = typeof<double> then box (toDouble value)
        elif targetType = typeof<string> then box (toString value)
        else failwithf "Unsupported target type: %s" targetType.FullName

    /// <summary>목표 타입으로 변환 시도</summary>
    let tryConvertToType (targetType: Type) (value: obj) : obj option =
        try Some (convertToType targetType value)
        with _ -> None

    // ─────────────────────────────────────────────────────────────────
    // Try 변환 함수들
    // ─────────────────────────────────────────────────────────────────

    let tryToBool (value: obj) : bool option =
        try Some (toBool value) with _ -> None

    let tryToSByte (value: obj) : sbyte option =
        try Some (toSByte value) with _ -> None

    let tryToByte (value: obj) : byte option =
        try Some (toByte value) with _ -> None

    let tryToShort (value: obj) : int16 option =
        try Some (toShort value) with _ -> None

    let tryToUShort (value: obj) : uint16 option =
        try Some (toUShort value) with _ -> None

    let tryToInt (value: obj) : int option =
        try Some (toInt value) with _ -> None

    let tryToUInt (value: obj) : uint32 option =
        try Some (toUInt value) with _ -> None

    let tryToLong (value: obj) : int64 option =
        try Some (toLong value) with _ -> None

    let tryToULong (value: obj) : uint64 option =
        try Some (toULong value) with _ -> None

    let tryToDouble (value: obj) : double option =
        try Some (toDouble value) with _ -> None

    let tryToString (value: obj) : string option =
        try Some (toString value) with _ -> None

    // ─────────────────────────────────────────────────────────────────
    // 타입 검증 함수들
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

    /// <summary>숫자 범위 검증 (모든 정수 타입 + double)</summary>
    let checkRange (typ: Type) (value: obj) : obj =
        if typ = typeof<sbyte> then
            let i = toSByte value
            if i >= SByte.MinValue && i <= SByte.MaxValue then box i
            else failwith "SByte out of range"
        elif typ = typeof<byte> then
            let i = toByte value
            if i >= Byte.MinValue && i <= Byte.MaxValue then box i
            else failwith "Byte out of range"
        elif typ = typeof<int16> then
            let i = toShort value
            if i >= Int16.MinValue && i <= Int16.MaxValue then box i
            else failwith "Int16 out of range"
        elif typ = typeof<uint16> then
            let i = toUShort value
            if i >= UInt16.MinValue && i <= UInt16.MaxValue then box i
            else failwith "UInt16 out of range"
        elif typ = typeof<int> then
            let i = toInt value
            if i >= Int32.MinValue && i <= Int32.MaxValue then box i
            else failwith "Int32 out of range"
        elif typ = typeof<uint> then
            let i = toUInt value
            if i >= UInt32.MinValue && i <= UInt32.MaxValue then box i
            else failwith "UInt32 out of range"
        elif typ = typeof<int64> then
            let i = toLong value
            if i >= Int64.MinValue && i <= Int64.MaxValue then box i
            else failwith "Int64 out of range"
        elif typ = typeof<uint64> then
            let i = toULong value
            if i >= UInt64.MinValue && i <= UInt64.MaxValue then box i
            else failwith "UInt64 out of range"
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
        elif t = typeof<sbyte> then "SByte"
        elif t = typeof<byte> then "Byte"
        elif t = typeof<int16> then "Short"
        elif t = typeof<uint16> then "UShort"
        elif t = typeof<int> then "Int32"  // Changed to match tryParseTypeName
        elif t = typeof<uint> then "UInt32"  // Changed for consistency
        elif t = typeof<int64> then "Long"
        elif t = typeof<uint64> then "ULong"
        elif t = typeof<double> then "Double"
        elif t = typeof<string> then "String"
        else t.Name

    /// <summary>IEC 61131-3 PLC 타입명 반환</summary>
    let getPlcTypeName (t: Type) : string =
        if t = typeof<bool> then "BOOL"
        elif t = typeof<sbyte> then "SINT"
        elif t = typeof<byte> then "USINT"
        elif t = typeof<int16> then "INT"
        elif t = typeof<uint16> then "UINT"
        elif t = typeof<int> then "DINT"
        elif t = typeof<uint> then "UDINT"
        elif t = typeof<int64> then "LINT"
        elif t = typeof<uint64> then "ULINT"
        elif t = typeof<double> then "LREAL"
        elif t = typeof<string> then "STRING"
        else t.Name
