module Ev2.PLC.Common.Tests.MockDriverTests

open Xunit
open FsUnit.Xunit
open System
open System.Threading.Tasks
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces

/// Mock PLC Driver for testing
type MockPlcDriver(plcId: string, connectionConfig: ConnectionConfig) =
    let mutable connectionStatus = ConnectionStatus.Disconnected
    let mutable isDisposed = false
    let connectionStateChangedEvent = Event<ConnectionStateChangedEvent>()
    let tagValueChangedEvent = Event<ScanResult>()
    let scanCompletedEvent = Event<ScanBatch>()
    let diagnosticEvent = Event<DiagnosticEvent>()
    let errorOccurredEvent = Event<string * exn option>()
    
    // Mock data storage
    let mutableTags = System.Collections.Concurrent.ConcurrentDictionary<string, PlcValue>()
    
    // Initialize with some test data
    do
        mutableTags.TryAdd("DB1.DBX0.0", PlcValue.BoolValue true) |> ignore
        mutableTags.TryAdd("DB1.DBW2", PlcValue.Int16Value 1234s) |> ignore
        mutableTags.TryAdd("DB1.DBD4", PlcValue.Int32Value 56789) |> ignore
        mutableTags.TryAdd("DB1.DBD8", PlcValue.Float32Value 3.14f) |> ignore
        mutableTags.TryAdd("DB1.DBString12", PlcValue.StringValue "TestString") |> ignore
    
    member this.SetTagValue(address: string, value: PlcValue) =
        mutableTags.AddOrUpdate(address, value, fun _ _ -> value) |> ignore
    
    member this.SimulateConnectionLoss() =
        let oldStatus = connectionStatus
        connectionStatus <- ConnectionStatus.Disconnected
        let event = ConnectionStateChangedEvent.Create(plcId, oldStatus, connectionStatus)
        connectionStateChangedEvent.Trigger(event)
    
    interface IPlcDriver with
        member this.PlcId = plcId
        member this.ConnectionStatus = connectionStatus
        
        member this.ConnectionInfo = ConnectionInfo.Create(plcId, connectionConfig)
        
        member this.Diagnostics = PlcDiagnostics.Create(plcId)
        member this.Capabilities = DriverCapabilities.Default
        
        member this.ConnectAsync() =
            task {
                if connectionStatus <> ConnectionStatus.Connected then
                    let oldStatus = connectionStatus
                    connectionStatus <- ConnectionStatus.Connecting
                    connectionStateChangedEvent.Trigger(ConnectionStateChangedEvent.Create(plcId, oldStatus, connectionStatus))
                    
                    // Simulate connection delay
                    do! Task.Delay(100)
                    
                    connectionStatus <- ConnectionStatus.Connected
                    connectionStateChangedEvent.Trigger(ConnectionStateChangedEvent.Create(plcId, connectionStatus, connectionStatus))
                    
                    return Ok ()
                else
                    return Ok ()
            }
        
        member this.DisconnectAsync() =
            task {
                if connectionStatus <> ConnectionStatus.Disconnected then
                    let oldStatus = connectionStatus
                    connectionStatus <- ConnectionStatus.Disconnected
                    connectionStateChangedEvent.Trigger(ConnectionStateChangedEvent.Create(plcId, oldStatus, connectionStatus))
            }
        
        member this.HealthCheckAsync() =
            task {
                // Simulate health check
                do! Task.Delay(50)
                return connectionStatus.IsOperational
            }
        
        member this.ResetConnectionAsync() =
            task {
                let! _ = (this :> IPlcDriver).DisconnectAsync()
                do! Task.Delay(100)
                return! (this :> IPlcDriver).ConnectAsync()
            }
        
        member this.ReadTagAsync(tagConfig) =
            task {
                if not connectionStatus.CanRead then
                    return Result.Error "Connection not available for reading"
                else
                    match mutableTags.TryGetValue(tagConfig.Address.Raw) with
                    | (true, value) ->
                        let result = ScanResult.Create(tagConfig.Id, plcId, ScanOperation.Read, value, DataQuality.Good)
                        return Ok result
                    | (false, _) ->
                        let errorValue = PlcValue.ErrorValue "Tag not found"
                        let badQuality = DataQuality.Bad "Tag not found"
                        let errorResult = ScanResult.Create(tagConfig.Id, plcId, ScanOperation.Read, errorValue, badQuality)
                        return Ok errorResult
            }
        
        member this.WriteTagAsync(tagConfig, value) =
            task {
                if not connectionStatus.CanWrite then
                    return Result.Error "Connection not available for writing"
                else
                    mutableTags.AddOrUpdate(tagConfig.Address.Raw, value, fun _ _ -> value) |> ignore
                    return Ok ()
            }
        
        member this.ReadTagsAsync(tagConfigs) =
            task {
                if not connectionStatus.CanRead then
                    return Result.Error "Connection not available for reading"
                else
                    let results = 
                        tagConfigs
                        |> List.map (fun tag ->
                            match mutableTags.TryGetValue(tag.Address.Raw) with
                            | (true, value) ->
                                ScanResult.Create(tag.Id, plcId, ScanOperation.Read, value, DataQuality.Good)
                            | (false, _) ->
                                let errorValue = PlcValue.ErrorValue "Tag not found"
                                let badQuality = DataQuality.Bad "Tag not found"
                                ScanResult.Create(tag.Id, plcId, ScanOperation.Read, errorValue, badQuality))
                    
                    let batch = ScanBatch.Create(System.Guid.NewGuid().ToString(), plcId, "BATCH_READ", results)
                    scanCompletedEvent.Trigger(batch)
                    return Ok batch
            }
        
        member this.WriteTagsAsync(tagValues) =
            task {
                if not connectionStatus.CanWrite then
                    return Result.Error "Connection not available for writing"
                else
                    for (tag, value) in tagValues do
                        mutableTags.AddOrUpdate(tag.Address.Raw, value, fun _ _ -> value) |> ignore
                    return Ok ()
            }
        
        member this.ExecuteScanAsync(request) =
            task {
                // Convert TagIds to TagConfiguration objects for processing
                // For mock testing, we'll create dummy TagConfigurations
                let tagConfigs = request.TagIds |> List.map (fun tagId ->
                    let address = PlcAddress.Create(tagId)
                    TagConfiguration.Create(tagId, plcId, tagId, address, PlcDataType.String(100)))
                let! result = (this :> IPlcDriver).ReadTagsAsync(tagConfigs)
                return result
            }
        
        member this.ExecuteBatchScanAsync(requests) =
            task {
                let batches = ResizeArray<ScanBatch>()
                for request in requests do
                    let! result = (this :> IPlcDriver).ExecuteScanAsync(request)
                    match result with
                    | Ok batch -> batches.Add(batch)
                    | Result.Error _ -> ()
                return Ok (batches |> List.ofSeq)
            }
        
        member this.ValidateTagConfiguration(tagConfig) =
            if String.IsNullOrWhiteSpace(tagConfig.Address.Raw) then
                Result.Error "Tag address cannot be empty"
            elif String.IsNullOrWhiteSpace(tagConfig.Id) then
                Result.Error "Tag ID cannot be empty"
            else
                Ok ()
        
        member this.GetSupportedDataTypes() = 
            [PlcDataType.Bool; PlcDataType.UInt8; PlcDataType.Int16; PlcDataType.Int32; PlcDataType.Int64; 
             PlcDataType.Float32; PlcDataType.Float64; PlcDataType.String(100)]
        
        // Missing interface methods
        member this.ParseAddress(addressString: string) = 
            Ok (PlcAddress.Create(addressString))
        
        member this.ValidateAddress(address: PlcAddress) = 
            if String.IsNullOrWhiteSpace(address.Raw) then Result.Error "Invalid address"
            else Ok ()
        
        member this.SubscribeAsync(subscription: TagSubscription) = 
            task { return Result.Error "Subscriptions not implemented in mock" }
        
        member this.UnsubscribeAsync(subscriptionId: string) = 
            task { return Result.Error "Subscriptions not implemented in mock" }
        
        member this.GetSubscriptions() = []
        
        member this.UpdateDiagnosticsAsync() = Task.FromResult(())
        member this.GetPerformanceMetrics() = PerformanceMetrics.Empty(plcId)
        member this.GetSystemInfoAsync() = 
            task {
                let systemInfo = PlcSystemInfo.Empty(plcId)
                let updatedInfo = 
                    { systemInfo with 
                        CpuType = Some "MockPLC"
                        FirmwareVersion = Some "1.0.0"
                        SoftwareVersion = Some "Mock Driver" }
                return Ok updatedInfo
            }
        
        // Events
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

/// Mock PLC Driver Factory
type MockPlcDriverFactory() =
    interface IPlcDriverFactory with
        member this.SupportedVendors = ["Mock"; "Test"]
        
        member this.IsVendorSupported(vendor) = 
            ["Mock"; "Test"] |> List.contains vendor
        
        member this.CreateDriver(plcId, vendor, connectionConfig) =
            if (this :> IPlcDriverFactory).IsVendorSupported(vendor) then
                Ok (new MockPlcDriver(plcId, connectionConfig) :> IPlcDriver)
            else
                Result.Error $"Vendor {vendor} not supported"
        
        member this.CreateAdvancedDriver(plcId, vendor, connectionConfig) =
            Result.Error "Advanced driver not supported for mock"
        
        member this.GetCapabilities(vendor) =
            if (this :> IPlcDriverFactory).IsVendorSupported(vendor) then
                Ok DriverCapabilities.Default
            else
                Result.Error $"Vendor {vendor} not supported"
        
        member this.CreateDriverFromConnectionString(plcId, vendor, connectionString) =
            Result.Error "Connection string parsing not implemented"
        
        member this.ValidateConfiguration(vendor, connectionConfig) =
            if (this :> IPlcDriverFactory).IsVendorSupported(vendor) then Ok ()
            else Result.Error $"Vendor {vendor} not supported"

// Tests
[<Fact>]
let ``MockPlcDriver should connect successfully`` () =
    task {
        let config = ConnectionConfig.ForTCP("localhost", 502)
        let driver = new MockPlcDriver("TEST001", config) :> IPlcDriver
        
        driver.ConnectionStatus |> should equal ConnectionStatus.Disconnected
        
        let! result = driver.ConnectAsync()
        
        result |> should equal (Ok ())
        driver.ConnectionStatus |> should equal ConnectionStatus.Connected
        driver.ConnectionStatus.IsOperational |> should equal true
    }

[<Fact>]
let ``MockPlcDriver should read tags correctly`` () =
    task {
        let config = ConnectionConfig.ForTCP("localhost", 502)
        let driver = new MockPlcDriver("TEST001", config) :> IPlcDriver
        
        let! _ = driver.ConnectAsync()
        
        let address = PlcAddress.Create("DB1.DBX0.0")
        let tagConfig = TagConfiguration.Create("TEST_BOOL", "TEST001", "Test Boolean", address, PlcDataType.Bool)
        let! result = driver.ReadTagAsync(tagConfig)
        
        match result with
        | Ok scanResult ->
            scanResult.TagId |> should equal "TEST_BOOL"
            scanResult.PlcId |> should equal "TEST001"
            scanResult.Value |> should equal (PlcValue.BoolValue true)
            scanResult.Quality.IsGood |> should equal true
        | Result.Error error ->
            failwith $"Expected successful read, got error: {error}"
    }

[<Fact>]
let ``MockPlcDriver should write tags correctly`` () =
    task {
        let config = ConnectionConfig.ForTCP("localhost", 502)
        let mockDriver = new MockPlcDriver("TEST001", config)
        let driver = mockDriver :> IPlcDriver
        
        let! _ = driver.ConnectAsync()
        
        let address = PlcAddress.Create("DB1.DBW100")
        let tagConfig = TagConfiguration.Create("TEST_INT", "TEST001", "Test Integer", address, PlcDataType.Int16)
        let newValue = PlcValue.Int16Value 9999s
        
        let! writeResult = driver.WriteTagAsync(tagConfig, newValue)
        writeResult |> should equal (Ok ())
        
        let! readResult = driver.ReadTagAsync(tagConfig)
        match readResult with
        | Ok scanResult ->
            scanResult.Value |> should equal newValue
        | Result.Error error ->
            failwith $"Expected successful read after write, got error: {error}"
    }

[<Fact>]
let ``MockPlcDriver should handle connection events`` () =
    task {
        let config = ConnectionConfig.ForTCP("localhost", 502)
        let driver = new MockPlcDriver("TEST001", config) :> IPlcDriver
        
        let mutable eventReceived = false
        let mutable eventData = None
        
        driver.ConnectionStateChanged.Add(fun evt ->
            eventReceived <- true
            eventData <- Some evt)
        
        let! _ = driver.ConnectAsync()
        
        // Give event time to fire
        do! Task.Delay(10)
        
        eventReceived |> should equal true
        match eventData with
        | Some evt ->
            evt.PlcId |> should equal "TEST001"
            evt.NewStatus |> should equal ConnectionStatus.Connected
        | None ->
            failwith "Expected connection event data"
    }

[<Fact>]
let ``MockPlcDriverFactory should create drivers correctly`` () =
    let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
    let config = ConnectionConfig.ForTCP("localhost", 502)
    
    factory.SupportedVendors |> should contain "Mock"
    factory.IsVendorSupported("Mock") |> should equal true
    factory.IsVendorSupported("InvalidVendor") |> should equal false
    
    match factory.CreateDriver("TEST001", "Mock", config) with
    | Ok driver ->
        driver.PlcId |> should equal "TEST001"
        driver.ConnectionInfo.Config |> should equal config
    | Result.Error error ->
        failwith $"Expected successful driver creation, got error: {error}"

[<Fact>]
let ``MockPlcDriver should validate tag configurations`` () =
    let config = ConnectionConfig.ForTCP("localhost", 502)
    let driver = new MockPlcDriver("TEST001", config) :> IPlcDriver
    
    let address1 = PlcAddress.Create("DB1.DBX0.0")
    let address2 = PlcAddress.Create("")
    let validTag = TagConfiguration.Create("VALID", "TEST001", "Valid Tag", address1, PlcDataType.Bool)
    let invalidTag1 = TagConfiguration.Create("", "TEST001", "Invalid Tag", address1, PlcDataType.Bool)  // Empty ID
    let invalidTag2 = TagConfiguration.Create("INVALID", "TEST001", "Invalid Tag", address2, PlcDataType.Bool)    // Empty address
    
    driver.ValidateTagConfiguration(validTag) |> should equal (Ok ())
    
    match driver.ValidateTagConfiguration(invalidTag1) with
    | Result.Error _ -> () // Expected
    | Ok _ -> failwith "Expected validation error for empty tag ID"
    
    match driver.ValidateTagConfiguration(invalidTag2) with
    | Result.Error _ -> () // Expected  
    | Ok _ -> failwith "Expected validation error for empty tag address"