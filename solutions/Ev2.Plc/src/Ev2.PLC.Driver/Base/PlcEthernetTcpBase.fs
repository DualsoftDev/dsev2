namespace Ev2.PLC.Driver.Base

open System
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

/// Base class for TCP PLC communications.
[<AbstractClass>]
type PlcEthernetTcpBase(ip: string, port: int, timeoutMs: int) =
    let mutable client: TcpClient option = None
    let mutable connected = false

    let connect() =
        try
            let tcpClient = new TcpClient()
            let cts = new CancellationTokenSource(timeoutMs)
            let task = tcpClient.ConnectAsync(ip, port)
            let completed = task.Wait(timeoutMs, cts.Token)
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
            eprintfn "[PLC TCP Connect] %s" ex.Message
            connected <- false
            false

    do connect() |> ignore

    member _.Ip = ip
    member _.Port = port
    member _.SourcePort =
        match client with
        | Some tcp when connected ->
            match tcp.Client.LocalEndPoint with
            | :? System.Net.IPEndPoint as endpoint -> endpoint.Port
            | _ -> 0
        | _ -> 0

    member _.IsConnected = connected

    member this.Connect() = if connected then true else connect()

    member this.ReConnect() =
        this.Disconnect() |> ignore
        this.Connect()

    member this.Disconnect() =
        match client with
        | Some tcp ->
            tcp.Close()
            client <- None
            connected <- false
            true
        | None -> false

    member this.GetStream() =
        match client with
        | Some tcp when connected -> tcp.GetStream()
        | _ -> failwith "PLC connection is not established."

    member this.SendFrame(frame: byte[]) =
        let stream = this.GetStream()
        stream.Write(frame, 0, frame.Length)

    member this.ReceiveFrame(bufferSize: int) =
        let stream = this.GetStream()
        let buffer = Array.zeroCreate<byte> bufferSize
        let _ = stream.Read(buffer, 0, buffer.Length)
        buffer
