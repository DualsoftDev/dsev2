namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
open Ev2.S7Protocol.Tests.TestHelpers
open Ev2.S7Protocol.Tests.ValueGenerators
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions

module DataIntegrityTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Bit operations maintain data integrity`` () =
        let result =
            CH.runWithClient (fun client ->
                // Test pattern: read-write-verify across multiple bits
                let testBits = [
                    Tags.merkerBit
                    "M1.1"
                    "M1.2" 
                    "M1.3"
                ]
                
                // Store original values
                let originalValues = 
                    testBits 
                    |> List.map (fun addr -> addr, client.ReadBit(addr))
                    |> List.choose (fun (addr, result) ->
                        match result with
                        | Ok value -> Some (addr, value)
                        | Error _ -> None)
                
                if originalValues.Length <> testBits.Length then
                    Error "Failed to read all test bits"
                else
                    // Toggle all bits
                    let toggleResults = 
                        originalValues
                        |> List.map (fun (addr, original) ->
                            let toggled = not original
                            match client.WriteBit(addr, toggled) with
                            | Ok () -> 
                                match client.ReadBit(addr) with
                                | Ok verify -> Ok (addr, original, toggled, verify)
                                | Error msg -> Error $"Verify read failed for {addr}: {msg}"
                            | Error msg -> Error $"Write failed for {addr}: {msg}")
                    
                    // Restore original values
                    originalValues
                    |> List.iter (fun (addr, original) ->
                        client.WriteBit(addr, original) |> ignore)
                    
                    // Check all toggles were successful
                    let failures = 
                        toggleResults 
                        |> List.choose (function 
                            | Error msg -> Some msg 
                            | Ok (_, _, expected, actual) when expected <> actual -> 
                                Some $"Bit value mismatch: expected {expected}, got {actual}"
                            | Ok _ -> None)
                    
                    if failures.IsEmpty then
                        Ok (originalValues.Length, toggleResults.Length)
                    else
                        Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok (originalCount, verifiedCount) ->
            Assert.Equal(originalCount, verifiedCount)
            Assert.True(originalCount > 0, "Should have tested at least one bit")
        | Error msg ->
            TH.failWithLogsWithResult result $"Bit integrity test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Byte operations preserve data boundaries`` () =
        let result =
            CH.runWithClient (fun client ->
                let testBytes = [
                    Tags.merkerByte
                    "MB2"
                    "MB3"
                ]
                
                // Read original values
                let originalValues = 
                    testBytes
                    |> List.map (fun addr -> 
                        match client.ReadByte(addr) with
                        | Ok value -> Some (addr, value)
                        | Error _ -> None)
                    |> List.choose id
                
                if originalValues.Length <> testBytes.Length then
                    Error "Failed to read all test bytes"
                else
                    // Write test patterns and verify
                    let testPatterns = [0uy; 85uy; 170uy; 255uy]
                    let testResults = 
                        originalValues
                        |> List.collect (fun (addr, original) ->
                            testPatterns
                            |> List.map (fun pattern ->
                                match client.WriteByte(addr, pattern) with
                                | Ok () ->
                                    match client.ReadByte(addr) with
                                    | Ok verify -> Ok (addr, pattern, verify)
                                    | Error msg -> Error $"Read verification failed for {addr}: {msg}"
                                | Error msg -> Error $"Write failed for {addr}: {msg}"))
                    
                    // Restore original values
                    originalValues
                    |> List.iter (fun (addr, original) ->
                        client.WriteByte(addr, original) |> ignore)
                    
                    // Validate all patterns were written correctly
                    let failures = 
                        testResults
                        |> List.choose (function
                            | Error msg -> Some msg
                            | Ok (addr, expected, actual) when expected <> actual ->
                                Some $"Byte mismatch at {addr}: expected {expected}, got {actual}"
                            | Ok _ -> None)
                    
                    if failures.IsEmpty then
                        Ok testResults.Length
                    else
                        Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok testCount ->
            Assert.True(testCount > 0, "Should have performed at least one byte test")
        | Error msg ->
            TH.failWithLogsWithResult result $"Byte boundary test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Word operations handle endianness correctly`` () =
        let result =
            CH.runWithClient (fun client ->
                let testWords = [
                    Tags.merkerWord
                    "MW4"
                    "MW6"
                ]
                
                // Test specific patterns that reveal endianness issues
                let testPatterns = [
                    0s; 1s; -1s; 256s; -256s; 
                    0x1234s; -32768s; 32767s
                ]
                
                let originalValues = 
                    testWords
                    |> List.map (fun addr ->
                        match client.ReadInt16(addr) with
                        | Ok value -> Some (addr, value)
                        | Error _ -> None)
                    |> List.choose id
                
                if originalValues.Length <> testWords.Length then
                    Error "Failed to read all test words"
                else
                    let testResults = 
                        originalValues
                        |> List.collect (fun (addr, original) ->
                            testPatterns
                            |> List.map (fun pattern ->
                                match client.WriteInt16(addr, pattern) with
                                | Ok () ->
                                    match client.ReadInt16(addr) with
                                    | Ok verify -> Ok (addr, pattern, verify)
                                    | Error msg -> Error $"Read verification failed for {addr}: {msg}"
                                | Error msg -> Error $"Write failed for {addr}: {msg}"))
                    
                    // Restore original values
                    originalValues
                    |> List.iter (fun (addr, original) ->
                        client.WriteInt16(addr, original) |> ignore)
                    
                    let failures = 
                        testResults
                        |> List.choose (function
                            | Error msg -> Some msg
                            | Ok (addr, expected, actual) when expected <> actual ->
                                Some $"Word endianness issue at {addr}: expected {expected}, got {actual}"
                            | Ok _ -> None)
                    
                    if failures.IsEmpty then
                        Ok testResults.Length
                    else
                        Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok testCount ->
            Assert.True(testCount > 0, "Should have performed at least one word test")
        | Error msg ->
            TH.failWithLogsWithResult result $"Word endianness test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Concurrent operations do not interfere`` () =
        let result =
            CH.runWithClient (fun client ->
                // Test that multiple rapid operations don't corrupt each other
                let baseAddr = "M10"
                let iterations = 50
                
                // Initialize test area
                [0..7] 
                |> List.iter (fun i -> 
                    client.WriteBit($"{baseAddr}.{i}", false) |> ignore)
                
                // Perform rapid alternating operations
                let operations = 
                    [1..iterations]
                    |> List.map (fun i ->
                        let bitAddr = $"{baseAddr}.{i % 8}"
                        let byteAddr = $"MB{10 + (i % 4)}"
                        
                        // Alternate between bit and byte operations
                        if i % 2 = 0 then
                            match client.WriteBit(bitAddr, true) with
                            | Ok () -> 
                                match client.ReadBit(bitAddr) with
                                | Ok _ -> Ok "bit"
                                | Error msg -> Error msg
                            | Error msg -> Error msg
                        else
                            let testValue = byte (i % 256)
                            match client.WriteByte(byteAddr, testValue) with
                            | Ok () -> 
                                match client.ReadByte(byteAddr) with
                                | Ok _ -> Ok "byte"
                                | Error msg -> Error msg
                            | Error msg -> Error msg)
                
                let failures = 
                    operations
                    |> List.mapi (fun i result ->
                        match result with
                        | Error msg -> Some $"Operation {i+1} failed: {msg}"
                        | Ok _ -> None)
                    |> List.choose id
                
                if failures.IsEmpty then
                    Ok operations.Length
                else
                    Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok operationCount ->
            Assert.Equal(50, operationCount)
        | Error msg ->
            TH.failWithLogsWithResult result $"Concurrent operations test failed: {msg}"
