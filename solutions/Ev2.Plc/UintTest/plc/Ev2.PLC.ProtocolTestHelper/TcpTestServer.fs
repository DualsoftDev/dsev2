namespace ProtocolTestHelper

open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

/// Scripted TCP steps used by <see cref="TcpTestServer"/>.
type TcpStep =
    | Expect of label: string * data: byte[]
    | Respond of label: string * data: byte[]
    | Delay of TimeSpan
    | Custom of (NetworkStream -> unit)

/// <summary>
///     Simple scripted TCP server used to emulate PLC behaviour during unit tests.  The server processes a sequence of
///     <see cref="TcpStep"/> values for a single client connection and records a textual log for troubleshooting.
/// </summary>
type TcpTestServer(port: int, steps: TcpStep list, ?readTimeout: TimeSpan, ?writeTimeout: TimeSpan) =
    let listener = new TcpListener(IPAddress.Loopback, port)
    let cancellation = new CancellationTokenSource()
    let log = ConcurrentQueue<string>()
    let readTimeout = defaultArg readTimeout (TimeSpan.FromSeconds 5.0)
    let writeTimeout = defaultArg writeTimeout (TimeSpan.FromSeconds 5.0)
    let mutable runTask: Task option = None
    let mutable started = false

    let record message =
        let timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
        log.Enqueue($"[{timestamp}] {message}")

    let readExact (stream: NetworkStream) (buffer: byte[]) =
        let mutable offset = 0
        while offset < buffer.Length do
            let read = stream.Read(buffer, offset, buffer.Length - offset)
            if read = 0 then
                raise (IOException("Connection closed while reading from stream."))
            offset <- offset + read

    let processSteps (stream: NetworkStream) =
        stream.ReadTimeout <- int readTimeout.TotalMilliseconds
        stream.WriteTimeout <- int writeTimeout.TotalMilliseconds

        for step in steps do
            cancellation.Token.ThrowIfCancellationRequested()
            match step with
            | Expect (label, expected) ->
                record $"Expecting {label} ({expected.Length} bytes)"
                let buffer = Array.zeroCreate<byte> expected.Length
                readExact stream buffer
                BufferAssert.equalWithLabel (sprintf "Mismatch in step '%s'" label) expected buffer
                record $"Received {label}"
            | Respond (label, payload) ->
                record $"Sending {label} ({payload.Length} bytes)"
                stream.Write(payload, 0, payload.Length)
                stream.Flush()
            | Delay duration ->
                record $"Delaying {duration.TotalMilliseconds} ms"
                Thread.Sleep(int duration.TotalMilliseconds)
            | Custom handler ->
                record "Invoking custom handler"
                handler stream

    let executionLoop () =
        try
            use client = listener.AcceptTcpClient()
            record "Client connected"
            use stream = client.GetStream()
            processSteps stream
            record "Script completed"
        with
        | :? OperationCanceledException ->
            record "Server cancelled."
        | ex ->
            record $"Server failed: {ex.Message}"
            raise ex

    member _.Port = port
    member _.Logs = log.ToArray()

    member this.Start() =
        if started then invalidOp "TcpTestServer already started."
        started <- true
        listener.Start()
        record $"Listening on port {port}"
        runTask <- Some(Task.Run(Action executionLoop, cancellation.Token))

    member this.Stop() =
        if started then
            cancellation.Cancel()
            try listener.Stop() with _ -> ()
            runTask |> Option.iter (fun task ->
                try task.Wait(TimeSpan.FromSeconds 2.0) |> ignore with _ -> ())
            record "Server stopped"
            started <- false

    interface IDisposable with
        member this.Dispose() =
            this.Stop()
            cancellation.Dispose()
