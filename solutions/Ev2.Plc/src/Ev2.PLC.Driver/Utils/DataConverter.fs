namespace Ev2.PLC.Driver.Utils

open System
open Ev2.PLC.Common.Types

/// 데이터 변환 유틸리티 모듈
module DataConverter =
    
    /// 엔디안 변환이 필요한지 확인
    let requiresEndianConversion (dataType: PlcTagDataType) (isLittleEndian: bool) : bool =
        let systemIsLittleEndian = BitConverter.IsLittleEndian
        match dataType with
        | Int16 | UInt16 | Int32 | UInt32 | Int64 | UInt64 | Float32 | Float64 ->
            systemIsLittleEndian <> isLittleEndian
        | _ -> false
    
    /// 바이트 배열 엔디안 변환
    let convertEndian (bytes: byte[]) : byte[] =
        Array.rev bytes
    
    /// 16비트 값의 바이트 순서 변환
    let swapBytes16 (value: uint16) : uint16 =
        ((value &&& 0x00FFus) <<< 8) ||| ((value &&& 0xFF00us) >>> 8)
    
    /// 32비트 값의 바이트 순서 변환
    let swapBytes32 (value: uint32) : uint32 =
        ((value &&& 0x000000FFu) <<< 24) |||
        ((value &&& 0x0000FF00u) <<< 8) |||
        ((value &&& 0x00FF0000u) >>> 8) |||
        ((value &&& 0xFF000000u) >>> 24)
    
    /// 64비트 값의 바이트 순서 변환
    let swapBytes64 (value: uint64) : uint64 =
        ((value &&& 0x00000000000000FFUL) <<< 56) |||
        ((value &&& 0x000000000000FF00UL) <<< 40) |||
        ((value &&& 0x0000000000FF0000UL) <<< 24) |||
        ((value &&& 0x00000000FF000000UL) <<< 8) |||
        ((value &&& 0x000000FF00000000UL) >>> 8) |||
        ((value &&& 0x0000FF0000000000UL) >>> 24) |||
        ((value &&& 0x00FF000000000000UL) >>> 40) |||
        ((value &&& 0xFF00000000000000UL) >>> 56)
    
    /// 스칼라 값을 바이트 배열로 변환 (엔디안 고려)
    let rec toBytes (value: ScalarValue) (isLittleEndian: bool) : byte[] =
        let systemIsLittleEndian = BitConverter.IsLittleEndian
        
        match value with
        | BoolValue b -> [| if b then 1uy else 0uy |]
        | Int8Value i -> [| byte i |]
        | UInt8Value i -> [| i |]
        | Int16Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | UInt16Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | Int32Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | UInt32Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | Int64Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | UInt64Value i -> 
            let bytes = BitConverter.GetBytes(i)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | Float32Value f -> 
            let bytes = BitConverter.GetBytes(f)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | Float64Value f -> 
            let bytes = BitConverter.GetBytes(f)
            if systemIsLittleEndian = isLittleEndian then bytes else convertEndian bytes
        | StringValue s -> System.Text.Encoding.UTF8.GetBytes(s)
        | BytesValue bytes -> bytes
        | ArrayValue values -> 
            values |> Array.collect (fun v -> toBytes v isLittleEndian)
        | StructValue fields -> 
            fields 
            |> Map.toList 
            |> List.collect (fun (_, v) -> toBytes v isLittleEndian |> Array.toList) 
            |> Array.ofList
    
    /// 바이트 배열을 스칼라 값으로 변환 (엔디안 고려)
    let rec fromBytes (bytes: byte[]) (dataType: PlcTagDataType) (isLittleEndian: bool) : ScalarValue option =
        try
            let systemIsLittleEndian = BitConverter.IsLittleEndian
            let adjustedBytes = 
                if requiresEndianConversion dataType isLittleEndian then
                    convertEndian bytes
                else
                    bytes
            
            match dataType with
            | Bool -> Some (BoolValue (bytes.[0] <> 0uy))
            | Int8 -> Some (Int8Value (sbyte bytes.[0]))
            | UInt8 -> Some (UInt8Value bytes.[0])
            | Int16 -> Some (Int16Value (BitConverter.ToInt16(adjustedBytes, 0)))
            | UInt16 -> Some (UInt16Value (BitConverter.ToUInt16(adjustedBytes, 0)))
            | Int32 -> Some (Int32Value (BitConverter.ToInt32(adjustedBytes, 0)))
            | UInt32 -> Some (UInt32Value (BitConverter.ToUInt32(adjustedBytes, 0)))
            | Int64 -> Some (Int64Value (BitConverter.ToInt64(adjustedBytes, 0)))
            | UInt64 -> Some (UInt64Value (BitConverter.ToUInt64(adjustedBytes, 0)))
            | Float32 -> Some (Float32Value (BitConverter.ToSingle(adjustedBytes, 0)))
            | Float64 -> Some (Float64Value (BitConverter.ToDouble(adjustedBytes, 0)))
            | String maxLength -> 
                let str = System.Text.Encoding.UTF8.GetString(bytes, 0, min maxLength bytes.Length)
                Some (StringValue (str.TrimEnd('\000')))
            | Bytes _ -> Some (BytesValue bytes)
            | Array (elementType, length) ->
                let elementSize = elementType.Size
                if bytes.Length >= elementSize * length then
                    let values = Array.init length (fun i ->
                        let offset = i * elementSize
                        let elementBytes = bytes.[offset..offset + elementSize - 1]
                        match fromBytes elementBytes elementType isLittleEndian with
                        | Some value -> value
                        | None -> ScalarValue.GetDefaultValue(elementType))
                    Some (ArrayValue values)
                else
                    None
            | Struct fields ->
                let mutable offset = 0
                let fieldValues = fields |> List.choose (fun (name, fieldType) ->
                    let fieldSize = fieldType.Size
                    if offset + fieldSize <= bytes.Length then
                        let fieldBytes = bytes.[offset..offset + fieldSize - 1]
                        offset <- offset + fieldSize
                        match fromBytes fieldBytes fieldType isLittleEndian with
                        | Some value -> Some (name, value)
                        | None -> None
                    else
                        None)
                if fieldValues.Length = fields.Length then
                    Some (StructValue (Map.ofList fieldValues))
                else
                    None
        with
        | ex -> None
    
    /// 데이터 타입 간 변환
    let convertValue (value: ScalarValue) (targetType: PlcTagDataType) : ScalarValue option =
        if value.DataType = targetType then
            Some value
        else
            try
                match value, targetType with
                // 숫자 타입 간 변환
                | Int16Value i, Int32 -> Some (Int32Value (int32 i))
                | Int16Value i, Int64 -> Some (Int64Value (int64 i))
                | Int16Value i, Float32 -> Some (Float32Value (float32 i))
                | Int16Value i, Float64 -> Some (Float64Value (float i))
                | Int32Value i, Int16 -> Some (Int16Value (int16 i))
                | Int32Value i, Int64 -> Some (Int64Value (int64 i))
                | Int32Value i, Float32 -> Some (Float32Value (float32 i))
                | Int32Value i, Float64 -> Some (Float64Value (float i))
                | Float32Value f, Float64 -> Some (Float64Value (float f))
                | Float64Value f, Float32 -> Some (Float32Value (float32 f))
                | Float32Value f, Int32 -> Some (Int32Value (int32 f))
                | Float64Value f, Int32 -> Some (Int32Value (int32 f))
                
                // 불린 변환
                | BoolValue b, Int16 -> Some (Int16Value (if b then 1s else 0s))
                | BoolValue b, Int32 -> Some (Int32Value (if b then 1 else 0))
                | Int16Value i, Bool -> Some (BoolValue (i <> 0s))
                | Int32Value i, Bool -> Some (BoolValue (i <> 0))
                
                // 문자열 변환
                | StringValue s, Int32 ->
                    match Int32.TryParse(s) with
                    | true, i -> Some (Int32Value i)
                    | false, _ -> None
                | Int32Value i, String _ -> Some (StringValue (i.ToString()))
                | Float32Value f, String _ -> Some (StringValue (f.ToString()))
                | Float64Value f, String _ -> Some (StringValue (f.ToString()))
                | BoolValue b, String _ -> Some (StringValue (b.ToString()))
                
                | _ -> None
            with
            | _ -> None
    
    /// 값의 범위 검증
    let validateRange (value: ScalarValue) (minValue: ScalarValue option) (maxValue: ScalarValue option) : bool =
        match minValue, maxValue with
        | None, None -> true
        | Some min, None -> 
            match value, min with
            | Int32Value v, Int32Value minV -> v >= minV
            | Float64Value v, Float64Value minV -> v >= minV
            | _ -> true
        | None, Some max -> 
            match value, max with
            | Int32Value v, Int32Value maxV -> v <= maxV
            | Float64Value v, Float64Value maxV -> v <= maxV
            | _ -> true
        | Some min, Some max -> 
            match value, min, max with
            | Int32Value v, Int32Value minV, Int32Value maxV -> v >= minV && v <= maxV
            | Float64Value v, Float64Value minV, Float64Value maxV -> v >= minV && v <= maxV
            | _ -> true