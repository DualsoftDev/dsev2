namespace Ev2.PLC.Common.Types

open System

[<AutoOpen>]
module PlcTagDataTypeExtensions =

    let rec private elementBitSize (dataType: PlcTagDataType) =
        match dataType with
        | PlcTagDataType.Bool -> 1
        | PlcTagDataType.Int8 | PlcTagDataType.UInt8 -> 8
        | PlcTagDataType.Int16 | PlcTagDataType.UInt16 -> 16
        | PlcTagDataType.Int32 | PlcTagDataType.UInt32 | PlcTagDataType.Float32 -> 32
        | PlcTagDataType.Int64 | PlcTagDataType.UInt64 | PlcTagDataType.Float64 -> 64
        | PlcTagDataType.String length -> 8 * length
        | PlcTagDataType.Bytes length -> 8 * length
        | PlcTagDataType.Array (inner, length) -> elementBitSize inner * length
        | PlcTagDataType.Struct fields -> fields |> List.sumBy (snd >> elementBitSize)

    /// Gets the number of bits represented by the tag data type.
    let bitSize (dataType: PlcTagDataType) = elementBitSize dataType

    /// Gets the number of bytes represented by the tag data type.
    let byteSize (dataType: PlcTagDataType) =
        let bits = bitSize dataType
        (bits + 7) / 8

    /// Builds a primitive tag data type from the number of bits.
    let fromBitSize bitCount =
        match bitCount with
        | 1 -> PlcTagDataType.Bool
        | 8 -> PlcTagDataType.UInt8
        | 16 -> PlcTagDataType.UInt16
        | 32 -> PlcTagDataType.UInt32
        | 64 -> PlcTagDataType.UInt64
        | _ when bitCount % 8 = 0 -> PlcTagDataType.Bytes (bitCount / 8)
        | _ -> invalidArg "bitCount" (sprintf "Unsupported bit size %d" bitCount)

    /// Attempts to parse a textual representation of a primitive into a tag data type.
    let tryFromString (text: string) =
        match text.Trim().ToUpperInvariant() with
        | "BOOL" | "BOOLEAN" | "BIT" -> Some PlcTagDataType.Bool
        | "SBYTE" | "SINT" -> Some PlcTagDataType.Int8
        | "BYTE" | "USINT" -> Some PlcTagDataType.UInt8
        | "INT" | "INT16" -> Some PlcTagDataType.Int16
        | "UINT" | "UINT16" | "WORD" -> Some PlcTagDataType.UInt16
        | "DINT" | "INT32" -> Some PlcTagDataType.Int32
        | "UDINT" | "UINT32" | "DWORD" -> Some PlcTagDataType.UInt32
        | "LINT" | "INT64" -> Some PlcTagDataType.Int64
        | "ULINT" | "UINT64" | "LWORD" -> Some PlcTagDataType.UInt64
        | "REAL" | "FLOAT" | "FLOAT32" -> Some PlcTagDataType.Float32
        | "LREAL" | "DOUBLE" | "FLOAT64" -> Some PlcTagDataType.Float64
        | "STRING" -> Some (PlcTagDataType.String 0)
        | _ -> None

    let fromString text =
        match tryFromString text with
        | Some value -> value
        | None -> invalidArg "text" (sprintf "Unsupported data type string '%s'" text)

