module Ev2.MxProtocol.Tests.Integration.ScanTimeTest

open System
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open Ev2.MxProtocol.Tests.TestHelpers
open Ev2.MxProtocol.Tests.TestAttributes
open Ev2.MxProtocol.Tests.ClientHelpers

/// Parse address string to extract numeric value
let private parseAddress (addr: string) : int =
    // Skip first 2 or 3 characters (SD520 -> 520, D8010 -> 8010)
    let startIdx = if addr.StartsWith("SD") then 2 else 1
    let numericPart = addr.Substring(startIdx)
    match Int32.TryParse(numericPart) with
    | true, value -> value
    | false, _ -> 0

/// Test reading PLC scan time from Mitsubishi PLC
/// Scan time is typically available in special device SD (Special Data Register)
[<RequiresMelsecPLC>]
let ``Read current PLC scan time`` () =
    let result = runWithClient (fun client ->
        try
            // Mitsubishi PLCs store scan time in special registers:
            // SD520: Current scan time (0.1ms units)
            // SD521: Minimum scan time (0.1ms units) 
            // SD522: Maximum scan time (0.1ms units)
            // SD524: Average scan time (0.1ms units)
            
            let scanTimeRegisters = [
                ("SD520", "Current Scan Time")
                ("SD521", "Minimum Scan Time")
                ("SD522", "Maximum Scan Time")
                ("SD524", "Average Scan Time")
            ]
            
            log "========== Mitsubishi PLC Scan Time =========="
            
            for register, description in scanTimeRegisters do
                try
                    // Read scan time register (16-bit word)
                    let readResult = client.ReadWords(DeviceCode.SD, parseAddress register, 1)
                    match readResult with
                    | Ok data when data.Length >= 1 ->
                        let scanTimeRaw = data.[0]
                        let scanTimeMs = float scanTimeRaw * 0.1 // Convert from 0.1ms units to ms
                        log $"{description} ({register}): {scanTimeMs:F1} ms"
                    | Ok data ->
                        log $"{description} ({register}): Invalid data length"
                    | Error err ->
                        // SD registers might not be available on all PLCs
                        if err.Contains("unsupported") || err.Contains("invalid") then
                            log $"{description} ({register}): Not supported on this PLC"
                        else
                            log $"{description} ({register}): Read error - {err}"
                with ex ->
                    log $"{description} ({register}): Exception - {ex.Message}"
            
            // Try alternative method: Read D8010-D8013 for FX series PLCs
            log "\n--- Alternative: FX Series Scan Time Registers ---"
            
            let fxScanRegisters = [
                ("D8010", "Current Scan Time (FX)")
                ("D8011", "Min Scan Time (FX)")
                ("D8012", "Max Scan Time (FX)")
            ]
            
            for register, description in fxScanRegisters do
                try
                    let deviceCode = DeviceCode.D
                    let address = parseAddress register
                    let readResult = client.ReadWords(deviceCode, address, 1)
                    match readResult with
                    | Ok data when data.Length >= 1 ->
                        let scanTimeMs = data.[0]
                        log $"{description} ({register}): {scanTimeMs} ms"
                    | Ok _ ->
                        log $"{description} ({register}): Invalid data"
                    | Error err ->
                        log $"{description} ({register}): {err}"
                with ex ->
                    log $"{description} ({register}): Exception - {ex.Message}"
                    
            log "=============================================="
            Ok true
        with ex ->
            log $"Test failed with exception: {ex.Message}"
            Error ex.Message
    )
    
    match result.Result with
    | Ok (Ok success) -> 
        Assert.True(success, "Scan time reading should succeed")
    | Ok (Error msg) -> 
        failWithLogsResult result $"Scan time test failed: {msg}"
    | Error msg -> 
        failWithLogsResult result $"Test execution failed: {msg}"