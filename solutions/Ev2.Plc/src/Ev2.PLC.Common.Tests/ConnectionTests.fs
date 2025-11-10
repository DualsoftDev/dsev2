module Ev2.PLC.Common.Tests.ConnectionTests

open Xunit
open FsUnit.Xunit
open System
open System.Net
open Ev2.PLC.Common.Types

[<Fact>]
let ``NetworkEndpoint should convert to IPEndPoint correctly`` () =
    let endpoint = NetworkEndpoint.Create("127.0.0.1", 502, TransportType.TCP)
    
    match endpoint.ToIPEndPoint() with
    | Some ipEndPoint ->
        ipEndPoint.Address |> should equal IPAddress.Loopback
        ipEndPoint.Port |> should equal 502
    | None -> failwith "Expected valid IPEndPoint"

[<Fact>]
let ``NetworkEndpoint should handle invalid hostnames`` () =
    let endpoint = NetworkEndpoint.Create("invalid-hostname-12345", 502, TransportType.TCP)
    
    endpoint.ToIPEndPoint() |> should equal None

[<Fact>]
let ``NetworkEndpoint should validate port ranges`` () =
    let validEndpoint = NetworkEndpoint.Create("localhost", 8080, TransportType.TCP)
    let invalidEndpoint1 = NetworkEndpoint.Create("localhost", 0, TransportType.TCP)
    let invalidEndpoint2 = NetworkEndpoint.Create("localhost", 70000, TransportType.TCP)
    
    validEndpoint.IsValid |> should equal true
    invalidEndpoint1.IsValid |> should equal false
    invalidEndpoint2.IsValid |> should equal false

[<Fact>]
let ``SerialConfig should create with correct defaults`` () =
    let defaultConfig = SerialConfig.Default
    
    defaultConfig.PortName |> should equal "COM1"
    defaultConfig.BaudRate |> should equal 9600
    defaultConfig.DataBits |> should equal 8
    defaultConfig.StopBits |> should equal 1
    defaultConfig.Parity |> should equal "None"
    defaultConfig.FlowControl |> should equal "None"

[<Fact>]
let ``SerialConfig should create with custom values`` () =
    let customConfig = SerialConfig.Create("COM3", 115200, 7)
    
    customConfig.PortName |> should equal "COM3"
    customConfig.BaudRate |> should equal 115200
    customConfig.DataBits |> should equal 7

[<Fact>]
let ``ConnectionConfig should not allow both Endpoint and SerialConfig`` () =
    let invalidConfig = {
        ConnectionConfig.Default with
            Endpoint = Some (NetworkEndpoint.Create("localhost", 502, TransportType.TCP))
            SerialConfig = Some SerialConfig.Default
    }
    
    invalidConfig.IsValid |> should equal false

[<Fact>]
let ``ConnectionConfig should require either Endpoint or SerialConfig`` () =
    let invalidConfig = {
        ConnectionConfig.Default with
            Endpoint = None
            SerialConfig = None
    }
    
    invalidConfig.IsValid |> should equal false

[<Fact>]
let ``TransportType should have correct network properties`` () =
    TransportType.TCP.IsNetworkBased |> should equal true
    TransportType.UDP.IsNetworkBased |> should equal true
    TransportType.Ethernet.IsNetworkBased |> should equal true
    TransportType.Serial.IsNetworkBased |> should equal false
    TransportType.USB.IsNetworkBased |> should equal false

[<Fact>]
let ``TransportType should have correct names`` () =
    TransportType.TCP.Name |> should equal "TCP"
    TransportType.UDP.Name |> should equal "UDP"
    TransportType.Serial.Name |> should equal "Serial"
    TransportType.USB.Name |> should equal "USB"
    TransportType.Ethernet.Name |> should equal "Ethernet"

[<Fact>]
let ``ConnectionInfo should create correctly`` () =
    let endpoint = NetworkEndpoint.Create("192.168.1.100", 44818, TransportType.TCP)
    let connectionInfo = ConnectionInfo.Create("PLC001", endpoint, None)
    
    connectionInfo.PlcId |> should equal "PLC001"
    connectionInfo.Endpoint |> should equal endpoint
    connectionInfo.SerialConfig |> should equal None
    connectionInfo.IsConnected |> should equal false

[<Fact>]
let ``ConnectionStateChangedEvent should create correctly`` () =
    let oldStatus = ConnectionStatus.Disconnected
    let newStatus = ConnectionStatus.Connected
    let timestamp = DateTime.UtcNow
    
    let event = ConnectionStateChangedEvent.Create("PLC001", oldStatus, newStatus, timestamp)
    
    event.PlcId |> should equal "PLC001"
    event.OldStatus |> should equal oldStatus
    event.NewStatus |> should equal newStatus
    event.Timestamp |> should equal timestamp

[<Fact>]
let ``ReconnectionPolicy should have reasonable defaults`` () =
    let policy = ReconnectionPolicy.Default
    
    policy.MaxAttempts |> should equal 5
    policy.InitialDelay |> should equal (TimeSpan.FromSeconds(1.0))
    policy.MaxDelay |> should equal (TimeSpan.FromMinutes(5.0))
    policy.BackoffMultiplier |> should equal 2.0
    policy.JitterEnabled |> should equal true
    policy.ExponentialBackoff |> should equal true

[<Fact>]
let ``ConnectionConfig additional parameters should work`` () =
    let config = {
        ConnectionConfig.Default with
            AdditionalParams = Map.ofList [("Rack", "0"); ("Slot", "1"); ("Timeout", "5000")]
    }
    
    config.AdditionalParams.["Rack"] |> should equal "0"
    config.AdditionalParams.["Slot"] |> should equal "1" 
    config.AdditionalParams.["Timeout"] |> should equal "5000"