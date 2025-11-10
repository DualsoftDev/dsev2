module Ev2.PLC.Common.Tests.IntegrationTests

open Xunit
open FsUnit.Xunit
open System
open System.Threading.Tasks
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces
// Simple mock for integration tests - inline implementation

[<Fact>]
let ``Complete workflow test - Connect, Read, Write, Disconnect`` () =
    task {
        // Arrange
        let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
        let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
        
        // Act & Assert - Create driver
        match factory.CreateDriver("PLC001", "Mock", config) with
        | Result.Error error -> failwith $"Failed to create driver: {error}"
        | Ok driver ->
            
            // Test initial state
            driver.PlcId |> should equal "PLC001"
            driver.ConnectionStatus |> should equal ConnectionStatus.Disconnected
            driver.ConnectionStatus.IsOperational |> should equal false
            
            // Test connection
            let! connectResult = driver.ConnectAsync()
            connectResult |> should equal (Ok ())
            driver.ConnectionStatus |> should equal ConnectionStatus.Connected
            driver.ConnectionStatus.IsOperational |> should equal true
            
            // Test health check
            let! isHealthy = driver.HealthCheckAsync()
            isHealthy |> should equal true
            
            // Test reading existing tag
            let address1 = PlcAddress.Create("DB1.DBX0.0")
            let boolTag = TagConfiguration.Create("TEST_BOOL", "PLC001", "Test Boolean", address1, PlcDataType.Bool)
            let! readResult = driver.ReadTagAsync(boolTag)
            
            match readResult with
            | Result.Error error -> failwith $"Failed to read tag: {error}"
            | Ok scanResult ->
                scanResult.TagId |> should equal "TEST_BOOL"
                scanResult.PlcId |> should equal "PLC001"
                scanResult.Quality.IsGood |> should equal true
                scanResult.Value |> should equal (PlcValue.BoolValue true)
            
            // Test writing and reading back
            let address2 = PlcAddress.Create("DB1.DBW200")
            let intTag = TagConfiguration.Create("TEST_INT", "PLC001", "Test Integer", address2, PlcDataType.Int16)
            let testValue = PlcValue.Int16Value 12345s
            
            let! writeResult = driver.WriteTagAsync(intTag, testValue)
            writeResult |> should equal (Ok ())
            
            let! readBackResult = driver.ReadTagAsync(intTag)
            match readBackResult with
            | Result.Error error -> failwith $"Failed to read back written value: {error}"
            | Ok scanResult ->
                scanResult.Value |> should equal testValue
                scanResult.Quality.IsGood |> should equal true
            
            // Test batch operations
            let tags = [
                TagConfiguration.Create("BATCH_BOOL", "PLC001", "Batch Boolean", PlcAddress.Create("DB1.DBX0.0"), PlcDataType.Bool)
                TagConfiguration.Create("BATCH_INT", "PLC001", "Batch Integer", PlcAddress.Create("DB1.DBW2"), PlcDataType.Int16)
                TagConfiguration.Create("BATCH_FLOAT", "PLC001", "Batch Float", PlcAddress.Create("DB1.DBD8"), PlcDataType.Float32)
                TagConfiguration.Create("BATCH_STRING", "PLC001", "Batch String", PlcAddress.Create("DB1.DBString12"), PlcDataType.String(100))
            ]
            
            let! batchResult = driver.ReadTagsAsync(tags)
            match batchResult with
            | Result.Error error -> failwith $"Failed to read batch: {error}"
            | Ok batch ->
                batch.PlcId |> should equal "PLC001"
                batch.Results |> should haveLength 4
                batch.Results |> List.forall (fun r -> r.Quality.IsGood) |> should equal true
                
                // Verify specific values
                let boolResult = batch.Results |> List.find (fun r -> r.TagId = "BATCH_BOOL")
                let intResult = batch.Results |> List.find (fun r -> r.TagId = "BATCH_INT") 
                let floatResult = batch.Results |> List.find (fun r -> r.TagId = "BATCH_FLOAT")
                let stringResult = batch.Results |> List.find (fun r -> r.TagId = "BATCH_STRING")
                
                boolResult.Value |> should equal (PlcValue.BoolValue true)
                intResult.Value |> should equal (PlcValue.Int16Value 1234s)
                floatResult.Value |> should equal (PlcValue.Float32Value 3.14f)
                stringResult.Value |> should equal (PlcValue.StringValue "TestString")
            
            // Test scan request
            let tagIds = tags |> List.map (fun t -> t.Id)
            let scanRequest = ScanRequest.Create("SCAN001", "PLC001", tagIds, ScanOperation.Read)
            let! scanResult = driver.ExecuteScanAsync(scanRequest)
            
            match scanResult with
            | Result.Error error -> failwith $"Failed to execute scan: {error}"
            | Ok batch ->
                batch.Results |> should haveLength 4
                batch.Results |> List.forall (fun r -> r.Quality.IsGood) |> should equal true
            
            // Test disconnect
            do! driver.DisconnectAsync()
            driver.ConnectionStatus |> should equal ConnectionStatus.Disconnected
            driver.ConnectionStatus.IsOperational |> should equal false
    }

[<Fact>]
let ``Error handling test - Invalid operations when disconnected`` () =
    task {
        let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
        let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
        
        match factory.CreateDriver("PLC001", "Mock", config) with
        | Result.Error error -> failwith $"Failed to create driver: {error}"
        | Ok driver ->
            
            // Ensure disconnected state
            driver.ConnectionStatus |> should equal ConnectionStatus.Disconnected
            
            // Test read when disconnected
            let address = PlcAddress.Create("DB1.DBX0.0")
            let tag = TagConfiguration.Create("TEST", "PLC001", "Test Tag", address, PlcDataType.Bool)
            let! readResult = driver.ReadTagAsync(tag)
            
            match readResult with
            | Ok _ -> failwith "Expected read to fail when disconnected"
            | Result.Error error -> error |> should contain "not available"
            
            // Test write when disconnected
            let! writeResult = driver.WriteTagAsync(tag, PlcValue.BoolValue false)
            
            match writeResult with
            | Ok _ -> failwith "Expected write to fail when disconnected"
            | Result.Error error -> error |> should contain "not available"
    }

[<Fact>]
let ``Event handling integration test`` () =
    task {
        let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
        let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
        
        match factory.CreateDriver("PLC001", "Mock", config) with
        | Result.Error error -> failwith $"Failed to create driver: {error}"
        | Ok driver ->
            
            let mutable connectionEvents = []
            let mutable scanEvents = []
            
            // Subscribe to events
            driver.ConnectionStateChanged.Add(fun evt ->
                connectionEvents <- evt :: connectionEvents)
            
            driver.ScanCompleted.Add(fun batch ->
                scanEvents <- batch :: scanEvents)
            
            // Trigger connection (should generate events)
            let! _ = driver.ConnectAsync()
            
            // Execute scan (should generate scan event)
            let eventAddress = PlcAddress.Create("DB1.DBX0.0")
            let tags = [TagConfiguration.Create("EVENT_TEST", "PLC001", "Event Test", eventAddress, PlcDataType.Bool)]
            let! _ = driver.ReadTagsAsync(tags)
            
            // Give events time to fire
            do! Task.Delay(50)
            
            // Verify events
            connectionEvents |> should not' (be Empty)
            scanEvents |> should not' (be Empty)
            
            let latestConnectionEvent = connectionEvents |> List.head
            latestConnectionEvent.PlcId |> should equal "PLC001"
            latestConnectionEvent.NewStatus |> should equal ConnectionStatus.Connected
            
            let latestScanEvent = scanEvents |> List.head
            latestScanEvent.PlcId |> should equal "PLC001"
            latestScanEvent.Results |> should not' (be Empty)
    }

[<Fact>]
let ``Performance metrics integration test`` () =
    task {
        let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
        let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
        
        match factory.CreateDriver("PLC001", "Mock", config) with
        | Result.Error error -> failwith $"Failed to create driver: {error}"
        | Ok driver ->
            
            let! _ = driver.ConnectAsync()
            
            // Get initial metrics
            let initialMetrics = driver.GetPerformanceMetrics()
            initialMetrics.PlcId |> should equal "PLC001"
            
            // Execute several operations
            let perfAddress = PlcAddress.Create("DB1.DBX0.0")
            let tag = TagConfiguration.Create("PERF_TEST", "PLC001", "Performance Test", perfAddress, PlcDataType.Bool)
            
            for i in 1..10 do
                let! _ = driver.ReadTagAsync(tag)
                do! Task.Delay(10)
            
            // Note: In a real implementation, metrics would be updated
            // Here we just verify the structure exists
            let finalMetrics = driver.GetPerformanceMetrics()
            finalMetrics.PlcId |> should equal "PLC001"
    }

[<Fact>]
let ``System information integration test`` () =
    task {
        let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
        let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
        
        match factory.CreateDriver("PLC001", "Mock", config) with
        | Result.Error error -> failwith $"Failed to create driver: {error}"
        | Ok driver ->
            
            let! _ = driver.ConnectAsync()
            
            let! systemInfoResult = driver.GetSystemInfoAsync()
            
            match systemInfoResult with
            | Result.Error error -> failwith $"Failed to get system info: {error}"
            | Ok systemInfo ->
                systemInfo.PlcId |> should equal "PLC001"
                systemInfo.CpuType |> should equal (Some "MockPLC")
                systemInfo.FirmwareVersion |> should equal (Some "1.0.0")
                systemInfo.SoftwareVersion |> should equal (Some "Mock Driver")
    }

[<Fact>]
let ``Tag validation integration test`` () =
    let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
    let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
    
    match factory.CreateDriver("PLC001", "Mock", config) with
    | Result.Error error -> failwith $"Failed to create driver: {error}"
    | Ok driver ->
        
        // Test valid configurations
        let validTags = [
            TagConfiguration.Create("BOOL_TAG", "PLC001", "Boolean Tag", PlcAddress.Create("DB1.DBX0.0"), PlcDataType.Bool)
            TagConfiguration.Create("INT_TAG", "PLC001", "Integer Tag", PlcAddress.Create("DB1.DBW2"), PlcDataType.Int16)
            TagConfiguration.Create("FLOAT_TAG", "PLC001", "Float Tag", PlcAddress.Create("DB1.DBD4"), PlcDataType.Float32)
        ]
        
        for tag in validTags do
            driver.ValidateTagConfiguration(tag) |> should equal (Ok ())
        
        // Test invalid configurations
        let invalidTags = [
            TagConfiguration.Create("", "PLC001", "Invalid Tag", PlcAddress.Create("DB1.DBX0.0"), PlcDataType.Bool)  // Empty ID
            TagConfiguration.Create("INVALID", "PLC001", "Invalid Tag", PlcAddress.Create(""), PlcDataType.Bool)     // Empty address
        ]
        
        for tag in invalidTags do
            match driver.ValidateTagConfiguration(tag) with
            | Ok _ -> failwith $"Expected validation to fail for tag: {tag.Id}"
            | Result.Error _ -> () // Expected

[<Fact>]
let ``Supported data types integration test`` () =
    let factory = new MockPlcDriverFactory() :> IPlcDriverFactory
    let config = ConnectionConfig.ForTCP("192.168.1.100", 44818)
    
    match factory.CreateDriver("PLC001", "Mock", config) with
    | Result.Error error -> failwith $"Failed to create driver: {error}"
    | Ok driver ->
        
        let supportedTypes = driver.GetSupportedDataTypes()
        
        supportedTypes |> should contain PlcDataType.Bool
        supportedTypes |> should contain PlcDataType.UInt8
        supportedTypes |> should contain PlcDataType.Int16
        supportedTypes |> should contain PlcDataType.Int32
        supportedTypes |> should contain PlcDataType.Int64
        supportedTypes |> should contain PlcDataType.Float32
        supportedTypes |> should contain PlcDataType.Float64
        (supportedTypes |> List.exists (function PlcDataType.String _ -> true | _ -> false)) |> should equal true
        
        supportedTypes |> should not' (be Empty)