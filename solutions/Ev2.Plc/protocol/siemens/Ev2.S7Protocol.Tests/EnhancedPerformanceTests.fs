namespace Ev2.S7Protocol.Tests

open System.Diagnostics
open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module Tags = Ev2.S7Protocol.Tests.TagDefinitions
module TH = Ev2.S7Protocol.Tests.TestHelpers

module EnhancedPerformanceTests =

    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``stress read write cycles`` () =
        let iterations = 20
        let result =
            CH.runWithClient (fun client ->
                let stopwatch = Stopwatch.StartNew()
                let mutable lastValue = 0uy
                let mutable failures = []
                for _ in 1 .. iterations do
                    match client.ReadByte(Tags.merkerByte) with
                    | Ok value ->
                        lastValue <- value
                        let desired = byte ((int value + 1) % 255)
                        match client.WriteByte(Tags.merkerByte, desired) with
                        | Ok () -> ignore (client.WriteByte(Tags.merkerByte, value))
                        | Error msg -> failures <- ("Write", msg) :: failures
                    | Error msg -> failures <- ("Read", msg) :: failures
                stopwatch.Stop()
                if List.isEmpty failures then
                    Ok (lastValue, stopwatch.ElapsedMilliseconds)
                else
                    Error (failures |> List.rev |> List.map snd |> String.concat "; "))
        match CH.unwrap connectionError result with
        | Ok (_, elapsedMs) ->
            let maxBudget = int64 (TH.s7TimeoutMs * iterations)
            Assert.True(elapsedMs <= maxBudget, $"Elapsed {elapsedMs}ms exceeds budget {maxBudget}ms")
        | Error msg ->
            TH.failWithLogsWithResult result $"Stress read/write cycles failed: {msg}"
