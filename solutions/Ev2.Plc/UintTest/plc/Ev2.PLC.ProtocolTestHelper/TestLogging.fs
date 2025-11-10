namespace ProtocolTestHelper

open System
open PacketLogger

/// Shared logging helpers for protocol test suites.
module TestLogging =

    let private defaultPreviewLimit = 256

    let log (logger: TestLogger) (message: string) =
        logger.Log(message)

    let logf (logger: TestLogger) (format: string) (args: obj[]) =
        logger.LogFormat(format, args)

    let private renderPacket previewLimit (direction: string) (bytes: byte[]) (length: int) =
        let timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
        let actualLength = min length bytes.Length
        let previewCount = min previewLimit actualLength

        let hex =
            bytes
            |> Seq.truncate previewCount
            |> Seq.map (fun b -> sprintf "%02X" b)
            |> String.concat " "

        let suffix =
            if actualLength > previewCount then
                sprintf " ... (+%d bytes)" (actualLength - previewCount)
            else
                String.Empty

        $"[{timestamp}] {direction} ({actualLength} bytes) {hex}{suffix}"

    let logPacketWithLimit previewLimit (logger: TestLogger) (direction: string) (bytes: byte[]) (length: int) =
        logger.Log(renderPacket previewLimit direction bytes length)

    let logPacket (logger: TestLogger) (direction: string) (bytes: byte[]) (length: int) =
        logPacketWithLimit defaultPreviewLimit logger direction bytes length

    let dump (logger: TestLogger) (header: string) =
        logger.Dump(header)

    let slicePayload length (bytes: byte[]) =
        let actualLength =
            length
            |> max 0
            |> min bytes.Length

        if actualLength = 0 then
            Array.empty
        else
            Array.sub bytes 0 actualLength

    let forwardPacket protocolName host port direction (bytes: byte[]) length =
        let payload = slicePayload length bytes
        let description =
            match direction with
            | "[TX]" -> "Unit Test Request"
            | "[RX]" -> "Unit Test Response"
            | other -> other

        match direction with
        | "[TX]" -> logRequest protocolName host port payload description Map.empty None
        | "[RX]" -> logResponse protocolName host port payload description Map.empty None
        | _ -> ()
