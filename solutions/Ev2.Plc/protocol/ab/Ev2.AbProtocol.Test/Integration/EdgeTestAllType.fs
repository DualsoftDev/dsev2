module Ev2.AbProtocol.Test.Integration.EdgeTestAllType

open System
open Xunit
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Test
open Ev2.AbProtocol.Test.TagFixtures

/// Edge case tests for Allen-Bradley tags based on actual PLC tag list
/// Tests real tags exported from RSLogix 5000 v33.00
[<IntegrationFact>]
let ``Test all tag types at edge cases`` () =
    let connectionError (error: AbProtocolError) = $"Connection failed: {error.Message}"
    
    // Define simplified test tags for edge case testing
    // Using subset of real tags for faster execution
    let testTags = [
        // === BOOL Tags ===
        { Name = "Alarm_Active"; DataType = DataType.BOOL; ElementCount = 1 }
        { Name = "Motor_Enable"; DataType = DataType.BOOL; ElementCount = 1 }
        { Name = "BitArr"; DataType = DataType.BOOL; ElementCount = 1024 }
        
        // === INT Tags (16-bit Integer) ===
        { Name = "Counter1"; DataType = DataType.INT; ElementCount = 1 }
        { Name = "Analog_Input"; DataType = DataType.INT; ElementCount = 300 }
        
        // === DINT Tags (32-bit Integer) ===
        { Name = "Error_Code"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "Total_Count"; DataType = DataType.DINT; ElementCount = 1 }
        
        // === REAL Tags (32-bit Float) ===
        { Name = "Flow_Rate"; DataType = DataType.REAL; ElementCount = 1 }
        { Name = "Weight_KG"; DataType = DataType.REAL; ElementCount = 1 }
    ]
    
    let result = ClientHarness.runWithClient (fun client ->
        let mutable successCount = 0
        let mutable totalCount = 0
        
        for tag in testTags do
            totalCount <- totalCount + 1
            try
                // Test tag read using ClientHarness
                let (status, payload) = ClientHarness.readTag client tag
                match status with
                | NoError ->
                    match payload with
                    | Some data ->
                        TestHelpers.log $"✓ {tag.Name}: Read OK (Type: {tag.DataType})"
                        successCount <- successCount + 1
                    | None ->
                        TestHelpers.log $"✗ {tag.Name}: No data returned"
                | error ->
                    if error.Message.Contains("does not exist") || error.Message.Contains("not found") then
                        TestHelpers.log $"- {tag.Name}: Tag doesn't exist on PLC"
                    else
                        TestHelpers.log $"✗ {tag.Name}: Read failed - {error.Message}"
            with ex ->
                TestHelpers.log $"✗ {tag.Name}: Exception - {ex.Message}"
        
        let successRate = (float successCount / float totalCount * 100.0).ToString("F1")
        TestHelpers.log "\n========== Allen-Bradley Edge Test Summary =========="
        TestHelpers.log $"Total tests: {totalCount}"
        TestHelpers.log $"Successful: {successCount}"
        TestHelpers.log ("Success rate: " + successRate + "%")
        TestHelpers.log "====================================================="
        
        // Return result
        (successCount, totalCount)
    )
    
    let (successCount, totalCount) = ClientHarness.unwrap connectionError result
    Assert.True(successCount > 0, $"At least some edge tests should pass. Success: {successCount}/{totalCount}")