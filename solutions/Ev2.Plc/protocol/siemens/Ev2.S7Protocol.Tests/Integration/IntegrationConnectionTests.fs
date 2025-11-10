namespace Ev2.S7Protocol.Tests.Integration

open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions

module IntegrationConnectionTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Connect negotiates PDU size`` () =
        let result =
            CH.runWithClient (fun client ->
                client.PDUSize, client.GetStatistics())
        let pduSize, stats =
            CH.unwrap connectionError result
        Assert.True(pduSize > 0, "Negotiated PDU size must be positive")
        Assert.True(stats.SuccessRate >= 0.0)
    
    [<RequiresS7PLC>]
    let ``Success statistics increase after connect`` () =
        let result =
            CH.runWithClient (fun client ->
                let before = client.GetStatistics()
                let readResult = client.ReadBit(Tags.merkerBit)
                let after = client.GetStatistics()
                before, after, readResult)
        let beforeStats, afterStats, readOutcome =
            CH.unwrap connectionError result
        match readOutcome with
        | Ok _ ->
            Assert.True(afterStats.PacketsReceived >= beforeStats.PacketsReceived, "PacketsReceived should not decrease")
            Assert.True(afterStats.SuccessRate >= 0.0)
        | Error msg ->
            TH.failWithLogsWithResult result $"Initial merker read failed: {msg}"
