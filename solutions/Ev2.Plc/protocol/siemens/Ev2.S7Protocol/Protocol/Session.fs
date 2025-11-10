namespace Ev2.S7Protocol.Protocol

open System
open System.Net
open System.Net.Sockets
open Ev2.S7Protocol.Core

type PacketLogger = string -> byte[] -> int -> unit

/// <summary>
///     Manages the lifetime of an S7 TCP session with proper TPKT frame handling.
///     Ensures complete frame reception by respecting ISO-on-TCP length field.
/// </summary>
type SessionManager(config: S7Config, ?packetLogger: PacketLogger) =

    let mutable socket: Socket option = None
    let mutable connected = false
    let mutable negotiatedPduSize = 480
    let mutable pduReference = 0us
    let packetLogger = packetLogger

    let logPacket direction (bytes: byte[]) length =
        match packetLogger with
        | Some logger -> logger direction bytes length
        | None -> ()

    let nextPduReference () =
        let reference = pduReference
        pduReference <- pduReference + 1us
        reference

    let createSocket () =
        let sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        sock.ReceiveTimeout <- int config.Timeout.TotalMilliseconds
        sock.SendTimeout <- int config.Timeout.TotalMilliseconds
        sock.NoDelay <- true
        sock

    let send (sock: Socket) (payload: byte[]) =
        logPacket "[TX]" payload payload.Length
        let rec loop offset =
            if offset < payload.Length then
                let sent = sock.Send(payload, offset, payload.Length - offset, SocketFlags.None)
                if sent <= 0 then failwith "Socket send failed."
                loop (offset + sent)
        loop 0

    /// <summary>
    /// Reads exactly the specified number of bytes from the socket.
    /// Handles TCP fragmentation by looping until all bytes are received.
    /// </summary>
    let receiveExact (sock: Socket) (buffer: byte[]) (offset: int) (length: int) =
        let rec loop currentOffset remaining =
            if remaining > 0 then
                let received = sock.Receive(buffer, currentOffset, remaining, SocketFlags.None)
                if received <= 0 then
                    failwith "Connection closed during receive."
                loop (currentOffset + received) (remaining - received)
        loop offset length

    /// <summary>
    /// Receives a complete TPKT frame by first reading the 4-byte header,
    /// extracting the total length, then reading the exact remaining payload.
    /// This prevents fragmentation issues with large PDUs.
    /// </summary>
    let receive (sock: Socket) =
        // Step 1: Read TPKT header (4 bytes)
        let tpktHeader = Array.zeroCreate<byte> 4
        receiveExact sock tpktHeader 0 4

        // Step 2: Verify TPKT version
        if tpktHeader.[0] <> 0x03uy then
            failwith $"Invalid TPKT version: 0x{tpktHeader.[0]:X2}"

        // Step 3: Extract total length from TPKT header
        let totalLength = (int tpktHeader.[2] <<< 8) ||| int tpktHeader.[3]
        
        if totalLength < 7 then
            failwith $"Invalid TPKT length: {totalLength}"

        // Step 4: Calculate payload length (total - header)
        let payloadLength = totalLength - 4

        // Step 5: Read exact payload bytes
        let payload = Array.zeroCreate<byte> payloadLength
        receiveExact sock payload 0 payloadLength

        // Step 6: Return complete frame
        let frame = Array.concat [tpktHeader; payload]
        logPacket "[RX]" frame frame.Length
        frame

    /// <summary>
    /// Receives data into a pre-allocated buffer (for handshake operations).
    /// Returns the number of bytes actually received.
    /// </summary>
    let receiveInto (sock: Socket) (buffer: byte[]) =
        // Read TPKT header first
        receiveExact sock buffer 0 4
        
        // Extract total length
        let totalLength = (int buffer.[2] <<< 8) ||| int buffer.[3]
        let payloadLength = totalLength - 4
        
        if payloadLength > buffer.Length - 4 then
            failwith "Buffer too small for TPKT payload"
        
        // Read remaining payload
        receiveExact sock buffer 4 payloadLength
        logPacket "[RX]" (buffer.[0..totalLength-1]) totalLength
        totalLength

    let sendHandshake (sock: Socket) =
        // COTP connection
        send sock (S7Protocol.buildCotpConnect config.LocalTSAP config.RemoteTSAP)
        let handshakeBuffer = Array.zeroCreate<byte> 256
        let handshakeLength = receiveInto sock handshakeBuffer
        if handshakeLength < 22 || handshakeBuffer.[5] <> 0xD0uy then
            failwith "Unexpected COTP connection response."

        // S7 setup
        let setupFrame = S7Protocol.buildS7Setup()
        let pduRef = nextPduReference()
        setupFrame.[11] <- byte (pduRef >>> 8)
        setupFrame.[12] <- byte pduRef

        send sock setupFrame

        let setupLength = receiveInto sock handshakeBuffer
        if setupLength < 27 then
            failwith "S7 setup response too short."
        if handshakeBuffer.[7] <> 0x32uy || handshakeBuffer.[8] <> 0x03uy then
            failwith "Invalid S7 setup response."

        negotiatedPduSize <- (int handshakeBuffer.[25] <<< 8) ||| int handshakeBuffer.[26]

    let wrapInTpkt (payload: byte[]) =
        let length = payload.Length + 7
        Array.concat [
            [|
                0x03uy; 0x00uy
                byte (length >>> 8); byte length
                0x02uy; 0xF0uy; 0x80uy
            |]
            payload
        ]

    member this.Connect() =
        try
            let sock = createSocket ()
            let endpoint = IPEndPoint(IPAddress.Parse(config.IpAddress), config.Port)
            sock.Connect(endpoint)
            sendHandshake sock
            socket <- Some sock
            connected <- true
            Ok ()
        with 
        | :? SocketException as ex ->
            connected <- false
            socket |> Option.iter (fun s -> try s.Close() with _ -> ())
            socket <- None
            Error $"Socket error [{ex.SocketErrorCode}]: {ex.Message}"
        | ex ->
            connected <- false
            socket |> Option.iter (fun s -> try s.Close() with _ -> ())
            socket <- None
            Error $"Connection failed: {ex.Message}"

    member private this.SendRequest(request: byte[]) =
        match socket with
        | None -> Error "Not connected."
        | Some sock when not connected -> Error "Not connected."
        | Some sock ->
            try
                if request.Length >= 6 && request.[0] = 0x32uy then
                    let reference = nextPduReference()
                    request.[4] <- byte (reference >>> 8)
                    request.[5] <- byte reference

                send sock (wrapInTpkt request)
                let response = receive sock
                if response.Length < 7 then
                    Error "Invalid S7 response."
                else
                    Ok (Array.sub response 7 (response.Length - 7))
            with 
            | :? SocketException as ex ->
                Error $"Socket error [{ex.SocketErrorCode}]: {ex.Message}"
            | ex ->
                Error $"Request failed: {ex.Message}"

    /// <summary>
    /// Reads bytes with automatic chunking for large requests.
    /// Respects negotiated PDU size and 512-byte consistency limit.
    /// </summary>
    member this.ReadBytes(area: DataArea, db: int, startByte: int, count: int) =
        if not connected then
            Error "Not connected."
        elif count <= 0 then
            Error "Count must be positive."
        else
            // Calculate maximum data per request (PDU size - protocol overhead)
            let maxDataPerRequest = min (negotiatedPduSize - 18) 512

            if count <= maxDataPerRequest then
                // Single request
                let request = S7Protocol.buildReadBytesRequest area db startByte count
                match this.SendRequest request with
                | Error err -> Error err
                | Ok response -> S7Protocol.parseReadResponse response
            else
                // Split into multiple chunks
                let rec readChunks offset remaining acc =
                    if remaining <= 0 then
                        Ok (Array.concat (List.rev acc))
                    else
                        let chunkSize = min remaining maxDataPerRequest
                        match this.ReadBytes(area, db, offset, chunkSize) with
                        | Ok data -> 
                            readChunks (offset + chunkSize) (remaining - chunkSize) (data :: acc)
                        | Error err -> 
                            Error $"Chunk read failed at offset {offset}: {err}"
                
                readChunks startByte count []

    /// <summary>
    /// Writes bytes with automatic chunking for large requests.
    /// </summary>
    member this.WriteBytes(area: DataArea, db: int, startByte: int, data: byte[]) =
        if not connected then
            Error "Not connected."
        elif data.Length = 0 then
            Error "Data cannot be empty."
        else
            let maxDataPerRequest = min (negotiatedPduSize - 35) 512

            if data.Length <= maxDataPerRequest then
                // Single request
                let request = S7Protocol.buildWriteBytesRequest area db startByte data
                match this.SendRequest request with
                | Error err -> Error err
                | Ok response -> S7Protocol.parseWriteResponse response
            else
                // Split into multiple chunks
                let rec writeChunks offset remaining =
                    if remaining <= 0 then
                        Ok ()
                    else
                        let chunkSize = min remaining maxDataPerRequest
                        let chunk = Array.sub data offset chunkSize
                        match this.WriteBytes(area, db, (startByte + offset), chunk) with
                        | Ok () -> 
                            writeChunks (offset + chunkSize) (remaining - chunkSize)
                        | Error err -> 
                            Error $"Chunk write failed at offset {offset}: {err}"
                
                writeChunks 0 data.Length

    member this.WriteBit(area: DataArea, db: int, startByte: int, bit: int, value: bool) =
        if not connected then
            Error "Not connected."
        elif bit < 0 || bit > 7 then
            Error $"Bit offset must be 0-7, got {bit}"
        else
            let request = S7Protocol.buildWriteBitRequest area db startByte bit value
            match this.SendRequest request with
            | Error err -> Error err
            | Ok response -> S7Protocol.parseWriteResponse response

    member this.ReadBit(area: DataArea, db: int, startByte: int, bit: int) =
        if bit < 0 || bit > 7 then
            Error $"Bit offset must be 0-7, got {bit}"
        else
            match this.ReadBytes(area, db, startByte, 1) with
            | Error err -> Error err
            | Ok data when data.Length > 0 ->
                let mask = 1uy <<< bit
                Ok ((data.[0] &&& mask) <> 0uy)
            | Ok _ -> Error "No data received"

    member this.Disconnect() =
        connected <- false
        match socket with
        | Some sock ->
            try sock.Shutdown(SocketShutdown.Both) with _ -> ()
            try sock.Close() with _ -> ()
            socket <- None
        | None -> ()

    member _.IsConnected = connected
    member _.PDUSize = negotiatedPduSize

    interface IDisposable with
        member this.Dispose() = this.Disconnect()
