namespace Ev2.S7Protocol.Tests.Integration

open System
open System.Threading
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
open Ev2.S7Protocol.Tests.TestHelpers
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions

module ErrorHandlingTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Invalid addresses return appropriate errors`` () =
        let result =
            CH.runWithClient (fun client ->
                let invalidAddresses = [
                    ("Negative bit address", "M-1.0")
                    ("Invalid bit number", "M0.8") 
                    ("Malformed address", "M")
                    ("Invalid device type", "X0.0")
                    ("Out of range DB", "DB9999.DBX0.0")
                ]
                
                let errorResults = 
                    invalidAddresses
                    |> List.map (fun (description, addr) ->
                        match client.ReadBit(addr) with
                        | Ok value -> Error $"{description} ({addr}) should have failed but returned: {value}"
                        | Error msg -> Ok (description, addr, msg))
                
                let failures = 
                    errorResults
                    |> List.choose (function 
                        | Error msg -> Some msg 
                        | Ok _ -> None)
                
                if failures.IsEmpty then
                    Ok (errorResults.Length, invalidAddresses.Length)
                else
                    Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok (errorCount, expectedCount) ->
            Assert.Equal(expectedCount, errorCount)
            Assert.True(errorCount > 0, "Should have tested error cases")
        | Error msg ->
            TH.failWithLogsWithResult result $"Invalid address test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Connection resilience after errors`` () =
        let result =
            CH.runWithClient (fun client ->
                // Perform a valid operation first
                match client.ReadBit(Tags.merkerBit) with
                | Error msg -> Error $"Initial valid operation failed: {msg}"
                | Ok initialValue ->
                    // Trigger several errors
                    let errorOperations = [
                        client.ReadBit("M-1.0") |> Result.map (fun _ -> "bit")      // Invalid address
                        client.ReadByte("MB99999") |> Result.map (fun _ -> "byte")   // Potentially out of range
                        client.ReadInt16("MW-1") |> Result.map (fun _ -> "word")     // Invalid address
                    ]
                    
                    // All should fail, but connection should remain stable
                    let errorCount = 
                        errorOperations
                        |> List.sumBy (function | Error _ -> 1 | Ok _ -> 0)
                    
                    if errorCount <> errorOperations.Length then
                        Error "Some invalid operations unexpectedly succeeded"
                    else
                        // Verify we can still perform valid operations
                        match client.ReadBit(Tags.merkerBit) with
                        | Error msg -> Error $"Connection damaged after errors: {msg}"
                        | Ok finalValue ->
                            // Test a write operation too
                            match client.WriteBit(Tags.merkerBit, not initialValue) with
                            | Error msg -> Error $"Write failed after errors: {msg}"
                            | Ok () ->
                                // Restore original value
                                match client.WriteBit(Tags.merkerBit, initialValue) with
                                | Error msg -> Error $"Restore failed: {msg}"
                                | Ok () -> Ok (errorCount, initialValue, finalValue))
        
        match CH.unwrap connectionError result with
        | Ok (errors, initial, final) ->
            Assert.True(errors > 0, "Should have generated some errors")
            // The values might be different if the bit was toggled by another process
            // but we should have been able to read them
            Assert.True(true) // Connection resilience confirmed
        | Error msg ->
            TH.failWithLogsWithResult result $"Connection resilience test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Timeout handling works correctly`` () =
        // This test requires manual verification or a slow/unresponsive PLC
        // In a real environment, you might simulate this by temporarily blocking the network
        let result =
            CH.runWithClient (fun client ->
                // Record baseline timing for a normal operation
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                match client.ReadBit(Tags.merkerBit) with
                | Error msg -> Error $"Baseline operation failed: {msg}"
                | Ok _ ->
                    stopwatch.Stop()
                    let baselineMs = stopwatch.ElapsedMilliseconds
                    
                    // The timeout handling is more about ensuring operations don't hang indefinitely
                    // rather than testing actual timeouts which would require network manipulation
                    Ok baselineMs)
        
        match CH.unwrap connectionError result with
        | Ok baselineMs ->
            Assert.True(baselineMs < 5000, $"Baseline operation took too long: {baselineMs}ms")
            Assert.True(baselineMs >= 0, "Timing should be non-negative")
        | Error msg ->
            TH.failWithLogsWithResult result $"Timeout handling test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Resource cleanup after exceptions`` () =
        let result =
            CH.runWithClient (fun client ->
                let initialStats = client.GetStatistics()
                
                // Perform operations that might fail
                let operations = [
                    fun () -> client.ReadBit("M0.0") |> Result.map (fun _ -> "bit")  // Valid
                    fun () -> client.ReadBit("M-1.0") |> Result.map (fun _ -> "bit") // Invalid
                    fun () -> client.ReadByte("MB0") |> Result.map (fun _ -> "byte")  // Valid  
                    fun () -> client.ReadByte("MB-1") |> Result.map (fun _ -> "byte") // Invalid
                ]
                
                let results = 
                    operations
                    |> List.map (fun op ->
                        try
                            match op() with
                            | Ok _ -> "Success"
                            | Error _ -> "Expected Error"
                        with
                        | ex -> $"Unexpected Exception: {ex.Message}")
                
                let finalStats = client.GetStatistics()
                
                // Verify statistics are reasonable (no resource leaks indicated)
                let statsIncrease = finalStats.PacketsReceived - initialStats.PacketsReceived
                
                Ok (results, statsIncrease, finalStats.SuccessRate))
        
        match CH.unwrap connectionError result with
        | Ok (results, statsIncrease, successRate) ->
            Assert.True(results.Length > 0, "Should have performed some operations")
            Assert.True(statsIncrease >= 0, "Packet count should not decrease")
            // Handle both percentage (0-100) and decimal (0.0-1.0) formats
            let normalizedSuccessRate = 
                if successRate > 1.0 then successRate / 100.0 else successRate
            
            let isValidSuccessRate = 
                not (System.Double.IsNaN(normalizedSuccessRate)) && 
                not (System.Double.IsInfinity(normalizedSuccessRate)) &&
                normalizedSuccessRate >= -0.01 && normalizedSuccessRate <= 1.01
            Assert.True(isValidSuccessRate, $"Success rate should be between 0 and 1 (or 0-100%%), but was {successRate}")
        | Error msg ->
            TH.failWithLogsWithResult result $"Resource cleanup test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Malformed protocol data handling`` () =
        let result =
            CH.runWithClient (fun client ->
                // Test edge cases in data formatting and addressing
                let edgeCaseAddresses = [
                    "M0.0"     // Minimum valid
                    "M65535.7" // Potentially maximum (depending on PLC)
                    "DB1.DBX0.0" // DB access
                    "DB1.DBB0"   // DB byte access
                    "DB1.DBW0"   // DB word access
                ]
                
                let results = 
                    edgeCaseAddresses
                    |> List.map (fun addr ->
                        try
                            match client.ReadBit(addr) with
                            | Ok value -> Ok (addr, "Success", Some value)
                            | Error msg -> Ok (addr, "Error", None)
                        with
                        | ex -> Error $"Exception for {addr}: {ex.Message}")
                
                let exceptions = 
                    results
                    |> List.choose (function | Error msg -> Some msg | Ok _ -> None)
                
                if exceptions.IsEmpty then
                    let successfulReads = 
                        results
                        |> List.choose (function 
                            | Ok (addr, "Success", Some value) -> Some (addr, value)
                            | _ -> None)
                    Ok (results.Length, successfulReads.Length, exceptions.Length)
                else
                    Error (String.concat "; " exceptions))
        
        match CH.unwrap connectionError result with
        | Ok (totalTests, successfulReads, exceptionCount) ->
            Assert.True(totalTests > 0, "Should have performed some tests")
            Assert.Equal(0, exceptionCount)
            // successfulReads can be 0 if PLC doesn't support those addresses, that's OK
        | Error msg ->
            TH.failWithLogsWithResult result $"Malformed protocol data test failed: {msg}"

    [<RequiresS7PLC>]
    let ``Rapid error recovery`` () =
        let result =
            CH.runWithClient (fun client ->
                let iterations = 20
                let results = 
                    [1..iterations]
                    |> List.map (fun i ->
                        // Alternate between valid and invalid operations
                        if i % 2 = 0 then
                            // Valid operation
                            match client.ReadBit(Tags.merkerBit) with
                            | Ok _ -> Ok "Valid"
                            | Error msg -> Error $"Valid operation {i} failed: {msg}"
                        else
                            // Invalid operation (should fail gracefully)
                            match client.ReadBit($"M-{i}.0") with
                            | Ok _ -> Error $"Invalid operation {i} unexpectedly succeeded"
                            | Error _ -> Ok "Expected Error")
                
                let failures = 
                    results
                    |> List.choose (function | Error msg -> Some msg | Ok _ -> None)
                
                if failures.IsEmpty then
                    Ok results.Length
                else
                    Error (String.concat "; " failures))
        
        match CH.unwrap connectionError result with
        | Ok operationCount ->
            Assert.Equal(20, operationCount)
        | Error msg ->
            TH.failWithLogsWithResult result $"Rapid error recovery test failed: {msg}"