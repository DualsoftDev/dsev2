module Ev2.PLC.Common.Tests.TypesTests

open Xunit
open FsUnit.Xunit
open System
open Ev2.PLC.Common.Types

[<Fact>]
let ``PlcDataType should have correct basic types`` () =
    PlcDataType.Bool |> should equal PlcDataType.Bool
    PlcDataType.Int8 |> should equal PlcDataType.Int8
    PlcDataType.UInt8 |> should equal PlcDataType.UInt8
    PlcDataType.Int16 |> should equal PlcDataType.Int16
    PlcDataType.UInt16 |> should equal PlcDataType.UInt16
    PlcDataType.Int32 |> should equal PlcDataType.Int32
    PlcDataType.UInt32 |> should equal PlcDataType.UInt32
    PlcDataType.Int64 |> should equal PlcDataType.Int64
    PlcDataType.UInt64 |> should equal PlcDataType.UInt64
    PlcDataType.Float32 |> should equal PlcDataType.Float32
    PlcDataType.Float64 |> should equal PlcDataType.Float64

[<Fact>]
let ``PlcDataType should support complex types`` () =
    let stringType = PlcDataType.String(255)
    let binaryType = PlcDataType.Binary(1024)
    let arrayType = PlcDataType.Array(PlcDataType.Int32, 10)
    let structType = PlcDataType.Struct("TestStruct", [("field1", PlcDataType.Int32); ("field2", PlcDataType.Bool)])
    let customType = PlcDataType.Custom("WORD", 2)
    
    stringType |> should equal (PlcDataType.String(255))
    binaryType |> should equal (PlcDataType.Binary(1024))
    arrayType |> should equal (PlcDataType.Array(PlcDataType.Int32, 10))
    structType |> should equal (PlcDataType.Struct("TestStruct", [("field1", PlcDataType.Int32); ("field2", PlcDataType.Bool)]))
    customType |> should equal (PlcDataType.Custom("WORD", 2))

[<Fact>]
let ``PlcDataType SizeInBytes should be correct`` () =
    PlcDataType.Bool.SizeInBytes |> should equal 1
    PlcDataType.Int8.SizeInBytes |> should equal 1
    PlcDataType.UInt8.SizeInBytes |> should equal 1
    PlcDataType.Int16.SizeInBytes |> should equal 2
    PlcDataType.UInt16.SizeInBytes |> should equal 2
    PlcDataType.Int32.SizeInBytes |> should equal 4
    PlcDataType.UInt32.SizeInBytes |> should equal 4
    PlcDataType.Float32.SizeInBytes |> should equal 4
    PlcDataType.Int64.SizeInBytes |> should equal 8
    PlcDataType.UInt64.SizeInBytes |> should equal 8
    PlcDataType.Float64.SizeInBytes |> should equal 8
    PlcDataType.String(50).SizeInBytes |> should equal 50
    PlcDataType.Binary(100).SizeInBytes |> should equal 100
    PlcDataType.Array(PlcDataType.Int32, 5).SizeInBytes |> should equal 20
    PlcDataType.Custom("WORD", 2).SizeInBytes |> should equal 2

[<Fact>]
let ``PlcDataType IsNumeric should work correctly`` () =
    PlcDataType.Bool.IsNumeric |> should equal false
    PlcDataType.Int8.IsNumeric |> should equal true
    PlcDataType.UInt8.IsNumeric |> should equal true
    PlcDataType.Int16.IsNumeric |> should equal true
    PlcDataType.UInt16.IsNumeric |> should equal true
    PlcDataType.Int32.IsNumeric |> should equal true
    PlcDataType.UInt32.IsNumeric |> should equal true
    PlcDataType.Int64.IsNumeric |> should equal true
    PlcDataType.UInt64.IsNumeric |> should equal true
    PlcDataType.Float32.IsNumeric |> should equal true
    PlcDataType.Float64.IsNumeric |> should equal true
    PlcDataType.String(10).IsNumeric |> should equal false

[<Fact>]
let ``PlcValue should create correct types`` () =
    let boolValue = PlcValue.BoolValue true
    let int32Value = PlcValue.Int32Value 42
    let float32Value = PlcValue.Float32Value 3.14f
    let stringValue = PlcValue.StringValue "test"
    
    boolValue |> should equal (PlcValue.BoolValue true)
    int32Value |> should equal (PlcValue.Int32Value 42)
    float32Value |> should equal (PlcValue.Float32Value 3.14f)
    stringValue |> should equal (PlcValue.StringValue "test")

[<Fact>]
let ``PlcValue DataType should return correct types`` () =
    PlcValue.BoolValue(true).DataType |> should equal PlcDataType.Bool
    PlcValue.Int8Value(42y).DataType |> should equal PlcDataType.Int8
    PlcValue.UInt8Value(42uy).DataType |> should equal PlcDataType.UInt8
    PlcValue.Int16Value(42s).DataType |> should equal PlcDataType.Int16
    PlcValue.UInt16Value(42us).DataType |> should equal PlcDataType.UInt16
    PlcValue.Int32Value(42).DataType |> should equal PlcDataType.Int32
    PlcValue.UInt32Value(42u).DataType |> should equal PlcDataType.UInt32
    PlcValue.Int64Value(42L).DataType |> should equal PlcDataType.Int64
    PlcValue.UInt64Value(42UL).DataType |> should equal PlcDataType.UInt64
    PlcValue.Float32Value(3.14f).DataType |> should equal PlcDataType.Float32
    PlcValue.Float64Value(3.14).DataType |> should equal PlcDataType.Float64
    PlcValue.StringValue("test").DataType |> should equal (PlcDataType.String 4)

[<Fact>]
let ``PlcValue ToString should return correct string representations`` () =
    PlcValue.BoolValue(true).ToString() |> should equal "true"
    PlcValue.Int32Value(42).ToString() |> should equal "42"
    PlcValue.Float32Value(3.14f).ToString() |> should startWith "3.14"
    PlcValue.StringValue("test").ToString() |> should equal "\"test\""

[<Fact>]
let ``DataQuality should have correct properties`` () =
    DataQuality.Good.IsGood |> should equal true
    DataQuality.Good.IsBad |> should equal false
    DataQuality.Good.IsUncertain |> should equal false
    
    let badQuality = DataQuality.Bad("error")
    badQuality.IsGood |> should equal false
    badQuality.IsBad |> should equal true
    
    let uncertainQuality = DataQuality.Uncertain("uncertain")
    uncertainQuality.IsUncertain |> should equal true

[<Fact>]
let ``DataQuality Score should be correct`` () =
    DataQuality.Good.Score |> should equal 100
    DataQuality.Bad("error").Score |> should equal 0
    let uncertainScore = DataQuality.Uncertain("uncertain").Score
    uncertainScore |> should be (greaterThan 0)
    uncertainScore |> should be (lessThan 100)

[<Fact>]
let ``ConnectionStatus should work correctly`` () =
    ConnectionStatus.Disconnected.IsOperational |> should equal false
    ConnectionStatus.Connecting.IsOperational |> should equal false
    ConnectionStatus.Connected.IsOperational |> should equal true
    ConnectionStatus.Reconnecting.IsOperational |> should equal false
    ConnectionStatus.Maintenance.IsOperational |> should equal false

[<Fact>]
let ``ConnectionStatus CanRead should work correctly`` () =
    ConnectionStatus.Connected.CanRead |> should equal true
    ConnectionStatus.Maintenance.CanRead |> should equal true
    ConnectionStatus.Disconnected.CanRead |> should equal false
    ConnectionStatus.Connecting.CanRead |> should equal false

[<Fact>]
let ``ConnectionStatus CanWrite should work correctly`` () =
    ConnectionStatus.Connected.CanWrite |> should equal true
    ConnectionStatus.Maintenance.CanWrite |> should equal false
    ConnectionStatus.Disconnected.CanWrite |> should equal false

[<Fact>]
let ``TagConfiguration should create with default values`` () =
    let address = PlcAddress.Create("DB1.DBX0.0")
    let tag = TagConfiguration.Create("test_tag", "PLC001", "Test Tag", address, PlcDataType.Bool)
    
    tag.Id |> should equal "test_tag"
    tag.Address.Raw |> should equal "DB1.DBX0.0"
    tag.DataType |> should equal PlcDataType.Bool
    tag.AccessRights |> should equal TagAccessRights.ReadWrite
    tag.UpdateMode |> should equal TagUpdateMode.OnScan
    tag.IsEnabled |> should equal true

[<Fact>]
let ``NetworkEndpoint should validate correctly`` () =
    let validTcp = NetworkEndpoint.Create("192.168.1.100", 44818, TransportType.TCP)
    let invalidTcp = NetworkEndpoint.Create("", 0, TransportType.TCP)
    
    validTcp.IsValid |> should equal true
    invalidTcp.IsValid |> should equal false

[<Fact>]
let ``ConnectionConfig should create correctly`` () =
    let tcpConfig = ConnectionConfig.ForTCP("192.168.1.100", 44818, 5000)
    let serialConfig = ConnectionConfig.ForSerial("COM1", 9600)
    
    tcpConfig.IsValid |> should equal true
    serialConfig.IsValid |> should equal true
    
    match tcpConfig.Endpoint with
    | Some endpoint -> 
        endpoint.Host |> should equal "192.168.1.100"
        endpoint.Port |> should equal 44818
    | None -> failwith "Expected TCP endpoint"

[<Fact>]
let ``ScanResult should create correctly`` () =
    let timestamp = DateTime.UtcNow
    let quality = DataQuality.Good
    let value = PlcValue.Int32Value(42)
    let operation = ScanOperation.Read
    
    let result = ScanResult.Create("TAG001", "PLC001", operation, value, quality)
    
    result.TagId |> should equal "TAG001"
    result.PlcId |> should equal "PLC001"
    result.Value |> should equal value
    result.Quality |> should equal quality
    result.Status.Quality |> should equal quality
    result.Timestamp |> should be (greaterThan (timestamp.AddSeconds(-1.0)))

[<Fact>]
let ``PerformanceMetrics should have correct initial values`` () =
    let metrics = PerformanceMetrics.Empty("PLC001")
    
    // 초기 상태 확인
    metrics.PlcId |> should equal "PLC001"
    metrics.TotalRequests |> should equal 0L
    metrics.SuccessfulRequests |> should equal 0L
    metrics.FailedRequests |> should equal 0L
    metrics.AverageResponseTime |> should equal TimeSpan.Zero