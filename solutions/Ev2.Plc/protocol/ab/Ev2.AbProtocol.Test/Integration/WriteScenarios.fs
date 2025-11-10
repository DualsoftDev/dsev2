namespace Ev2.AbProtocol.Test.Integration

open System
open Xunit
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Test
open Ev2.AbProtocol.Test.TagFixtures

module WriteScenarios =
    
    let private connectionError (error: AbProtocolError) =
        $"Connection failed: {error.Message}"
    
    let private boolPayload value = if value then [| 0xFFuy |] else [| 0uy |]
    
    [<IntegrationFact>]
    let ``BOOL tag can be toggled and restored``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                let (initialStatus, payload) = ClientHarness.readTag client Tags.boolScalar
                if not initialStatus.IsSuccess then
                    failwithf "Initial read failed: %s" initialStatus.Message
                
                let original = payload |> Option.map expectBool |> Option.defaultValue false
                let desired = not original
                
                let writeError =
                    client.WriteTag(Tags.boolScalar.Name, DataType.BOOL, boolPayload desired)
                if writeError.IsError then
                    failwithf "Write failed: %s" writeError.Message
                
                let (verifyStatus, verifyPayload) = ClientHarness.readTag client Tags.boolScalar
                if not verifyStatus.IsSuccess then
                    failwithf "Verification read failed: %s" verifyStatus.Message
                
                let verified = verifyPayload |> Option.map expectBool |> Option.defaultValue false
                let restoreError =
                    client.WriteTag(Tags.boolScalar.Name, DataType.BOOL, boolPayload original)
                if restoreError.IsError then
                    TestHelpers.log $"⚠ Failed to restore BOOL tag: {restoreError.Message}"
                
                original, desired, verified)
        let (original, desired, verified) =
            ClientHarness.unwrap connectionError result
        Assert.NotEqual(original, desired)
        Assert.Equal(desired, verified)
    
    [<IntegrationFact>]
    let ``DINT tag can be incremented and restored``() =
        let result =
            ClientHarness.runWithClient (fun client ->
                let (initialStatus, payload) = ClientHarness.readTag client Tags.dintScalar
                if not initialStatus.IsSuccess then
                    failwithf "Initial read failed: %s" initialStatus.Message
                
                let original = payload |> Option.map expectInt32 |> Option.defaultValue 0
                let desired = if original = Int32.MaxValue then original - 1 else original + 1
                
                let desiredBytes = BitConverter.GetBytes desired
                let writeError = ClientHarness.writeScalar client Tags.dintScalar desiredBytes
                if writeError.IsError then
                    failwithf "Write failed: %s" writeError.Message
                
                let (verifyStatus, verifyPayload) = ClientHarness.readTag client Tags.dintScalar
                if not verifyStatus.IsSuccess then
                    failwithf "Verification read failed: %s" verifyStatus.Message
                
                let verified = verifyPayload |> Option.map expectInt32 |> Option.defaultValue original
                let restoreBytes = BitConverter.GetBytes original
                let restoreError = ClientHarness.writeScalar client Tags.dintScalar restoreBytes
                if restoreError.IsError then
                    TestHelpers.log $"⚠ Failed to restore DINT tag: {restoreError.Message}"
                
                original, desired, verified)
        let (original, desired, verified) =
            ClientHarness.unwrap connectionError result
        Assert.NotEqual(original, desired)
        Assert.Equal(desired, verified)
