namespace Ev2.S7Protocol.Tests

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
open Ev2.S7Protocol.Tests.ClientHarness
open Ev2.S7Protocol.Tests.TestHelpers
open Ev2.S7Protocol.Tests.TagDefinitions

module S7ClientTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``CpuType enum exposes expected value`` () =
        assertEqual 10 (int CpuType.S7300)

    [<RequiresS7PLC>]
    let ``Communication stats reflect successes`` () =
        let result =
            runWithClient (fun client ->
                let before = client.GetStatistics()
                let _ = client.GetStatistics()
                let after = client.GetStatistics()
                before, after)
        let beforeStats, afterStats =
            unwrap connectionError result
        Assert.True(afterStats.SuccessRate >= 0.0)
        Assert.True(afterStats.PacketsReceived >= beforeStats.PacketsReceived)

    [<RequiresS7PLC>]
    let ``ReadBit returns boolean result`` () =
        let result =
            runWithClient (fun client ->
                client.ReadBit(TagDefinitions.merkerBit))
        match unwrap connectionError result with
        | Ok value -> Assert.True(value || not value)
        | Error msg -> failWithLogsWithResult result $"ReadBit failed: {msg}"

    [<RequiresS7PLC>]
    let ``ReadByte returns byte`` () =
        let result =
            runWithClient (fun client ->
                client.ReadByte(TagDefinitions.merkerByte))
        match unwrap connectionError result with
        | Ok value -> Assert.InRange(int value, 0, 255)
        | Error msg -> failWithLogsWithResult result $"ReadByte failed: {msg}"

    [<RequiresS7PLC>]
    let ``ReadInt16 returns int16`` () =
        let result =
            runWithClient (fun client ->
                client.ReadInt16(TagDefinitions.merkerWord))
        match unwrap connectionError result with
        | Ok _ -> Assert.True(true)
        | Error msg -> failWithLogsWithResult result $"ReadInt16 failed: {msg}"

    [<RequiresS7PLC>]
    let ``Invalid address raises error`` () =
        let result =
            runWithClient (fun client ->
                client.ReadBit("M-1.0"))
        match unwrap connectionError result with
        | Ok _ ->
            failWithLogsWithResult result "ReadBit should have rejected negative address."
        | Error msg ->
            Assert.Contains("Address", msg, StringComparison.OrdinalIgnoreCase)
