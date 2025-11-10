namespace Ev2.LsProtocol.Tests

open System
open System.Net.NetworkInformation
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module XgkDebugConnectionTest =

    [<Fact>]
    let ``Debug XGK EFMTB connection`` () =
        let ip = "192.168.9.103"  // XGK EFMTB device
        let port = 2004
        let timeoutMs = 3000
        
        printfn "=== Debug XGK EFMTB Connection (192.168.9.103) ==="
        
        // First test basic network connectivity
        let ping = new Ping()
        let reply = ping.Send(ip, timeoutMs)
        
        printfn $"Ping result: {reply.Status}"
        Assert.True(reply.Status = IPStatus.Success, $"Cannot ping {ip}: {reply.Status}")
        
        // Test EFMTB mode (false for EFMTB module)
        printfn "Testing XGK EFMTB mode (isLocalEthernet = false)..."
        let clientEFMTB = createClient (ip, port, timeoutMs, false)
        
        printfn $"Client created: IP={clientEFMTB.IpAddress}, Port={clientEFMTB.Port}, IsLocalEthernet={clientEFMTB.IsLocalEthernet}"
        
        let isConnectedEFMTB = clientEFMTB.Connect()
        printfn $"XGK EFMTB Connection result: {isConnectedEFMTB}"
        
        if isConnectedEFMTB then
            printfn $"XGK EFMTB Connected! SourcePort: {clientEFMTB.SourcePort}, IsConnected: {clientEFMTB.IsConnected}"
            
            // Test basic XGK read operation
            try
                printfn "Testing basic XGK read operation..."
                let value = clientEFMTB.Read("MW100", PlcTagDataType.Int16)
                printfn $"Read MW100: {value}"
            with ex ->
                printfn $"Read test failed: {ex.Message}"
                
            // Test XGK-specific addresses
            let xgkTestAddresses = [
                ("P100", PlcTagDataType.Bool, "XGK Input bit")
                ("MW200", PlcTagDataType.Int16, "XGK Memory word")  
                ("DW1000", PlcTagDataType.Int16, "XGK Data register word")
                ("K50", PlcTagDataType.Bool, "XGK Keep relay bit")
            ]
            
            for (address, dataType, description) in xgkTestAddresses do
                try
                    printfn $"Testing {description}: {address}"
                    let value = clientEFMTB.Read(address, dataType)
                    printfn $"Read {address}: {value}"
                with ex ->
                    printfn $"Read {address} failed: {ex.Message}"
            
            clientEFMTB.Disconnect() |> ignore
            printfn "XGK EFMTB test completed successfully"
        else
            Assert.True(false, "XGK EFMTB connection failed")

    [<Fact>] 
    let ``Debug XGK LocalEthernet connection`` () =
        let ip = "192.168.9.105"  // XGK LocalEthernet device
        let port = 2004
        let timeoutMs = 3000
        
        printfn "=== Debug XGK LocalEthernet Connection (192.168.9.105) ==="
        
        // First test basic network connectivity
        let ping = new Ping()
        let reply = ping.Send(ip, timeoutMs)
        
        printfn $"Ping result: {reply.Status}"
        Assert.True(reply.Status = IPStatus.Success, $"Cannot ping {ip}: {reply.Status}")
        
        // Test LocalEthernet mode (true for CPU UN port)
        printfn "Testing XGK LocalEthernet mode (isLocalEthernet = true)..."
        let clientLocal = createClient (ip, port, timeoutMs, true)
        
        printfn $"Client created: IP={clientLocal.IpAddress}, Port={clientLocal.Port}, IsLocalEthernet={clientLocal.IsLocalEthernet}"
        
        let isConnectedLocal = clientLocal.Connect()
        printfn $"XGK LocalEthernet Connection result: {isConnectedLocal}"
        
        if isConnectedLocal then
            printfn $"XGK LocalEthernet Connected! SourcePort: {clientLocal.SourcePort}, IsConnected: {clientLocal.IsConnected}"
            
            // Test basic XGK read operation
            try
                printfn "Testing basic XGK read operation..."
                let value = clientLocal.Read("MW100", PlcTagDataType.Int16)
                printfn $"Read MW100: {value}"
            with ex ->
                printfn $"Read test failed: {ex.Message}"
            
            // Test XGK multi-read (same data type)
            try
                printfn "Testing XGK multi-read operation..."
                let addresses = [| "MW100"; "MW101"; "MW102" |]
                let dataTypes = [| PlcTagDataType.Int16; PlcTagDataType.Int16; PlcTagDataType.Int16 |]
                let buffer = Array.zeroCreate<byte> (dataTypes |> Array.sumBy XgtTypes.byteSize)
                
                clientLocal.Reads(addresses, dataTypes, buffer)
                printfn "Multi-read completed"
            with ex ->
                printfn $"Multi-read test failed: {ex.Message}"
                
            // Test XGK-specific addresses
            let xgkTestAddresses = [
                ("P50", PlcTagDataType.Bool, "XGK Input bit")
                ("PW50", PlcTagDataType.Int16, "XGK Input word")  
                ("MD100", PlcTagDataType.Int32, "XGK Memory double word")
                ("TW5", PlcTagDataType.Int16, "XGK Timer word")
                ("DW2000", PlcTagDataType.Int16, "XGK Data register word")
            ]
            
            for (address, dataType, description) in xgkTestAddresses do
                try
                    printfn $"Testing {description}: {address}"
                    let value = clientLocal.Read(address, dataType)
                    printfn $"Read {address}: {value}"
                with ex ->
                    printfn $"Read {address} failed: {ex.Message}"
            
            clientLocal.Disconnect() |> ignore
            printfn "XGK LocalEthernet test completed successfully"
        else
            Assert.True(false, "XGK LocalEthernet connection failed")