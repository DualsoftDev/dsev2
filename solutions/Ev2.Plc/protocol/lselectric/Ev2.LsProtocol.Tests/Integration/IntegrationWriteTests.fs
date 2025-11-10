namespace Ev2.LsProtocol.Tests.Integration

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module IntegrationWriteTests =

    [<Fact>]
    let ``Can write Bool values to XGT PLC`` () =
        skipIfIntegrationDisabled "XGT Bool write test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "P10" |]
            let dataTypes = [| PlcTagDataType.Bool |]
            let values = [| ScalarValue.BoolValue true |]
            
            let result = client.Writes(addresses, dataTypes, values)
            Assert.True(result, "Write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write Word values to XGT PLC`` () =
        skipIfIntegrationDisabled "XGT Word write test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "M100" |]
            let dataTypes = [| PlcTagDataType.UInt16 |]
            let values = [| ScalarValue.UInt16Value 1234us |]
            
            let result = client.Writes(addresses, dataTypes, values)
            Assert.True(result, "Write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write DWord values to XGT PLC`` () =
        skipIfIntegrationDisabled "XGT DWord write test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "D1000" |]
            let dataTypes = [| PlcTagDataType.UInt32 |]
            let values = [| ScalarValue.UInt32Value 12345678u |]
            
            let result = client.Writes(addresses, dataTypes, values)
            Assert.True(result, "Write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write multiple values to XGT PLC`` () =
        skipIfIntegrationDisabled "XGT multiple write test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [|  "D1000" ; "D2000" ; "D3000" |]
            let dataTypes = [| PlcTagDataType.UInt32; PlcTagDataType.UInt32; PlcTagDataType.UInt32 |]
            let values = [| 
                ScalarValue.UInt32Value 87654321u
                ScalarValue.UInt32Value 87654321u
                ScalarValue.UInt32Value 87654321u
            |]
            let result = client.Writes(addresses, dataTypes, values)
            let buffer = Array.zeroCreate<byte> 12
            client.Reads(addresses, dataTypes, buffer)

            Assert.True(result, "Multiple write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Should handle write errors gracefully`` () =
        skipIfIntegrationDisabled "XGT write error test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            // Test with empty address array - should throw ArgumentException
            let addresses = [||]
            let dataTypes = [||]
            let values = [||]
            
            Assert.Throws<System.ArgumentException>(fun () ->
                client.Writes(addresses, dataTypes, values) |> ignore)
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Should handle array length mismatch`` () =
        skipIfIntegrationDisabled "XGT write array mismatch test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            // Test with mismatched array lengths
            let addresses = [| "M100" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt32 |] // Wrong length
            let values = [| ScalarValue.UInt16Value 123us |]
            
            Assert.Throws<System.ArgumentException>(fun () ->
                client.Writes(addresses, dataTypes, values) |> ignore)
            
        finally
            client.Disconnect() |> ignore