module Ev2.PLC.Server.Tests.ServerTypesTests

open Xunit
open FsUnit.Xunit
open System
open DSPLCServer.Common
open Ev2.PLC.Common.Types

[<Fact>]
let ``PlcVendor DisplayName should return correct names`` () =
    PlcVendor.CreateAllenBradley().DisplayName |> should haveSubstring "Allen-Bradley"
    PlcVendor.CreateSiemens().DisplayName |> should haveSubstring "Siemens"
    PlcVendor.CreateMitsubishi().DisplayName |> should haveSubstring "Mitsubishi"
    PlcVendor.CreateLSElectric().DisplayName |> should haveSubstring "LS Electric"
    PlcVendor.CreateCustom("Generic").DisplayName |> should haveSubstring "Generic"

[<Fact>]
let ``PlcServerConfig.Create should set correct defaults`` () =
    let connectionConfig = ConnectionConfig.ForTCP("192.168.1.100", 44818)
    let vendor = PlcVendor.CreateSiemens()
    let config = PlcServerConfig.Create("PLC001", vendor, "Test PLC", connectionConfig)

    config.PlcId |> should equal "PLC001"
    config.Vendor.Manufacturer |> should equal "Siemens"
    config.Name |> should equal "Test PLC"
    config.Description |> should equal None
    config.ConnectionConfig |> should equal connectionConfig
    config.IsEnabled |> should equal true
    config.ScanInterval |> should equal (TimeSpan.FromSeconds(1.0))
    config.MaxRetries |> should equal 3
    config.Timeout |> should equal (TimeSpan.FromSeconds(30.0))
    config.Tags |> should be Empty
    config.CreatedAt |> should be (greaterThan (DateTime.UtcNow.AddSeconds(-5.0)))
    config.UpdatedAt |> should be (greaterThan (DateTime.UtcNow.AddSeconds(-5.0)))

[<Fact>]
let ``ServerScanStatistics.FromScanBatch should calculate correct statistics`` () =
    let timestamp1 = DateTime.UtcNow
    let timestamp2 = timestamp1.AddMilliseconds(100.0)
    
    let goodResult = ScanResult.Create("TAG001", "PLC001", ScanOperation.Read, PlcValue.BoolValue(true), DataQuality.Good)
    let badResult = ScanResult.Create("TAG002", "PLC001", ScanOperation.Read, PlcValue.ErrorValue("Error"), DataQuality.Bad("Communication error"))
    
    let results = [goodResult; badResult]
    let batch = ScanBatch.Create("BATCH001", "PLC001", "REQ001", results)
    
    let stats = ServerScanStatistics.FromScanBatch(batch)
    
    stats.PlcId |> should equal "PLC001"
    stats.TotalTags |> should equal 2
    stats.SuccessfulTags |> should equal 1
    stats.FailedTags |> should equal 1
    stats.ErrorRate |> should equal 50.0
    stats.QualityScore |> should equal 50.0  // (100 + 0) / 2

[<Fact>]
let ``ServerHealthStatus.Create should set correct defaults`` () =
    let health = ServerHealthStatus.Create()
    
    health.OverallStatus |> should equal DataQuality.Good
    health.ConnectedPlcs |> should equal 0
    health.TotalPlcs |> should equal 0
    health.ActiveScans |> should equal 0
    health.TotalErrors |> should equal 0
    health.LastUpdateTime |> should be (greaterThan (DateTime.UtcNow.AddSeconds(-5.0)))
    health.Uptime |> should equal TimeSpan.Zero

[<Fact>]
let ``ServerConfiguration.Default should have correct values`` () =
    let config = ServerConfiguration.Default
    
    config.ServerId |> should equal "DSPLCServer"
    config.Port |> should equal 8080
    config.LogLevel |> should equal "Information"
    config.DatabaseConnectionString |> should equal "Data Source=plcserver.db"
    config.PlcConfigurations |> should be Empty
    config.GlobalScanInterval |> should equal (TimeSpan.FromSeconds(1.0))
    config.HealthCheckInterval |> should equal (TimeSpan.FromMinutes(1.0))
    config.MaxConcurrentScans |> should equal 10
    config.EnableDiagnostics |> should equal true
    config.DataRetentionDays |> should equal 30

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
let ``ScanJobPriority should have correct values`` () =
    ScanJobPriority.Low |> int |> should equal 1
    ScanJobPriority.Normal |> int |> should equal 2
    ScanJobPriority.High |> int |> should equal 3
    ScanJobPriority.Critical |> int |> should equal 4

[<Fact>]
let ``ServerEvent discriminated union should work correctly`` () =
    let connected = ServerEvent.PlcConnected("PLC001")
    let disconnected = ServerEvent.PlcDisconnected("PLC001", "Network error")
    let started = ServerEvent.ServerStarted
    let stopped = ServerEvent.ServerStopped
    
    match connected with
    | ServerEvent.PlcConnected plcId -> plcId |> should equal "PLC001"
    | _ -> failwith "Expected PlcConnected"
    
    match disconnected with
    | ServerEvent.PlcDisconnected (plcId, reason) -> 
        plcId |> should equal "PLC001"
        reason |> should equal "Network error"
    | _ -> failwith "Expected PlcDisconnected"
    
    match started with
    | ServerEvent.ServerStarted -> ()
    | _ -> failwith "Expected ServerStarted"
    
    match stopped with
    | ServerEvent.ServerStopped -> ()
    | _ -> failwith "Expected ServerStopped"

[<Fact>]
let ``ServerScanStatistics should handle empty results correctly`` () =
    let emptyBatch = ScanBatch.Create("BATCH001", "PLC001", "REQ001", [])
    let stats = ServerScanStatistics.FromScanBatch(emptyBatch)
    
    stats.TotalTags |> should equal 0
    stats.SuccessfulTags |> should equal 0
    stats.FailedTags |> should equal 0
    stats.ErrorRate |> should equal 0.0
    stats.QualityScore |> should equal 0.0
    stats.AverageResponseTime |> should equal TimeSpan.Zero
    stats.MaxResponseTime |> should equal TimeSpan.Zero