namespace Ev2.PLC.Common.Types

open System

// ===================================
// Core PLC Data Types - Universal across all PLC vendors
// ===================================

/// Universal PLC data type enumeration
/// This represents the fundamental data types supported across all PLC vendors
type PlcDataType =
    // Basic types
    | Bool
    | Int8 | UInt8
    | Int16 | UInt16  
    | Int32 | UInt32
    | Int64 | UInt64
    | Float32 | Float64
    // String and binary data
    | String of maxLength: int
    | Binary of maxLength: int
    // Complex types
    | Array of elementType: PlcDataType * count: int
    | Struct of name: string * fields: (string * PlcDataType) list
    // Vendor-specific extension point
    | Custom of typeName: string * size: int

    /// Get the size in bytes for this data type
    member this.SizeInBytes =
        match this with
        | Bool -> 1
        | Int8 | UInt8 -> 1
        | Int16 | UInt16 -> 2
        | Int32 | UInt32 | Float32 -> 4
        | Int64 | UInt64 | Float64 -> 8
        | String maxLength -> maxLength
        | Binary maxLength -> maxLength
        | Array (elementType, count) -> elementType.SizeInBytes * count
        | Struct (_, fields) -> fields |> List.sumBy (snd >> (_.SizeInBytes))
        | Custom (_, size) -> size

    /// Check if this is a numeric type
    member this.IsNumeric =
        match this with
        | Int8 | UInt8 | Int16 | UInt16 | Int32 | UInt32 | Int64 | UInt64 
        | Float32 | Float64 -> true
        | _ -> false

    /// Check if this is an integer type
    member this.IsInteger =
        match this with
        | Int8 | UInt8 | Int16 | UInt16 | Int32 | UInt32 | Int64 | UInt64 -> true
        | _ -> false

    /// Check if this is a floating point type
    member this.IsFloat =
        match this with
        | Float32 | Float64 -> true
        | _ -> false

    /// Check if this is a signed numeric type
    member this.IsSigned =
        match this with
        | Int8 | Int16 | Int32 | Int64 | Float32 | Float64 -> true
        | _ -> false

    /// Parse data type from string representation
    static member TryParse(typeStr: string) =
        match typeStr.Trim().ToLowerInvariant() with
        | "bool" | "boolean" | "bit" -> Some Bool
        | "int8" | "sbyte" | "sint" -> Some Int8
        | "uint8" | "byte" | "usint" -> Some UInt8
        | "int16" | "short" | "int" -> Some Int16
        | "uint16" | "ushort" | "uint" -> Some UInt16
        | "int32" | "dint" | "long" -> Some Int32
        | "uint32" | "udint" | "ulong" -> Some UInt32
        | "int64" | "lint" -> Some Int64
        | "uint64" | "ulint" -> Some UInt64
        | "float32" | "float" | "real" -> Some Float32
        | "float64" | "double" | "lreal" -> Some Float64
        | s when s.StartsWith("string[") || s.StartsWith("str[") ->
            let lengthPart = s.Substring(s.IndexOf('[') + 1).TrimEnd(']')
            match Int32.TryParse(lengthPart) with
            | true, length when length > 0 -> Some (String length)
            | _ -> None
        | s when s.StartsWith("binary[") || s.StartsWith("bytes[") ->
            let lengthPart = s.Substring(s.IndexOf('[') + 1).TrimEnd(']')
            match Int32.TryParse(lengthPart) with
            | true, length when length > 0 -> Some (Binary length)
            | _ -> None
        | _ -> None

/// Universal scalar value type for PLC data
/// This can hold any value from any PLC vendor in a unified format
type PlcValue =
    // Basic values
    | BoolValue of bool
    | Int8Value of sbyte | UInt8Value of byte
    | Int16Value of int16 | UInt16Value of uint16
    | Int32Value of int32 | UInt32Value of uint32
    | Int64Value of int64 | UInt64Value of uint64
    | Float32Value of float32 | Float64Value of float
    // String and binary
    | StringValue of string
    | BinaryValue of byte[]
    // Complex values
    | ArrayValue of PlcValue[]
    | StructValue of name: string * fields: Map<string, PlcValue>
    // Special values
    | NullValue
    | ErrorValue of message: string

    /// Get the data type of this value
    member this.DataType =
        match this with
        | BoolValue _ -> Bool
        | Int8Value _ -> Int8 | UInt8Value _ -> UInt8
        | Int16Value _ -> Int16 | UInt16Value _ -> UInt16
        | Int32Value _ -> Int32 | UInt32Value _ -> UInt32
        | Int64Value _ -> Int64 | UInt64Value _ -> UInt64
        | Float32Value _ -> Float32 | Float64Value _ -> Float64
        | StringValue s -> String s.Length
        | BinaryValue bytes -> Binary bytes.Length
        | ArrayValue values -> 
            if values.Length > 0 then 
                Array (values.[0].DataType, values.Length)
            else 
                Array (Bool, 0)
        | StructValue (name, fields) -> 
            let fieldTypes = fields |> Map.toList |> List.map (fun (k, v) -> (k, v.DataType))
            Struct (name, fieldTypes)
        | NullValue -> Custom ("null", 0)
        | ErrorValue _ -> Custom ("error", 0)

    /// Check if this value represents an error
    member this.IsError =
        match this with
        | ErrorValue _ -> true
        | _ -> false

    /// Check if this value is null
    member this.IsNull =
        match this with
        | NullValue -> true
        | _ -> false

    /// Check if this value is valid (not null or error)
    member this.IsValid = not (this.IsError || this.IsNull)

    /// Convert value to string representation
    override this.ToString() =
        match this with
        | BoolValue b -> if b then "true" else "false"
        | Int8Value i -> i.ToString() | UInt8Value i -> i.ToString()
        | Int16Value i -> i.ToString() | UInt16Value i -> i.ToString()
        | Int32Value i -> i.ToString() | UInt32Value i -> i.ToString()
        | Int64Value i -> i.ToString() | UInt64Value i -> i.ToString()
        | Float32Value f -> f.ToString("G") | Float64Value f -> f.ToString("G")
        | StringValue s -> $"\"{s}\""
        | BinaryValue bytes -> $"Binary[{bytes.Length}]"
        | ArrayValue values -> 
            let valueStrs = values |> Array.map (_.ToString())
            "[" + String.concat "; " valueStrs + "]"
        | StructValue (name, fields) -> 
            let fieldStrs = fields |> Map.toList |> List.map (fun (k, v) -> $"{k}: {v}")
            $"{name} {{ " + String.concat "; " fieldStrs + " }"
        | NullValue -> "null"
        | ErrorValue msg -> $"Error: {msg}"

    /// Get default value for a given data type
    static member GetDefaultValue(dataType: PlcDataType) =
        match dataType with
        | Bool -> BoolValue false
        | Int8 -> Int8Value 0y | UInt8 -> UInt8Value 0uy
        | Int16 -> Int16Value 0s | UInt16 -> UInt16Value 0us
        | Int32 -> Int32Value 0 | UInt32 -> UInt32Value 0u
        | Int64 -> Int64Value 0L | UInt64 -> UInt64Value 0UL
        | Float32 -> Float32Value 0.0f | Float64 -> Float64Value 0.0
        | String _ -> StringValue ""
        | Binary length -> BinaryValue (Array.zeroCreate length)
        | Array (elementType, length) -> 
            ArrayValue (Array.init length (fun _ -> PlcValue.GetDefaultValue(elementType)))
        | Struct (name, fields) -> 
            let fieldMap = fields |> List.map (fun (fieldName, fieldType) -> 
                (fieldName, PlcValue.GetDefaultValue(fieldType))) |> Map.ofList
            StructValue (name, fieldMap)
        | Custom (_, _) -> NullValue

    /// Convert value to byte array
    member this.ToBytes() =
        match this with
        | BoolValue b -> [| if b then 1uy else 0uy |]
        | Int8Value i -> [| byte i |]
        | UInt8Value i -> [| i |]
        | Int16Value i -> BitConverter.GetBytes(i)
        | UInt16Value i -> BitConverter.GetBytes(i)
        | Int32Value i -> BitConverter.GetBytes(i)
        | UInt32Value i -> BitConverter.GetBytes(i)
        | Int64Value i -> BitConverter.GetBytes(i)
        | UInt64Value i -> BitConverter.GetBytes(i)
        | Float32Value f -> BitConverter.GetBytes(f)
        | Float64Value f -> BitConverter.GetBytes(f)
        | StringValue s -> System.Text.Encoding.UTF8.GetBytes(s)
        | BinaryValue bytes -> bytes
        | ArrayValue values -> 
            values |> Array.collect (_.ToBytes())
        | StructValue (_, fields) -> 
            fields |> Map.toList |> List.collect (fun (_, v) -> v.ToBytes() |> Array.toList) |> Array.ofList
        | NullValue -> [||]
        | ErrorValue _ -> [||]

    /// Create value from byte array
    static member FromBytes(bytes: byte[], dataType: PlcDataType) =
        try
            match dataType with
            | Bool -> BoolValue (bytes.Length > 0 && bytes.[0] <> 0uy)
            | Int8 -> Int8Value (if bytes.Length > 0 then sbyte bytes.[0] else 0y)
            | UInt8 -> UInt8Value (if bytes.Length > 0 then bytes.[0] else 0uy)
            | Int16 -> Int16Value (if bytes.Length >= 2 then BitConverter.ToInt16(bytes, 0) else 0s)
            | UInt16 -> UInt16Value (if bytes.Length >= 2 then BitConverter.ToUInt16(bytes, 0) else 0us)
            | Int32 -> Int32Value (if bytes.Length >= 4 then BitConverter.ToInt32(bytes, 0) else 0)
            | UInt32 -> UInt32Value (if bytes.Length >= 4 then BitConverter.ToUInt32(bytes, 0) else 0u)
            | Int64 -> Int64Value (if bytes.Length >= 8 then BitConverter.ToInt64(bytes, 0) else 0L)
            | UInt64 -> UInt64Value (if bytes.Length >= 8 then BitConverter.ToUInt64(bytes, 0) else 0UL)
            | Float32 -> Float32Value (if bytes.Length >= 4 then BitConverter.ToSingle(bytes, 0) else 0.0f)
            | Float64 -> Float64Value (if bytes.Length >= 8 then BitConverter.ToDouble(bytes, 0) else 0.0)
            | String maxLength -> 
                let str = System.Text.Encoding.UTF8.GetString(bytes, 0, min maxLength bytes.Length)
                StringValue (str.TrimEnd('\000'))
            | Binary _ -> BinaryValue bytes
            | Array (elementType, count) ->
                let elementSize = elementType.SizeInBytes
                let values = Array.init count (fun i ->
                    let offset = i * elementSize
                    let endOffset = min (offset + elementSize) bytes.Length
                    if offset < bytes.Length then
                        let elementBytes = bytes.[offset..endOffset-1]
                        PlcValue.FromBytes(elementBytes, elementType)
                    else
                        PlcValue.GetDefaultValue(elementType))
                ArrayValue values
            | Struct (name, fields) ->
                let mutable offset = 0
                let fieldValues = fields |> List.choose (fun (fieldName, fieldType) ->
                    let fieldSize = fieldType.SizeInBytes
                    if offset + fieldSize <= bytes.Length then
                        let fieldBytes = bytes.[offset..offset + fieldSize - 1]
                        offset <- offset + fieldSize
                        Some (fieldName, PlcValue.FromBytes(fieldBytes, fieldType))
                    else
                        None)
                StructValue (name, Map.ofList fieldValues)
            | Custom (_, _) -> NullValue
        with
        | ex -> ErrorValue $"Failed to parse bytes: {ex.Message}"

    /// Try to convert this value to another data type
    member this.TryConvertTo(targetType: PlcDataType) =
        try
            match this, targetType with
            // Same type - no conversion needed
            | v, t when v.DataType = t -> Some v
            
            // Numeric conversions
            | BoolValue b, Int8 -> Some (Int8Value (if b then 1y else 0y))
            | BoolValue b, UInt8 -> Some (UInt8Value (if b then 1uy else 0uy))
            | BoolValue b, Int16 -> Some (Int16Value (if b then 1s else 0s))
            | BoolValue b, UInt16 -> Some (UInt16Value (if b then 1us else 0us))
            | BoolValue b, Int32 -> Some (Int32Value (if b then 1 else 0))
            | BoolValue b, UInt32 -> Some (UInt32Value (if b then 1u else 0u))
            | BoolValue b, Float32 -> Some (Float32Value (if b then 1.0f else 0.0f))
            | BoolValue b, Float64 -> Some (Float64Value (if b then 1.0 else 0.0))
            
            // Integer to integer conversions
            | Int8Value i, Int16 -> Some (Int16Value (int16 i))
            | Int8Value i, Int32 -> Some (Int32Value (int32 i))
            | Int8Value i, Int64 -> Some (Int64Value (int64 i))
            | Int8Value i, Float32 -> Some (Float32Value (float32 i))
            | Int8Value i, Float64 -> Some (Float64Value (float i))
            
            | Int16Value i, Int8 when i >= int16 SByte.MinValue && i <= int16 SByte.MaxValue -> 
                Some (Int8Value (sbyte i))
            | Int16Value i, Int32 -> Some (Int32Value (int32 i))
            | Int16Value i, Int64 -> Some (Int64Value (int64 i))
            | Int16Value i, Float32 -> Some (Float32Value (float32 i))
            | Int16Value i, Float64 -> Some (Float64Value (float i))
            
            | Int32Value i, Int8 when i >= int32 SByte.MinValue && i <= int32 SByte.MaxValue -> 
                Some (Int8Value (sbyte i))
            | Int32Value i, Int16 when i >= int32 Int16.MinValue && i <= int32 Int16.MaxValue -> 
                Some (Int16Value (int16 i))
            | Int32Value i, Int64 -> Some (Int64Value (int64 i))
            | Int32Value i, Float32 -> Some (Float32Value (float32 i))
            | Int32Value i, Float64 -> Some (Float64Value (float i))
            
            // Float to integer conversions
            | Float32Value f, Int32 when f >= float32 Int32.MinValue && f <= float32 Int32.MaxValue -> 
                Some (Int32Value (int32 f))
            | Float64Value f, Int32 when f >= float Int32.MinValue && f <= float Int32.MaxValue -> 
                Some (Int32Value (int32 f))
            | Float32Value f, Float64 -> Some (Float64Value (float f))
            | Float64Value f, Float32 -> Some (Float32Value (float32 f))
            
            // String conversions
            | v, String _ -> Some (StringValue (v.ToString().Trim('"')))
            
            | _ -> None
        with
        | _ -> None

/// Module for working with PLC values
module PlcValue =
    
    /// Check if two values are equal within a tolerance (for floating point)
    let areEqual (tolerance: float option) (value1: PlcValue) (value2: PlcValue) =
        match value1, value2, tolerance with
        | Float32Value f1, Float32Value f2, Some tol -> abs(f1 - f2) <= float32 tol
        | Float64Value f1, Float64Value f2, Some tol -> abs(f1 - f2) <= tol
        | v1, v2, _ -> v1 = v2

    /// Check if a value is within a specified range
    let isInRange (minValue: PlcValue option) (maxValue: PlcValue option) (value: PlcValue) =
        let compareValues v1 v2 =
            match v1, v2 with
            | Int8Value a, Int8Value b -> Some (compare a b)
            | UInt8Value a, UInt8Value b -> Some (compare a b)
            | Int16Value a, Int16Value b -> Some (compare a b)
            | UInt16Value a, UInt16Value b -> Some (compare a b)
            | Int32Value a, Int32Value b -> Some (compare a b)
            | UInt32Value a, UInt32Value b -> Some (compare a b)
            | Int64Value a, Int64Value b -> Some (compare a b)
            | UInt64Value a, UInt64Value b -> Some (compare a b)
            | Float32Value a, Float32Value b -> Some (compare a b)
            | Float64Value a, Float64Value b -> Some (compare a b)
            | _ -> None

        let withinMin = 
            match minValue with
            | None -> true
            | Some minVal -> 
                match compareValues value minVal with
                | Some cmp -> cmp >= 0
                | None -> true

        let withinMax = 
            match maxValue with
            | None -> true
            | Some maxVal -> 
                match compareValues value maxVal with
                | Some cmp -> cmp <= 0
                | None -> true

        withinMin && withinMax

    /// Apply scaling to a numeric value
    let applyScaling (scale: float option) (offset: float option) (value: PlcValue) =
        match value, scale, offset with
        | Float32Value f, Some s, Some o -> Float32Value (f * float32 s + float32 o)
        | Float64Value f, Some s, Some o -> Float64Value (f * s + o)
        | Int32Value i, Some s, Some o -> Float64Value (float i * s + o)
        | Int16Value i, Some s, Some o -> Float64Value (float i * s + o)
        | Float32Value f, Some s, None -> Float32Value (f * float32 s)
        | Float64Value f, Some s, None -> Float64Value (f * s)
        | Int32Value i, Some s, None -> Float64Value (float i * s)
        | Int16Value i, Some s, None -> Float64Value (float i * s)
        | Float32Value f, None, Some o -> Float32Value (f + float32 o)
        | Float64Value f, None, Some o -> Float64Value (f + o)
        | Int32Value i, None, Some o -> Float64Value (float i + o)
        | Int16Value i, None, Some o -> Float64Value (float i + o)
        | _ -> value