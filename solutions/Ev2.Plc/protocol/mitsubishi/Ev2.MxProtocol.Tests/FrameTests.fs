module Ev2.MxProtocol.Tests.FrameTests

open System
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Protocol
open Ev2.MxProtocol.Tests.TestHelpers

[<Fact>]
let ``Frame.buildFrame creates correct 3E binary frame header`` () =
    log "Validating 3E binary frame header."
    let config = defaultTestConfig
    let request = {
        Command = CommandCode.BatchRead
        Subcommand = SubcommandCode.WordUnits
        Payload = [| 0xA8uy; 0x00uy; 0x00uy; 0x00uy; 0x0Auy; 0x00uy |]
    }
    
    let frame = Frame.buildFrame config request
    
    // Check frame header
    assertEqual 0x50uy frame.[0]
    assertEqual 0x00uy frame.[1]
    assertEqual 0x00uy frame.[2]
    assertEqual 0xFFuy frame.[3]   // StationNumber from config (0xFF to avoid station conflicts)
    assertEqual 0xFFuy frame.[4]   // IoNumber low byte (0x03FF)
    assertEqual 0x03uy frame.[5]   // IoNumber high byte  
    assertEqual 0x00uy frame.[6]   // RelayType from config (0x00)
    
    // Data length should be monitoring timer(2) + command(2) + subcommand(2) + payload(6) = 12 bytes  
    // According to buildFrame implementation: 6 + payload.Length = 6 + 6 = 12
    let dataLength = int frame.[7] ||| (int frame.[8] <<< 8)
    assertEqual 12 dataLength
    
    // Monitoring timer
    assertEqual 0x10uy frame.[9]
    assertEqual 0x00uy frame.[10]
    
    // Command
    assertEqual 0x01uy frame.[11]
    assertEqual 0x04uy frame.[12]
    
    // Subcommand
    assertEqual 0x00uy frame.[13]
    assertEqual 0x00uy frame.[14]

[<Fact>]
let ``Frame.parseFrame correctly parses success response`` () =
    log "Parsing success response frame."
    let config = defaultTestConfig
    
    // Create a mock response frame
    let response = [|
        0xD0uy; 0x00uy; // Subheader (response)
        0x00uy;         // Network number
        0xFFuy;         // PC number
        0xFFuy; 0x03uy; // IO number
        0x00uy;         // Station number
        0x06uy; 0x00uy; // Data length (6 bytes: endcode(2) + data(4))
        0x00uy; 0x00uy; // End code (success)
        0x12uy; 0x34uy; // Data
        0x56uy; 0x78uy  // Data
    |]
    
    match Frame.parseFrame config response with
    | Ok result ->
        assertTrue result.EndCode.IsSuccess "Expected success end code."
        assertEqual 4 result.Data.Length
        assertEqual 0x12uy result.Data.[0]
        assertEqual 0x34uy result.Data.[1]
        assertEqual 0x56uy result.Data.[2]
        assertEqual 0x78uy result.Data.[3]
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``Frame.parseFrame correctly parses error response`` () =
    log "Parsing error response frame."
    let config = defaultTestConfig
    
    // Create a mock error response frame
    let response = [|
        0xD0uy; 0x00uy; // Subheader (response)
        0x00uy;         // Network number
        0xFFuy;         // PC number
        0xFFuy; 0x03uy; // IO number
        0x00uy;         // Station number
        0x04uy; 0x00uy; // Data length (4 bytes: endcode(2) + network(1) + station(1))
        0x01uy; 0x50uy; // End code (error 0x5001)
        0x01uy;         // Network error
        0x02uy          // Station error
    |]
    
    match Frame.parseFrame config response with
    | Ok result ->
        assertFalse result.EndCode.IsSuccess "Expected error end code."
        assertEqual 0x5001us result.EndCode.Code
        match result.EndCode with
        | EndCodeError (_, netErr, stationErr) ->
            assertEqual 0x01uy netErr
            assertEqual 0x02uy stationErr
        | _ -> failWithLogs "Expected EndCodeError"
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``Frame.createWriteBitRequest creates correct frame`` () =
    log "Validating write bit request payload."
    let values = [| true; false; true; true; false |]
    let request = Frame.createWriteBitRequest DeviceCode.M 100 values
    
    assertEqual CommandCode.BatchWrite request.Command
    assertEqual SubcommandCode.BitUnits request.Subcommand
    
    // Payload should contain device info and bit values
    assertTrue (request.Payload.Length >= 7) "Payload too short for bit write."
    
    // Check address bytes first (address 100 = 0x64)
    assertEqual 0x64uy request.Payload.[0]   // Address low byte
    assertEqual 0x00uy request.Payload.[1]   // Address mid byte
    assertEqual 0x00uy request.Payload.[2]   // Address high byte
    
    // Check device code (M = 0x90)
    assertEqual 0x90uy request.Payload.[3]
    
    // Check count (5 bits)
    assertEqual 0x05uy request.Payload.[4]
    assertEqual 0x00uy request.Payload.[5]

[<Fact>]
let ``Frame validates minimum response length`` () =
    log "Validating minimum response length handling."
    let config = defaultTestConfig
    
    // Too short response
    let shortResponse = [| 0xD0uy; 0x00uy |]
    
    match Frame.parseFrame config shortResponse with
    | Error msg ->
        assertContains "short" (msg.ToLower())
    | Ok _ ->
        failWithLogs "Should have failed with short response"

[<Fact>]
let ``Frame handles various frame types`` () =
    let testFrameType frameType =
        let config = { defaultTestConfig with FrameType = frameType }
        let request = {
            Command = CommandCode.BatchRead
            Subcommand = SubcommandCode.WordUnits
            Payload = [||]
        }
        
        let frame = Frame.buildFrame config request
        assertNotNull (frame :> obj) "Frame should not be null."
        assertTrue (frame.Length > 0) "Frame length should be positive."

    testFrameType FrameType.QnA_3E_Binary
    testFrameType FrameType.QnA_3E_Ascii
    testFrameType FrameType.A_1E_Binary
