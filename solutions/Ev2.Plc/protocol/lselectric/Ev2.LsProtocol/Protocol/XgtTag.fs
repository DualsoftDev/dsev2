namespace Ev2.LsProtocol

open System
open Ev2.LsProtocol
open Ev2.LsProtocol


/// LS XGT specific scan tag representation.
type XgtTag(name: string, address: string, dataType: PlcTagDataType, bitOffset: int, isOutput: bool, ?comment: string) =

    let isXgiAddress = address.StartsWith("%", StringComparison.OrdinalIgnoreCase)

    member val LWordOffset = -1 with get, set
    member val QWordOffset = -1 with get, set

    member this.Device =
        let trimmed = address.TrimStart('%')
        if String.IsNullOrEmpty trimmed then "A"
        elif trimmed.StartsWith("ZR") then trimmed.Substring(0, 2)
        else trimmed.Substring(0, 1)

    member _.BitOffset = bitOffset

    member this.LWordTag =
        if isXgiAddress then sprintf "%%%sL%d" this.Device (this.BitOffset / 64)
        else sprintf "%sL%d" this.Device (this.BitOffset / 64)

    member this.QWordTag =
        if isXgiAddress then sprintf "%%%sQ%d" this.Device (this.BitOffset / 128)
        else sprintf "%sQ%d" this.Device (this.BitOffset / 128)

    member this.AddressKey = $"{this.Device}_{this.BitOffset}"

    member this.GetAddressAlias(targetType: PlcTagDataType) =
        let alias =
            match targetType with
            | PlcTagDataType.Bool ->
                if isXgiAddress then sprintf "%sX%d" this.Device this.BitOffset
                else sprintf "%s%d" this.Device this.BitOffset
            | PlcTagDataType.UInt8 | PlcTagDataType.Int8 ->
                sprintf "%sB%d.%d" this.Device (this.BitOffset / 8) (this.BitOffset % 8)
            | PlcTagDataType.UInt16 | PlcTagDataType.Int16 ->
                sprintf "%sW%d.%d" this.Device (this.BitOffset / 16) (this.BitOffset % 16)
            | PlcTagDataType.UInt32 | PlcTagDataType.Int32 | PlcTagDataType.Float32 ->
                sprintf "%sD%d.%d" this.Device (this.BitOffset / 32) (this.BitOffset % 32)
            | PlcTagDataType.UInt64 | PlcTagDataType.Int64 | PlcTagDataType.Float64 ->
                sprintf "%sL%d.%d" this.Device (this.BitOffset / 64) (this.BitOffset % 64)
            | _ -> invalidArg "targetType" (sprintf "Unsupported alias for %A" targetType)
        if isXgiAddress then "%" + alias else alias
