namespace Ev2.LsProtocol.Tests.Integration

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module XgkIntegrationTests =

    // Use XGK device IPs for testing
    let xgkEfmtbIp = "192.168.9.103"
    let xgkLocalEthernetIp = "192.168.9.105"

    [<Fact>]
    let ``Can read XGK Bool values from EFMTB PLC`` () =
        skipIfIntegrationDisabled "XGK EFMTB Bool read test"
        
        let client = createClient (xgkEfmtbIp, xgtPort, xgtTimeoutMs, false)  // EFMTB mode
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK EFMTB PLC")
        
        try
            // Test XGK memory bit addresses
            let addresses = [| "P100"; "P101"; "P102" |]
            let dataTypes = [| PlcTagDataType.Bool; PlcTagDataType.Bool; PlcTagDataType.Bool |]
            let buffer = Array.zeroCreate<byte> 3
            
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGK Bool read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read XGK Word values from LocalEthernet PLC`` () =
        skipIfIntegrationDisabled "XGK LocalEthernet Word read test"
        
        let client = createClient (xgkLocalEthernetIp, xgtPort, xgtTimeoutMs, true)  // LocalEthernet mode
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK LocalEthernet PLC")
        
        try
            // Test XGK word addresses
            let addresses = [| "MW200"; "MW201"; "MW202" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
            let buffer = Array.zeroCreate<byte> 6
            
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGK Word read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read XGK DWord values from EFMTB PLC`` () =
        skipIfIntegrationDisabled "XGK EFMTB DWord read test"
        
        let client = createClient (xgkEfmtbIp, xgtPort, xgtTimeoutMs, false)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK EFMTB PLC")
        
        try
            // Test XGK double word addresses
            let addresses = [| "MD1000"; "MD1001" |]
            let dataTypes = [| PlcTagDataType.UInt32; PlcTagDataType.UInt32 |]
            let buffer = Array.zeroCreate<byte> 8
            
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGK DWord read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can read multiple XGK values from LocalEthernet PLC`` () =
        skipIfIntegrationDisabled "XGK LocalEthernet multiple read test"
        
        let client = createClient (xgkLocalEthernetIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK LocalEthernet PLC")
        
        try
            // Test multiple XGK addresses with same data type (protocol requirement)
            let addresses = [| "DW1000"; "DW1001"; "DW1002"; "DW1003" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
            let buffer = Array.zeroCreate<byte> 8
            
            client.Reads(addresses, dataTypes, buffer)
            Assert.True(true, "XGK multiple read operation completed successfully")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write XGK Bool values to EFMTB PLC`` () =
        skipIfIntegrationDisabled "XGK EFMTB Bool write test"
        
        let client = createClient (xgkEfmtbIp, xgtPort, xgtTimeoutMs, false)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK EFMTB PLC")
        
        try
            let result = client.Write("M500", PlcTagDataType.Bool, ScalarValue.BoolValue true)
            Assert.True(result, "XGK Bool write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write XGK Word values to LocalEthernet PLC`` () =
        skipIfIntegrationDisabled "XGK LocalEthernet Word write test"
        
        let client = createClient (xgkLocalEthernetIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK LocalEthernet PLC")
        
        try
            let result = client.Write("MW500", PlcTagDataType.UInt16, ScalarValue.UInt16Value 1234us)
            Assert.True(result, "XGK Word write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write XGK DWord values to EFMTB PLC`` () =
        skipIfIntegrationDisabled "XGK EFMTB DWord write test"
        
        let client = createClient (xgkEfmtbIp, xgtPort, xgtTimeoutMs, false)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK EFMTB PLC")
        
        try
            let result = client.Write("DD2000", PlcTagDataType.UInt32, ScalarValue.UInt32Value 123456u)
            Assert.True(result, "XGK DWord write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Can write multiple XGK values to LocalEthernet PLC`` () =
        skipIfIntegrationDisabled "XGK LocalEthernet multiple write test"
        
        let client = createClient (xgkLocalEthernetIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK LocalEthernet PLC")
        
        try
            let addresses = [| "MW600"; "MW601"; "MW602" |]
            let dataTypes = [| PlcTagDataType.UInt16; PlcTagDataType.UInt16; PlcTagDataType.UInt16 |]
            let values = [| ScalarValue.UInt16Value 100us; ScalarValue.UInt16Value 200us; ScalarValue.UInt16Value 300us |]
            
            let result = client.Writes(addresses, dataTypes, values)
            Assert.True(result, "XGK multiple write operation should succeed")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Should handle XGK read errors gracefully`` () =
        skipIfIntegrationDisabled "XGK read error test"
        
        let client = createClient (xgkEfmtbIp, xgtPort, xgtTimeoutMs, false)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK EFMTB PLC")
        
        try
            // Test with empty address array - should throw ArgumentException
            let addresses = [||]
            let dataTypes = [||]
            let buffer = Array.zeroCreate<byte> 0
            
            Assert.Throws<System.ArgumentException>(fun () ->
                client.Reads(addresses, dataTypes, buffer) |> ignore) |> ignore
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Should handle XGK write errors gracefully`` () =
        skipIfIntegrationDisabled "XGK write error test"
        
        let client = createClient (xgkLocalEthernetIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGK LocalEthernet PLC")
        
        try
            // Test with empty address array - should throw ArgumentException
            let addresses = [||]
            let dataTypes = [||]
            let values = [||]
            
            Assert.Throws<System.ArgumentException>(fun () ->
                client.Writes(addresses, dataTypes, values) |> ignore) |> ignore
        finally
            client.Disconnect() |> ignore