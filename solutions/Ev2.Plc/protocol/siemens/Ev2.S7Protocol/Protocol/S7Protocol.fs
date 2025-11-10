namespace Ev2.S7Protocol.Protocol

open System
open Ev2.S7Protocol.Core

/// <summary>
///     Helpers that build well-formed S7 request frames with correct address encoding.
///     Fixed overflow calculation to use proper 24-bit addressing (high byte + low word).
/// </summary>
[<AutoOpen>]
module S7Protocol =

    // ---------------------------------------------------------------------
    // Header composition helpers
    // ---------------------------------------------------------------------

    let private tpktHeader payloadLength =
        [|
            0x03uy
            0x00uy
            byte (payloadLength >>> 8)
            byte payloadLength
        |]

    let private cotpConnectionRequest =
        [|
            0x11uy // Length
            0xE0uy // CR
            0x00uy; 0x00uy // Dest ref
            0x00uy; 0x01uy // Src ref
            0x00uy        // Flags
        |]

    let private cotpData =
        [|
            0x02uy
            0xF0uy
            0x80uy
        |]

    let private s7JobHeader parameterLength dataLength =
        [|
            0x32uy              // Protocol ID
            0x01uy              // PDU type: Job
            0x00uy; 0x00uy      // Reserved
            0x00uy; 0x00uy      // PDU reference (caller sets)
            byte (parameterLength >>> 8)
            byte parameterLength
            byte (dataLength >>> 8)
            byte dataLength
        |]

    // ---------------------------------------------------------------------
    // Connection/setup frames
    // ---------------------------------------------------------------------

    let buildCotpConnect localTsap remoteTsap =
        Array.concat [
            tpktHeader 0x16
            cotpConnectionRequest
            [|
                0xC0uy; 0x01uy; 0x0Auy
                0xC1uy; 0x02uy; byte (localTsap >>> 8); byte localTsap
                0xC2uy; 0x02uy; byte (remoteTsap >>> 8); byte remoteTsap
            |]
        ]

    let buildS7Setup () =
        let parameter =
            [|
                0xF0uy; 0x00uy
                0x00uy; 0x01uy
                0x00uy; 0x01uy
                0x01uy; 0xE0uy
            |]

        Array.concat [
            tpktHeader 0x19
            cotpData
            s7JobHeader parameter.Length 0
            parameter
        ]

    // ---------------------------------------------------------------------
    // Read/write requests with FIXED address encoding
    // ---------------------------------------------------------------------

    /// <summary>
    /// Packs variable specification with correct 24-bit address encoding.
    /// S7 uses: [high_byte(1)][low_word(2)] for bit addresses.
    /// 
    /// FIXED: Changed from (startByte * 8) / 0xFFFF to (startByte * 8) >>> 16
    /// 
    /// Example: startByte = 65535
    ///   OLD (incorrect): overflow = 8, address = 65528
    ///   NEW (correct):   overflow = 7, address = 65528
    /// </summary>
    let private packReadWriteSpec area db startByte count transportSize =
        // Convert byte address to a 24-bit bit address safely.
        let bitAddress = int64 startByte * 8L

        // Extract the high byte and low word segments used in S7ANY addressing.
        let overflow = byte (bitAddress >>> 16)
        let address = int bitAddress &&& 0xFFFF

        [|
            0x12uy  // Variable spec
            0x0Auy  // Length of following address specification
            0x10uy  // Syntax ID: S7ANY
            transportSize
            byte (count >>> 8); byte count
            byte (db >>> 8); byte db
            byte area
            overflow
            byte (address >>> 8); byte address
        |]

    let buildReadBytesRequest (area: DataArea) (db: int) (startByte: int) (count: int) =
        let parameters =
            Array.concat [
                [| 0x04uy; 0x01uy |]  // Function: Read Variable, Item count: 1
                packReadWriteSpec (byte area) db startByte count 0x02uy  // Transport size: BYTE
            ]

        Array.concat [
            s7JobHeader parameters.Length 0
            parameters
        ]

    let buildWriteBytesRequest (area: DataArea) (db: int) (startByte: int) (data: byte[]) =
        let parameters =
            Array.concat [
                [| 0x05uy; 0x01uy |]  // Function: Write Variable, Item count: 1
                packReadWriteSpec (byte area) db startByte data.Length 0x02uy
            ]
        
        let payloadLength = data.Length + 4
        let dataSection =
            Array.concat [
                [| 
                    0x00uy  // Return code (filled by client)
                    0x04uy  // Transport size: BYTE/WORD/INTEGER
                    byte ((data.Length * 8) >>> 8)
                    byte ((data.Length * 8) &&& 0xFF)
                |]
                data
            ]

        Array.concat [
            s7JobHeader parameters.Length payloadLength
            parameters
            dataSection
        ]

    /// <summary>
    /// Builds write bit request with correct bit-level addressing.
    /// </summary>
    let buildWriteBitRequest (area: DataArea) (db: int) (startByte: int) (bit: int) (value: bool) =
        // Calculate bit address: (byte * 8) + bit_offset
        let bitAddress = startByte * 8 + bit
        
        // Extract 24-bit components
        let overflow = byte (bitAddress >>> 16)
        let address = bitAddress &&& 0xFFFF

        let parameters =
            [|
                0x05uy; 0x01uy  // Function: Write Variable, Item count: 1
                0x12uy; 0x0Auy  // Variable spec, length
                0x10uy          // Syntax ID: S7ANY
                0x01uy          // Transport size: BIT
                0x00uy; 0x01uy  // Count: 1 bit
                byte (db >>> 8); byte db
                byte area
                overflow
                byte (address >>> 8); byte address
            |]

        let dataSection =
            [|
                0x00uy  // Return code
                0x03uy  // Transport size: BIT
                0x00uy; 0x01uy  // Length in bits
                if value then 0x01uy else 0x00uy
            |]

        Array.concat [
            s7JobHeader parameters.Length dataSection.Length
            parameters
            dataSection
        ]

    let buildReadBitRequest area db startByte =
        buildReadBytesRequest area db startByte 1

    // ---------------------------------------------------------------------
    // Response parsing helpers with improved error messages
    // ---------------------------------------------------------------------

    let parseReadResponse (buffer: byte[]) =
        if buffer.Length < 14 then
            Error $"Response too short: {buffer.Length} bytes (minimum 14 required)."
        elif buffer.[0] <> 0x32uy then
            Error $"Invalid protocol identifier: 0x{buffer.[0]:X2} (expected 0x32)."
        elif buffer.[1] <> 0x03uy then
            Error $"Unexpected PDU type: 0x{buffer.[1]:X2} (expected 0x03 AckData)."
        else
            let parameterLength = (int buffer.[6] <<< 8) ||| int buffer.[7]
            let dataStart = 12 + parameterLength

            if dataStart >= buffer.Length then
                Error $"Response payload offset {dataStart} out of range (buffer length: {buffer.Length})."
            else
                let returnCode = buffer.[dataStart]
                match returnCode with
                | 0xFFuy ->
                    if dataStart + 4 >= buffer.Length then
                        Error "Read response truncated (missing data length fields)."
                    else
                        let lengthBits = (int buffer.[dataStart + 2] <<< 8) ||| int buffer.[dataStart + 3]
                        let lengthBytes = (lengthBits + 7) / 8

                        if dataStart + 4 + lengthBytes > buffer.Length then
                            Error $"Read response data incomplete: expected {lengthBytes} bytes, buffer has {buffer.Length - dataStart - 4}."
                        else
                            Ok (Array.sub buffer (dataStart + 4) lengthBytes)
                | 0x01uy -> Error "Hardware fault."
                | 0x03uy -> Error "Access denied (check permissions)."
                | 0x05uy -> Error "Address out of range."
                | 0x06uy -> Error "Data type not supported."
                | 0x07uy -> Error "Data type inconsistent."
                | 0x0Auy -> Error "Object does not exist."
                | other -> Error $"Item error code: 0x{other:X2}."

    let parseWriteResponse (buffer: byte[]) =
        if buffer.Length < 14 then
            Error $"Write response too short: {buffer.Length} bytes (minimum 14 required)."
        elif buffer.[0] <> 0x32uy then
            Error $"Invalid protocol identifier: 0x{buffer.[0]:X2} (expected 0x32)."
        elif buffer.[1] <> 0x03uy then
            Error $"Unexpected PDU type: 0x{buffer.[1]:X2} (expected 0x03 AckData)."
        else
            let parameterLength = (int buffer.[6] <<< 8) ||| int buffer.[7]
            let dataStart = 12 + parameterLength
            
            if dataStart >= buffer.Length then
                Error "Write response missing return code."
            else
                let responseCode = buffer.[dataStart]
                match responseCode with
                | 0xFFuy -> Ok ()
                | 0x01uy -> Error "Hardware error."
                | 0x03uy -> Error "Access denied (check password protection)."
                | 0x04uy -> Error "Invalid address (write)."
                | 0x05uy -> Error "Address out of range (write)."
                | 0x06uy -> Error "Data type not supported (write)."
                | 0x07uy -> Error "Data type inconsistent (write)."
                | 0x0Auy -> Error "Object does not exist (write)."
                | other -> Error $"Write error code: 0x{other:X2}."
