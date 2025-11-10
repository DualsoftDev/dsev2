namespace Ev2.LsProtocol

open System
open System.Text
open Ev2.LsProtocol

/// <summary>
///     Core constants, enumerations, and helpers shared across the LS Electric XGT protocol implementation.
///     Centralising these definitions keeps the frame builder and communication layers slim and easier to reason about.
/// </summary>
[<AutoOpen>]
module XgtTypes =

    // -------------------------------------------------------------------------
    // Company / header metadata
    // -------------------------------------------------------------------------

    module CompanyHeader =
        /// ASCII identifier written into every XGT frame header.
        [<Literal>]
        let Id = "LSIS-XGT"

        /// Identifier padded to the required 12 bytes.
        let IdBytes =
            let buffer = Array.create 12 0uy
            let ascii = Encoding.ASCII.GetBytes(Id)
            Array.Copy(ascii, buffer, ascii.Length)
            buffer

    module SupportedAreaCodes =
        /// Area codes available on XGI CPUs.
        let internal xgi = [ 'I'; 'Q'; 'F'; 'M'; 'L'; 'N'; 'K'; 'U'; 'R'; 'A'; 'W' ]

        /// Area codes available on XGK CPUs.
        let internal xgk = [ 'P'; 'M'; 'K'; 'T'; 'C'; 'U'; 'S'; 'L'; 'N'; 'D'; 'R' ]

        /// Combined and de-duplicated set of known area codes.
        let All =
            xgi
            |> List.append xgk
            |> List.distinct

    // -------------------------------------------------------------------------
    // Enumerations
    // -------------------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type CpuType =
        | XGK_HighPerformance = 0x01
        | XGK_Standard = 0x02

    [<Flags>]
    type SystemStatus =
        | Stop   = 0x02
        | Run    = 0x04
        | Pause  = 0x08
        | Debug  = 0x10

    [<RequireQualifiedAccess>]
    type FrameSource =
        | ClientToServer = 0x33uy
        | ServerToClient = 0x11uy

    [<RequireQualifiedAccess>]
    type CommandCode =
        | ReadRequestEFMTB  = 0x1000us
        | ReadRequest       = 0x0054us
        | ReadResponse      = 0x0055us
        | WriteRequestEFMTB = 0x1010us
        | WriteRequest      = 0x0058us
        | WriteResponse     = 0x0059us
        | StatusRequest     = 0x00B0us
        | StatusResponse    = 0x00B1us

    [<RequireQualifiedAccess>]
    type DataType =
        | Bit     = 0x00us
        | Byte    = 0x01us
        | Word    = 0x02us
        | DWord   = 0x03us
        | LWord   = 0x04us

    [<RequireQualifiedAccess>]
    type DeviceType =
        | P = 0x50uy
        | M = 0x51uy
        | K = 0x53uy
        | T = 0x55uy
        | C = 0x56uy
        | U = 0x57uy
        | S = 0x58uy
        | L = 0x52uy
        | N = 0x59uy
        | D = 0x5Duy
        | R = 0x5Euy
        | I = 0x60uy
        | Q = 0x61uy
        | F = 0x62uy
        | A = 0x63uy
        | W = 0x64uy

    [<RequireQualifiedAccess>]
    type ErrorCode =
        | FrameError       = 0x00us
        | UnknownCommand   = 0x02us
        | UnknownSubCmd    = 0x03us
        | AddressError     = 0x04us
        | DataValueError   = 0x05us
        | DataSizeError    = 0x10us
        | DataTypeError    = 0x11us
        | DeviceTypeError  = 0x12us
        | TooManyBlocks    = 0x13us

    [<RequireQualifiedAccess>]
    type ResponseStatus =
        | Ok = 0x0000us
        | Error = 0xFFFFus

    // -------------------------------------------------------------------------
    // Helper functions
    // -------------------------------------------------------------------------

    /// Provides a human readable description of a low-level XGT error code.
    let getXgtErrorDescription (code: byte) : string =
        match code with
        | 0x10uy -> "Unsupported command."
        | 0x11uy -> "Command format error."
        | 0x12uy -> "Command length error."
        | 0x13uy -> "Data type error."
        | 0x14uy -> "Variable count error (max 16)."
        | 0x15uy -> "Variable name length error (max 16 chars)."
        | 0x16uy -> "Variable name format error."
        | 0x17uy -> "Variable not found or inaccessible."
        | 0x18uy -> "Read permission denied."
        | 0x19uy -> "Write permission denied."
        | 0x1Auy -> "PLC memory error."
        | 0x1Fuy -> "Unknown error."
        | 0x21uy -> "Frame checksum (BCC) error."
        | _      -> sprintf "Unknown error code: 0x%02X" code

    /// Maps our generic <see cref="PlcTagDataType"/> to the protocol specific <see cref="DataType"/>.
    let toDataTypeCode (dataType: PlcTagDataType) =
        match dataType with
        | PlcTagDataType.Bool -> DataType.Bit
        | PlcTagDataType.Int8
        | PlcTagDataType.UInt8 -> DataType.Byte
        | PlcTagDataType.Int16
        | PlcTagDataType.UInt16 -> DataType.Word
        | PlcTagDataType.Int32
        | PlcTagDataType.UInt32
        | PlcTagDataType.Float32 -> DataType.DWord
        | PlcTagDataType.Int64
        | PlcTagDataType.UInt64
        | PlcTagDataType.Float64 -> DataType.LWord
        | _ ->
            invalidArg "dataType" (sprintf "Unsupported PlcTagDataType for XGT: %A" dataType)

    /// Returns the mnemonic used when formatting addresses for diagnostic output.
    let toDataTypeChar (dataType: PlcTagDataType) =
        match dataType with
        | PlcTagDataType.Bool -> 'X'
        | PlcTagDataType.Int8
        | PlcTagDataType.UInt8 -> 'B'
        | PlcTagDataType.Int16
        | PlcTagDataType.UInt16 -> 'W'
        | PlcTagDataType.Int32
        | PlcTagDataType.UInt32
        | PlcTagDataType.Float32 -> 'D'
        | PlcTagDataType.Int64
        | PlcTagDataType.UInt64
        | PlcTagDataType.Float64 -> 'L'
        | _ ->
            invalidArg "dataType" (sprintf "Unsupported PlcTagDataType for XGT: %A" dataType)

    /// Number of bits represented by the provided tag data type.
    let rec bitSize (dataType: PlcTagDataType) =
        match dataType with
        | PlcTagDataType.Bool -> 1
        | PlcTagDataType.Int8
        | PlcTagDataType.UInt8 -> 8
        | PlcTagDataType.Int16
        | PlcTagDataType.UInt16 -> 16
        | PlcTagDataType.Int32
        | PlcTagDataType.UInt32
        | PlcTagDataType.Float32 -> 32
        | PlcTagDataType.Int64
        | PlcTagDataType.UInt64
        | PlcTagDataType.Float64 -> 64
        | PlcTagDataType.String length
        | PlcTagDataType.Bytes length -> 8 * length
        | PlcTagDataType.Array (inner, length) -> bitSize inner * length
        | PlcTagDataType.Struct fields -> fields |> List.sumBy (snd >> bitSize)

    /// Number of bytes represented by the tag data type (rounded up to the nearest byte).
    let byteSize (dataType: PlcTagDataType) =
        let bits = bitSize dataType
        (bits + 7) / 8

