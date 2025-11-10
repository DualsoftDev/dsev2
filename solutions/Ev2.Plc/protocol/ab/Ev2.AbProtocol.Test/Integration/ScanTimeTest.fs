module Ev2.AbProtocol.Test.Integration.ScanTimeTest

open System
open Xunit
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Test
open Ev2.AbProtocol.Test.TagFixtures

/// Test reading PLC scan time from Allen-Bradley PLC
/// Scan time is typically available in controller-scoped tags or GSV instructions
[<IntegrationFact>]
let ``Read current PLC scan time`` () =
    let connectionError (error: AbProtocolError) = $"Connection failed: {error.Message}"
    
    // Define scan time related tags
    // These are common tag names used in Allen-Bradley PLCs
    let scanTimeTags = [
        // Controller-scoped tags (if configured)
        { Name = "LastScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "MaxScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "MinScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "AverageScanTime"; DataType = DataType.REAL; ElementCount = 1 }
        
        // Alternative common names
        { Name = "ScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "CycleTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "ProgramScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        
        // Task-related tags (for multi-tasking PLCs)
        { Name = "MainTask.LastScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "MainTask.MaxScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        
        // System status tags
        { Name = "Controller.LastScanTime"; DataType = DataType.DINT; ElementCount = 1 }
        { Name = "PROGRAM:MainProgram.LastScanTime"; DataType = DataType.DINT; ElementCount = 1 }
    ]
    
    // Additional system tags to check PLC status
    let statusTags = [
        { Name = "Controller.OK"; DataType = DataType.BOOL; ElementCount = 1 }
        { Name = "Controller.MajorFault"; DataType = DataType.BOOL; ElementCount = 1 }
        { Name = "Controller.MinorFault"; DataType = DataType.BOOL; ElementCount = 1 }
        { Name = "S:FS"; DataType = DataType.BOOL; ElementCount = 1 }  // First scan flag
        { Name = "S:N"; DataType = DataType.BOOL; ElementCount = 1 }   // Clock flag
    ]
    
    let result = ClientHarness.runWithClient (fun client ->
        TestHelpers.log "========== Allen-Bradley PLC Scan Time =========="
        
        TestHelpers.log "--- Scan Time Tags ---"
        let mutable foundScanTime = false
        
        for tag in scanTimeTags do
            try
                let (status, payload) = ClientHarness.readTag client tag
                match status with
                | NoError ->
                    match payload with
                    | Some data ->
                        foundScanTime <- true
                        match tag.DataType with
                        | DataType.DINT ->
                            // DINT values are typically in microseconds
                            if data :? byte[] then
                                let bytes = data :?> byte[]
                                if bytes.Length >= 4 then
                                    let microSeconds = BitConverter.ToInt32(bytes, 0)
                                    let milliSeconds = float microSeconds / 1000.0
                                    TestHelpers.log $"{tag.Name}: {milliSeconds:F2} ms ({microSeconds} μs)"
                                else
                                    TestHelpers.log $"{tag.Name}: Invalid data length"
                            else
                                TestHelpers.log $"{tag.Name}: Data received"
                        | DataType.REAL ->
                            // REAL values might be in milliseconds
                            if data :? byte[] then
                                let bytes = data :?> byte[]
                                if bytes.Length >= 4 then
                                    let scanTime = BitConverter.ToSingle(bytes, 0)
                                    TestHelpers.log $"{tag.Name}: {scanTime:F2} ms"
                                else
                                    TestHelpers.log $"{tag.Name}: Invalid data length"
                            else
                                TestHelpers.log $"{tag.Name}: Data received"
                        | _ ->
                            TestHelpers.log $"{tag.Name}: Read OK"
                    | None ->
                        TestHelpers.log $"{tag.Name}: No data"
                | error ->
                    // Tag doesn't exist - this is normal, not all PLCs have all tags
                    if not (error.Message.Contains("does not exist") || error.Message.Contains("not found")) then
                        TestHelpers.log $"{tag.Name}: Error - {error.Message}"
            with ex ->
                TestHelpers.log $"{tag.Name}: Exception - {ex.Message}"
        
        if not foundScanTime then
            TestHelpers.log "No standard scan time tags found - may need GSV instruction in PLC program"
        
        TestHelpers.log "\n--- System Status Tags ---"
        
        for tag in statusTags do
            try
                let (status, payload) = ClientHarness.readTag client tag
                match status with
                | NoError ->
                    match payload with
                    | Some data ->
                        if tag.DataType = DataType.BOOL && data :? byte[] then
                            let bytes = data :?> byte[]
                            if bytes.Length > 0 then
                                let value = bytes.[0] <> 0uy
                                let statusText = if value then "TRUE" else "FALSE"
                                TestHelpers.log $"{tag.Name}: {statusText}"
                            else
                                TestHelpers.log $"{tag.Name}: Read OK"
                        else
                            TestHelpers.log $"{tag.Name}: Read OK"
                    | None ->
                        TestHelpers.log $"{tag.Name}: No data"
                | error ->
                    if not (error.Message.Contains("does not exist") || error.Message.Contains("not found")) then
                        TestHelpers.log $"{tag.Name}: {error.Message}"
            with ex ->
                TestHelpers.log $"{tag.Name}: Exception - {ex.Message}"
        
        TestHelpers.log "\n--- PLC Information ---"
        
        // Try to read some common system information tags
        let infoTags = [
            { Name = "Local:1:I.Ch0Data"; DataType = DataType.INT; ElementCount = 1 }  // Input module data
            { Name = "Local:2:O.Ch0Data"; DataType = DataType.INT; ElementCount = 1 }  // Output module data
            { Name = "PROGRAM:MainProgram"; DataType = DataType.DINT; ElementCount = 1 }
        ]
        
        for tag in infoTags do
            try
                let (status, _) = ClientHarness.readTag client tag
                match status with
                | NoError -> TestHelpers.log $"{tag.Name}: Available"
                | _ -> () // Silent - these are optional
            with _ -> ()
        
        TestHelpers.log "=============================================="
        
        true // Test passes if we can connect and attempt reads
    )
    
    let success = ClientHarness.unwrap connectionError result
    Assert.True(success, "Scan time test completed")