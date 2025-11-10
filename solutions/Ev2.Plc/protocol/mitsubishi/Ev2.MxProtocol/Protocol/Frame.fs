namespace Ev2.MxProtocol.Protocol

open System
open Ev2.MxProtocol.Core

/// <summary>
///     Frame helpers for the MELSEC 3E binary protocol. The functions in this module are deliberately
///     lightweight so the packet builders can focus on higher level orchestration.
/// </summary>
[<AutoOpen>]
module Frame =

    // ---------------------------------------------------------------------
    // Basic utilities
    // ---------------------------------------------------------------------

    /// Serialises a 16-bit unsigned integer in little-endian order.
    let inline toBytes (value: uint16) =
        [| byte value; byte (value >>> 8) |]

    /// Reads a 16-bit unsigned integer from the supplied buffer.
    let inline private readUInt16 (buffer: byte[]) offset =
        uint16 buffer.[offset] ||| (uint16 buffer.[offset + 1] <<< 8)

    /// <summary>
    /// Converts a device address into the MELSEC binary address representation.
    /// For bit-unit operations, uses the address directly.
    /// For word-unit operations on bit devices, multiplies by 16.
    /// </summary>
    /// <param name="device">Device code</param>
    /// <param name="address">Device address/number</param>
    /// <param name="isBitUnit">True for bit-unit operations (0x0001), False for word-unit (0x0000)</param>
    let private deviceAddressToBytes (device: DeviceCode) (address: int) (isBitUnit: bool) =
        if device.IsWordDevice() then
            // Word devices always use direct address
            [| byte address; byte (address >>> 8); byte (address >>> 16) |]
        else
            // Bit devices: use direct address for bit-unit, multiply by 16 for word-unit
            let actualAddress = if isBitUnit then address else address * 16
            [| byte actualAddress; byte (actualAddress >>> 8); byte (actualAddress >>> 16) |]

    // ---------------------------------------------------------------------
    // Device helpers exposed to packet builders
    // ---------------------------------------------------------------------

    module Device =
        /// <summary>
        /// Encodes a standard device reference (used by batch and block operations).
        /// </summary>
        /// <param name="device">Device code</param>
        /// <param name="address">Device address</param>
        /// <param name="isBitUnit">True if this is a bit-unit operation</param>
        let encodeBinary (device: DeviceCode) (address: int) (isBitUnit: bool) =
            [|
                yield! deviceAddressToBytes device address isBitUnit
                yield device.ToByte()
            |]

        /// <summary>
        /// Encodes a device reference for random access operations (word vs. dword).
        /// Random access always uses word-unit addressing.
        /// </summary>
        let encodeRandomBinary (device: DeviceCode) (address: int) (isDWord: bool) =
            [|
                yield! deviceAddressToBytes device address false  // Random access uses word-unit
                yield device.ToByte()
                yield if isDWord then 1uy else 0uy
            |]

    // ---------------------------------------------------------------------
    // Frame builders / parsers
    // ---------------------------------------------------------------------

    /// Builds a MELSEC 3E request frame from the supplied request record.
    let buildFrame (config: MelsecConfig) (request: MelsecRequest) =
        let header = [|
            // Subheader
            yield! toBytes SubheaderRequestBinary
            // Access route
            yield config.AccessRoute.NetworkNumber
            yield config.AccessRoute.StationNumber
            yield! toBytes config.AccessRoute.IoNumber
            yield config.AccessRoute.RelayType
            // Data length (command + subcommand + payload)
            yield! toBytes (uint16 (6 + request.Payload.Length))
        |]

        [|
            yield! header
            yield! toBytes config.MonitoringTimer
            yield! toBytes (uint16 request.Command)
            yield! toBytes (uint16 request.Subcommand)
            yield! request.Payload
        |]

    /// Parses a MELSEC 3E response frame and extracts the end code and payload when present.
    let parseFrame (_: MelsecConfig) (buffer: byte[]) : Result<MelsecResponse, string> =
        try
            if buffer.Length < FrameHeaderWithEndCode then
                Error (sprintf "Frame too short. Minimum %d bytes, received %d." FrameHeaderWithEndCode buffer.Length)
            else
                let subHeader = readUInt16 buffer 0
                if subHeader <> SubheaderResponseBinary then
                    Error (sprintf "Unexpected sub header 0x%04X." subHeader)
                else
                    let dataLength = int (readUInt16 buffer 7)
                    let expectedLength = FrameHeaderLength3E + dataLength

                    if buffer.Length < expectedLength then
                        Error (sprintf "Incomplete frame. Expected %d bytes, received %d." expectedLength buffer.Length)
                    else
                        let endCodeValue = readUInt16 buffer 9
                        let endCode =
                            if endCodeValue = 0us then
                                EndCodeSuccess 0us
                            else
                                let networkError = if buffer.Length > 11 then buffer.[11] else 0uy
                                let stationError = if buffer.Length > 12 then buffer.[12] else 0uy
                                EndCodeError (endCodeValue, networkError, stationError)

                        let dataStartIndex = if endCodeValue = 0us then 11 else 13
                        let payload =
                            if expectedLength > dataStartIndex && buffer.Length >= expectedLength then
                                buffer.[dataStartIndex .. expectedLength - 1]
                            else
                                [||]

                        Ok { EndCode = endCode; Data = payload }
        with
        | ex -> Error (sprintf "Failed to parse frame: %s" ex.Message)

    // ---------------------------------------------------------------------
    // Convenience request builders used by tests and integrations
    // ---------------------------------------------------------------------

    let createReadBitRequest (device: DeviceCode) (address: int) (count: int) : MelsecRequest =
        let payload = [|
            yield! deviceAddressToBytes device address true  // Bit-unit operation
            yield device.ToByte()
            yield! toBytes (uint16 count)
        |]
        { Command = CommandCode.BatchRead
          Subcommand = SubcommandCode.BitUnits
          Payload = payload }

    let createWriteBitRequest (device: DeviceCode) (address: int) (values: bool array) : MelsecRequest =
        let payload = [|
            yield! deviceAddressToBytes device address true  // Bit-unit operation
            yield device.ToByte()
            yield! toBytes (uint16 values.Length)
            // Pack 2 bits per byte (MELSEC bit-unit standard)
            for i in 0 .. 2 .. values.Length - 1 do
                let byte1 = if values.[i] then 0x10uy else 0x00uy
                let byte2 = if i + 1 < values.Length && values.[i + 1] then 0x01uy else 0x00uy
                yield byte1 ||| byte2
        |]
        { Command = CommandCode.BatchWrite
          Subcommand = SubcommandCode.BitUnits
          Payload = payload }

    let createReadWordRequest (device: DeviceCode) (address: int) (count: int) : MelsecRequest =
        let payload = [|
            yield! deviceAddressToBytes device address false  // Word-unit operation
            yield device.ToByte()
            yield! toBytes (uint16 count)
        |]
        { Command = CommandCode.BatchRead
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }

    let createWriteWordRequest (device: DeviceCode) (address: int) (values: uint16 array) : MelsecRequest =
        let payload = [|
            yield! deviceAddressToBytes device address false  // Word-unit operation
            yield device.ToByte()
            yield! toBytes (uint16 values.Length)
            for value in values do
                yield! toBytes value
        |]
        { Command = CommandCode.BatchWrite
          Subcommand = SubcommandCode.WordUnits
          Payload = payload }