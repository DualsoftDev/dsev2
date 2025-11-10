namespace Ev2.PLC.Driver.Base

open System
open System.Net
open System.Net.Sockets

/// Base class for UDP PLC communications.
[<AbstractClass>]
type PlcEthernetUdpBase(ip: string, port: int, timeoutMs: int) =
    let remote = IPEndPoint(IPAddress.Parse ip, port)
    let mutable client: UdpClient option = None
    let mutable connected = false

    let connect() =
        try
            let udp = new UdpClient()
            udp.Client.ReceiveTimeout <- timeoutMs
            udp.Client.SendTimeout <- timeoutMs
            udp.Connect(remote)
            client <- Some udp
            connected <- true
            true
        with
        | :? SocketException
        | :? FormatException ->
            connected <- false
            false
        | ex ->
            eprintfn "[PLC UDP Connect] %s" ex.Message
            connected <- false
            false

    do connect() |> ignore

    member _.Ip = ip
    member _.Port = port
    member _.SourcePort =
        match client with
        | Some udp when connected ->
            match udp.Client.LocalEndPoint with
            | :? IPEndPoint as endpoint -> endpoint.Port
            | _ -> 0
        | _ -> 0

    member _.IsConnected = connected

    member this.Connect() = if connected then true else connect()

    member this.ReConnect() =
        this.Disconnect() |> ignore
        this.Connect()

    member _.Disconnect() =
        match client with
        | Some udp ->
            udp.Close()
            client <- None
            connected <- false
            true
        | None -> false

    member this.GetSocket() =
        match client with
        | Some udp when connected -> udp
        | _ -> failwith "PLC (UDP) connection is not established."

    member this.SendFrame(frame: byte[]) =
        let udp = this.GetSocket()
        let sent = udp.Send(frame, frame.Length)
        if sent <> frame.Length then
            failwithf "Partial send detected: %d of %d bytes" sent frame.Length

    member this.ReceiveFrame(maxBytes: int) =
        let udp = this.GetSocket()
        let mutable endpoint = Unchecked.defaultof<IPEndPoint>
        try
            let data = udp.Receive(&endpoint)
            if data.Length > maxBytes then Array.sub data 0 maxBytes else data
        with
        | :? SocketException as se when se.SocketErrorCode = SocketError.TimedOut ->
            failwith "Receive timeout"
