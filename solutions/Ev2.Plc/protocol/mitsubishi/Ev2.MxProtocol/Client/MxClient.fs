namespace Ev2.MxProtocol.Client

open System
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Protocol
open System.Net

/// High-level MELSEC protocol client
type PacketLogger = string -> byte[] -> int -> unit

type MelsecClient(config: MelsecConfig, ?packetLogger: PacketLogger) =
    
    let mutable tcpClient: TcpClient option = None
    let lockObj = obj()
    let packetLogger = packetLogger

    let logPacket direction (bytes: byte[]) length =
        match packetLogger with
        | Some logger -> logger direction bytes length
        | None -> ()
    
    // =========================================================================
    // Connection Management
    // =========================================================================
    
    /// Connect to PLC
    member _.Connect() =
        // Close existing connections first
        lock lockObj (fun () ->
            tcpClient |> Option.iter (fun c -> c.Close(); tcpClient <- None)
        )
        
        let client = new TcpClient()
        client.ReceiveTimeout <- config.TimeoutMilliseconds
        client.SendTimeout <- config.TimeoutMilliseconds
        
        // Use async connect with timeout
        let connectTask = client.ConnectAsync(config.Host, config.Port)
        if not (connectTask.Wait(config.TimeoutMilliseconds)) then
            client.Close()
            raise (TimeoutException($"Connection to {config.Host}:{config.Port} timed out after {config.TimeoutMilliseconds}ms"))
        
        lock lockObj (fun () -> tcpClient <- Some client)
    
    /// Disconnect from PLC
    member _.Disconnect() =
        lock lockObj (fun () ->
            tcpClient |> Option.iter (fun c -> c.Close(); tcpClient <- None)
        )
    
    /// Check if connected
    member _.IsConnected =
        match tcpClient with
        | Some tcp -> tcp.Connected
        | None -> false
    
    // =========================================================================
    // Low-Level Communication
    // =========================================================================
    
    /// Send request and receive response with proper TCP stream handling
    member private this.SendReceive(request: MelsecRequest) =
        if not this.IsConnected then
            this.Connect()
    
        let frameData = Frame.buildFrame config request
        logPacket "[TX]" frameData frameData.Length
    
        match tcpClient with
        | Some tcp ->
            let stream = tcp.GetStream()
            stream.ReadTimeout <- config.TimeoutMilliseconds
            stream.WriteTimeout <- config.TimeoutMilliseconds
        
            // Send request
            stream.Write(frameData, 0, frameData.Length)
        
            // Helper function to read exact number of bytes from stream
            // TCP does not guarantee all data arrives in single Read call
            let rec readFully (buffer: byte[]) (offset: int) (count: int) =
                if count = 0 then
                    Ok ()
                else
                    let bytesRead = stream.Read(buffer, offset, count)
                    if bytesRead = 0 then
                        Error "Connection closed before all data received"
                    else
                        readFully buffer (offset + bytesRead) (count - bytesRead)
        
            // Read header (9 bytes) + end code (2 bytes) = 11 bytes minimum
            let headerBuffer = Array.zeroCreate<byte> FrameHeaderWithEndCode
            
            match readFully headerBuffer 0 FrameHeaderWithEndCode with
            | Error msg -> Error msg
            | Ok () ->
                // Parse data length from header (bytes 7-8)
                let dataLength = int headerBuffer.[7] ||| (int headerBuffer.[8] <<< 8)
            
                // dataLength includes end code (2 bytes), so remaining data = dataLength - 2
                let remainingBytes = dataLength - 2
                let totalLength = FrameHeaderWithEndCode + remainingBytes
            
                let fullBuffer = Array.zeroCreate<byte> totalLength
                Array.Copy(headerBuffer, fullBuffer, FrameHeaderWithEndCode)
            
                if remainingBytes > 0 then
                    match readFully fullBuffer FrameHeaderWithEndCode remainingBytes with
                    | Ok () ->
                        logPacket "[RX]" fullBuffer totalLength
                        Frame.parseFrame config fullBuffer
                    | Error msg -> Error msg
                else
                    logPacket "[RX]" fullBuffer totalLength
                    Frame.parseFrame config fullBuffer

        | None ->
            Error "Not connected"
    
    // =========================================================================
    // Device Memory Operations
    // =========================================================================
    
    /// Read bit devices
    member this.ReadBits(device: DeviceCode, address: int, count: int) =
        let request = PacketBuilder.buildBatchRead device address (uint16 count) true
        
        match this.SendReceive(request) with
        | Ok response -> PacketParser.parseBatchReadBits response count
        | Error msg -> Error msg

    /// Write bit devices
    member this.WriteBits(device: DeviceCode, address: int, values: bool array) =
        let request = Frame.createWriteBitRequest device address values
    
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then Ok ()
            else Error $"Write failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    /// Read word devices
    member this.ReadWords(device: DeviceCode, address: int, count: int) =
        let request = PacketBuilder.buildBatchRead device address (uint16 count) false
        
        match this.SendReceive(request) with
        | Ok response -> PacketParser.parseBatchReadWords response count
        | Error msg -> Error msg
    
    /// Write word devices
    member this.WriteWords(device: DeviceCode, address: int, values: uint16 array) =
        let request = PacketBuilder.buildBatchWrite device address values false
        
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then
                Ok ()
            else
                Error $"Write failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    // =========================================================================
    // Random Access Operations
    // =========================================================================
    
    /// Read multiple devices at random addresses
    member this.ReadRandom(devices: RandomDeviceAccess array) =
        let request = PacketBuilder.buildRandomRead devices
        
        match this.SendReceive(request) with
        | Ok response -> PacketParser.parseRandomRead response devices
        | Error msg -> Error msg
    
    /// Write multiple bit devices at random addresses
    member this.WriteRandomBits(devices: (DeviceCode * int * bool) array) =
        let request = PacketBuilder.buildRandomWriteBit devices
        
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then
                Ok ()
            else
                Error $"Write failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    // =========================================================================
    // PLC Control Operations
    // =========================================================================
    
    /// Start PLC (Remote RUN)
    member this.RemoteRun() =
        let request = PacketBuilder.buildRemoteRun()
        
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then
                Ok ()
            else
                Error $"Remote RUN failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    /// Stop PLC (Remote STOP)
    member this.RemoteStop() =
        let request = PacketBuilder.buildRemoteStop()
        
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then
                Ok ()
            else
                Error $"Remote STOP failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    /// Read CPU type/model
    member this.ReadCpuType() =
        let request = PacketBuilder.buildReadCpuType()
        
        match this.SendReceive(request) with
        | Ok response -> PacketParser.parseCpuType response
        | Error msg -> Error msg
    
    // =========================================================================
    // Buffer Memory Operations
    // =========================================================================
    
    /// Read buffer memory
    member this.ReadBuffer(startAddress: uint16, count: uint16) =
        let request = PacketBuilder.buildBufferRead startAddress count
        
        match this.SendReceive(request) with
        | Ok response -> PacketParser.parseBatchReadWords response (int count)
        | Error msg -> Error msg
    
    /// Write buffer memory
    member this.WriteBuffer(startAddress: uint16, values: uint16 array) =
        let request = PacketBuilder.buildBufferWrite startAddress values
        
        match this.SendReceive(request) with
        | Ok response ->
            if response.EndCode.IsSuccess then
                Ok ()
            else
                Error $"Write failed: {PacketParser.getErrorDescription response.EndCode.Code}"
        | Error msg -> Error msg
    
    // =========================================================================
    // IDisposable Implementation
    // =========================================================================
    
    interface IDisposable with
        member this.Dispose() =
            this.Disconnect()
