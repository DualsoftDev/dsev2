namespace Ev2.S7Protocol.Tests

open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers

module SessionManagerTests =

    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``session manager connect handshake`` () =
        let result =
            CH.runWithClient (fun client ->
                let negotiated = client.PDUSize
                let stats = client.GetStatistics()
                client.IsConnected, negotiated, stats)
        let (isConnected, pduSize, stats) =
            CH.unwrap connectionError result
        Assert.True(isConnected, "Session should report connected state")
        Assert.True(pduSize > 0, "PDU negotiation should produce positive value")
        Assert.True(stats.SuccessRate >= 0.0)
