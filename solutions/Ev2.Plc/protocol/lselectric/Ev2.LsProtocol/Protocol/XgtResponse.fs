namespace Ev2.LsProtocol

open System
open Ev2.LsProtocol

open XgtTypes
open XgtDataConverter

/// <summary>
///     Response parsing helpers for XGT Ethernet frames.  The functions validate the response structure and either produce
///     decoded values or throw descriptive exceptions that bubble up to the caller.
/// </summary>
[<AutoOpen>]
module XgtResponse =

    [<Literal>]
    let private MinimumResponseLength = 32

    [<Literal>]
    let private ErrorStateOffset = 26

    [<Literal>]
    let private CommandOffset = 20

    [<Literal>]
    let private DataStartOffset = 32

    [<Literal>]
    let private MultiReadDataStartOffset = 30

    [<Literal>]
    let private ReadResponseCommand = 0x55uy

    let private ensureResponseSize (buffer: byte[]) =
        if buffer.Length < MinimumResponseLength then
            invalidArg "buffer" $"Response buffer too small. Expected >= {MinimumResponseLength}, actual {buffer.Length}."

    let private ensureSuccess (buffer: byte[]) =
        let errorState = BitConverter.ToUInt16(buffer, ErrorStateOffset)
        if errorState <> 0us then
            let errorCode = buffer.[ErrorStateOffset]
            let description = getXgtErrorDescription errorCode
            failwithf "PLC responded with error 0x%02X (%s)." errorCode description

    let private ensureReadCommand (buffer: byte[]) =
        if buffer.[CommandOffset] <> ReadResponseCommand then
            failwithf "Unexpected XGT command response. Expected 0x%02X, actual 0x%02X."
                ReadResponseCommand
                buffer.[CommandOffset]

    let private ensureTargetSize (target: byte[]) requiredLength =
        if target.Length < requiredLength then
            invalidArg "target" $"Target buffer too small. Required {requiredLength}, actual {target.Length}."

    let private copyElement (source: byte[]) (sourceOffset: int) (target: byte[]) (targetOffset: int) size =
        if sourceOffset + size > source.Length then
            failwith "Source buffer does not contain enough data for the requested element."
        Array.Copy(source, sourceOffset, target, targetOffset, size)

    let private parseMultiRead (buffer: byte[]) (count: int) (dataTypes: PlcTagDataType[]) (target: byte[]) (isEFMTB: bool) =
        ensureResponseSize buffer
        ensureSuccess buffer

        if dataTypes.Length <> count then
            invalidArg "dataTypes" $"Data type array length ({dataTypes.Length}) must equal count ({count})."

        let totalBytes = dataTypes |> Array.sumBy byteSize
        ensureTargetSize target totalBytes

        let mutable targetOffset = 0
        for idx = 0 to count - 1 do
            let elementType = dataTypes.[idx]
            let elementSize = byteSize elementType
            let sourceOffset =
                if isEFMTB then
                    MultiReadDataStartOffset + targetOffset + 2
                else
                    MultiReadDataStartOffset + (idx * 10) + 2

            copyElement buffer sourceOffset target targetOffset elementSize
            targetOffset <- targetOffset + elementSize

    let parseStandardMultiRead buffer count dataTypes target =
        parseMultiRead buffer count dataTypes target false

    let parseEFMTBMultiRead buffer count dataTypes target =
        parseMultiRead buffer count dataTypes target true

    let parseReadResponse (buffer: byte[]) (dataType: PlcTagDataType) =
        ensureResponseSize buffer
        ensureSuccess buffer
        ensureReadCommand buffer
        let payload = Array.sub buffer DataStartOffset (byteSize dataType)
        ScalarValue.FromBytes(payload, dataType)

    let extractValues (buffer: byte[]) (dataTypes: PlcTagDataType[]) =
        let mutable offset = 0
        dataTypes
        |> Array.map (fun dataType ->
            let size = byteSize dataType
            let slice = Array.sub buffer offset size
            offset <- offset + size
            ScalarValue.FromBytes(slice, dataType))

    let parseAndExtractValues buffer count dataTypes =
        let totalBytes = dataTypes |> Array.sumBy byteSize
        let temp = Array.zeroCreate<byte> totalBytes
        parseStandardMultiRead buffer count dataTypes temp
        extractValues temp dataTypes
