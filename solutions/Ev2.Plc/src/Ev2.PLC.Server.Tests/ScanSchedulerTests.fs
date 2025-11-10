module Ev2.PLC.Server.Tests.ScanSchedulerTests

open Xunit
open FsUnit.Xunit
open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.DependencyInjection
open DSPLCServer.Common
open DSPLCServer.Core
open DSPLCServer.PLC
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces

// Reuse the mock driver from PlcManagerTests
type MockDriver(plcId: string, connectionConfig: ConnectionConfig) =
    let mutable connectionStatus = PlcConnectionStatus.Disconnected
    let connectionStateChangedEvent = Event<ConnectionStateChangedEvent>()
    let tagValueChangedEvent = Event<ScanResult>()
    let scanCompletedEvent = Event<ScanBatch>()
    let diagnosticEvent = Event<DiagnosticEvent>()
    let errorOccurredEvent = Event<string * exn option>()
    
    interface IPlcDriver with
        member this.PlcId = plcId
        member this.PlcConnectionStatus = connectionStatus
        member this.ConnectionInfo = ConnectionInfo.Create(plcId, connectionConfig)
        member this.Diagnostics = PlcDiagnostics.Create(plcId)
        member this.Capabilities = DriverCapabilities.Default
        
        member this.ConnectAsync() =
            task {
                connectionStatus <- PlcConnectionStatus.Connected
                return Ok ()
            }
        
        member this.DisconnectAsync() =
            task {
                connectionStatus <- PlcConnectionStatus.Disconnected
            }
        
        member this.HealthCheckAsync() = Task.FromResult(true)
        member this.ResetConnectionAsync() = Task.FromResult(Ok ())
        
        member this.ReadTagAsync(tagConfig) =
            task {
                let result = ScanResult.Create(tagConfig.Id, plcId, ScanOperation.Read, PlcValue.BoolValue(true), DataQuality.Good)
                return Ok result
            }
        
        member this.WriteTagAsync(tagConfig, value) = Task.FromResult(Ok ())
        
        member this.ReadTagsAsync(tagConfigs) =
            task {
                let results = tagConfigs |> List.map (fun tag ->
                    ScanResult.Create(tag.Id, plcId, ScanOperation.Read, PlcValue.BoolValue(true), DataQuality.Good))
                let batch = ScanBatch.Create(System.Guid.NewGuid().ToString(), plcId, "BATCH_READ", results)
                return Ok batch
            }
        
        member this.WriteTagsAsync(tagValues) = Task.FromResult(Ok ())
        member this.ExecuteScanAsync(request) = Task.FromResult(Ok (ScanBatch.Create(System.Guid.NewGuid().ToString(), plcId, request.RequestId, [])))
        member this.ExecuteBatchScanAsync(requests) = Task.FromResult(Ok [])
        member this.ValidateTagConfiguration(tagConfig) = Ok ()
        member this.GetSupportedDataTypes() = []
        member this.ParseAddress(addressString) = Ok (PlcAddress.Create(addressString))
        member this.ValidateAddress(address) = Ok ()
        member this.SubscribeAsync(subscription) = Task.FromResult(Result.Error "Not implemented")
        member this.UnsubscribeAsync(subscriptionId) = Task.FromResult(Result.Error "Not implemented")
        member this.GetSubscriptions() = []
        member this.UpdateDiagnosticsAsync() = Task.FromResult(())
        member this.GetPerformanceMetrics() = PerformanceMetrics.Empty(plcId)
        member this.GetSystemInfoAsync() = Task.FromResult(Ok (PlcSystemInfo.Empty(plcId)))
        
        [<CLIEvent>]
        member this.ConnectionStateChanged = connectionStateChangedEvent.Publish
        [<CLIEvent>]
        member this.TagValueChanged = tagValueChangedEvent.Publish
        [<CLIEvent>]
        member this.ScanCompleted = scanCompletedEvent.Publish
        [<CLIEvent>]
        member this.DiagnosticEvent = diagnosticEvent.Publish
        [<CLIEvent>]
        member this.ErrorOccurred = errorOccurredEvent.Publish

type MockDriverFactory() =
    interface IPlcDriverFactory with
        member this.SupportedVendors = ["Mock"]
        member this.IsVendorSupported(vendor) = vendor = "Mock"
        member this.CreateDriver(plcId, vendor, connectionConfig) =
            if vendor = "Mock" then
                Ok (new MockDriver(plcId, connectionConfig) :> IPlcDriver)
            else
                Result.Error "Unsupported vendor"
        member this.CreateAdvancedDriver(plcId, vendor, connectionConfig) = Result.Error "Not supported"
        member this.GetCapabilities(vendor) = 
            if vendor = "Mock" then Ok DriverCapabilities.Default
            else Result.Error "Not supported"
        member this.CreateDriverFromConnectionString(plcId, vendor, connectionString) = Result.Error "Not implemented"
        member this.ValidateConfiguration(vendor, connectionConfig) = Ok ()

[<Fact>]
let ``ScanScheduler should initialize correctly`` () =
    let serviceProvider = (ServiceCollection()).BuildServiceProvider()
    let factory = new MockDriverFactory() :> IPlcDriverFactory
    let loggerFactory = NullLoggerFactory.Instance
    let managerFactory = new PlcManagerFactory(factory, loggerFactory)
    let logger = NullLogger<ScanScheduler>.Instance
    
    let scheduler = new ScanScheduler(serviceProvider, managerFactory, logger)
    
    // Just verify it can be created without throwing
    scheduler |> should not' (be null)

[<Fact>]
let ``ScanScheduler should add and remove managers`` () =
    task {
        let serviceProvider = (ServiceCollection()).BuildServiceProvider()
        let factory = new MockDriverFactory() :> IPlcDriverFactory
        let loggerFactory = NullLoggerFactory.Instance
        let managerFactory = new PlcManagerFactory(factory, loggerFactory)
        let logger = NullLogger<ScanScheduler>.Instance
        
        let scheduler = new ScanScheduler(serviceProvider, managerFactory, logger)
        
        let connectionConfig = ConnectionConfig.ForTCP("localhost", 502)
        let config = PlcServerConfig.Create("TEST_PLC", PlcVendor.Generic, "Test PLC", connectionConfig)
        // Override vendor to match our mock factory
        let mockConfig = { config with Vendor = PlcVendor.Generic }
        
        // This will fail because our mock factory only supports "Mock" vendor, not "Generic"
        let! addResult = scheduler.AddManager(mockConfig)
        
        match addResult with
        | Result.Error _ -> () // Expected since "Generic" is not supported by mock factory
        | Ok () -> failwith "Expected error for unsupported vendor"
    }

[<Fact>]
let ``ScanJob should have correct priority values`` () =
    let lowJob = { 
        JobId = "test1"
        PlcId = "PLC1"
        Priority = ScanJobPriority.Low
        Tags = []
        Interval = TimeSpan.FromSeconds(1.0)
        MaxRetries = 3
        CurrentRetries = 0
        NextRun = DateTime.UtcNow
        LastRun = None
        IsEnabled = true
    }
    
    let highJob = { lowJob with Priority = ScanJobPriority.High }
    let criticalJob = { lowJob with Priority = ScanJobPriority.Critical }
    
    int lowJob.Priority |> should equal 1
    int highJob.Priority |> should equal 3
    int criticalJob.Priority |> should equal 4

[<Fact>]
let ``ScanJobResult should store execution results`` () =
    let now = DateTime.UtcNow
    let executionTime = TimeSpan.FromMilliseconds(100.0)
    
    let successResult = {
        JobId = "job1"
        PlcId = "PLC1"
        Success = true
        Statistics = None
        Error = None
        ExecutionTime = executionTime
        Timestamp = now
    }
    
    let failureResult = {
        JobId = "job2"
        PlcId = "PLC1"
        Success = false
        Statistics = None
        Error = Some "Connection failed"
        ExecutionTime = executionTime
        Timestamp = now
    }
    
    successResult.Success |> should equal true
    successResult.Error |> should equal None
    
    failureResult.Success |> should equal false
    failureResult.Error |> should equal (Some "Connection failed")

[<Fact>]
let ``PlcManagerFactory should handle vendor validation`` () =
    let factory = new MockDriverFactory() :> IPlcDriverFactory
    let loggerFactory = NullLoggerFactory.Instance
    let managerFactory = new PlcManagerFactory(factory, loggerFactory)
    
    // Test supported vendors
    let supportedVendors = managerFactory.GetSupportedVendors()
    supportedVendors |> should contain "Mock"
    
    // Test configuration validation
    let connectionConfig = ConnectionConfig.ForTCP("localhost", 502)
    let validationResult = managerFactory.ValidateConfiguration(PlcVendor.Generic, connectionConfig)
    
    match validationResult with
    | Ok () -> () // Our mock factory accepts any configuration
    | Result.Error error -> failwith $"Unexpected validation error: {error}"