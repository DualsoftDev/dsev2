namespace Ev2.S7Protocol.Tests

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers

module DebugConnectionTest =

    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``toggle diagnostic mode`` () =
        let result =
            CH.runWithClient (fun client ->
                let beforeStats = client.GetStatistics()
                let initialConnected = client.IsConnected
                // Perform a lightweight diagnostic read
                let diagResult = client.ReadBytes(DataArea.Merker, 0, 0, 4)
                let afterStats = client.GetStatistics()
                initialConnected, diagResult, afterStats, beforeStats)
        let (initialConnected, diagResult, afterStats, beforeStats) =
            CH.unwrap connectionError result
        Assert.True(initialConnected, "Client should report connected state")
        match diagResult with
        | Ok data -> Assert.True(data.Length = 4, "Diagnostic read should return 4 bytes")
        | Error msg -> TH.failWithLogsWithResult result $"Diagnostic read failed: {msg}"
        Assert.True(afterStats.PacketsReceived >= beforeStats.PacketsReceived, "PacketsReceived should not decrease")
