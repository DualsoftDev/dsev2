namespace Ev2.PLC.Mapper.Test.Unit

open System
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Configuration
open Ev2.PLC.Mapper.Core.Engine
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes

/// 변수 분석기 테스트
module VariableAnalyzerTests =

    let createTestConfigProvider() =
        let logger = NullLogger<ConfigurationProvider>.Instance
        let provider = ConfigurationProvider(logger) :> IConfigurationProvider
        provider

    let createVariableAnalyzer(configProvider: IConfigurationProvider) =
        let logger = NullLogger<VariableAnalyzer>.Instance
        VariableAnalyzerFactory.create logger configProvider

    [<Fact>]
    let ``VariableAnalyzer should analyze standard variable name correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let variable = 
            TestVariableBuilder()
                .WithName("AREA1_MOTOR1_START")
                .WithAddress("X100")
                .WithDataType("BOOL")
                .Build()
        
        let conventions = NamingConvention.GetDefaults()
        
        // Act
        let! result = analyzer.AnalyzeVariableNameAsync(variable, conventions)
        
        // Assert
        result |> should be (instanceOfType<VariableNamingPattern option>)
        match result with
        | Some pattern ->
            pattern.OriginalName |> should equal "AREA1_MOTOR1_START"
            pattern.Prefix |> should equal (Some "AREA1")
            pattern.DeviceName |> should equal "MOTOR1"
            pattern.ApiSuffix |> should equal (Some "START")
            pattern.IOType |> should equal Input
            pattern.Confidence |> should be (greaterThan 0.8)
        | None ->
            failwith "Should have parsed the variable name"
    }

    [<Fact>]
    let ``VariableAnalyzer should handle unparseable variable name`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let variable = 
            TestVariableBuilder()
                .WithName("INVALID_VARIABLE_NAME_PATTERN")
                .WithAddress("X100")
                .WithDataType("BOOL")
                .Build()
        
        let conventions = NamingConvention.GetDefaults()
        
        // Act
        let! result = analyzer.AnalyzeVariableNameAsync(variable, conventions)
        
        // Assert
        result |> should equal None
    }

    [<Fact>]
    let ``VariableAnalyzer should infer device type correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        // Act & Assert
        let! motorType = analyzer.InferDeviceTypeAsync("MOTOR1")
        motorType |> should equal Motor
        
        let! cylinderType = analyzer.InferDeviceTypeAsync("CYL_MAIN")
        cylinderType |> should equal Cylinder
        
        let! sensorType = analyzer.InferDeviceTypeAsync("PROXIMITY_SENSOR")
        sensorType |> should equal Sensor
        
        let! customType = analyzer.InferDeviceTypeAsync("UNKNOWN_DEVICE")
        match customType with
        | DeviceType.Custom "UNKNOWN_DEVICE" -> ()
        | _ -> failwith "Should infer as custom device type"
    }

    [<Fact>]
    let ``VariableAnalyzer should infer API type correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        // Act & Assert
        let! startApiType = analyzer.InferApiTypeAsync("START", Motor)
        startApiType |> should equal Command
        
        let! runningApiType = analyzer.InferApiTypeAsync("RUNNING", Motor)
        runningApiType |> should equal Status
        
        let! speedApiType = analyzer.InferApiTypeAsync("SPEED", Motor)
        speedApiType |> should equal Parameter
        
        let! valueApiType = analyzer.InferApiTypeAsync("VALUE", Sensor)
        valueApiType |> should equal Feedback
    }

    [<Fact>]
    let ``VariableAnalyzer should analyze variables in batch correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let variables = [
            TestVariableBuilder()
                .WithName("AREA1_MOTOR1_START")
                .WithAddress("X100")
                .WithDataType("BOOL")
                .Build()
            
            TestVariableBuilder()
                .WithName("AREA1_MOTOR1_RUNNING")
                .WithAddress("Y100")
                .WithDataType("BOOL")
                .Build()
            
            TestVariableBuilder()
                .WithName("AREA2_CYL1_EXTEND")
                .WithAddress("Y200")
                .WithDataType("BOOL")
                .Build()
        ]
        
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())
        
        // Act
        let! results = analyzer.AnalyzeVariablesBatchAsync(variables, config)
        
        // Assert
        results |> should haveLength 3
        
        // 첫 번째 변수 확인
        let firstResult = results.Head
        firstResult.IsValid |> should equal true
        firstResult.Pattern |> should not' (be None)
        firstResult.Device |> should not' (be None)
        firstResult.Api |> should not' (be None)
        
        match firstResult.Device with
        | Some device -> 
            device.Name |> should equal "MOTOR1"
            device.Type |> should equal Motor
        | None -> failwith "Device should be parsed"
    }

    [<Fact>]
    let ``VariableAnalyzer should extract areas correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let variables = [
            TestVariableBuilder().WithName("AREA1_MOTOR1_START").WithAddress("X100").Build()
            TestVariableBuilder().WithName("AREA1_MOTOR2_START").WithAddress("X101").Build()
            TestVariableBuilder().WithName("AREA2_CYL1_EXTEND").WithAddress("Y200").Build()
            TestVariableBuilder().WithName("AREA3_SENSOR1_DETECT").WithAddress("X300").Build()
        ]
        
        // Act
        let! areas = analyzer.ExtractAreasAsync(variables)
        
        // Assert
        areas |> should haveLength 3
        let areaNames = areas |> List.map (fun a -> a.Name) |> List.sort
        areaNames |> should equal ["AREA1"; "AREA2"; "AREA3"]
    }

    [<Fact>]
    let ``VariableAnalyzer should extract devices correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let variables = [
            TestVariableBuilder().WithName("AREA1_MOTOR1_START").WithAddress("X100").Build()
            TestVariableBuilder().WithName("AREA1_MOTOR1_STOP").WithAddress("X101").Build()
            TestVariableBuilder().WithName("AREA1_MOTOR2_START").WithAddress("X102").Build()
            TestVariableBuilder().WithName("AREA2_CYL1_EXTEND").WithAddress("Y200").Build()
        ]
        
        let areas = [
            Area.Create("AREA1")
            Area.Create("AREA2")
        ]
        
        // Act
        let! devices = analyzer.ExtractDevicesAsync(variables, areas)
        
        // Assert
        devices |> should haveLength 3
        let deviceNames = devices |> List.map (fun d -> d.Name) |> List.sort
        deviceNames |> should equal ["CYL1"; "MOTOR1"; "MOTOR2"]
    }

    [<Fact>]
    let ``VariableAnalyzer should generate API definitions correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        let devices = [
            Device.Create("MOTOR1", Motor, "AREA1")
            Device.Create("CYL1", Cylinder, "AREA2")
        ]
        
        // Act
        let! apiDefs = analyzer.GenerateApiDefinitionsAsync(devices)
        
        // Assert
        apiDefs |> should not' (be Empty)
        
        // Motor APIs 확인
        let motorApis = apiDefs |> List.filter (fun api -> 
            Motor.StandardApis |> List.contains api.Name)
        motorApis |> should not' (be Empty)
        
        // Cylinder APIs 확인
        let cylinderApis = apiDefs |> List.filter (fun api -> 
            Cylinder.StandardApis |> List.contains api.Name)
        cylinderApis |> should not' (be Empty)
    }

    [<Fact>]
    let ``VariableAnalyzer should handle configuration reload`` () =
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        // Act - 설정 재로드 시도
        match analyzer with
        | :? VariableAnalyzer as va ->
            // 재로드 메서드 호출 (실제 파일이 없어도 오류 없이 처리되어야 함)
            va.ReloadConfiguration()
            // 예외가 발생하지 않으면 성공
            ()
        | _ ->
            failwith "Analyzer should be of type VariableAnalyzer"

    [<Fact>]
    let ``VariableAnalyzer should calculate confidence levels correctly`` () = task {
        // Arrange
        let configProvider = createTestConfigProvider()
        let analyzer = createVariableAnalyzer configProvider
        
        // Perfect match (area + device + api)
        let perfectVariable = 
            TestVariableBuilder()
                .WithName("AREA1_MOTOR1_START")
                .WithAddress("X100")
                .WithDataType("BOOL")
                .Build()
        
        // Partial match (device + api)
        let partialVariable = 
            TestVariableBuilder()
                .WithName("MOTOR1_START")
                .WithAddress("X100")
                .WithDataType("BOOL")
                .Build()
        
        let conventions = NamingConvention.GetDefaults()
        
        // Act
        let! perfectResult = analyzer.AnalyzeVariableNameAsync(perfectVariable, conventions)
        let! partialResult = analyzer.AnalyzeVariableNameAsync(partialVariable, conventions)
        
        // Assert
        match perfectResult, partialResult with
        | Some perfect, Some partial ->
            perfect.Confidence |> should be (greaterThan partial.Confidence)
            perfect.Confidence |> should be (greaterThan 0.8)
            partial.Confidence |> should be (greaterThan 0.5)
            partial.Confidence |> should be (lessThan 0.9)
        | _ ->
            failwith "Both variables should be parsed successfully"
    }