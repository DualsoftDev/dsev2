module Ev2.PLC.Mapper.Tests.MapperTypesTests

open System
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

[<Fact>]
let ``RawVariable should create correctly`` () =
    let variable = {
        Name = "MOTOR_SPEED"
        Address = "D100"
        DataType = "INT"
        Comment = Some "Motor speed control"
        InitialValue = None
        Scope = None
        AccessLevel = None
        Properties = Map.empty
    }

    variable.Name |> should equal "MOTOR_SPEED"
    variable.Address |> should equal "D100"
    variable.DataType |> should equal "INT"
    variable.Comment |> should equal (Some "Motor speed control")
    Map.isEmpty variable.Properties |> should equal true

[<Fact>]
let ``RawVariable.Create should set defaults`` () =
    let variable = RawVariable.Create("TEST_VAR", "M100", "BOOL")

    variable.Name |> should equal "TEST_VAR"
    variable.Address |> should equal "M100"
    variable.DataType |> should equal "BOOL"
    variable.Comment |> should equal None
    variable.InitialValue |> should equal None
    variable.Scope |> should equal None
    variable.AccessLevel |> should equal None

[<Fact>]
let ``DeviceType should have correct display names`` () =
    Motor.DisplayName |> should equal "Motor"
    Cylinder.DisplayName |> should equal "Cylinder"
    Sensor.DisplayName |> should equal "Sensor"
    Valve.DisplayName |> should equal "Valve"
    Conveyor.DisplayName |> should equal "Conveyor"
    (DeviceType.Custom "MyDevice").DisplayName |> should equal "MyDevice"

[<Fact>]
let ``DeviceType should have standard APIs`` () =
    Motor.StandardApis |> should contain "FWD"
    Motor.StandardApis |> should contain "STOP"
    Cylinder.StandardApis |> should contain "UP"
    Cylinder.StandardApis |> should contain "DOWN"
    Sensor.StandardApis |> should contain "DETECT"

[<Fact>]
let ``ApiType should have correct descriptions`` () =
    Command.Description |> should haveSubstring "Command"
    Status.Description |> should haveSubstring "status"
    Parameter.Description |> should haveSubstring "parameter"
    Feedback.Description |> should haveSubstring "feedback"

[<Fact>]
let ``IODirection should have correct values`` () =
    Input.ToString() |> should haveSubstring "Input"
    Output.ToString() |> should haveSubstring "Output"
    Bidirectional.ToString() |> should haveSubstring "Bidirectional"
    Internal.ToString() |> should haveSubstring "Internal"

[<Fact>]
let ``MappingConfiguration should set defaults correctly`` () =
    let vendor = PlcVendor.CreateLSElectric()
    let config = MappingConfiguration.Default(vendor)

    config.Vendor.Manufacturer |> should equal "LS Electric"
    config.OptimizationEnabled |> should equal true
    config.NamingConventions |> should not' (be Empty)
    Map.isEmpty config.DeviceTypeMapping |> should equal true
    Map.isEmpty config.ApiTypeMapping |> should equal true

[<Fact>]
let ``VariableNamingPattern should extract parts correctly`` () =
    let pattern = {
        OriginalName = "AREA_MOTOR_SPEED"
        Prefix = Some "AREA"
        DeviceName = "MOTOR"
        ApiSuffix = Some "SPEED"
        IOType = Output
        Confidence = 0.95
    }

    pattern.OriginalName |> should equal "AREA_MOTOR_SPEED"
    pattern.Prefix |> should equal (Some "AREA")
    pattern.DeviceName |> should equal "MOTOR"
    pattern.ApiSuffix |> should equal (Some "SPEED")
    pattern.Confidence |> should be (greaterThan 0.9)
    pattern.HasValidPattern |> should equal true

[<Fact>]
let ``VariableNamingPattern.Create should set defaults`` () =
    let pattern = VariableNamingPattern.Create("TEST_VAR", "TEST")

    pattern.OriginalName |> should equal "TEST_VAR"
    pattern.DeviceName |> should equal "TEST"
    pattern.Prefix |> should equal None
    pattern.ApiSuffix |> should equal None
    pattern.Confidence |> should equal 0.5
    pattern.HasValidPattern |> should equal false

[<Fact>]
let ``Area should create correctly`` () =
    let area = {
        Name = "STATION_1"
        Description = "Production Station 1"
        Devices = []
        Priority = 1
        Properties = Map.empty
    }

    area.Name |> should equal "STATION_1"
    area.Description |> should equal "Production Station 1"
    area.Devices |> should be Empty
    area.Priority |> should equal 1

[<Fact>]
let ``Area.Create should set defaults`` () =
    let area = Area.Create("ZONE_A")

    area.Name |> should equal "ZONE_A"
    area.Description |> should equal ""
    area.Devices |> should be Empty
    area.Priority |> should equal 0
    Map.isEmpty area.Properties |> should equal true

[<Fact>]
let ``Device should create correctly`` () =
    let device = {
        Name = "MOTOR_01"
        Type = Motor
        Area = "STATION_1"
        Description = "Main conveyor motor"
        SupportedApis = []
        IOMapping = IOMapping.Empty
        Properties = Map.empty
        Position = Some (10.0, 20.0)
    }

    device.Name |> should equal "MOTOR_01"
    device.Type |> should equal Motor
    device.Area |> should equal "STATION_1"
    device.SupportedApis |> should be Empty
    device.Position |> should equal (Some (10.0, 20.0))

[<Fact>]
let ``Device.Create should set defaults`` () =
    let device = Device.Create("CYL_01", Cylinder, "ZONE_A")

    device.Name |> should equal "CYL_01"
    device.Type |> should equal Cylinder
    device.Area |> should equal "ZONE_A"
    device.Description |> should equal ""
    device.SupportedApis |> should be Empty
    device.Position |> should equal None

[<Fact>]
let ``ApiDefinition should create with defaults`` () =
    let apiDef = ApiDefinition.Create("START", Command, PlcDataType.Bool, Output)

    apiDef.Name |> should equal "START"
    apiDef.Type |> should equal Command
    apiDef.DataType |> should equal PlcDataType.Bool
    apiDef.Direction |> should equal Output
    apiDef.SafetyLevel |> should equal 0
    apiDef.PrecedingApis |> should be Empty
    apiDef.InterlockApis |> should be Empty
    apiDef.Description |> should equal ""
    apiDef.Unit |> should equal None

[<Fact>]
let ``IOVariable should create with defaults`` () =
    let address = PlcAddress.Create("D100", deviceArea = "D", index = 100)
    let ioVar = IOVariable.Create("MOTOR_SPEED", address, PlcDataType.Int16, Input)

    ioVar.LogicalName |> should equal "MOTOR_SPEED"
    ioVar.PhysicalAddress.DeviceArea |> should equal (Some "D")
    ioVar.PhysicalAddress.Index |> should equal (Some 100)
    ioVar.DataType |> should equal PlcDataType.Int16
    ioVar.Direction |> should equal Input
    ioVar.Device |> should equal ""
    ioVar.Api |> should equal None
    ioVar.InitialValue |> should equal None
    ioVar.Scaling |> should equal None

[<Fact>]
let ``IOMapping should manage variables by direction`` () =
    let address = PlcAddress.Create("D100", deviceArea = "D", index = 100)
    let inputVar = IOVariable.Create("INPUT_1", address, PlcDataType.Bool, Input)
    let outputVar = IOVariable.Create("OUTPUT_1", address, PlcDataType.Bool, Output)

    let mapping = IOMapping.Empty
                  |> fun m -> m.AddVariable(inputVar)
                  |> fun m -> m.AddVariable(outputVar)

    mapping.Inputs |> should haveLength 1
    mapping.Outputs |> should haveLength 1
    mapping.AllVariables |> should haveLength 2

[<Fact>]
let ``MappingStatistics should calculate mapping rate`` () =
    let stats = {
        TotalVariables = 100
        MappedVariables = 95
        TotalAreas = 5
        TotalDevices = 20
        TotalApis = 50
        ProcessingTime = TimeSpan.FromSeconds(1.5)
        FileSize = 1024L
        ParsingTime = TimeSpan.FromSeconds(0.5)
        AnalysisTime = TimeSpan.FromSeconds(1.0)
    }

    stats.MappingRate |> should (equalWithin 0.01) 95.0
    stats.VariablesPerSecond |> should be (greaterThan 60.0)

[<Fact>]
let ``MappingResult should store conversion info`` () =
    let vendor = PlcVendor.CreateLSElectric()
    let format = PlcProgramFormat.LSElectricXML("test.xml")
    let projectInfo = ProjectInfo.Create("TestProject", vendor, format, "test.xml")

    let result = MappingResult.CreateSuccess(projectInfo, [], [])

    result.Success |> should equal true
    result.ProjectInfo.Name |> should equal "TestProject"
    result.Areas |> should be Empty
    result.Devices |> should be Empty
    result.Errors |> should be Empty

[<Fact>]
let ``MappingResult error should contain error messages`` () =
    let vendor = PlcVendor.CreateLSElectric()
    let format = PlcProgramFormat.LSElectricXML("test.xml")
    let projectInfo = ProjectInfo.Create("TestProject", vendor, format, "test.xml")

    let errors = ["Error 1"; "Error 2"]
    let result = MappingResult.CreateError(projectInfo, errors)

    result.Success |> should equal false
    result.Errors |> should haveLength 2
    result.Errors |> should contain "Error 1"

[<Fact>]
let ``ValidationResult should create success`` () =
    let result = ValidationResult.Success

    result.IsValid |> should equal true
    result.Severity |> should equal ValidationSeverity.ValidationInfo

[<Fact>]
let ``ValidationResult should create error`` () =
    let result = ValidationResult.Error("Test error", variable = "TEST_VAR")

    result.IsValid |> should equal false
    result.Severity |> should equal ValidationSeverity.ValidationError
    result.Message |> should equal "Test error"
    result.Variable |> should equal (Some "TEST_VAR")

[<Fact>]
let ``ValidationResult should create warning`` () =
    let result = ValidationResult.Warning("Test warning", suggestion = "Fix this")

    result.IsValid |> should equal true
    result.Severity |> should equal ValidationSeverity.ValidationWarning
    result.Message |> should equal "Test warning"
    result.Suggestion |> should equal (Some "Fix this")
