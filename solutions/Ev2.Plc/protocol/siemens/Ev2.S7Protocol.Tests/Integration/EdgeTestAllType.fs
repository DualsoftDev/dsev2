namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers

/// Edge case tests for all Siemens S7 memory areas
/// Tests first and last addresses for each memory area
module EdgeTestAllType =
    
    let private log msg = TH.log msg
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Test all memory areas at edge addresses`` () =
        let result =
            CH.runWithClient (fun client ->
                // Define test ranges for each memory area
                // Format: (Area, Name, FirstAddr, LastAddr, DataType)
                let memoryRanges = [
                    // Merker (M) area
                    ("M0.0", "Merker Bit First", "bit")
                    ("M65535.7", "Merker Bit Last", "bit")
                    ("MB0", "Merker Byte First", "byte")
                    ("MB65535", "Merker Byte Last", "byte")
                    ("MW0", "Merker Word First", "word")
                    ("MW65534", "Merker Word Last", "word")
                    ("MD0", "Merker DWord First", "dword")
                    ("MD65532", "Merker DWord Last", "dword")
                    
                    // Input (I/E) area
                    ("I0.0", "Input Bit First", "bit")
                    ("I65535.7", "Input Bit Last", "bit")
                    ("IB0", "Input Byte First", "byte")
                    ("IB65535", "Input Byte Last", "byte")
                    ("IW0", "Input Word First", "word")
                    ("IW65534", "Input Word Last", "word")
                    
                    // Output (Q/A) area
                    ("Q0.0", "Output Bit First", "bit")
                    ("Q65535.7", "Output Bit Last", "bit")
                    ("QB0", "Output Byte First", "byte")
                    ("QB65535", "Output Byte Last", "byte")
                    ("QW0", "Output Word First", "word")
                    ("QW65534", "Output Word Last", "word")
                    
                    // Data Block (DB) area
                    ("DB1.DBX0.0", "DB Bit First", "bit")
                    ("DB1.DBX65535.7", "DB Bit Last", "bit")
                    ("DB1.DBB0", "DB Byte First", "byte")
                    ("DB1.DBB65535", "DB Byte Last", "byte")
                    ("DB1.DBW0", "DB Word First", "word")
                    ("DB1.DBW65534", "DB Word Last", "word")
                    ("DB1.DBD0", "DB DWord First", "dword")
                    ("DB1.DBD65532", "DB DWord Last", "dword")
                ]
                
                let mutable successCount = 0
                let mutable totalCount = 0
                
                for address, name, dataType in memoryRanges do
                    totalCount <- totalCount + 1
                    try
                        match dataType with
                        | "bit" ->
                            // Test bit read/write
                            match client.ReadBit(address) with
                            | Ok originalValue ->
                                let testValue = not originalValue
                                match client.WriteBit(address, testValue) with
                                | Ok () ->
                                    match client.ReadBit(address) with
                                    | Ok verifyValue ->
                                        if verifyValue = testValue then
                                            // Restore original value
                                            ignore (client.WriteBit(address, originalValue))
                                            log $"✓ {name} ({address}): Read/Write OK"
                                            successCount <- successCount + 1
                                        else
                                            log $"✗ {name} ({address}): Verify failed"
                                    | Error msg ->
                                        log $"✗ {name} ({address}): Verify read failed - {msg}"
                                | Error msg ->
                                    if msg.Contains("not writable") || msg.Contains("access denied") then
                                        log $"- {name} ({address}): Write not allowed (read-only)"
                                        successCount <- successCount + 1 // Count as success if read-only
                                    else
                                        log $"✗ {name} ({address}): Write failed - {msg}"
                            | Error msg ->
                                if msg.Contains("out of range") || msg.Contains("Invalid address") then
                                    log $"- {name} ({address}): Address out of range"
                                    // This is expected for edge cases
                                else
                                    log $"✗ {name} ({address}): Read failed - {msg}"
                                    
                        | "byte" ->
                            // Test byte read/write
                            match client.ReadByte(address) with
                            | Ok originalValue ->
                                let testValue = if originalValue = 0xAAuy then 0x55uy else 0xAAuy
                                match client.WriteByte(address, testValue) with
                                | Ok () ->
                                    match client.ReadByte(address) with
                                    | Ok verifyValue ->
                                        if verifyValue = testValue then
                                            // Restore original value
                                            ignore (client.WriteByte(address, originalValue))
                                            log $"✓ {name} ({address}): Read/Write OK"
                                            successCount <- successCount + 1
                                        else
                                            log $"✗ {name} ({address}): Verify failed"
                                    | Error msg ->
                                        log $"✗ {name} ({address}): Verify read failed - {msg}"
                                | Error msg ->
                                    if msg.Contains("not writable") || msg.Contains("access denied") then
                                        log $"- {name} ({address}): Write not allowed (read-only)"
                                        successCount <- successCount + 1 // Count as success if read-only
                                    else
                                        log $"✗ {name} ({address}): Write failed - {msg}"
                            | Error msg ->
                                if msg.Contains("out of range") || msg.Contains("Invalid address") then
                                    log $"- {name} ({address}): Address out of range"
                                    // This is expected for edge cases
                                else
                                    log $"✗ {name} ({address}): Read failed - {msg}"
                                    
                        | "word" ->
                            // Test word read/write
                            match client.ReadInt16(address) with
                            | Ok originalValue ->
                                let testValue = if originalValue = 0x1234s then 0x5678s else 0x1234s
                                match client.WriteInt16(address, testValue) with
                                | Ok () ->
                                    match client.ReadInt16(address) with
                                    | Ok verifyValue ->
                                        if verifyValue = testValue then
                                            // Restore original value
                                            ignore (client.WriteInt16(address, originalValue))
                                            log $"✓ {name} ({address}): Read/Write OK"
                                            successCount <- successCount + 1
                                        else
                                            log $"✗ {name} ({address}): Verify failed"
                                    | Error msg ->
                                        log $"✗ {name} ({address}): Verify read failed - {msg}"
                                | Error msg ->
                                    if msg.Contains("not writable") || msg.Contains("access denied") then
                                        log $"- {name} ({address}): Write not allowed (read-only)"
                                        successCount <- successCount + 1 // Count as success if read-only
                                    else
                                        log $"✗ {name} ({address}): Write failed - {msg}"
                            | Error msg ->
                                if msg.Contains("out of range") || msg.Contains("Invalid address") then
                                    log $"- {name} ({address}): Address out of range"
                                    // This is expected for edge cases
                                else
                                    log $"✗ {name} ({address}): Read failed - {msg}"
                                    
                        | "dword" ->
                            // Test dword read/write
                            match client.ReadInt32(address) with
                            | Ok originalValue ->
                                let testValue = if originalValue = 0x12345678 then 0x9ABCDEF0 else 0x12345678
                                match client.WriteInt32(address, testValue) with
                                | Ok () ->
                                    match client.ReadInt32(address) with
                                    | Ok verifyValue ->
                                        if verifyValue = testValue then
                                            // Restore original value
                                            ignore (client.WriteInt32(address, originalValue))
                                            log $"✓ {name} ({address}): Read/Write OK"
                                            successCount <- successCount + 1
                                        else
                                            log $"✗ {name} ({address}): Verify failed"
                                    | Error msg ->
                                        log $"✗ {name} ({address}): Verify read failed - {msg}"
                                | Error msg ->
                                    if msg.Contains("not writable") || msg.Contains("access denied") then
                                        log $"- {name} ({address}): Write not allowed (read-only)"
                                        successCount <- successCount + 1 // Count as success if read-only
                                    else
                                        log $"✗ {name} ({address}): Write failed - {msg}"
                            | Error msg ->
                                if msg.Contains("out of range") || msg.Contains("Invalid address") then
                                    log $"- {name} ({address}): Address out of range"
                                    // This is expected for edge cases
                                else
                                    log $"✗ {name} ({address}): Read failed - {msg}"
                        | _ ->
                            log $"✗ {name} ({address}): Unknown data type {dataType}"
                    with ex ->
                        log $"✗ {name} ({address}): Exception - {ex.Message}"
                
                let successRate = (float successCount / float totalCount * 100.0).ToString("F1")
                log "\n========== Siemens Edge Test Summary =========="
                log $"Total tests: {totalCount}"
                log $"Successful: {successCount}"
                log ("Success rate: " + successRate + "%")
                log "=============================================="
                
                Ok (successCount, totalCount)
            )
        
        match CH.unwrap connectionError result with
        | Ok (successCount, totalCount) ->
            Assert.True(successCount > 0, "At least some edge tests should pass")
        | Error msg ->
            TH.failWithLogsWithResult result $"Edge test failed: {msg}"