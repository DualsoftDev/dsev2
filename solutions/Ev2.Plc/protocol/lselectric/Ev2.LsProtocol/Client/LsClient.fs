namespace Ev2.LsProtocol

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open Ev2.LsProtocol
open Ev2.LsProtocol.Core

open XgtTypes
open XgtFrameBuilder
open XgtResponse
open ReadWriteBlockFactory
open Ev2.LsProtocol

/// <summary>
///     High-level LS Electric XGT Ethernet client.  Handles frame construction, invocation and response parsing.
///     Supports both synchronous and asynchronous operations with Result-based error handling for C# interop.
/// </summary>
type LsClient(ip: string, port: int, timeoutMs: int, isLocalEthernet: bool, ?config: LsProtocolConfig, ?packetLogger: PacketLogger) as this =
    inherit XgtTcpClientBase(ip, port, timeoutMs, ?packetLogger = packetLogger)

    let frameId () = getFrameIdBytes this.IpAddress this.SourcePort
    let mutable storedConfig = config

    member _.IsLocalEthernet = isLocalEthernet

    /// <summary>Optional configuration object for higher-level features</summary>
    member _.Config
        with get() = storedConfig
        and set(value) = storedConfig <- value

    member private _.ExpectedResponseSize(count: int) =
        // heuristic derived from the legacy implementation: header + overhead + payload
        HeaderSize + 32 + (count * 16)

    member private this.DispatchReadFrame(addresses: string[], dataTypes: PlcTagDataType[], buffer: byte[]) =
        let frame =
            if isLocalEthernet then
                createMultiReadFrame (frameId ()) addresses dataTypes
            else
                createMultiReadFrameEFMTB (frameId ()) addresses dataTypes

        this.SendFrame frame
        let response = this.ReceiveFrame(this.ExpectedResponseSize(addresses.Length))

        if isLocalEthernet then
            parseStandardMultiRead response addresses.Length dataTypes buffer
        else
            parseEFMTBMultiRead response addresses.Length dataTypes buffer

    // ============================================================================
    // Synchronous API (original methods)
    // ============================================================================

    member this.Reads(addresses: string[], dataTypes: PlcTagDataType[], readBuffer: byte[]) =
        if addresses.Length = 0 then invalidArg "addresses" "At least one address is required."
        if addresses.Length <> dataTypes.Length then invalidArg "dataTypes" "Data type array length mismatch."
        this.DispatchReadFrame(addresses, dataTypes, readBuffer)

    member this.Writes(addresses: string[], dataTypes: PlcTagDataType[], values: ScalarValue[]) =
        if addresses.Length = 0 then invalidArg "addresses" "At least one address is required."
        if addresses.Length <> dataTypes.Length || addresses.Length <> values.Length then
            invalidArg "values" "Array lengths must match."

        let blocks =
            Array.init addresses.Length (fun idx -> getWriteBlock addresses.[idx] dataTypes.[idx] values.[idx])

        let frame =
            if isLocalEthernet then
                createMultiWriteFrame (frameId ()) blocks
            else
                createMultiWriteFrameEFMTB (frameId ()) blocks

        this.SendFrame frame
        let _ = this.ReceiveFrame(this.ExpectedResponseSize(addresses.Length))
        true

    member this.Read(address: string, dataType: PlcTagDataType) =
        let buffer = Array.zeroCreate<byte> (byteSize dataType)
        this.Reads([| address |], [| dataType |], buffer)
        ScalarValue.FromBytes(buffer, dataType)

    member this.Write(address: string, dataType: PlcTagDataType, value: ScalarValue) =
        this.Writes([| address |], [| dataType |], [| value |])

    // ============================================================================
    // Asynchronous API with Result-based error handling (for C# interop)
    // ============================================================================

    /// <summary>ScalarValue를 byte[]로 변환</summary>
    static member private ScalarValueToBytes(value: ScalarValue, dataType: PlcTagDataType) =
        match value with
        | ScalarValue.BoolValue b -> [| if b then 1uy else 0uy |]
        | ScalarValue.Int8Value i -> [| byte i |]
        | ScalarValue.UInt8Value u -> [| u |]
        | ScalarValue.Int16Value i -> BitConverter.GetBytes(i)
        | ScalarValue.UInt16Value u -> BitConverter.GetBytes(u)
        | ScalarValue.Int32Value i -> BitConverter.GetBytes(i)
        | ScalarValue.UInt32Value u -> BitConverter.GetBytes(u)
        | ScalarValue.Int64Value i -> BitConverter.GetBytes(i)
        | ScalarValue.UInt64Value u -> BitConverter.GetBytes(u)
        | ScalarValue.Float32Value f -> BitConverter.GetBytes(f)
        | ScalarValue.Float64Value f -> BitConverter.GetBytes(f)
        | _ -> [||]

    /// <summary>byte[]를 ScalarValue로 변환</summary>
    static member private BytesToScalarValue(bytes: byte[], dataType: PlcTagDataType) =
        match dataType with
        | PlcTagDataType.Bool -> ScalarValue.BoolValue (bytes.[0] <> 0uy)
        | PlcTagDataType.UInt8 -> ScalarValue.UInt8Value bytes.[0]
        | PlcTagDataType.Int8 -> ScalarValue.Int8Value (sbyte bytes.[0])
        | PlcTagDataType.UInt16 -> ScalarValue.UInt16Value (BitConverter.ToUInt16(bytes, 0))
        | PlcTagDataType.Int16 -> ScalarValue.Int16Value (BitConverter.ToInt16(bytes, 0))
        | PlcTagDataType.UInt32 -> ScalarValue.UInt32Value (BitConverter.ToUInt32(bytes, 0))
        | PlcTagDataType.Int32 -> ScalarValue.Int32Value (BitConverter.ToInt32(bytes, 0))
        | PlcTagDataType.UInt64 -> ScalarValue.UInt64Value (BitConverter.ToUInt64(bytes, 0))
        | PlcTagDataType.Int64 -> ScalarValue.Int64Value (BitConverter.ToInt64(bytes, 0))
        | PlcTagDataType.Float32 -> ScalarValue.Float32Value (BitConverter.ToSingle(bytes, 0))
        | PlcTagDataType.Float64 -> ScalarValue.Float64Value (BitConverter.ToDouble(bytes, 0))
        | _ -> ScalarValue.UInt16Value 0us

    /// <summary>Connect to PLC asynchronously with Result-based error handling</summary>
    member this.ConnectAsync() : Task<Result<unit, string>> =
        task {
            try
                let connected = this.Connect()
                if connected then
                    return Ok ()
                else
                    return Error $"Failed to connect to PLC at {ip}:{port}"
            with
            | ex -> return Error $"Connection failed: {ex.Message}"
        }

    /// <summary>Disconnect from PLC asynchronously</summary>
    member this.DisconnectAsync() : Task<Result<unit, string>> =
        task {
            try
                eprintfn "[LsClient] Disconnecting..."
                this.Disconnect() |> ignore
                eprintfn "[LsClient] Disconnected successfully"
                return Ok ()
            with
            | :? System.IO.IOException as ioEx ->
                eprintfn "[LsClient] Expected IO exception during disconnect: %s" ioEx.Message
                return Ok ()  // IO exception during disconnect is expected
            | ex ->
                eprintfn "[LsClient] Error during disconnect: %s" ex.Message
                return Error $"Disconnect failed: {ex.Message}"
        }


    /// <summary>Read single address asynchronously with specific data type</summary>
    member this.ReadAsync(address: string, dataType: PlcTagDataType) : Task<Result<byte[], string>> =
        task {
            if not this.IsConnected then
                return Error "Client not connected"
            else
                try
                    let value = this.Read(address, dataType)
                    let bytes = LsClient.ScalarValueToBytes(value, dataType)
                    return Ok bytes
                with
                | ex -> return Error $"Read failed: {ex.Message}"
        }

    /// <summary>Write single address asynchronously with specific data type</summary>
    member this.WriteAsync(address: string, dataType: PlcTagDataType, data: byte[]) : Task<Result<unit, string>> =
        task {
            if not this.IsConnected then
                return Error "Client not connected"
            else
                try
                    let value = LsClient.BytesToScalarValue(data, dataType)
                    let success = this.Write(address, dataType, value)
                    if success then
                        return Ok ()
                    else
                        return Error "Write operation failed"
                with
                | ex -> return Error $"Write failed: {ex.Message}"
        }

    /// <summary>Read multiple addresses asynchronously</summary>
    member this.ReadMultiAsync(addresses: string[]) : Task<Result<(string * byte[])[], string>> =
        task {
            if not this.IsConnected then
                return Error "Client not connected"
            else
                try
                    // 배치 읽기 구현
                    let dataTypes = Array.create addresses.Length PlcTagDataType.UInt16
                    let buffer = Array.zeroCreate<byte> (addresses.Length * 2)  // 각 주소당 2바이트

                    this.Reads(addresses, dataTypes, buffer)
                    // Reads 메서드는 unit을 반환하고 예외 발생 시 실패로 간주
                    let results = Array.zeroCreate<string * byte[]> addresses.Length
                    for i = 0 to addresses.Length - 1 do
                        let startIndex = i * 2
                        let addressData = Array.sub buffer startIndex 2
                        results.[i] <- (addresses.[i], addressData)
                    return Ok results
                with
                | ex -> return Error $"Multi-read failed: {ex.Message}"
        }

// ============================================================================
// Static Factory Methods (for C# interop)
// ============================================================================

/// <summary>Static factory and module methods for LsClient</summary>
module LsClientModule =

    /// <summary>Create a new LsClient from configuration</summary>
    let create (config: LsProtocolConfig) : Result<LsClient, string> =
        try
            match config.IpAddress, config.Port with
            | Some ip, Some port ->
                let timeoutMs = int config.Timeout.TotalMilliseconds
                let isLocalEthernet = config.LocalEthernet
                let client = new LsClient(ip, port, timeoutMs, isLocalEthernet, config = config)
                Ok client
            | _ ->
                Error "IP address and port must be specified for Ethernet connection"
        with
        | ex -> Error $"Client creation failed: {ex.Message}"

    /// <summary>Connect to PLC asynchronously</summary>
    let connectAsync (client: LsClient) : Task<Result<LsClient, string>> =
        task {
            let! result = client.ConnectAsync()
            match result with
            | Ok () -> return Ok client
            | Error msg -> return Error msg
        }

    /// <summary>Disconnect from PLC asynchronously</summary>
    let disconnectAsync (client: LsClient) : Task<LsClient> =
        task {
            let! _ = client.DisconnectAsync()
            return client
        }

    /// <summary>Read single address asynchronously</summary>
    let readAsync (client: LsClient) (address: string) (dataType:PlcTagDataType): Task<Result<byte[], string>> =
        client.ReadAsync(address, dataType)

    /// <summary>Write single address asynchronously</summary>
    let writeAsync (client: LsClient) (address: string) (dataType: PlcTagDataType) (data: byte[]) : Task<Result<unit, string>> =
        client.WriteAsync(address, dataType, data)

    /// <summary>Read multiple addresses asynchronously</summary>
    let readMultiAsync (client: LsClient) (addresses: string[]) : Task<Result<(string * byte[])[], string>> =
        client.ReadMultiAsync(addresses)

