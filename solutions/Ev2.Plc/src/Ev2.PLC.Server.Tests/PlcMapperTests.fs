module Ev2.PLC.Server.Tests.PlcMapperTests

open Xunit
open FsUnit.Xunit
open System
open Microsoft.Extensions.Logging.Abstractions
open DSPLCServer.Core
open DSPLCServer.Common
open Ev2.PLC.Common.Types

[<Fact>]
let ``TagMapping should store all required fields`` () =
    let address = PlcAddress.Create("D100")
    let mapping = {
        LogicalTagId = "TANK_LEVEL"
        PhysicalAddress = address
        DataType = PlcDataType.Float32
        ScaleFactor = Some 0.1
        Offset = Some 10.0
        Description = Some "Tank water level sensor"
        IsReadOnly = false
        Group = Some "Sensors"
    }
    
    mapping.LogicalTagId |> should equal "TANK_LEVEL"
    mapping.PhysicalAddress |> should equal address
    mapping.DataType |> should equal PlcDataType.Float32
    mapping.ScaleFactor |> should equal (Some 0.1)
    mapping.Offset |> should equal (Some 10.0)
    mapping.Description |> should equal (Some "Tank water level sensor")
    mapping.IsReadOnly |> should equal false
    mapping.Group |> should equal (Some "Sensors")

[<Fact>]
let ``AddressMapping should support pattern matching`` () =
    let mapping = {
        LogicalPattern = "TANK_*"
        PhysicalTemplate = "D{0}"
        DataTypeMapping = Map [("Float32", PlcDataType.Float32)]
        DefaultDataType = PlcDataType.Int32
        ScaleFactor = Some 0.01
        Offset = None
    }
    
    mapping.LogicalPattern |> should equal "TANK_*"
    mapping.PhysicalTemplate |> should equal "D{0}"
    mapping.DefaultDataType |> should equal PlcDataType.Int32

[<Fact>]
let ``VendorAddressFormat should contain patterns for all data types`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let siemensFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Siemens)
    
    siemensFormat.Vendor |> should equal PlcVendor.Siemens
    siemensFormat.DefaultPattern |> should equal "MW{0}"
    siemensFormat.AddressPatterns.ContainsKey(PlcDataType.Bool) |> should equal true
    siemensFormat.AddressPatterns.ContainsKey(PlcDataType.Int16) |> should equal true
    siemensFormat.AddressPatterns.ContainsKey(PlcDataType.Int32) |> should equal true
    siemensFormat.AddressPatterns.ContainsKey(PlcDataType.Float32) |> should equal true

[<Fact>]
let ``PlcMapperFactory should create mappers for different vendors`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    
    let siemensFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Siemens)
    let mitsubishiFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    let allenBradleyFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.AllenBradley)
    let lsElectricFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.LSElectric)
    
    siemensFormat.Vendor |> should equal PlcVendor.Siemens
    mitsubishiFormat.Vendor |> should equal PlcVendor.Mitsubishi
    allenBradleyFormat.Vendor |> should equal PlcVendor.AllenBradley
    lsElectricFormat.Vendor |> should equal PlcVendor.LSElectric
    
    // Check vendor-specific patterns
    siemensFormat.AddressPatterns.[PlcDataType.Bool] |> should equal "M{0}"
    mitsubishiFormat.AddressPatterns.[PlcDataType.Int16] |> should equal "D{0}"
    allenBradleyFormat.AddressPatterns.[PlcDataType.Int32] |> should startWith "Program:MainProgram"
    lsElectricFormat.AddressPatterns.[PlcDataType.Float32] |> should equal "D{0}"

[<Fact>]
let ``PlcMapper should perform explicit tag mapping`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let physicalAddress = PlcAddress.Create("D200")
    let tagMapping = {
        LogicalTagId = "MOTOR_SPEED"
        PhysicalAddress = physicalAddress
        DataType = PlcDataType.Int32
        ScaleFactor = Some 10.0
        Offset = Some 0.0
        Description = Some "Motor speed setpoint"
        IsReadOnly = false
        Group = Some "Motors"
    }
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = [tagMapping]
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = false
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("MOTOR_SPEED", "PLC001", "Motor Speed", PlcAddress.Create("MOTOR_SPEED"), PlcDataType.Float32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok result ->
            result.LogicalTag.Id |> should equal "MOTOR_SPEED"
            result.PhysicalTag.Address |> should equal physicalAddress
            result.PhysicalTag.DataType |> should equal PlcDataType.Int32
            result.IsAutoMapped |> should equal false
            result.ScaleFactor |> should equal (Some 10.0)
        | Result.Error error -> failwith $"Mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should perform automatic mapping with patterns`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let addressMapping = {
        LogicalPattern = "SENSOR_*"
        PhysicalTemplate = "D100"
        DataTypeMapping = Map [("Float32", PlcDataType.Float32)]
        DefaultDataType = PlcDataType.Int16
        ScaleFactor = Some 0.1
        Offset = Some 0.0
    }
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = []
        AddressMappings = [addressMapping]
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("SENSOR_01", "PLC001", "Sensor 1", PlcAddress.Create("SENSOR_01"), PlcDataType.Float32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok result ->
            result.LogicalTag.Id |> should equal "SENSOR_01"
            result.PhysicalTag.Address.Raw |> should equal "D100"
            result.IsAutoMapped |> should equal true
            result.ScaleFactor |> should equal (Some 0.1)
        | Result.Error error -> failwith $"Auto-mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should handle direct mapping when no rules match`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("UNMAPPED_TAG", "PLC001", "Unmapped Tag", PlcAddress.Create("D100"), PlcDataType.Int32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok result ->
            result.LogicalTag.Id |> should equal "UNMAPPED_TAG"
            result.PhysicalTag.Address |> should equal logicalTag.Address
            result.PhysicalTag.DataType |> should equal logicalTag.DataType
            result.IsAutoMapped |> should equal false
            result.ScaleFactor |> should equal None
        | Result.Error error -> failwith $"Direct mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should convert values with scaling for read operations`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("TEST_TAG", "PLC001", "Test Tag", PlcAddress.Create("D100"), PlcDataType.Int32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok mappingResult ->
            // Simulate mapping with scaling
            let resultWithScaling = { mappingResult with ScaleFactor = Some 0.1; Offset = Some 10.0 }
            
            // Test physical to logical conversion (read operation)
            let physicalValue = PlcValue.Int32Value(1000)
            let conversionResult = mapper.ConvertPhysicalToLogical(physicalValue, resultWithScaling)
            
            conversionResult.OriginalValue |> should equal physicalValue
            conversionResult.IsConverted |> should equal true
            conversionResult.ScaleFactor |> should equal (Some 0.1)
            conversionResult.Offset |> should equal (Some 10.0)
            
            // Expected: (1000 * 0.1) + 10.0 = 110
            match conversionResult.ConvertedValue with
            | PlcValue.Int32Value(value) -> value |> should equal 110
            | _ -> failwith "Expected Int32Value"
        | Result.Error error -> failwith $"Mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should convert values with scaling for write operations`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("TEST_TAG", "PLC001", "Test Tag", PlcAddress.Create("D100"), PlcDataType.Int32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok mappingResult ->
            // Simulate mapping with scaling
            let resultWithScaling = { mappingResult with ScaleFactor = Some 0.1; Offset = Some 10.0 }
            
            // Test logical to physical conversion (write operation)
            let logicalValue = PlcValue.Int32Value(110)
            let conversionResult = mapper.ConvertLogicalToPhysical(logicalValue, resultWithScaling)
            
            conversionResult.OriginalValue |> should equal logicalValue
            conversionResult.IsConverted |> should equal true
            conversionResult.ScaleFactor |> should equal (Some 0.1)
            conversionResult.Offset |> should equal (Some 10.0)
            
            // Expected: (110 - 10.0) / 0.1 = 1000
            match conversionResult.ConvertedValue with
            | PlcValue.Int32Value(value) -> value |> should equal 1000
            | _ -> failwith "Expected Int32Value"
        | Result.Error error -> failwith $"Mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should handle Float32 scaling correctly`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Siemens)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Siemens
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Float32
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("PRESSURE", "PLC001", "Pressure Sensor", PlcAddress.Create("MD100"), PlcDataType.Float32)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok mappingResult ->
            // Simulate mapping with scaling for pressure sensor (bar to kPa)
            let resultWithScaling = { mappingResult with ScaleFactor = Some 100.0; Offset = None }
            
            // Test conversion
            let physicalValue = PlcValue.Float32Value(1.5f) // 1.5 bar
            let conversionResult = mapper.ConvertPhysicalToLogical(physicalValue, resultWithScaling)
            
            // Expected: 1.5 * 100.0 = 150.0 kPa
            match conversionResult.ConvertedValue with
            | PlcValue.Float32Value(value) -> value |> should equal 150.0f
            | _ -> failwith "Expected Float32Value"
        | Result.Error error -> failwith $"Mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should handle values without scaling`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.AllenBradley)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.AllenBradley
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = true
        DefaultDataType = PlcDataType.Bool
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("MOTOR_RUN", "PLC001", "Motor Running", PlcAddress.Create("Program:MainProgram.MotorRun"), PlcDataType.Bool)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok mappingResult ->
            // No scaling for boolean values
            let boolValue = PlcValue.BoolValue(true)
            let conversionResult = mapper.ConvertPhysicalToLogical(boolValue, mappingResult)
            
            conversionResult.OriginalValue |> should equal boolValue
            conversionResult.ConvertedValue |> should equal boolValue
            conversionResult.IsConverted |> should equal false
            conversionResult.ScaleFactor |> should equal None
            conversionResult.Offset |> should equal None
        | Result.Error error -> failwith $"Mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should cache mapping results`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.LSElectric)
    
    let tagMapping = {
        LogicalTagId = "CACHE_TEST"
        PhysicalAddress = PlcAddress.Create("D500")
        DataType = PlcDataType.Int16
        ScaleFactor = None
        Offset = None
        Description = None
        IsReadOnly = false
        Group = None
    }
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.LSElectric
        TagMappings = [tagMapping]
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = false
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("CACHE_TEST", "PLC001", "Cache Test", PlcAddress.Create("CACHE_TEST"), PlcDataType.Int16)
        
        // First mapping call
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok result1 ->
            // Second mapping call (should use cache)
            match mapper.MapLogicalToPhysical(logicalTag) with
            | Result.Ok result2 ->
                result1.LogicalTag.Id |> should equal result2.LogicalTag.Id
                result1.PhysicalTag.Address |> should equal result2.PhysicalTag.Address
                result1.IsAutoMapped |> should equal result2.IsAutoMapped
            | Result.Error error -> failwith $"Second mapping failed: {error}"
        | Result.Error error -> failwith $"First mapping failed: {error}"
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should get tag mapping by ID`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
    
    let tagMapping = {
        LogicalTagId = "LOOKUP_TEST"
        PhysicalAddress = PlcAddress.Create("D600")
        DataType = PlcDataType.Float32
        ScaleFactor = Some 0.01
        Offset = Some 5.0
        Description = Some "Test lookup"
        IsReadOnly = true
        Group = Some "TestGroup"
    }
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Mitsubishi
        TagMappings = [tagMapping]
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = false
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        match mapper.GetTagMapping("LOOKUP_TEST") with
        | Some mapping ->
            mapping.LogicalTagId |> should equal "LOOKUP_TEST"
            mapping.PhysicalAddress.Raw |> should equal "D600"
            mapping.DataType |> should equal PlcDataType.Float32
            mapping.ScaleFactor |> should equal (Some 0.01)
            mapping.IsReadOnly |> should equal true
        | None -> failwith "Tag mapping not found"
        
        // Test non-existent tag
        mapper.GetTagMapping("NON_EXISTENT") |> should equal None
    | Result.Error error -> failwith $"Mapper creation failed: {error}"

[<Fact>]
let ``PlcMapper should fail when auto-mapping is disabled and no mapping exists`` () =
    let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
    let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Siemens)
    
    let config = {
        PlcId = "PLC001"
        Vendor = PlcVendor.Siemens
        TagMappings = []
        AddressMappings = []
        AddressFormat = addressFormat
        EnableAutoMapping = false  // Disabled
        DefaultDataType = PlcDataType.Int16
    }
    
    match mapperFactory.CreateMapper(config) with
    | Result.Ok mapper ->
        let logicalTag = TagConfiguration.Create("UNMAPPED_TAG", "PLC001", "Unmapped Tag", PlcAddress.Create("MW100"), PlcDataType.Int16)
        
        match mapper.MapLogicalToPhysical(logicalTag) with
        | Result.Ok _ -> failwith "Expected mapping to fail when auto-mapping is disabled"
        | Result.Error errorMsg -> errorMsg.Contains("No mapping configured") |> should equal true
    | Result.Error error -> failwith $"Mapper creation failed: {error}"