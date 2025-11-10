namespace Ev2.LsProtocol

open System
open System.Net
open System.Text
open System.Text.RegularExpressions
open Ev2.LsProtocol

open XgtTypes
open Ev2.LsProtocol

/// <summary>
///     Low-level helpers shared across the XGT implementation.  The module is intentionally lightweight so the core
///     communication class can stay focused on network concerns.
/// </summary>
[<AutoOpen>]
module XgtUtil =

    [<Literal>]
    let HeaderSize = 20

    /// <summary>
    ///     Constructs the textual address used by XGT devices (e.g. <c>%MW100</c>).
    /// </summary>
    let formatAddress (deviceType: char) (dataType: PlcTagDataType) (bitOffset: int) =
        let index =
            match dataType with
            | PlcTagDataType.Bool -> bitOffset
            | PlcTagDataType.Int8
            | PlcTagDataType.UInt8 -> bitOffset / 8
            | PlcTagDataType.Int16
            | PlcTagDataType.UInt16 -> bitOffset / 16
            | PlcTagDataType.Int32
            | PlcTagDataType.UInt32
            | PlcTagDataType.Float32 -> bitOffset / 32
            | PlcTagDataType.Int64
            | PlcTagDataType.UInt64
            | PlcTagDataType.Float64 -> bitOffset / 64
            | PlcTagDataType.String _
            | PlcTagDataType.Bytes _
            | PlcTagDataType.Array _
            | PlcTagDataType.Struct _ ->
                invalidArg "dataType" $"Cannot derive address index for composite type: {dataType}"
        sprintf "%%%c%c%d" deviceType (toDataTypeChar dataType) index

    /// Generates a two byte frame identifier based on the IP and source port.
    let getFrameIdBytes (ip: string) (sourcePort: int) =
        let parts = ip.Split('.')
        if parts.Length <> 4 then invalidArg "ip" $"Invalid IP address: {ip}"
        [| byte sourcePort; byte (int parts.[3]) |]

    /// Copies the LS company header into the provided frame buffer.
    let copyCompanyId (frame: byte[]) =
        if frame.Length < CompanyHeader.IdBytes.Length then
            invalidArg "frame" "Frame length must be at least 8 bytes."
        Array.Copy(CompanyHeader.IdBytes, 0, frame, 0, CompanyHeader.IdBytes.Length)

/// <summary>
///     Utilities for the conversion between <see cref="ScalarValue"/> and raw byte arrays.
/// </summary>
[<AutoOpen>]
module XgtDataConverter =

    let toBytes (dataType: PlcTagDataType) (value: ScalarValue) =
        match value with
        | ScalarValue.BoolValue _
        | ScalarValue.Int8Value _
        | ScalarValue.UInt8Value _
        | ScalarValue.Int16Value _
        | ScalarValue.UInt16Value _
        | ScalarValue.Int32Value _
        | ScalarValue.UInt32Value _
        | ScalarValue.Int64Value _
        | ScalarValue.UInt64Value _
        | ScalarValue.Float32Value _
        | ScalarValue.Float64Value _
        | ScalarValue.StringValue _
        | ScalarValue.BytesValue _ -> value.ToBytes()
        | _ ->
            invalidArg "value" $"Unsupported scalar value for XGT write: {value}"

    let fromBytes (bytes: byte[]) (dataType: PlcTagDataType) =
        ScalarValue.FromBytes(bytes, dataType)

/// <summary>
///     Frame utility helpers - checksum calculation and manual fallbacks for parsing legacy addresses.
/// </summary>
[<AutoOpen>]
module FrameUtils =

    let calculateChecksum (data: byte[]) (length: int) : byte =
        data
        |> Seq.take length
        |> Seq.sumBy int
        |> byte

    /// Manual parser used as last resort when a tag fails to parse via the generated tag parsers.
    let private tryParseManualTag (address: string) =
        let pattern = @"([A-Z]+)([XBWDLQ])(\d+)$"
        let regex = Regex(pattern, RegexOptions.IgnoreCase)
        let matchResult = regex.Match(address.TrimStart('%'))
        if matchResult.Success then
            let deviceName = matchResult.Groups.[1].Value
            let dataTypeChar = matchResult.Groups.[2].Value.ToUpperInvariant()
            let offset = Int32.Parse(matchResult.Groups.[3].Value)
            let bitSize =
                match dataTypeChar with
                | "X" -> 1
                | "B" -> 8
                | "W" -> 16
                | "D" -> 32
                | "L" -> 64
                | "Q" -> 128
                | _ -> invalidArg "address" $"Unsupported data type symbol in manual parser: {dataTypeChar}"
            Some(deviceName, bitSize, bitSize * offset)
        else
            None

    /// Resolves device information for the specified tag using the available parsers.
    let resolveTagInfo (address: string) (isBit: bool) =
        match LsXgiTagParser.TryParse address with
        | Some info -> info
        | None ->
            match LsXgkTagParser.TryParseWithIsBoolType(address, isBit) with
            | Some info -> info
            | None ->
                match tryParseManualTag address with
                | Some info -> info
                | None -> invalidArg "address" $"Unsupported address format: {address}"

/// <summary>
///     Model representing a read or write block in an XGT frame.
/// </summary>
[<AutoOpen>]
module ReadWriteBlockFactory =

    type ReadWriteBlock = {
        DeviceType: char
        DataType: PlcTagDataType
        BitPosition: int
        ByteOffset: int
        Value: ScalarValue option
    } with
        member this.BitOffset = this.ByteOffset * 8 + this.BitPosition
        member this.Address = formatAddress this.DeviceType this.DataType this.BitOffset

    let private buildBlock address dataType value =
        let isBitType =
            match dataType with
            | PlcTagDataType.Bool -> true
            | _ -> false

        let deviceName, _, bitOffset = FrameUtils.resolveTagInfo address isBitType
        if String.IsNullOrWhiteSpace deviceName then
            invalidArg "address" $"Unable to resolve device name for {address}"

        let deviceType = deviceName.[0]
        let bitPosition = if dataType = PlcTagDataType.Bool then bitOffset % 8 else 0
        let byteOffset = bitOffset / 8

        { DeviceType = deviceType
          DataType = dataType
          BitPosition = bitPosition
          ByteOffset = byteOffset
          Value = value }

    let getReadBlock address dataType =
        buildBlock address dataType None

    let getWriteBlock address dataType value =
        buildBlock address dataType (Some value)
