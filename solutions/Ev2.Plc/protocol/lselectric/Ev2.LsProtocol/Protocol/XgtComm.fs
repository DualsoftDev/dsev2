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

type PacketLogger = string -> byte[] -> int -> unit

/// <summary>
///     Thin TCP abstraction used by <see cref="LsClient"/>.  The class intentionally keeps the API synchronous because PLC
///     interactions are typically interlocked with deterministic workflows.
/// </summary>
[<AbstractClass>]
type XgtTcpClientBase(ip: string, port: int, timeoutMs: int, ?packetLogger: PacketLogger) =

    let mutable client: TcpClient option = None
    let mutable connected = false
    let packetLogger = packetLogger

    let logPacket direction (bytes: byte[]) length =
        match packetLogger with
        | Some logger -> logger direction bytes length
        | None -> ()

    let establishConnection () =
        try
            let tcpClient = new TcpClient()
            let cts = new CancellationTokenSource(timeoutMs)
            let connectTask = tcpClient.ConnectAsync(ip, port)
            let completed = connectTask.Wait(timeoutMs, cts.Token)

            if completed && tcpClient.Connected then
                client <- Some tcpClient
                connected <- true
                true
            else
                tcpClient.Close()
                connected <- false
                false
        with
        | :? TaskCanceledException
        | :? SocketException ->
            connected <- false
            false
        | ex ->
            eprintfn "[XGT TCP Connect] %s" ex.Message
            connected <- false
            false


    member _.IpAddress = ip
    member _.Port = port

    member _.SourcePort =
        match client with
        | Some tcp when connected ->
            match tcp.Client.LocalEndPoint with
            | :? IPEndPoint as endpoint -> endpoint.Port
            | _ -> 0
        | _ -> 0

    member _.IsConnected = connected

    member this.Connect() =
        if connected then true else establishConnection ()

    member this.Reconnect() =
        this.Disconnect() |> ignore
        this.Connect()

    member this.Disconnect() =
        match client with
        | Some tcp ->
            tcp.Close()
            client <- None
            connected <- false
            true
        | None ->
            false

    member private this.RequireStream() =
        match client with
        | Some tcp when connected -> tcp.GetStream()
        | _ -> failwith "PLC connection is not established."

    member this.SendFrame(frame: byte[]) =
        let stream = this.RequireStream()
        logPacket "[TX]" frame frame.Length
        stream.Write(frame, 0, frame.Length)

    member this.ReceiveFrame(bufferSize: int) =
        try
            let startTime = System.DateTime.UtcNow
            let stream = this.RequireStream()
            let buffer = Array.zeroCreate<byte> bufferSize
            
            let received = stream.Read(buffer, 0, buffer.Length)
            let duration = System.DateTime.UtcNow - startTime
            
            if received = 0 then
                connected <- false
                failwith "Connection closed by remote host"
            
            logPacket "[RX]" buffer received
            buffer
        with
        | ex ->
            connected <- false
            if ex.InnerException <> null then
                eprintfn "[XGT TCP ReceiveFrame] Inner exception: %s" ex.InnerException.Message
            reraise()
