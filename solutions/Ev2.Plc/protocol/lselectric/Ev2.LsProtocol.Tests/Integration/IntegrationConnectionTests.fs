namespace Ev2.LsProtocol.Tests.Integration

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Tests
open ProtocolTestHelper

module IntegrationConnectionTests =

    [<Fact>]
    let ``Can establish connection to XGT PLC`` () =
        skipIfIntegrationDisabled "XGT connection test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        let isConnected = client.Connect()
        Assert.True(isConnected, "Failed to connect to XGT PLC")
        
        let disconnected = client.Disconnect()
        Assert.True(disconnected, "Failed to disconnect from XGT PLC")

    [<Fact>]
    let ``Connection timeout should work correctly`` () =
        skipIfIntegrationDisabled "XGT connection timeout test"
        
        // Use a non-existent IP to test timeout
        let invalidIp = "192.168.255.255"
        let shortTimeout = 1000 // 1 second
        
        let client = createClient (invalidIp, xgtPort, shortTimeout, true)
        
        let startTime = DateTime.UtcNow
        let isConnected = client.Connect()
        let elapsed = DateTime.UtcNow - startTime
        
        Assert.False(isConnected, "Connection should have failed")
        Assert.True(elapsed.TotalMilliseconds >= float shortTimeout * 0.8, "Timeout should have been respected")

    [<Fact>]
    let ``Can reconnect after disconnection`` () =
        skipIfIntegrationDisabled "XGT reconnection test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        // First connection
        let isConnected1 = client.Connect()
        Assert.True(isConnected1, "First connection failed")
        
        // Disconnect
        let disconnected = client.Disconnect()
        Assert.True(disconnected, "Disconnection failed")
        
        // Reconnect
        let isConnected2 = client.Reconnect()
        Assert.True(isConnected2, "Reconnection failed")
        
        let disconnected2 = client.Disconnect()
        Assert.True(disconnected2, "Second disconnection failed")

    [<Fact>]
    let ``Client properties should be accessible`` () =
        skipIfIntegrationDisabled "XGT client properties test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        Assert.Equal(xgtIp, client.IpAddress)
        Assert.Equal(xgtPort, client.Port)
        Assert.True(client.IsLocalEthernet)
        
        let isConnected = client.Connect()
        if isConnected then
            Assert.True(client.IsConnected)
            Assert.True(client.SourcePort > 0)
            
            let disconnected = client.Disconnect()
            Assert.True(disconnected)
            Assert.False(client.IsConnected)

    [<Fact>]
    let ``Should handle connection state correctly`` () =
        skipIfIntegrationDisabled "XGT connection state test"
        
        let client = createClient (xgtIp, xgtPort, xgtTimeoutMs, true)
        
        // Initially not connected
        Assert.False(client.IsConnected)
        
        // Connect
        let isConnected = client.Connect()
        if isConnected then
            Assert.True(client.IsConnected)
            
            // Disconnect
            let disconnected = client.Disconnect()
            Assert.True(disconnected)
            Assert.False(client.IsConnected)