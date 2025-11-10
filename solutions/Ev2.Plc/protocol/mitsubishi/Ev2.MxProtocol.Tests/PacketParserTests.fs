module Ev2.MxProtocol.Tests.PacketParserTests

open System
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Protocol
open Ev2.MxProtocol.Tests.TestHelpers

[<Fact>]
let ``parseBatchReadWords correctly parses word data`` () =
    log "Parsing batch read words."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x34uy; 0x12uy; 0x78uy; 0x56uy; 0xCDuy; 0xABuy |]
    }
    
    match PacketParser.parseBatchReadWords response 3 with
    | Ok words ->
        assertEqual 3 words.Length
        assertEqual 0x1234us words.[0]
        assertEqual 0x5678us words.[1]
        assertEqual 0xABCDus words.[2]
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``parseBatchReadWords handles error response`` () =
    log "Parsing batch read error response."
    let response = {
        EndCode = EndCodeError (0x5001us, 0x00uy, 0x00uy)
        Data = [||]
    }
    
    match PacketParser.parseBatchReadWords response 3 with
    | Error msg ->
        assertContains "5001" msg
    | Ok _ ->
        failWithLogs "Should have returned error"

[<Fact>]
let ``parseBatchReadBits correctly parses bit data`` () =
    log "Parsing batch read bits."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x11uy; 0x00uy; 0x01uy; 0x10uy |] // Packed format: bit pairs in each byte
    }
    
    match PacketParser.parseBatchReadBits response 8 with
    | Ok bits ->
        assertEqual 8 bits.Length
        assertTrue bits.[0] "Bit 0 should be true."   // 0x11 & 0x10 = 0x10 != 0
        assertTrue bits.[1] "Bit 1 should be true."   // 0x11 & 0x01 = 0x01 != 0
        assertFalse bits.[2] "Bit 2 should be false." // 0x00 & 0x10 = 0x00 = 0
        assertFalse bits.[3] "Bit 3 should be false." // 0x00 & 0x01 = 0x00 = 0
        assertFalse bits.[4] "Bit 4 should be false." // 0x01 & 0x10 = 0x00 = 0
        assertTrue bits.[5] "Bit 5 should be true."   // 0x01 & 0x01 = 0x01 != 0
        assertTrue bits.[6] "Bit 6 should be true."   // 0x10 & 0x10 = 0x10 != 0
        assertFalse bits.[7] "Bit 7 should be false." // 0x10 & 0x01 = 0x00 = 0
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``parseBatchReadBits handles packed bit format`` () =
    log "Parsing packed bit format."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x11uy; 0x10uy |] // Packed format: 0x11 = both bits true, 0x10 = first bit true, second false
    }
    
    // For packed format, each byte contains 2 bits: 0x10 (even index), 0x01 (odd index)
    match PacketParser.parseBatchReadBits response 4 with
    | Ok bits ->
        assertEqual 4 bits.Length
        assertTrue bits.[0] "First bit should be true."   // 0x11 & 0x10 = 0x10 != 0
        assertTrue bits.[1] "Second bit should be true."  // 0x11 & 0x01 = 0x01 != 0
        assertTrue bits.[2] "Third bit should be true."   // 0x10 & 0x10 = 0x10 != 0
        assertFalse bits.[3] "Fourth bit should be false." // 0x10 & 0x01 = 0x00 = 0
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``parseRandomRead correctly parses multiple device values`` () =
    log "Parsing random read result."
    let devices = [|
        { DeviceCode = DeviceCode.D; DeviceNumber = 100; AccessSize = 0uy }
        { DeviceCode = DeviceCode.W; DeviceNumber = 50; AccessSize = 0uy }
    |]
    
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x34uy; 0x12uy; 0x78uy; 0x56uy |]
    }
    
    match PacketParser.parseRandomRead response devices with
    | Ok values ->
        assertEqual 2 values.Length
        assertEqual 0x1234us values.[0].[0]
        assertEqual 0x5678us values.[1].[0]
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``parseRandomRead handles dword access size`` () =
    log "Parsing random read for dword size."
    let devices = [|
        { DeviceCode = DeviceCode.D; DeviceNumber = 100; AccessSize = 1uy } // DWORD
    |]
    
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x34uy; 0x12uy; 0x78uy; 0x56uy |]
    }
    
    match PacketParser.parseRandomRead response devices with
    | Ok values ->
        assertEqual 1 values.Length  // Only one device
        assertEqual 2 values.[0].Length  // DWORD = 2 words
        assertEqual 0x1234us values.[0].[0]  // Low word
        assertEqual 0x5678us values.[0].[1]  // High word
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``parseCpuType correctly parses CPU information`` () =
    log "Parsing CPU type info."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = 
            System.Text.Encoding.ASCII.GetBytes("Q04UDVCPU       ")
    }
    
    match PacketParser.parseCpuType response with
    | Ok cpuType ->
        assertContains "Q04UDVCPU" cpuType
    | Error msg ->
        failWithLogs ($"Parse failed: {msg}")

[<Fact>]
let ``getErrorDescription returns appropriate error messages`` () =
    log "Validating error description mapping."
    let desc1 = PacketParser.getErrorDescription 0xC050us  // Use actual error codes from implementation
    assertContains "command" (desc1.ToLower())
    
    let desc2 = PacketParser.getErrorDescription 0xC051us
    assertContains "device" (desc2.ToLower())
    
    let desc3 = PacketParser.getErrorDescription 0xC052us
    assertContains "device" (desc3.ToLower())
    
    let desc4 = PacketParser.getErrorDescription 0xC055us
    assertContains "length" (desc4.ToLower())
    
    let desc5 = PacketParser.getErrorDescription 0xC056us
    assertContains "count" (desc5.ToLower())
    
    let desc6 = PacketParser.getErrorDescription 0xC053us
    assertContains "written" (desc6.ToLower())
    
    let descUnknown = PacketParser.getErrorDescription 0xFFFFus
    assertContains "Unknown" descUnknown

[<Fact>]
let ``parseBatchReadWords handles insufficient data`` () =
    log "Validating insufficient data handling for words."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x34uy; 0x12uy |] // Only 1 word worth of data
    }
    
    match PacketParser.parseBatchReadWords response 3 with
    | Error msg ->
        assertContains "expected" (msg.ToLower())  // The actual error message contains "Expected"
    | Ok _ ->
        failWithLogs "Should have detected insufficient data"

[<Fact>]
let ``parseBatchReadBits handles insufficient data`` () =
    log "Validating insufficient data handling for bits."
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [| 0x01uy |] // Only 1 byte for 5 bits (need 3 bytes: (5+1)/2 = 3)
    }
    
    match PacketParser.parseBatchReadBits response 5 with
    | Error msg ->
        assertContains "insufficient" (msg.ToLower())
    | Ok _ ->
        failWithLogs "Should have detected insufficient data"

[<Fact>]
let ``parseRandomRead handles empty device list`` () =
    log "Validating empty device list handling."
    let devices = [||]
    let response = {
        EndCode = EndCodeSuccess 0x0000us
        Data = [||]
    }
    
    match PacketParser.parseRandomRead response devices with
    | Ok values ->
        assertEmpty values "Expected empty result when device list is empty."
    | Error msg ->
        failWithLogs ($"Should handle empty device list: {msg}")
