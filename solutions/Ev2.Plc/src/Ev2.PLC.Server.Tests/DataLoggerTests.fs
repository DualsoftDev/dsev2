module Ev2.PLC.Server.Tests.DataLoggerTests

open Xunit
open FsUnit.Xunit
open System
open DSPLCServer.Common
open Ev2.PLC.Common.Types

[<Fact>]
let ``LoggedDataPoint.FromScanResult should convert correctly`` () =
    let scanResult = ScanResult.Create("TAG001", "PLC001", ScanOperation.Read, PlcValue.Int32Value(42), DataQuality.Good)
    let loggedPoint = LoggedDataPoint.FromScanResult(scanResult)
    
    loggedPoint.Id |> should equal 0L
    loggedPoint.TagId |> should equal "TAG001"
    loggedPoint.PlcId |> should equal "PLC001"
    loggedPoint.Value |> should equal (PlcValue.Int32Value(42))
    loggedPoint.Quality |> should equal DataQuality.Good
    loggedPoint.Status |> should equal scanResult.Status
    loggedPoint.Timestamp |> should equal scanResult.Timestamp
    loggedPoint.SourceAddress |> should equal None
    loggedPoint.ResponseTime |> should equal scanResult.ResponseTime

[<Fact>]
let ``LoggedDataPoint should preserve all scan result data`` () =
    let timestamp = DateTime.UtcNow
    let responseTime = TimeSpan.FromMilliseconds(50.0)
    
    // Create a scan result with response time
    let scanResult = ScanResult.Create("TEMP_SENSOR", "PLC_MAIN", ScanOperation.Read, PlcValue.Float32Value(25.5f), DataQuality.Good)
    let updatedResult = { scanResult with Timestamp = timestamp; ResponseTime = Some responseTime }
    
    let loggedPoint = LoggedDataPoint.FromScanResult(updatedResult)
    
    loggedPoint.TagId |> should equal "TEMP_SENSOR"
    loggedPoint.PlcId |> should equal "PLC_MAIN"
    loggedPoint.Value |> should equal (PlcValue.Float32Value(25.5f))
    loggedPoint.Quality |> should equal DataQuality.Good
    loggedPoint.Timestamp |> should equal timestamp
    loggedPoint.ResponseTime |> should equal (Some responseTime)

[<Fact>]
let ``LoggedDataPoint should handle different data types`` () =
    let boolResult = ScanResult.Create("MOTOR_RUNNING", "PLC001", ScanOperation.Read, PlcValue.BoolValue(true), DataQuality.Good)
    let intResult = ScanResult.Create("COUNTER_VALUE", "PLC001", ScanOperation.Read, PlcValue.Int32Value(1234), DataQuality.Good)
    let stringResult = ScanResult.Create("PART_NUMBER", "PLC001", ScanOperation.Read, PlcValue.StringValue("ABC123"), DataQuality.Good)
    let errorResult = ScanResult.Create("BROKEN_TAG", "PLC001", ScanOperation.Read, PlcValue.ErrorValue("Sensor disconnected"), DataQuality.Bad("Communication error"))
    
    let boolPoint = LoggedDataPoint.FromScanResult(boolResult)
    let intPoint = LoggedDataPoint.FromScanResult(intResult)
    let stringPoint = LoggedDataPoint.FromScanResult(stringResult)
    let errorPoint = LoggedDataPoint.FromScanResult(errorResult)
    
    boolPoint.Value |> should equal (PlcValue.BoolValue(true))
    boolPoint.Quality.IsGood |> should equal true
    
    intPoint.Value |> should equal (PlcValue.Int32Value(1234))
    intPoint.Quality.IsGood |> should equal true
    
    stringPoint.Value |> should equal (PlcValue.StringValue("ABC123"))
    stringPoint.Quality.IsGood |> should equal true
    
    errorPoint.Value |> should equal (PlcValue.ErrorValue("Sensor disconnected"))
    errorPoint.Quality.IsBad |> should equal true

[<Fact>]
let ``LoggedDataPoint should handle quality degradation`` () =
    let goodResult = ScanResult.Create("SENSOR_01", "PLC001", ScanOperation.Read, PlcValue.Float32Value(23.5f), DataQuality.Good)
    let uncertainResult = ScanResult.Create("SENSOR_02", "PLC001", ScanOperation.Read, PlcValue.Float32Value(23.1f), DataQuality.Uncertain("Old data"))
    let badResult = ScanResult.Create("SENSOR_03", "PLC001", ScanOperation.Read, PlcValue.ErrorValue("Timeout"), DataQuality.Bad("Communication timeout"))
    
    let goodPoint = LoggedDataPoint.FromScanResult(goodResult)
    let uncertainPoint = LoggedDataPoint.FromScanResult(uncertainResult)
    let badPoint = LoggedDataPoint.FromScanResult(badResult)
    
    goodPoint.Quality.Score |> should equal 100
    uncertainPoint.Quality.Score |> should equal 50
    badPoint.Quality.Score |> should equal 0

[<Fact>]
let ``LoggedDataPoint should handle write operations`` () =
    let writeValue = PlcValue.Int32Value(5000)
    let writeResult = ScanResult.Create("SETPOINT", "PLC001", ScanOperation.Write(writeValue), writeValue, DataQuality.Good)
    
    let loggedPoint = LoggedDataPoint.FromScanResult(writeResult)
    
    loggedPoint.TagId |> should equal "SETPOINT"
    loggedPoint.PlcId |> should equal "PLC001"
    loggedPoint.Value |> should equal writeValue
    loggedPoint.Quality.IsGood |> should equal true

[<Fact>]
let ``ServerScanStatistics should handle empty scan batch`` () =
    let emptyBatch = ScanBatch.Create("BATCH001", "PLC001", "REQ001", [])
    let stats = ServerScanStatistics.FromScanBatch(emptyBatch)
    
    stats.PlcId |> should equal "PLC001"
    stats.TotalTags |> should equal 0
    stats.SuccessfulTags |> should equal 0
    stats.FailedTags |> should equal 0
    stats.ErrorRate |> should equal 0.0
    stats.QualityScore |> should equal 0.0

[<Fact>]
let ``LoggedDataPoint auto-increment ID should be zero for new instances`` () =
    let scanResult = ScanResult.Create("ANY_TAG", "ANY_PLC", ScanOperation.Read, PlcValue.BoolValue(false), DataQuality.Good)
    let loggedPoint = LoggedDataPoint.FromScanResult(scanResult)
    
    // ID should be 0 for new instances (will be auto-incremented by database)
    loggedPoint.Id |> should equal 0L