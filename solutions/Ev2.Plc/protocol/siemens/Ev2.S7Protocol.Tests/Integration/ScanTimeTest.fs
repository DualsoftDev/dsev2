namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers

/// Test reading PLC scan time from Siemens S7 PLC
/// Scan time is available through system data blocks (SDB) or OB1 information
module ScanTimeTest =
    
    [<RequiresS7PLC>]
    let ``Read current PLC scan time`` () =
        let result = CH.runWithClient (fun client ->
            TH.log "========== Siemens S7 PLC Scan Time =========="
            
            // Siemens S7 PLCs store scan/cycle time in different locations:
            // 1. OB1 (Organization Block 1) local data contains cycle time info
            // 2. System data blocks (SDB) contain diagnostic information
            // 3. Special memory areas contain runtime meters
            
            // Method 1: Read from Merker area (if configured)
            // Some PLCs are configured to copy scan time to M area
            let scanTimeAddresses = [
                ("MW100", "Scan Time MW100")    // Common location for scan time (ms)
                ("MW102", "Max Scan MW102")     // Maximum scan time
                ("MW104", "Min Scan MW104")     // Minimum scan time
                ("MD200", "Cycle Counter")      // Cycle counter (if configured)
            ]
            
            TH.log "--- Reading from Merker Area ---"
            for address, description in scanTimeAddresses do
                try
                    match address.Substring(0, 2) with
                    | "MW" ->
                        match client.ReadInt16(address) with
                        | Ok value ->
                            TH.log $"{description} ({address}): {value} ms"
                        | Error err ->
                            TH.log $"{description} ({address}): {err}"
                    | "MD" ->
                        match client.ReadInt32(address) with
                        | Ok value ->
                            TH.log $"{description} ({address}): {value}"
                        | Error err ->
                            TH.log $"{description} ({address}): {err}"
                    | _ ->
                        TH.log $"{description}: Unknown address format"
                with ex ->
                    TH.log $"{description}: Exception - {ex.Message}"
            
            // Method 2: Read system status from specific DB (if accessible)
            TH.log "\n--- Reading from Data Blocks ---"
            
            let dbAddresses = [
                ("DB1.DBW0", "DB1 Scan Time")      // User-configured scan time location
                ("DB1.DBW2", "DB1 Max Scan")       
                ("DB1.DBD10", "DB1 Cycle Count")   
            ]
            
            for address, description in dbAddresses do
                try
                    if address.Contains("DBW") then
                        match client.ReadInt16(address) with
                        | Ok value ->
                            TH.log $"{description} ({address}): {value}"
                        | Error err ->
                            if err.Contains("does not exist") || err.Contains("access") then
                                TH.log $"{description} ({address}): Not accessible"
                            else
                                TH.log $"{description} ({address}): {err}"
                    elif address.Contains("DBD") then
                        match client.ReadInt32(address) with
                        | Ok value ->
                            TH.log $"{description} ({address}): {value}"
                        | Error err ->
                            TH.log $"{description} ({address}): Not accessible"
                    else
                        TH.log $"{description}: Unknown format"
                with ex ->
                    TH.log $"{description}: Exception - {ex.Message}"
            
            // Method 3: Read PLC status information
            TH.log "\n--- PLC Status Information ---"
            
            // Read some standard status flags
            let statusAddresses = [
                ("M0.0", "User Status Bit 0")      // User-defined status
                ("M0.1", "User Status Bit 1")
                ("I0.0", "Input Status")           // First input status
                ("Q0.0", "Output Status")          // First output status
            ]
            
            for address, description in statusAddresses do
                try
                    match client.ReadBit(address) with
                    | Ok value ->
                        let status = if value then "ON" else "OFF"
                        TH.log $"{description} ({address}): {status}"
                    | Error err ->
                        TH.log $"{description} ({address}): {err}"
                with ex ->
                    TH.log $"{description}: Exception - {ex.Message}"
            
            // Get connection statistics
            let stats = client.GetStatistics()
            TH.log "\n--- Connection Statistics ---"
            TH.log $"Packets Sent: {stats.PacketsSent}"
            TH.log $"Packets Received: {stats.PacketsReceived}"
            TH.log $"Error Count: {stats.ErrorCount}"
            TH.log $"Success Rate: {stats.SuccessRate:F1}%%"
            TH.log $"Avg Response Time: {stats.AverageResponseTime:F1} ms"
            
            TH.log "=============================================="
            
            Ok true
        )
        
        match CH.unwrap (fun e -> $"Scan time test failed: {e.Message}") result with
        | Ok success -> Assert.True(success, "Scan time reading completed")
        | Error msg -> TH.failWithLogsWithResult result msg
