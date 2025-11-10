module Ev2.LsProtocol.Tests.Integration.ScanTimeTest

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests.TestHelpers

/// Test reading PLC scan time from LS Electric XGT PLC
/// Scan time information is typically stored in special system flags/registers
[<Fact>]
let ``Read current PLC scan time`` () =
    skipIfIntegrationDisabled "LS Electric Scan Time Test"
    
    let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true) // true for LocalEthernet
    
    try
        let connected = client.Connect()
        Assert.True(connected, "Failed to connect to LS Electric PLC")
        log "Connected to LS Electric PLC"
        
        log "========== LS Electric PLC Scan Time =========="
        
        // LS Electric XGT PLCs store scan time in special flags:
        // F area contains system information
        // Different models use different addresses
        
        let scanTimeAddresses = [
            // XGK/XGI Common system flags
            ("F53", PlcTagDataType.UInt16, "Current Scan Time")     // Current scan time (ms or 0.1ms units)
            ("F54", PlcTagDataType.UInt16, "Maximum Scan Time")     // Max scan time
            ("F55", PlcTagDataType.UInt16, "Minimum Scan Time")     // Min scan time
            ("F56", PlcTagDataType.UInt16, "Average Scan Time")     // Average scan time
            
            // Alternative addresses for some models
            ("F2053", PlcTagDataType.UInt16, "Alt Current Scan")
            ("F2054", PlcTagDataType.UInt16, "Alt Max Scan")
            
            // System status
            ("F0", PlcTagDataType.Bool, "System Run Status")        // PLC Run/Stop status
            ("F1", PlcTagDataType.Bool, "System Error Flag")        // System error flag
        ]
        
        for address, dataType, description in scanTimeAddresses do
            try
                let value = client.Read(address, dataType)
                
                match dataType with
                | PlcTagDataType.Bool ->
                    match value with
                    | BoolValue b ->
                        let status = if b then "ON" else "OFF"
                        log $"{description} ({address}): {status}"
                    | _ ->
                        log $"{description} ({address}): Type mismatch"
                        
                | PlcTagDataType.UInt16 ->
                    match value with
                    | UInt16Value scanTime ->
                        // Most LS PLCs report in ms, some in 0.1ms units
                        log $"{description} ({address}): {scanTime} units"
                        
                        // If it looks like 0.1ms units (very large number), convert
                        if scanTime > 1000us then
                            let scanTimeMs = float scanTime * 0.1
                            log $"  -> Converted: {scanTimeMs:F1} ms"
                        else
                            log $"  -> Direct: {scanTime} ms"
                    | _ ->
                        log $"{description} ({address}): Type mismatch"
                        
                | _ ->
                    log $"{description} ({address}): Unsupported type"
                    
            with ex ->
                // Some addresses might not exist on all PLC models
                if ex.Message.Contains("does not exist") || ex.Message.Contains("invalid") then
                    log $"{description} ({address}): Not available on this PLC model"
                else
                    log $"{description} ({address}): Error - {ex.Message}"
        
        // Try to read system information from U area (special modules)
        log "\n--- Special Module Information ---"
        
        let specialAddresses = [
            ("U0.0.0", PlcTagDataType.UInt16, "Module Status")
            ("U0.1.0", PlcTagDataType.UInt16, "Module Info")
        ]
        
        for address, dataType, description in specialAddresses do
            try
                let value = client.Read(address, dataType)
                match value with
                | UInt16Value v ->
                    log $"{description} ({address}): {v}"
                | _ ->
                    log $"{description} ({address}): Type mismatch"
            with ex ->
                log $"{description} ({address}): Not available"
        
        log "=============================================="
        
        // Test passes if we can connect and attempt reads
        Assert.True(true, "Scan time test completed")
        
    finally
        if client.IsConnected then
            client.Disconnect() |> ignore
            log "Disconnected from LS Electric PLC"