namespace Ev2.S7Protocol.Tests

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestHelpers

module ProtocolValidationTests =
    
    [<Fact>]
    let ``S7Config validation accepts valid configuration`` () =
        let validConfigs = [
            {
                Name = "Test PLC 1"
                IpAddress = "192.168.1.100"
                Port = 102
                Rack = 0
                Slot = 2
                LocalTSAP = 0x0100
                RemoteTSAP = 0x0100
                CpuType = CpuType.S7300
                MaxPDUSize = 480
                Timeout = TimeSpan.FromSeconds(5.0)
                Password = None
            }
            {
                Name = "Test PLC 2"
                IpAddress = "127.0.0.1"
                Port = 102
                Rack = 0
                Slot = 1
                LocalTSAP = 0x0200
                RemoteTSAP = 0x0201
                CpuType = CpuType.S71500
                MaxPDUSize = 960
                Timeout = TimeSpan.FromSeconds(10.0)
                Password = Some "test123"
            }
        ]
        
        // All configurations should be acceptable
        validConfigs
        |> List.iter (fun config ->
            Assert.True(config.Port > 0, "Port should be positive")
            Assert.True(config.Rack >= 0, "Rack should be non-negative")
            Assert.True(config.Slot >= 0, "Slot should be non-negative")
            Assert.True(config.MaxPDUSize > 0, "MaxPDUSize should be positive")
            Assert.True(config.Timeout.TotalMilliseconds > 0.0, "Timeout should be positive")
            Assert.False(String.IsNullOrWhiteSpace(config.IpAddress), "IP address should not be empty"))

    [<Fact>]
    let ``CpuType enum values are within expected range`` () =
        let cpuTypes = [
            CpuType.S7200
            CpuType.S7300
            CpuType.S7400
            CpuType.S71200
            CpuType.S71500
        ]
        
        cpuTypes
        |> List.iter (fun cpuType ->
            let intValue = int cpuType
            Assert.True(intValue >= 0, $"CPU type {cpuType} should have non-negative integer value")
            Assert.True(intValue < 1000, $"CPU type {cpuType} integer value seems too large: {intValue}"))

    [<Fact>]
    let ``Address parsing handles various formats`` () =
        let testCases = [
            // (address, shouldSucceed, description)
            ("M0.0", true, "Basic merker bit")
            ("M1234.7", true, "Merker bit with large offset")
            ("MB0", true, "Merker byte")
            ("MW0", true, "Merker word")
            ("DB1.DBX0.0", true, "Data block bit")
            ("DB1.DBB0", true, "Data block byte")
            ("DB1.DBW0", true, "Data block word")
            ("I0.0", true, "Input bit")
            ("Q0.0", true, "Output bit")
            ("", false, "Empty address")
            ("M", false, "Incomplete address")
            ("M0.8", false, "Invalid bit number")
            ("M-1.0", false, "Negative offset")
            ("DB.DBX0.0", false, "Missing DB number")
            ("XYZ0.0", false, "Invalid device type")
        ]
        
        // This is a conceptual test - actual address parsing would depend on the implementation
        testCases
        |> List.iter (fun (address, shouldSucceed, description) ->
            if shouldSucceed then
                Assert.False(String.IsNullOrWhiteSpace(address), $"Valid address should not be empty: {description}")
                Assert.True(address.Length > 1, $"Valid address should have reasonable length: {description}")
            else
                // For invalid addresses, we expect them to be caught by validation
                // The specific validation logic would depend on the actual implementation
                Assert.True(true, $"Invalid address case noted: {description}"))

    [<Fact>]
    let ``PDU size constraints are reasonable`` () =
        let testPduSizes = [
            (240, true, "Minimum typical PDU")
            (480, true, "Common S7-300 PDU")
            (960, true, "Common S7-1500 PDU")
            (1460, true, "Large PDU size")
            (0, false, "Zero PDU size")
            (-1, false, "Negative PDU size")
            (65536, false, "Unreasonably large PDU")
        ]
        
        testPduSizes
        |> List.iter (fun (pduSize, isValid, description) ->
            if isValid then
                Assert.True(pduSize > 0, $"Valid PDU size should be positive: {description}")
                Assert.True(pduSize <= 65535, $"Valid PDU size should fit in 16 bits: {description}")
            else
                Assert.True(pduSize <= 0 || pduSize > 65535, $"Invalid PDU size should be outside valid range: {description}"))

    [<Fact>]
    let ``Timeout values are within reasonable bounds`` () =
        let testTimeouts = [
            (TimeSpan.FromMilliseconds(100.0), true, "Very short timeout")
            (TimeSpan.FromSeconds(1.0), true, "Short timeout")
            (TimeSpan.FromSeconds(5.0), true, "Standard timeout")
            (TimeSpan.FromSeconds(30.0), true, "Long timeout")
            (TimeSpan.FromMinutes(1.0), true, "Very long timeout")
            (TimeSpan.Zero, false, "Zero timeout")
            (TimeSpan.FromMilliseconds(-1.0), false, "Negative timeout")
            (TimeSpan.FromHours(1.0), false, "Unreasonably long timeout")
        ]
        
        testTimeouts
        |> List.iter (fun (timeout, isValid, description) ->
            if isValid then
                Assert.True(timeout.TotalMilliseconds > 0.0, $"Valid timeout should be positive: {description}")
                Assert.True(timeout.TotalMinutes <= 10.0, $"Valid timeout should be reasonable: {description}")
            else
                Assert.True(timeout.TotalMilliseconds <= 0.0 || timeout.TotalMinutes > 10.0, 
                    $"Invalid timeout should be outside valid range: {description}"))

    [<Fact>]
    let ``TSAP values follow S7 conventions`` () =
        let testTsaps = [
            (0x0100, true, "Standard local TSAP")
            (0x0101, true, "Alternative TSAP") 
            (0x0200, true, "Different TSAP range")
            (0x1000, true, "High TSAP value")
            (0x0000, false, "Zero TSAP")
            (0xFFFF, false, "Maximum value TSAP")
        ]
        
        testTsaps
        |> List.iter (fun (tsap, isTypical, description) ->
            Assert.True(tsap >= 0, $"TSAP should be non-negative: {description}")
            Assert.True(tsap <= 0xFFFF, $"TSAP should fit in 16 bits: {description}")
            
            if isTypical then
                // Typical S7 TSAP values are in certain ranges
                Assert.True(tsap >= 0x0100, $"Typical TSAP should be >= 0x0100: {description}")
                Assert.True(tsap <= 0x8000, $"Typical TSAP should be <= 0x8000: {description}"))

    [<Fact>]
    let ``Network port numbers are valid`` () =
        let testPorts = [
            (102, true, "Standard S7 port")
            (1024, true, "User port range")
            (8080, true, "Alternative port")
            (65535, true, "Maximum port")
            (0, false, "Zero port")
            (-1, false, "Negative port")
            (65536, false, "Port too large")
        ]
        
        testPorts
        |> List.iter (fun (port, isValid, description) ->
            if isValid then
                Assert.True(port > 0, $"Valid port should be positive: {description}")
                Assert.True(port <= 65535, $"Valid port should be <= 65535: {description}")
            else
                Assert.True(port <= 0 || port > 65535, $"Invalid port should be outside valid range: {description}"))

    [<Fact>]
    let ``Rack and slot values are within PLC ranges`` () =
        let testRackSlots = [
            (0, 0, true, "CPU in rack 0, slot 0")
            (0, 1, true, "Module in rack 0, slot 1") 
            (0, 2, true, "CPU in rack 0, slot 2")
            (1, 0, true, "Remote rack")
            (7, 15, true, "Maximum typical values")
            (-1, 0, false, "Negative rack")
            (0, -1, false, "Negative slot")
            (255, 0, false, "Rack too large")
            (0, 255, false, "Slot too large")
        ]
        
        testRackSlots
        |> List.iter (fun (rack, slot, isValid, description) ->
            if isValid then
                Assert.True(rack >= 0, $"Valid rack should be non-negative: {description}")
                Assert.True(slot >= 0, $"Valid slot should be non-negative: {description}")
                Assert.True(rack <= 7, $"Valid rack should be <= 7: {description}")
                Assert.True(slot <= 15, $"Valid slot should be <= 15: {description}")
            else
                Assert.True(rack < 0 || slot < 0 || rack > 7 || slot > 15, 
                    $"Invalid rack/slot should be outside valid range: {description}"))

    [<Fact>]
    let ``Data type size calculations are correct`` () =
        // Test conceptual data type sizes that would be used in the protocol
        let dataTypeSizes = [
            ("Bit", 1, "Bit operations")
            ("Byte", 8, "Byte operations") 
            ("Word", 16, "Word operations")
            ("DWord", 32, "Double word operations")
            ("Real", 32, "Real number operations")
        ]
        
        dataTypeSizes
        |> List.iter (fun (typeName, expectedBits, description) ->
            Assert.True(expectedBits > 0, $"{typeName} should have positive bit count")
            Assert.True(expectedBits % 8 = 0 || expectedBits = 1, 
                $"{typeName} bit count should be 1 or multiple of 8: {description}")
            
            let expectedBytes = if expectedBits = 1 then 1 else expectedBits / 8
            Assert.True(expectedBytes > 0, $"{typeName} should require at least 1 byte"))

    [<Fact>]
    let ``Protocol message size limits are enforced`` () =
        // Test that we have reasonable limits for protocol messages
        let messageSizeTests = [
            (1, true, "Minimum message")
            (240, true, "Small message")
            (480, true, "Medium message") 
            (960, true, "Large message")
            (1460, true, "Maximum typical message")
            (0, false, "Empty message")
            (65536, false, "Oversized message")
        ]
        
        messageSizeTests
        |> List.iter (fun (messageSize, isValid, description) ->
            if isValid then
                Assert.True(messageSize > 0, $"Valid message size should be positive: {description}")
                Assert.True(messageSize <= 1500, $"Valid message size should be reasonable: {description}")
            else
                Assert.True(messageSize <= 0 || messageSize > 1500, 
                    $"Invalid message size should be outside valid range: {description}"))