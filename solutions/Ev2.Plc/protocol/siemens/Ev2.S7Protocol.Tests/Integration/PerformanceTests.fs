namespace Ev2.S7Protocol.Tests.Integration

open System
open System.Diagnostics
open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions
module CH = Ev2.S7Protocol.Tests.ClientHarness

module PerformanceTests =
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``Repeated merker byte reads stay within timeout budget`` () =
        let iterations = 10
        let budget = int64 (TH.s7TimeoutMs * iterations)
        let result =
            CH.runWithClient (fun client ->
                let stopwatch = Stopwatch.StartNew()
                let mutable lastValue = 0uy
                let mutable error: string option = None
                for _ in 1 .. iterations do
                    match client.ReadByte(Tags.merkerByte) with
                    | Ok value -> lastValue <- value
                    | Error msg ->
                        if error.IsNone then error <- Some msg
                stopwatch.Stop()
                match error with
                | Some msg -> Error msg
                | None -> Ok (lastValue, stopwatch.ElapsedMilliseconds))
        match CH.unwrap connectionError result with
        | Ok (_, elapsedMs) -> Assert.True(elapsedMs <= budget, $"Elapsed {elapsedMs}ms exceeds budget {budget}ms")
        | Error msg -> TH.failWithLogsWithResult result $"Performance read failed: {msg}"

    [<RequiresS7PLC>]
    let ``Write and read merker word loop`` () =
        let iterations = 5
        let result =
            CH.runWithClient (fun client ->
                match client.ReadInt16(Tags.merkerWord) with
                | Error msg -> Error $"Initial read failed: {msg}"
                | Ok baseline ->
                    let mutable current = baseline
                    let mutable error: string option = None
                    let mutable completed = 0
                    for _ in 1 .. iterations do
                        if error.IsNone then
                            let next = if current = Int16.MaxValue then Int16.MinValue else current + 1s
                            match client.WriteInt16(Tags.merkerWord, next) with
                            | Error msg -> error <- Some $"Write failed: {msg}"
                            | Ok () ->
                                match client.ReadInt16(Tags.merkerWord) with
                                | Ok value ->
                                    current <- value
                                    completed <- completed + 1
                                | Error msg -> error <- Some $"Read failed: {msg}"
                    let _ = client.WriteInt16(Tags.merkerWord, baseline)
                    match error with
                    | Some msg -> Error msg
                    | None -> Ok completed)
        match CH.unwrap connectionError result with
        | Ok finished -> Assert.Equal(iterations, finished)
        | Error msg -> TH.failWithLogsWithResult result $"Write/read loop failed: {msg}"
