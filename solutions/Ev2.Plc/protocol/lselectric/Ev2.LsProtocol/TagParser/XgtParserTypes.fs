namespace Ev2.LsProtocol
open System

type XgtParserTag = {
    DeviceType: string
    DataSize: int
    TotalBitOffset: int
}



/// PLC 데이터 타입
type PlcTagDataType =
    | Bool
    | Int8  | UInt8
    | Int16 | UInt16  
    | Int32 | UInt32
    | Int64 | UInt64
    | Float32 | Float64
    | String of maxLength: int
    | Bytes of maxLength: int
    | Array of elementType: PlcTagDataType * length: int
    | Struct of fields: (string * PlcTagDataType) list

    member this.Size =
        match this with
        | Bool -> 1
        | Int8 | UInt8 -> 1
        | Int16 | UInt16 -> 2
        | Int32 | UInt32 | Float32 -> 4
        | Int64 | UInt64 | Float64 -> 8
        | String maxLength -> maxLength
        | Bytes maxLength -> maxLength
        | Array (elementType, length) -> elementType.Size * length
        | Struct fields -> fields |> List.sumBy (fun (_, dataType) -> dataType.Size)

    member this.IsNumeric =
        match this with
        | Int8 | UInt8 | Int16 | UInt16 | Int32 | UInt32 | Int64 | UInt64 
        | Float32 | Float64 -> true
        | _ -> false

    member this.IsInteger =
        match this with
        | Int8 | UInt8 | Int16 | UInt16 | Int32 | UInt32 | Int64 | UInt64 -> true
        | _ -> false

    member this.IsFloat =
        match this with
        | Float32 | Float64 -> true
        | _ -> false

    member this.IsSigned =
        match this with
        | Int8 | Int16 | Int32 | Int64 | Float32 | Float64 -> true
        | _ -> false

    static member FromString(typeStr: string) =
        match typeStr.ToLower() with
        | "bool" | "boolean" -> Some Bool
        | "int8" | "sbyte" -> Some Int8
        | "uint8" | "byte" -> Some UInt8
        | "int16" | "short" -> Some Int16
        | "uint16" | "ushort" -> Some UInt16
        | "int32" | "int" -> Some Int32
        | "uint32" | "uint" -> Some UInt32
        | "int64" | "long" -> Some Int64
        | "uint64" | "ulong" -> Some UInt64
        | "float32" | "float" -> Some Float32
        | "float64" | "double" -> Some Float64
        | _ when typeStr.StartsWith("string[") ->
            let lengthStr = typeStr.Substring(7, typeStr.Length - 8)
            match Int32.TryParse(lengthStr) with
            | true, length -> Some (String length)
            | false, _ -> None
        | _ when typeStr.StartsWith("bytes[") ->
            let lengthStr = typeStr.Substring(6, typeStr.Length - 7)
            match Int32.TryParse(lengthStr) with
            | true, length -> Some (Bytes length)
            | false, _ -> None
        | _ -> None

/// ��Į�� �� Ÿ�� - PLC �������� ���� ���� ��� Ÿ��
type ScalarValue =
    | BoolValue of bool
    | Int8Value of sbyte    | UInt8Value of byte
    | Int16Value of int16   | UInt16Value of uint16
    | Int32Value of int32   | UInt32Value of uint32
    | Int64Value of int64   | UInt64Value of uint64
    | Float32Value of float32 | Float64Value of float
    | StringValue of string
    | BytesValue of byte[]
    | ArrayValue of ScalarValue[]
    | StructValue of Map<string, ScalarValue>

    override this.ToString() =
        match this with
        | BoolValue b -> b.ToString()
        | Int8Value i -> i.ToString()    | UInt8Value i -> i.ToString()
        | Int16Value i -> i.ToString()   | UInt16Value i -> i.ToString()
        | Int32Value i -> i.ToString()   | UInt32Value i -> i.ToString()
        | Int64Value i -> i.ToString()   | UInt64Value i -> i.ToString()
        | Float32Value f -> f.ToString() | Float64Value f -> f.ToString()
        | StringValue s -> s
        | BytesValue bytes -> Convert.ToBase64String(bytes)
        | ArrayValue values -> 
            let valueStrs = values |> Array.map (fun v -> v.ToString())
            "[" + String.concat "; " valueStrs + "]"
        | StructValue fields -> 
            let fieldStrs = fields |> Map.toList |> List.map (fun (k, v) -> k + ": " + v.ToString())
            "{" + String.concat "; " fieldStrs + "}"

    member this.DataType =
        match this with
        | BoolValue _ -> Bool
        | Int8Value _ -> Int8      | UInt8Value _ -> UInt8
        | Int16Value _ -> Int16    | UInt16Value _ -> UInt16
        | Int32Value _ -> Int32    | UInt32Value _ -> UInt32
        | Int64Value _ -> Int64    | UInt64Value _ -> UInt64
        | Float32Value _ -> Float32 | Float64Value _ -> Float64
        | StringValue s -> String s.Length
        | BytesValue bytes -> Bytes bytes.Length
        | ArrayValue values -> 
            if values.Length > 0 then 
                Array (values.[0].DataType, values.Length)
            else 
                Array (Bool, 0)
        | StructValue fields -> 
            let fieldTypes = fields |> Map.toList |> List.map (fun (k, v) -> (k, v.DataType))
            Struct fieldTypes

    /// Ÿ�Կ� �´� �⺻�� ��ȯ
    static member GetDefaultValue(dataType: PlcTagDataType) =
        match dataType with
        | Bool -> BoolValue false
        | Int8 -> Int8Value 0y        | UInt8 -> UInt8Value 0uy
        | Int16 -> Int16Value 0s      | UInt16 -> UInt16Value 0us
        | Int32 -> Int32Value 0       | UInt32 -> UInt32Value 0u
        | Int64 -> Int64Value 0L      | UInt64 -> UInt64Value 0UL
        | Float32 -> Float32Value 0.0f | Float64 -> Float64Value 0.0
        | String _ -> StringValue ""
        | Bytes length -> BytesValue (Array.zeroCreate length)
        | Array (elementType, length) -> 
            ArrayValue (Array.init length (fun _ -> ScalarValue.GetDefaultValue(elementType)))
        | Struct fields -> 
            let fieldMap = fields |> List.map (fun (name, fieldType) -> 
                (name, ScalarValue.GetDefaultValue(fieldType))) |> Map.ofList
            StructValue fieldMap

    /// ���� ����Ʈ ǥ�� ��ȯ
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
        | BytesValue bytes -> bytes
        | ArrayValue values -> 
            values |> Array.collect (fun v -> v.ToBytes())
        | StructValue fields -> 
            fields |> Map.toList |> List.collect (fun (_, v) -> v.ToBytes() |> Array.toList) |> Array.ofList

    /// ����Ʈ �迭�κ��� �� ����
    static member FromBytes(bytes: byte[], dataType: PlcTagDataType) =
        try
            match dataType with
            | Bool -> BoolValue (bytes.[0] <> 0uy)
            | Int8 -> Int8Value (sbyte bytes.[0])
            | UInt8 -> UInt8Value bytes.[0]
            | Int16 -> Int16Value (BitConverter.ToInt16(bytes, 0))
            | UInt16 -> UInt16Value (BitConverter.ToUInt16(bytes, 0))
            | Int32 -> Int32Value (BitConverter.ToInt32(bytes, 0))
            | UInt32 -> UInt32Value (BitConverter.ToUInt32(bytes, 0))
            | Int64 -> Int64Value (BitConverter.ToInt64(bytes, 0))
            | UInt64 -> UInt64Value (BitConverter.ToUInt64(bytes, 0))
            | Float32 -> Float32Value (BitConverter.ToSingle(bytes, 0))
            | Float64 -> Float64Value (BitConverter.ToDouble(bytes, 0))
            | String maxLength -> 
                let str = System.Text.Encoding.UTF8.GetString(bytes, 0, min maxLength bytes.Length)
                StringValue (str.TrimEnd('\000'))
            | Bytes _ -> BytesValue bytes
            | Array (elementType, length) ->
                let elementSize = elementType.Size
                let values = Array.init length (fun i ->
                    let offset = i * elementSize
                    let elementBytes = bytes.[offset..offset + elementSize - 1]
                    ScalarValue.FromBytes(elementBytes, elementType))
                ArrayValue values
            | Struct fields ->
                let mutable offset = 0
                let fieldValues = fields |> List.map (fun (name, fieldType) ->
                    let fieldSize = fieldType.Size
                    let fieldBytes = bytes.[offset..offset + fieldSize - 1]
                    offset <- offset + fieldSize
                    (name, ScalarValue.FromBytes(fieldBytes, fieldType)))
                StructValue (Map.ofList fieldValues)
        with
        | ex -> 
            failwith $"Failed to convert bytes to {dataType}: {ex.Message}"