module Ev2.MxProtocol.Tests.Integration.IntegrationConnectionTests

open System
open System.Globalization
open System.Net
open System.Net.Sockets
open System.Threading
open Xunit
open Xunit.Sdk
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Client
open Ev2.MxProtocol.Tests.TestHelpers
open Ev2.MxProtocol.Tests.TestAttributes
open Ev2.MxProtocol.Tests.ClientHelpers
open Ev2.MxProtocol.Tests

[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Client connects and disconnects properly`` () =
    use client = createTestClient()
    try
        Assert.False(client.IsConnected)
        
        client.Connect()
        Assert.True(client.IsConnected)
        
        client.Disconnect()
        Assert.False(client.IsConnected)
    with
    | ex -> 
        printfn $"Connect/disconnect test failed: {ex.Message}"
        failWithLogs $"Test failed: {ex.Message}"

[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Client reconnects after disconnect`` () =
    use client = createTestClient()
    try
        client.Connect()
        Assert.True(client.IsConnected)
        
        client.Disconnect()
        Assert.False(client.IsConnected)
        
        client.Connect()
        Assert.True(client.IsConnected)
    with
    | ex -> 
        printfn $"Reconnect test failed: {ex.Message}"
        failWithLogs $"Test failed: {ex.Message}"

[<Category(TestCategory.Integration)>]
[<Fact>]
let ``Client handles connection timeout`` () =
    let config = { 
        defaultTestConfig with 
            Host = "192.168.255.255" // Non-existent IP
            Timeout = TimeSpan.FromSeconds(1.0)
    }
    let logger = TestHelpers.createPacketLogger config
    let client = new MelsecClient(config, packetLogger = logger)
    
    let sw = System.Diagnostics.Stopwatch.StartNew()
    try
        client.Connect()
        failWithLogs "Should have thrown timeout exception"
    with
    | :? TimeoutException -> 
        sw.Stop()
        Assert.True(sw.Elapsed.TotalSeconds < 3.0, "Timeout took too long")
    | :? SocketException -> 
        sw.Stop()
        Assert.True(sw.Elapsed.TotalSeconds < 3.0, "Timeout took too long")
    | ex -> 
        failWithLogs $"Unexpected exception: {ex.Message}"

[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Client auto-connects when not connected`` () =
    use client = createTestClient()
    try
        Assert.False(client.IsConnected)
        
        // Attempt read without explicit connect
        let result = client.ReadWords(DeviceCode.D, 0, 1)
        
        // Should auto-connect and complete operation
        Assert.True(client.IsConnected)
    with
    | ex -> 
        printfn $"Auto-connect test failed: {ex.Message}"
        failWithLogs $"Test failed: {ex.Message}"

[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Multiple clients can connect to same server`` () =
    let config = defaultTestConfig
    use client1 = new MelsecClient(config, packetLogger = TestHelpers.createPacketLogger config)
    use client2 = new MelsecClient(config, packetLogger = TestHelpers.createPacketLogger config)
    
    try
        client1.Connect()
        client2.Connect()
        
        Assert.True(client1.IsConnected)
        Assert.True(client2.IsConnected)
    with
    | ex -> 
        printfn $"Multiple clients test failed: {ex.Message}"
        failWithLogs $"Test failed: {ex.Message}"


[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Client disposes properly`` () =
    let client = createTestClient()
    try
        client.Connect()
        Assert.True(client.IsConnected)
        
        (client :> IDisposable).Dispose()
        Assert.False(client.IsConnected)
    with
    | ex -> 
        printfn $"Client disposal test failed: {ex.Message}"
        failWithLogs $"Test failed: {ex.Message}"

[<Category(TestCategory.Integration)>]
[<RequiresMelsecPLC>]
let ``Connection can reconnect after disconnect`` () =
    let client, _ = createTestClientWithServer()
    try
        client.Connect()
        Assert.True(client.IsConnected)
        
        // Test disconnect and reconnect
        client.Disconnect()
        Assert.False(client.IsConnected)
        
        // Try to reconnect
        client.Connect()
        Assert.True(client.IsConnected)
    finally
        client.Disconnect()
