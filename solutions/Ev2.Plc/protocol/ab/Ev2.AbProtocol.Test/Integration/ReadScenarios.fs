namespace Ev2.AbProtocol.Test.Integration

open System
open Xunit
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Test
open Ev2.AbProtocol.Test.TagFixtures

module ReadScenarios =
    
    let private connectionError (error: AbProtocolError) =
        $"Connection failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``BOOL scalar can be read``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                ClientHarness.readTag client Tags.boolScalar)
        let (status, payload) = ClientHarness.unwrap connectionError result
        match status, payload with
        | NoError, Some value ->
            value |> expectBool |> ignore
        | error, _ ->
            TestHelpers.failWithLogs $"Read failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``INT scalar returns int16``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                ClientHarness.readTag client Tags.intScalar)
        let (status, payload) = ClientHarness.unwrap connectionError result
        match status, payload with
        | NoError, Some value ->
            value |> expectInt16 |> ignore
        | error, _ ->
            TestHelpers.failWithLogs $"Read failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``DINT scalar returns int32``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                ClientHarness.readTag client Tags.dintScalar)
        let (status, payload) = ClientHarness.unwrap connectionError result
        match status, payload with
        | NoError, Some value ->
            value |> expectInt32 |> ignore
        | error, _ ->
            TestHelpers.failWithLogs $"Read failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``Small DINT array length matches catalog``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                ClientHarness.readTag client Tags.smallDintArray)
        let (status, payload) = ClientHarness.unwrap connectionError result
        match status, payload with
        | NoError, Some (:? (int32[]) as values) ->
            Assert.Equal(Tags.smallDintArray.ElementCount, values.Length)
        | NoError, Some unexpected ->
            TestHelpers.failWithLogs $"Unexpected payload type: {unexpected.GetType().FullName}"
        | error, _ ->
            TestHelpers.failWithLogs $"Read failed: {error.Message}"
    
    [<IntegrationFact>]
    let ``Reading non-existent tag surfaces protocol error``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                ClientHarness.readTag client Tags.nonExistent)
        let (status, _) = ClientHarness.unwrap connectionError result
        Assert.True(status.IsError, "Non-existent tag should yield AbProtocolError")
