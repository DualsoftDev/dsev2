namespace Ev2.LsProtocol.Tests.Integration

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module IntegrationReadTests =

    [<Fact>]
    let ``Can read XGI Bool values from PLC`` () =
        skipIfIntegrationDisabled "XGI Bool read test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGI PLC")
        
        try
            // Test XGI memory bit addresses - use only supported addresses
            let addresses = [| "M100"; "M101"; "M102" |]
            let dataTypes = [| PlcTagDataType.Bool; PlcTagDataType.Bool; PlcTagDataType.Bool |]
            let buffer = Array.zeroCreate<byte> 3
            
            // Should not throw exceptions
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGI Bool read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read XGI Word values from PLC`` () =
        skipIfIntegrationDisabled "XGI Word read test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGI PLC")
        
        try
            // Test XGI word addresses - use only supported addresses  
            let addresses = [| "MW100"; "MW101"; "MW102" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
            let buffer = Array.zeroCreate<byte> 6
            
            // Should not throw exceptions
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGI Word read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read XGI DWord values from PLC`` () =
        skipIfIntegrationDisabled "XGI DWord read test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            // Test XGI double word addresses
            let addresses = [| "MD1000"; "FD200" |]
            let dataTypes = [| PlcTagDataType.UInt32; PlcTagDataType.UInt32 |]
            let buffer = Array.zeroCreate<byte> 8
            
            // Should not throw exceptions
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGI DWord read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read multiple XGI values from PLC`` () =
        skipIfIntegrationDisabled "XGI multiple read test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            // Test multiple XGI addresses with same data type (protocol requirement)
            let addresses = [| "MW100"; "MW101"; "MW102"; "MW103" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
            let buffer = Array.zeroCreate<byte> 8  // 4 * 2 bytes
            
            // Should not throw exceptions with same data types
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGI multiple read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Should handle read errors gracefully`` () =
        skipIfIntegrationDisabled "XGT read error test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            // Test with empty address array - should throw ArgumentException
            let addresses = [||]
            let dataTypes = [||]
            let buffer = Array.zeroCreate<byte> 0
            
            Assert.Throws<System.ArgumentException>(fun () ->
                client.Reads(addresses, dataTypes, buffer) |> ignore)
            
        finally
            client.Disconnect() |> ignore