namespace Ev2.S7Protocol.Tests

open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness

module HardwareValidationTest =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``sanity check hardware handshake`` () =
        let result =
            CH.runWithClient (fun client ->
                client.IsConnected, client.PDUSize, client.GetStatistics())
        let (isConnected, pduSize, stats) =
            CH.unwrap connectionError result
        Assert.True(isConnected, "Client should report connected state")
        Assert.True(pduSize > 0, "Negotiated PDU size must be positive")
        Assert.True(stats.SuccessRate >= 0.0, "Success rate should be non-negative")
