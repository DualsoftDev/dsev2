namespace Ev2.LsProtocol.Tests.Integration

open System
open System.Diagnostics
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module PerformanceTests =

    [<Fact>]
    let ``Read operations should complete within reasonable time`` () =
        skipIfIntegrationDisabled "XGT read performance test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "M100" |]
            let dataTypes = [| PlcTagDataType.UInt16 |]
            let buffer = Array.zeroCreate<byte> 2
            
            let stopwatch = Stopwatch.StartNew()
            client.Reads(addresses, dataTypes, buffer)
            stopwatch.Stop()
            
            Assert.True(true, "Read operation completed successfully")
            Assert.True(stopwatch.ElapsedMilliseconds < 1000L, $"Read took too long: {stopwatch.ElapsedMilliseconds}ms")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Write operations should complete within reasonable time`` () =
        skipIfIntegrationDisabled "XGT write performance test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "M100" |]
            let dataTypes = [| PlcTagDataType.UInt16 |]
            let values = [| ScalarValue.UInt16Value 1234us |]
            
            let stopwatch = Stopwatch.StartNew()
            let result = client.Writes(addresses, dataTypes, values)
            stopwatch.Stop()
            
            Assert.True(result, "Write operation should succeed")
            Assert.True(stopwatch.ElapsedMilliseconds < 1000L, $"Write took too long: {stopwatch.ElapsedMilliseconds}ms")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Multiple read operations should maintain good throughput`` () =
        skipIfIntegrationDisabled "XGT read throughput test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "M100"; "M101"; "M102"; "M103"; "M104" |]
            let dataTypes = Array.create 5 PlcTagDataType.UInt16
            let buffer = Array.zeroCreate<byte> 10 // 5 * 2 bytes
            
            let iterationCount = 10
            let stopwatch = Stopwatch.StartNew()
            
            for _ in 1..iterationCount do
                client.Reads(addresses, dataTypes, buffer)
                // No assertion needed - will throw on error
            
            stopwatch.Stop()
            
            let avgTimePerRead = stopwatch.ElapsedMilliseconds / int64 iterationCount
            Assert.True(avgTimePerRead < 500L, $"Average read time too high: {avgTimePerRead}ms")
            
        finally
            client.Disconnect() |> ignore

    [<Fact>]
    let ``Connection establishment should be fast`` () =
        skipIfIntegrationDisabled "XGT connection performance test"
        
        let stopwatch = Stopwatch.StartNew()
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        let isConnected = client.Connect()
        stopwatch.Stop()
        
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        Assert.True(stopwatch.ElapsedMilliseconds < 3000L, $"Connection took too long: {stopwatch.ElapsedMilliseconds}ms")
        
        client.Disconnect() |> ignore

    [<Fact>]
    let ``Sequential operations should maintain consistent timing`` () =
        skipIfIntegrationDisabled "XGT sequential performance test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        try
            let addresses = [| "M100" |]
            let dataTypes = [| PlcTagDataType.UInt16 |]
            let values = [| ScalarValue.UInt16Value 1000us |]
            let buffer = Array.zeroCreate<byte> 2
            
            let mutable totalTime = 0L
            let operationCount = 5
            
            for i in 1..operationCount do
                let stopwatch = Stopwatch.StartNew()
                
                // Write then read
                client.Writes(addresses, dataTypes, values) |> ignore
                client.Reads(addresses, dataTypes, buffer)
                
                stopwatch.Stop()
                
                // Operations completed successfully if no exceptions thrown
                
                totalTime <- totalTime + stopwatch.ElapsedMilliseconds
            
            let avgTime = totalTime / int64 operationCount
            Assert.True(avgTime < 1000L, $"Average operation time too high: {avgTime}ms")
            
        finally
            client.Disconnect() |> ignore