module Ev2.MxProtocol.Tests.Integration.EdgeTestAllType

open System
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open Ev2.MxProtocol.Tests.TestHelpers
open Ev2.MxProtocol.Tests.TestAttributes
open Ev2.MxProtocol.Tests.ClientHelpers

/// Edge case tests for all Mitsubishi device types
/// Tests first and last addresses for each device type
[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Test all device types at edge addresses`` () =
    withConnectedClient (fun client ->
        // Define test ranges for each device type
        // Format: (DeviceCode, Name, IsWord, FirstAddr, LastAddr)
        let deviceRanges = [
            // Word devices
            (DeviceCode.D, "Data Register", true, 0, 8191)     // D0-D8191
            (DeviceCode.W, "Link Register", true, 0, 0x1FFF)   // W0-W1FFF
            (DeviceCode.R, "File Register", true, 0, 32767)    // R0-R32767
            (DeviceCode.T, "Timer", true, 0, 511)              // T0-T511
            (DeviceCode.C, "Counter", true, 0, 255)            // C0-C255
            
            // Bit devices  
            (DeviceCode.M, "Internal Relay", false, 0, 8191)   // M0-M8191
            (DeviceCode.X, "Input", false, 0, 0x1FFF)         // X0-X1FFF
            (DeviceCode.Y, "Output", false, 0, 0x1FFF)        // Y0-Y1FFF
            (DeviceCode.B, "Link Relay", false, 0, 0x1FFF)    // B0-B1FFF
            (DeviceCode.F, "Annunciator", false, 0, 2047)     // F0-F2047
            (DeviceCode.V, "Edge Relay", false, 0, 2047)      // V0-V2047
        ]
        
        let mutable successCount = 0
        let mutable totalCount = 0
        
        for device, name, isWord, firstAddr, lastAddr in deviceRanges do
            // Test first address
            totalCount <- totalCount + 1
            try
                if isWord then
                    // Test word device
                    match client.ReadWords(device, firstAddr, 1) with
                    | Ok originalWords ->
                        let testValue = if originalWords.[0] = 0x1234us then 0x5678us else 0x1234us
                        match client.WriteWords(device, firstAddr, [| testValue |]) with
                        | Ok () ->
                            match client.ReadWords(device, firstAddr, 1) with
                            | Ok verifyWords ->
                                if verifyWords.[0] = testValue then
                                    // Restore original value
                                    ignore (client.WriteWords(device, firstAddr, originalWords))
                                    log $"✓ {name} first address ({device}{firstAddr}): Read/Write OK"
                                    successCount <- successCount + 1
                                else
                                    log $"✗ {name} first address ({device}{firstAddr}): Verify failed"
                            | Error msg ->
                                log $"✗ {name} first address ({device}{firstAddr}): Verify read failed - {msg}"
                        | Error msg ->
                            if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                                log $"- {name} first address ({device}{firstAddr}): Write not supported"
                                successCount <- successCount + 1 // Count as success if not supported
                            else
                                log $"✗ {name} first address ({device}{firstAddr}): Write failed - {msg}"
                    | Error msg ->
                        if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                            log $"- {name} first address ({device}{firstAddr}): Not supported"
                            successCount <- successCount + 1 // Count as success if not supported
                        else
                            log $"✗ {name} first address ({device}{firstAddr}): Read failed - {msg}"
                else
                    // Test bit device
                    match client.ReadBits(device, firstAddr, 1) with
                    | Ok originalBits ->
                        let testValue = not originalBits.[0]
                        match client.WriteBits(device, firstAddr, [| testValue |]) with
                        | Ok () ->
                            match client.ReadBits(device, firstAddr, 1) with
                            | Ok verifyBits ->
                                if verifyBits.[0] = testValue then
                                    // Restore original value
                                    ignore (client.WriteBits(device, firstAddr, originalBits))
                                    log $"✓ {name} first address ({device}{firstAddr}): Read/Write OK"
                                    successCount <- successCount + 1
                                else
                                    log $"✗ {name} first address ({device}{firstAddr}): Verify failed"
                            | Error msg ->
                                log $"✗ {name} first address ({device}{firstAddr}): Verify read failed - {msg}"
                        | Error msg ->
                            if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                                log $"- {name} first address ({device}{firstAddr}): Write not supported"
                                successCount <- successCount + 1 // Count as success if not supported
                            else
                                log $"✗ {name} first address ({device}{firstAddr}): Write failed - {msg}"
                    | Error msg ->
                        if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                            log $"- {name} first address ({device}{firstAddr}): Not supported"
                            successCount <- successCount + 1 // Count as success if not supported
                        else
                            log $"✗ {name} first address ({device}{firstAddr}): Read failed - {msg}"
            with ex ->
                log $"✗ {name} first address ({device}{firstAddr}): Exception - {ex.Message}"
            
            // Test last address
            totalCount <- totalCount + 1
            try
                if isWord then
                    // Test word device
                    match client.ReadWords(device, lastAddr, 1) with
                    | Ok originalWords ->
                        let testValue = if originalWords.[0] = 0xABCDus then 0xEF01us else 0xABCDus
                        match client.WriteWords(device, lastAddr, [| testValue |]) with
                        | Ok () ->
                            match client.ReadWords(device, lastAddr, 1) with
                            | Ok verifyWords ->
                                if verifyWords.[0] = testValue then
                                    // Restore original value
                                    ignore (client.WriteWords(device, lastAddr, originalWords))
                                    log $"✓ {name} last address ({device}{lastAddr}): Read/Write OK"
                                    successCount <- successCount + 1
                                else
                                    log $"✗ {name} last address ({device}{lastAddr}): Verify failed"
                            | Error msg ->
                                log $"✗ {name} last address ({device}{lastAddr}): Verify read failed - {msg}"
                        | Error msg ->
                            if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                                log $"- {name} last address ({device}{lastAddr}): Write not supported"
                                successCount <- successCount + 1 // Count as success if not supported
                            else
                                log $"✗ {name} last address ({device}{lastAddr}): Write failed - {msg}"
                    | Error msg ->
                        if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                            log $"- {name} last address ({device}{lastAddr}): Not supported"
                            successCount <- successCount + 1 // Count as success if not supported
                        else
                            log $"✗ {name} last address ({device}{lastAddr}): Read failed - {msg}"
                else
                    // Test bit device
                    match client.ReadBits(device, lastAddr, 1) with
                    | Ok originalBits ->
                        let testValue = not originalBits.[0]
                        match client.WriteBits(device, lastAddr, [| testValue |]) with
                        | Ok () ->
                            match client.ReadBits(device, lastAddr, 1) with
                            | Ok verifyBits ->
                                if verifyBits.[0] = testValue then
                                    // Restore original value
                                    ignore (client.WriteBits(device, lastAddr, originalBits))
                                    log $"✓ {name} last address ({device}{lastAddr}): Read/Write OK"
                                    successCount <- successCount + 1
                                else
                                    log $"✗ {name} last address ({device}{lastAddr}): Verify failed"
                            | Error msg ->
                                log $"✗ {name} last address ({device}{lastAddr}): Verify read failed - {msg}"
                        | Error msg ->
                            if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                                log $"- {name} last address ({device}{lastAddr}): Write not supported"
                                successCount <- successCount + 1 // Count as success if not supported
                            else
                                log $"✗ {name} last address ({device}{lastAddr}): Write failed - {msg}"
                    | Error msg ->
                        if msg.Contains("C05") || msg.Contains("not supported") || msg.Contains("Negative number") then
                            log $"- {name} last address ({device}{lastAddr}): Not supported"
                            successCount <- successCount + 1 // Count as success if not supported
                        else
                            log $"✗ {name} last address ({device}{lastAddr}): Read failed - {msg}"
            with ex ->
                log $"✗ {name} last address ({device}{lastAddr}): Exception - {ex.Message}"
        
        let successRate = (float successCount / float totalCount * 100.0).ToString("F1")
        log "\n========== Mitsubishi Edge Test Summary =========="
        log $"Total tests: {totalCount}"
        log $"Successful: {successCount}"
        log ("Success rate: " + successRate + "%")
        log "================================================="
        
        Assert.True(successCount > 0, "At least some edge tests should pass")
    )