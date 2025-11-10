namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions
module CH = Ev2.S7Protocol.Tests.ClientHarness

module IntegrationControlTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Communication statistics capture successes`` () =
        let result =
            CH.runWithClient (fun client ->
                let before = client.GetStatistics()
                let _ = client.ReadBit(Tags.merkerBit)
                let after = client.GetStatistics()
                before, after)
        let beforeStats, afterStats =
            CH.unwrap connectionError result
        Assert.True(afterStats.PacketsReceived >= beforeStats.PacketsReceived, "PacketsReceived should not decrease")
        Assert.True(afterStats.SuccessRate >= 0.0)
    
    [<RequiresS7PLC>]
    let ``Invalid address surfaces error`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadBit("M-1.0"))
        match CH.unwrap connectionError result with
        | Ok _ ->
            TH.failWithLogsWithResult result "ReadBit should have rejected negative address."
        | Error msg ->
            Assert.Contains("Address", msg, StringComparison.OrdinalIgnoreCase)
    
    [<RequiresS7PLC>]
    let ``Statistics capture error scenarios`` () =
        let result =
            CH.runWithClient (fun client ->
                let before = client.GetStatistics()
                let writeResult = client.WriteBytes(DataArea.Merker, 0, Int32.MaxValue, [| 0x01uy |])
                let after = client.GetStatistics()
                before, after, writeResult)
        let beforeStats, afterStats, writeOutcome =
            CH.unwrap connectionError result
        match writeOutcome with
        | Ok _ ->
            Assert.True(afterStats.ErrorCount >= beforeStats.ErrorCount)
        | Error _ ->
            Assert.True(afterStats.ErrorCount >= beforeStats.ErrorCount + 1L)
