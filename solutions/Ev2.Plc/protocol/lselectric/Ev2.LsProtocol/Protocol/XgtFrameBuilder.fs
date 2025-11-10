namespace Ev2.LsProtocol

open System
open System.Collections.Generic
open System.Text
open Ev2.LsProtocol

open XgtTypes
open XgtUtil
open XgtDataConverter
open ReadWriteBlockFactory
open FrameUtils

/// <summary>
///     Responsible for building XGT request frames.  The functions in this module are pure - no network access - so they
///     are straightforward to unit test and reason about.
/// </summary>
[<AutoOpen>]
module XgtFrameBuilder =

    [<Literal>]
    let private MaxLWordBatchSize = 16

    [<Literal>]
    let private MaxQWordBatchSize = 64

    // -------------------------------------------------------------------------
    // Header helpers
    // -------------------------------------------------------------------------

    let private writeHeader (frame: byte[]) (frameId: byte[]) bodyLength protocolId direction =
        copyCompanyId frame
        let lengthBytes = BitConverter.GetBytes(uint16 bodyLength)
        frame.[12] <- protocolId
        frame.[13] <- direction
        frame.[14] <- frameId.[0]
        frame.[15] <- frameId.[1]
        frame.[16] <- lengthBytes.[0]
        frame.[17] <- lengthBytes.[1]
        frame.[18] <- 0uy
        frame.[19] <- calculateChecksum frame 19

    let private createStandardHeader frameId bodyLength =
        let frame = Array.zeroCreate<byte> HeaderSize
        writeHeader frame frameId bodyLength 0x00uy (byte FrameSource.ClientToServer)
        frame

    let private setCommand (frame: byte[]) offset (command: CommandCode) =
        let cmdBytes = BitConverter.GetBytes(uint16 command)
        frame.[offset] <- cmdBytes.[0]
        frame.[offset + 1] <- cmdBytes.[1]

    // -------------------------------------------------------------------------
    // Address encoders
    // -------------------------------------------------------------------------

    let private encodeXgiAddress (address: string) dataType =
        let block = getReadBlock address dataType
        let device = block.Address.Substring(0, 3)
        let number = block.Address.Substring(3).PadLeft(5, '0')
        let bytes = Encoding.ASCII.GetBytes(device + number)
        if bytes.Length <> 8 then
            invalidArg "address" (sprintf "Unexpected encoded address length for %s" address)
        bytes

    let private encodeEFMTBAddress (address: string) dataType =
        let block = getReadBlock address dataType
        let payload = Array.zeroCreate<byte> 8
        payload.[0] <- byte block.DeviceType
        match dataType with
        | PlcTagDataType.Bool ->
            payload.[1] <- 0x58uy
            payload.[2] <- byte block.BitPosition
            Array.Copy(BitConverter.GetBytes(uint32 block.ByteOffset), 0, payload, 4, 4)
        | _ ->
            payload.[1] <- 0x42uy
            payload.[2] <- byte (byteSize dataType)
            Array.Copy(BitConverter.GetBytes(uint32 block.ByteOffset), 0, payload, 4, 4)
        payload

    // -------------------------------------------------------------------------
    // Validation helpers
    // -------------------------------------------------------------------------

    let private ensureSameDataType (dataTypes: PlcTagDataType[]) =
        if dataTypes |> Array.distinct |> Array.length > 1 then
            let names = dataTypes |> Array.map string |> String.concat ", "
            invalidArg "dataTypes" $"All data types in a single XGT frame must match. Found: {names}"

    let private ensureCount (items: 'a[]) minCount maxCount name =
        if items.Length < minCount || items.Length > maxCount then
            invalidArg name $"{name} count must be between {minCount} and {maxCount} (actual {items.Length})."

    // -------------------------------------------------------------------------
    // Read frames
    // -------------------------------------------------------------------------

    let private buildMultiReadFrame frameId addresses (dataTypes: PlcTagDataType[]) maxCount perVarLength commandCode headerFactory encoder isLocalEthernet =
        ensureCount addresses 1 maxCount "Address"
        if isLocalEthernet then ensureSameDataType dataTypes

        let bodyLength = 8 + perVarLength * addresses.Length
        let frame = Array.zeroCreate<byte> (HeaderSize + bodyLength)

        let header = headerFactory frameId bodyLength
        Array.Copy(header, 0, frame, 0, HeaderSize)
        setCommand frame 20 commandCode

        frame.[22] <-
            match commandCode with
            | CommandCode.ReadRequestEFMTB -> 0x10uy
            | _ ->
                match toDataTypeCode dataTypes.[0] with
                | DataType.Bit -> 0x00uy
                | DataType.Byte -> 0x01uy
                | DataType.Word -> 0x02uy
                | DataType.DWord -> 0x03uy
                | DataType.LWord -> 0x04uy
                | other -> invalidArg "dataTypes" $"Unsupported XGT data type code {other}"

        frame.[26] <- byte addresses.Length

        addresses
        |> Array.iteri (fun idx addr ->
            let dst = 28 + idx * perVarLength
            let encoded = encoder addr dataTypes.[idx]
            if perVarLength = 10 then
                frame.[dst] <- 0x08uy
                frame.[dst + 1] <- 0x00uy
                Array.Copy(encoded, 0, frame, dst + 2, 8)
            else
                Array.Copy(encoded, 0, frame, dst, 8))

        frame

    let createMultiReadFrame frameId addresses dataTypes =
        buildMultiReadFrame frameId addresses dataTypes MaxLWordBatchSize 10 CommandCode.ReadRequest createStandardHeader encodeXgiAddress true

    let createMultiReadFrameEFMTB frameId addresses dataTypes =
        buildMultiReadFrame frameId addresses dataTypes MaxQWordBatchSize 8 CommandCode.ReadRequestEFMTB createStandardHeader encodeEFMTBAddress false

    // -------------------------------------------------------------------------
    // Write frames
    // -------------------------------------------------------------------------

    let private serializeAddressBlock (block: ReadWriteBlock) =
        let result = ResizeArray<byte>()
        let ascii = Encoding.ASCII.GetBytes(block.Address)
        result.AddRange(BitConverter.GetBytes(uint16 ascii.Length))
        result.AddRange(ascii)
        result.ToArray()

    let private serializeDataBlock (block: ReadWriteBlock) =
        match block.Value with
        | None -> invalidArg "block.Value" "Write operation requires a value."
        | Some value ->
            let payload = toBytes block.DataType value
            let result = ResizeArray<byte>()
            result.AddRange(BitConverter.GetBytes(uint16 payload.Length))
            result.AddRange(payload)
            result.ToArray()

    let createMultiWriteFrame frameId (blocks: ReadWriteBlock[]) =
        ensureCount blocks 1 MaxLWordBatchSize "ReadWriteBlock"
        let dataTypes = blocks |> Array.map (fun b -> b.DataType)
        ensureSameDataType dataTypes

        let body = ResizeArray<byte>()
        body.AddRange(BitConverter.GetBytes(uint16 CommandCode.WriteRequest))

        let dataTypeCode =
            match toDataTypeCode dataTypes.[0] with
            | DataType.Bit -> 0us
            | DataType.Byte -> 1us
            | DataType.Word -> 2us
            | DataType.DWord -> 3us
            | DataType.LWord -> 4us
            | other -> invalidArg "blocks" $"Unsupported XGT data type code {other}"

        body.AddRange(BitConverter.GetBytes(dataTypeCode))
        body.AddRange(Array.zeroCreate<byte> 2)
        body.AddRange(BitConverter.GetBytes(uint16 blocks.Length))
        blocks |> Array.iter (serializeAddressBlock >> body.AddRange)
        blocks |> Array.iter (serializeDataBlock >> body.AddRange)

        let header = createStandardHeader frameId body.Count
        Array.concat [ header; body.ToArray() ]

    let createMultiWriteFrameEFMTB frameId (blocks: ReadWriteBlock[]) =
        ensureCount blocks 1 MaxQWordBatchSize "ReadWriteBlock"

        let payload =
            blocks
            |> Array.collect (fun block ->
                match block.Value with
                | None -> invalidArg "block.Value" "Write operation requires a value."
                | Some value ->
                    let dataBytes = toBytes block.DataType value
                    let typeFlag, metadata =
                        match block.DataType with
                        | PlcTagDataType.Bool -> [| 0x58uy |], BitConverter.GetBytes(uint16 block.BitPosition)
                        | _ -> [| 0x42uy |], BitConverter.GetBytes(uint16 dataBytes.Length)
                    [|
                        yield byte block.DeviceType
                        yield! typeFlag
                        yield! metadata
                        yield! BitConverter.GetBytes(uint32 block.ByteOffset)
                        yield! dataBytes
                    |])

        let frame = Array.zeroCreate<byte> (HeaderSize + 8 + payload.Length)
        let header = createStandardHeader frameId (8 + payload.Length)
        Array.Copy(header, 0, frame, 0, HeaderSize)
        setCommand frame 20 CommandCode.WriteRequestEFMTB
        frame.[22] <- 0x10uy
        frame.[26] <- byte blocks.Length
        Array.Copy(payload, 0, frame, HeaderSize + 8, payload.Length)
        frame
