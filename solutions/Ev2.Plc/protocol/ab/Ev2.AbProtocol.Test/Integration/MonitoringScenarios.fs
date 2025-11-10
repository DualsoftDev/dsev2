namespace Ev2.AbProtocol.Test.Integration

open System
open Xunit
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Test
open Ev2.AbProtocol.Test.TagFixtures

module MonitoringScenarios =
    
    let private connectionError (error: AbProtocolError) =
        $"Connection failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``Communication statistics track successful reads``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                let before = client.GetStatistics()
                let (status1, _) = ClientHarness.readTag client Tags.boolScalar
                if status1.IsError then failwithf "First read failed: %s" status1.Message
                let (status2, _) = ClientHarness.readTag client Tags.intScalar
                if status2.IsError then failwithf "Second read failed: %s" status2.Message
                let after = client.GetStatistics()
                before, after)
        let (before, after) =
            ClientHarness.unwrap connectionError result
        Assert.True(after.PacketsSent >= before.PacketsSent + 2L)
        Assert.True(after.PacketsReceived >= before.PacketsReceived + 2L)
        Assert.True(after.SuccessRate >= 0.0 && after.SuccessRate <= 100.0)
    
    [<IntegrationFact>]
    let ``Communication statistics capture protocol errors``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                let before = client.GetStatistics()
                let _ = ClientHarness.readTag client Tags.nonExistent
                let after = client.GetStatistics()
                before, after)
        let (before, after) =
            ClientHarness.unwrap connectionError result
        Assert.True(after.ErrorCount >= before.ErrorCount + 1L)
        Assert.True(after.LastError.IsSome)
        Assert.True(after.LastErrorMessage.IsSome)
    
    [<IntegrationFact>]
    let ``Repeated BOOL reads stay within timeout budget``() =
        let iterations = 10
        let timeoutBudget = int64 (TestHelpers.abTimeoutMs * iterations)
        let result =
            ClientHarness.runWithClient (fun client ->
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                for _ in 1 .. iterations do
                    let (status, payload) = ClientHarness.readTag client Tags.boolScalar
                    if status.IsError then
                        failwithf "Read failed: %s" status.Message
                    payload |> Option.iter (ignore << expectBool)
                stopwatch.Stop()
                stopwatch.ElapsedMilliseconds)
        let elapsed =
            ClientHarness.unwrap connectionError result
        Assert.True(elapsed <= timeoutBudget, $"Measured {elapsed}ms which exceeds budget {timeoutBudget}ms")
