module Ev2.MxProtocol.Tests.ClientHelpers

open System
open System.Net
open System.Net.Sockets
open System.Threading
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open ProtocolTestHelper
open ProtocolTestHelper.IntegrationTestRunner
open Ev2.MxProtocol.Tests.TestHelpers

type MockPlcServer(port: int) =
    let listener = new TcpListener(IPAddress.Any, port)
    let mutable isRunning = false
    let mutable thread: Thread option = None
    let memory = Collections.Concurrent.ConcurrentDictionary<string, byte>()
    
    let handleClient (client: TcpClient) =
        try
            try
                let stream = client.GetStream()
                let buffer = Array.zeroCreate 1024
                
                while client.Connected && isRunning do
                    let bytesRead = stream.Read(buffer, 0, buffer.Length)
                    if bytesRead > 0 then
                        // Create proper MELSEC 3E response frame
                        let responseHeader = [|
                            0xD0uy; 0x00uy; 0x00uy; 0x00uy; 0xFFuy; 0x03uy; 0x00uy; // Frame header
                            0x0Buy; 0x00uy; // Data length (11 bytes: end code + 4 bytes data)
                            0x00uy; 0x00uy; // End code success (0x0000)
                            0xFFuy; 0x03uy; 0x00uy; // Sample response data
                            0x01uy; 0x00uy; 0x00uy; 0x00uy // Additional sample data
                        |]
                        
                        stream.Write(responseHeader, 0, responseHeader.Length)
            with
            | _ -> ()
        finally
            client.Close()
    
    let serverLoop() =
        listener.Start()
        while isRunning do
            try
                let client = listener.AcceptTcpClient()
                ThreadPool.QueueUserWorkItem(fun _ -> handleClient client) |> ignore
            with
            | _ when not isRunning -> ()
            | ex -> printfn $"Server error: {ex.Message}"
    
    member _.Start() =
        if not isRunning then
            isRunning <- true
            let t = Thread(ThreadStart(serverLoop))
            t.IsBackground <- true
            t.Start()
            thread <- Some t
            Thread.Sleep(100) // Give server time to start
    
    member _.Stop() =
        isRunning <- false
        listener.Stop()
        thread |> Option.iter (fun t -> t.Join(1000) |> ignore)
        thread <- None
    
    member _.SetMemory(address: string, value: byte) =
        memory.[address] <- value
    
    member _.GetMemory(address: string) =
        match memory.TryGetValue(address) with
        | true, value -> Some value
        | _ -> None
    
    interface IDisposable with
        member this.Dispose() =
            this.Stop()

let createTestClient() =
    let config = TestHelpers.defaultTestConfig
    let logger = TestHelpers.createPacketLogger config
    new MelsecClient(config, packetLogger = logger)

let createTestClientPLC1() =
    let config = TestHelpers.plc1Config
    let logger = TestHelpers.createPacketLogger config
    new MelsecClient(config, packetLogger = logger)

let createTestClientPLC2() =
    let config = TestHelpers.plc2Config
    let logger = TestHelpers.createPacketLogger config
    new MelsecClient(config, packetLogger = logger)

let createTestClientWithServer() =
    let config = TestHelpers.defaultTestConfig 
    let logger = TestHelpers.createPacketLogger config
    let client = new MelsecClient(config, packetLogger = logger)
    
    (client, null)

let private appendLogs (message: string) (logs: string) =
    if String.IsNullOrWhiteSpace logs then message
    elif String.IsNullOrWhiteSpace message then logs
    else message + Environment.NewLine + Environment.NewLine + logs

let private augmentError (error: MxProtocolError) (logs: string) =
    if String.IsNullOrWhiteSpace logs then error
    else
        match error with
        | MxProtocolError.ConnectionError msg -> MxProtocolError.ConnectionError (appendLogs msg logs)
        | MxProtocolError.SessionError msg -> MxProtocolError.SessionError (appendLogs msg logs)
        | MxProtocolError.MelsecError (code, net, station, msg) ->
            MxProtocolError.MelsecError (code, net, station, appendLogs msg logs)
        | MxProtocolError.DeviceError msg -> MxProtocolError.DeviceError (appendLogs msg logs)
        | MxProtocolError.InvalidCommand cmd -> MxProtocolError.InvalidCommand (appendLogs cmd logs)
        | MxProtocolError.InvalidDevice dev -> MxProtocolError.InvalidDevice (appendLogs dev logs)
        | MxProtocolError.InvalidAddress addr -> MxProtocolError.InvalidAddress (appendLogs addr logs)
        | MxProtocolError.InvalidData msg -> MxProtocolError.InvalidData (appendLogs msg logs)
        | MxProtocolError.FrameError msg -> MxProtocolError.FrameError (appendLogs msg logs)
        | MxProtocolError.UnknownError msg -> MxProtocolError.UnknownError (appendLogs msg logs)
        | _ -> MxProtocolError.UnknownError (appendLogs error.Message logs)

let private lifecycle : ClientLifecycle<MelsecClient, MxProtocolError, unit> =
    { CreateClient = createTestClient
      Connect =
        fun client ->
            try
                client.Connect()
                Ok ()
            with ex ->
                Error (MxProtocolError.UnknownError ex.Message)
      Disconnect = fun client -> client.Disconnect()
      Dispose = fun client -> (client :> IDisposable).Dispose()
      MapException = fun ex -> MxProtocolError.UnknownError ex.Message
      DumpLogs = TestHelpers.dumpLogs
      AugmentError = augmentError }

let runWithClient action =
    IntegrationTestRunner.runWithClient lifecycle action

let withTestClient action =
    use client = createTestClient()
    action client

let withConnectedClient action =
    let result = runWithClient action
    match result.Result with
    | Ok value -> value
    | Error error ->
        TestHelpers.failWithLogsResult result $"Integration test failed: {error.Message}"
        Unchecked.defaultof<_>

let ensureConnected (client: MelsecClient) =
    if not client.IsConnected then
        client.Connect()

let safeExecute action =
    try
        action() |> Ok
    with
    | ex -> Error ex.Message
