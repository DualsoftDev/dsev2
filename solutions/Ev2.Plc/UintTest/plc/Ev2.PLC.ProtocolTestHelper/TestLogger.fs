namespace ProtocolTestHelper

open System
open System.Collections.Concurrent

/// In-memory test logger that captures timestamped messages for later assertion.
type TestLogger(?capacity: int) =
    let capacity = defaultArg capacity 200
    let entries = ConcurrentQueue<string>()

    let trimQueue () =
        while entries.Count > capacity do
            entries.TryDequeue() |> ignore

    member this.Log(message: string) =
        let timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
        entries.Enqueue($"[{timestamp}] {message}")
        trimQueue ()

    member this.LogFormat(format: string, [<ParamArray>] args: obj[]) =
        this.Log(String.Format(format, args))

    member _.Entries = entries.ToArray()

    member this.Dump(?header: string) =
        let builder = System.Text.StringBuilder()
        header |> Option.iter (fun h -> builder.AppendLine(h) |> ignore)
        for entry in entries do
            builder.AppendLine(entry) |> ignore
        builder.ToString()

/// Utilities for capturing console output during protocol tests.
module ConsoleCapture =
    open System.IO
    open System.Text

    type CaptureResult<'T> =
        { Result: Result<'T, exn>
          StdOut: string
          StdErr: string }

    let capture (action: unit -> 'T) =
        let originalOut = Console.Out
        let originalErr = Console.Error
        use outBuffer = new StringWriter()
        use errBuffer = new StringWriter()
        Console.SetOut(outBuffer)
        Console.SetError(errBuffer)
        try
            try
                let result = action()
                { Result = Ok result
                  StdOut = outBuffer.ToString()
                  StdErr = errBuffer.ToString() }
            with ex ->
                { Result = Error ex
                  StdOut = outBuffer.ToString()
                  StdErr = errBuffer.ToString() }
        finally
            Console.SetOut originalOut
            Console.SetError originalErr

    let private appendSection (builder: StringBuilder) label (content: string) =
        if not (String.IsNullOrWhiteSpace content) then
            builder
                .AppendLine($"--- {label} ---")
                .AppendLine(content.TrimEnd())
                .AppendLine()
            |> ignore

    let summarize (result: CaptureResult<_>) =
        let sb = StringBuilder()
        appendSection sb "STDOUT" result.StdOut
        appendSection sb "STDERR" result.StdErr
        sb.ToString().TrimEnd()

    let formatFailure (result: CaptureResult<_>) (message: string) =
        let summary = summarize result
        let exceptionInfo =
            match result.Result with
            | Error ex -> $"Exception: {ex.GetType().Name} - {ex.Message}"
            | Ok _ -> String.Empty

        [ message
          if String.IsNullOrWhiteSpace exceptionInfo then "" else exceptionInfo
          if String.IsNullOrWhiteSpace summary then "" else summary ]
        |> List.filter (fun s -> not (String.IsNullOrWhiteSpace s))
        |> String.concat (Environment.NewLine + Environment.NewLine)
