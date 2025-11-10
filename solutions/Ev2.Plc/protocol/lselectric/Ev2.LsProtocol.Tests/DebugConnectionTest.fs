namespace Ev2.LsProtocol.Tests

open System
open System.Net.NetworkInformation
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module DebugConnectionTest =

    [<Fact>]
    let ``Debug EFMTB connection`` () =
        let ip = "192.168.9.100"  // Dedicated EFMTB device
        let port = 2004
        let timeoutMs = 3000
        
        printfn "=== Debug EFMTB Connection (192.168.9.100) ==="
        
        // First test basic network connectivity
        let ping = new Ping()
        let reply = ping.Send(ip, timeoutMs)
        
        printfn $"Ping result: {reply.Status}"
        Assert.True(reply.Status = IPStatus.Success, $"Cannot ping {ip}: {reply.Status}")
        
        // Test EFMTB mode (false for EFMTB module)
        printfn "Testing EFMTB mode (isLocalEthernet = false)..."
        let clientEFMTB = createClient (ip, port, timeoutMs, false)
        
        printfn $"Client created: IP={clientEFMTB.IpAddress}, Port={clientEFMTB.Port}, IsLocalEthernet={clientEFMTB.IsLocalEthernet}"
        
        let isConnectedEFMTB = clientEFMTB.Connect()
        printfn $"EFMTB Connection result: {isConnectedEFMTB}"
        
        if isConnectedEFMTB then
            printfn $"EFMTB Connected! SourcePort: {clientEFMTB.SourcePort}, IsConnected: {clientEFMTB.IsConnected}"
            
            // Test basic XGI read operation
            try
                printfn "Testing basic XGI read operation..."
                let value = clientEFMTB.Read("MW100", PlcTagDataType.Int16)
                printfn $"Read MW100: {value}"
            with ex ->
                printfn $"Read test failed: {ex.Message}"
            
            clientEFMTB.Disconnect() |> ignore
            printfn "EFMTB test completed successfully"
        else
            Assert.True(false, "EFMTB connection failed")

    [<Fact>] 
    let ``Debug LocalEthernet connection`` () =
        let ip = "192.168.9.102"  // Dedicated LocalEthernet device
        let port = 2004
        let timeoutMs = 3000
        
        printfn "=== Debug LocalEthernet Connection (192.168.9.102) ==="
        
        // First test basic network connectivity
        let ping = new Ping()
        let reply = ping.Send(ip, timeoutMs)
        
        printfn $"Ping result: {reply.Status}"
        Assert.True(reply.Status = IPStatus.Success, $"Cannot ping {ip}: {reply.Status}")
        
        // Test LocalEthernet mode (true for CPU UN port)
        printfn "Testing LocalEthernet mode (isLocalEthernet = true)..."
        let clientLocal = createClient (ip, port, timeoutMs, true)
        
        printfn $"Client created: IP={clientLocal.IpAddress}, Port={clientLocal.Port}, IsLocalEthernet={clientLocal.IsLocalEthernet}"
        
        let isConnectedLocal = clientLocal.Connect()
        printfn $"LocalEthernet Connection result: {isConnectedLocal}"
        
        if isConnectedLocal then
            printfn $"LocalEthernet Connected! SourcePort: {clientLocal.SourcePort}, IsConnected: {clientLocal.IsConnected}"
            
            // Test basic XGI read operation
            try
                printfn "Testing basic XGI read operation..."
                let value = clientLocal.Read("MW100", PlcTagDataType.Int16)
                printfn $"Read MW100: {value}"
            with ex ->
                printfn $"Read test failed: {ex.Message}"
            
            // Test XGI multi-read (same data type)
            try
                printfn "Testing XGI multi-read operation..."
                let addresses = [| "MW100"; "MW101"; "MW102" |]
                let dataTypes = [| PlcTagDataType.Int16; PlcTagDataType.Int16; PlcTagDataType.Int16 |]
                let buffer = Array.zeroCreate<byte> (dataTypes |> Array.sumBy XgtTypes.byteSize)
                
                clientLocal.Reads(addresses, dataTypes, buffer)
                printfn "Multi-read completed"
            with ex ->
                printfn $"Multi-read test failed: {ex.Message}"
            
            clientLocal.Disconnect() |> ignore
            printfn "LocalEthernet test completed successfully"
        else
            Assert.True(false, "LocalEthernet connection failed")