namespace Ev2.PLC.Mapper.Test.TestData

open System
open System.IO
open System.Text
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes

/// 테스트용 샘플 데이터 제공자
module SampleDataProvider =

    /// LS일렉트릭 XML 샘플 생성
    let createLSElectricXmlSample (variables: RawVariable list) =
        let sb = StringBuilder()
        
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>") |> ignore
        sb.AppendLine("<Project Name=\"TestProject\" Version=\"1.0.0\">") |> ignore
        sb.AppendLine("  <SymbolTable>") |> ignore
        
        for variable in variables do
            sb.AppendFormat("    <Symbol Name=\"{0}\" Address=\"{1}\" DataType=\"{2}\"", 
                           variable.Name, variable.Address, variable.DataType) |> ignore
            
            match variable.Comment with
            | Some comment -> sb.AppendFormat(" Comment=\"{0}\"", comment) |> ignore
            | None -> ()
            
            match variable.InitialValue with
            | Some value -> sb.AppendFormat(" InitialValue=\"{0}\"", value) |> ignore
            | None -> ()
            
            sb.AppendLine(" />") |> ignore
        
        sb.AppendLine("  </SymbolTable>") |> ignore
        sb.AppendLine("  <LadderLogic>") |> ignore
        sb.AppendLine("    <Rung Number=\"1\">") |> ignore
        sb.AppendLine("      <Contact Variable=\"AREA1_MOTOR1_START\" />") |> ignore
        sb.AppendLine("      <Coil Variable=\"AREA1_MOTOR1_RUNNING\" />") |> ignore
        sb.AppendLine("    </Rung>") |> ignore
        sb.AppendLine("  </LadderLogic>") |> ignore
        sb.AppendLine("</Project>") |> ignore
        
        sb.ToString()

    /// Allen-Bradley L5K 샘플 생성
    let createAllenBradleyL5KSample (variables: RawVariable list) =
        let sb = StringBuilder()
        
        sb.AppendLine("RSLogix 5000 Export File") |> ignore
        sb.AppendLine("L5K") |> ignore
        sb.AppendLine("Version: 1.0") |> ignore
        sb.AppendLine("") |> ignore
        sb.AppendLine("PROJECT TestProject") |> ignore
        sb.AppendLine("  Description: Test Project for Allen-Bradley") |> ignore
        sb.AppendLine("") |> ignore
        sb.AppendLine("CONTROLLER TestController") |> ignore
        sb.AppendLine("  ProcessorType: 1756-L73") |> ignore
        sb.AppendLine("") |> ignore
        
        for variable in variables do
            let dataTypeWithScope = 
                match variable.DataType with
                | "BOOL" -> "Controller BOOL"
                | "INT" -> "Controller INT"
                | "DINT" -> "Controller DINT"
                | "REAL" -> "Controller REAL"
                | dt -> $"Controller {dt}"
            
            sb.AppendFormat("TAG {0} {1}", variable.Name, dataTypeWithScope) |> ignore
            
            match variable.InitialValue with
            | Some value -> sb.AppendFormat(" := {0}", value) |> ignore
            | None -> ()
            
            match variable.Comment with
            | Some comment -> sb.AppendFormat(" // {0}", comment) |> ignore
            | None -> ()
            
            sb.AppendLine("") |> ignore
        
        sb.ToString()

    /// 표준 테스트 변수 목록 생성
    let createStandardTestVariables() = [
        TestVariableBuilder()
            .WithName("AREA1_MOTOR1_START")
            .WithAddress("X100")
            .WithDataType("BOOL")
            .WithComment("Motor 1 Start Button")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA1_MOTOR1_STOP")
            .WithAddress("X101")
            .WithDataType("BOOL")
            .WithComment("Motor 1 Stop Button")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA1_MOTOR1_RUNNING")
            .WithAddress("Y100")
            .WithDataType("BOOL")
            .WithComment("Motor 1 Running Status")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA1_MOTOR1_SPEED")
            .WithAddress("D100")
            .WithDataType("INT")
            .WithComment("Motor 1 Speed Setting")
            .WithInitialValue("1500")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA2_CYL1_EXTEND")
            .WithAddress("Y200")
            .WithDataType("BOOL")
            .WithComment("Cylinder 1 Extend")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA2_CYL1_RETRACT")
            .WithAddress("Y201")
            .WithDataType("BOOL")
            .WithComment("Cylinder 1 Retract")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA2_CYL1_EXTENDED")
            .WithAddress("X200")
            .WithDataType("BOOL")
            .WithComment("Cylinder 1 Extended Sensor")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA2_CYL1_RETRACTED")
            .WithAddress("X201")
            .WithDataType("BOOL")
            .WithComment("Cylinder 1 Retracted Sensor")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA3_SENSOR1_DETECT")
            .WithAddress("X300")
            .WithDataType("BOOL")
            .WithComment("Proximity Sensor 1")
            .Build()
        
        TestVariableBuilder()
            .WithName("AREA3_LAMP1_ON")
            .WithAddress("Y300")
            .WithDataType("BOOL")
            .WithComment("Warning Lamp 1")
            .Build()
    ]

    /// 복잡한 테스트 시나리오용 변수 목록
    let createComplexTestVariables() = [
        // 다양한 명명 규칙
        TestVariableBuilder()
            .WithName("STATION1_ROBOT_ARM_UP")
            .WithAddress("Y100")
            .WithDataType("BOOL")
            .Build()
        
        TestVariableBuilder()
            .WithName("LINE2_CONV_BELT_SPEED")
            .WithAddress("D200")
            .WithDataType("INT")
            .Build()
        
        // 규칙에 맞지 않는 변수명
        TestVariableBuilder()
            .WithName("EMERGENCY_STOP")
            .WithAddress("X999")
            .WithDataType("BOOL")
            .Build()
        
        TestVariableBuilder()
            .WithName("GLOBAL_COUNTER")
            .WithAddress("D999")
            .WithDataType("DINT")
            .Build()
        
        // 특수 문자 포함
        TestVariableBuilder()
            .WithName("AREA1_MOTOR-1_STATUS")
            .WithAddress("M100")
            .WithDataType("BOOL")
            .Build()
        
        // 한글 포함 (테스트용)
        TestVariableBuilder()
            .WithName("구역1_모터1_시작")
            .WithAddress("X500")
            .WithDataType("BOOL")
            .Build()
    ]

    /// 에러 케이스용 변수 목록
    let createErrorCaseVariables() = [
        // 빈 이름
        TestVariableBuilder()
            .WithName("")
            .WithAddress("X100")
            .WithDataType("BOOL")
            .Build()
        
        // 잘못된 주소
        TestVariableBuilder()
            .WithName("TEST_VAR")
            .WithAddress("")
            .WithDataType("BOOL")
            .Build()
        
        // 지원하지 않는 데이터 타입
        TestVariableBuilder()
            .WithName("TEST_VAR2")
            .WithAddress("D100")
            .WithDataType("CUSTOM_TYPE")
            .Build()
    ]

    /// 테스트 시나리오 생성
    let createTestScenarios() = [
        {
            Name = "StandardMapping"
            Description = "표준 명명 규칙을 따르는 변수들의 매핑 테스트"
            InputFile = "standard_test.xml"
            ExpectedResults = {
                ShouldSucceed = true
                ExpectedVariableCount = Some 10
                ExpectedAreaCount = Some 3
                ExpectedDeviceCount = Some 4
                ExpectedApiCount = Some 8
                ExpectedErrors = []
                ExpectedWarnings = []
            }
            Config = None
        }
        
        {
            Name = "ComplexMapping"
            Description = "복잡한 명명 규칙과 다양한 패턴의 매핑 테스트"
            InputFile = "complex_test.xml"
            ExpectedResults = {
                ShouldSucceed = true
                ExpectedVariableCount = Some 6
                ExpectedAreaCount = Some 2
                ExpectedDeviceCount = Some 3
                ExpectedApiCount = Some 4
                ExpectedErrors = []
                ExpectedWarnings = ["Could not parse variable name pattern"]
            }
            Config = None
        }
        
        {
            Name = "ErrorHandling"
            Description = "에러 케이스 처리 테스트"
            InputFile = "error_test.xml"
            ExpectedResults = {
                ShouldSucceed = false
                ExpectedVariableCount = Some 3
                ExpectedAreaCount = Some 0
                ExpectedDeviceCount = Some 0
                ExpectedApiCount = Some 0
                ExpectedErrors = ["Could not parse variable name pattern"]
                ExpectedWarnings = []
            }
            Config = None
        }
    ]

    /// 테스트 파일 생성
    let createTestFiles(outputDirectory: string) =
        if not (Directory.Exists(outputDirectory)) then
            Directory.CreateDirectory(outputDirectory) |> ignore
        
        // 표준 테스트 파일
        let standardVars = createStandardTestVariables()
        let standardXml = createLSElectricXmlSample standardVars
        File.WriteAllText(Path.Combine(outputDirectory, "standard_test.xml"), standardXml)
        
        let standardL5K = createAllenBradleyL5KSample standardVars
        File.WriteAllText(Path.Combine(outputDirectory, "standard_test.l5k"), standardL5K)
        
        // 복잡한 테스트 파일
        let complexVars = createComplexTestVariables()
        let complexXml = createLSElectricXmlSample complexVars
        File.WriteAllText(Path.Combine(outputDirectory, "complex_test.xml"), complexXml)
        
        // 에러 케이스 파일
        let errorVars = createErrorCaseVariables()
        let errorXml = createLSElectricXmlSample errorVars
        File.WriteAllText(Path.Combine(outputDirectory, "error_test.xml"), errorXml)
        
        // 빈 파일
        File.WriteAllText(Path.Combine(outputDirectory, "empty_test.xml"), "")
        
        // 잘못된 XML 파일
        File.WriteAllText(Path.Combine(outputDirectory, "invalid_test.xml"), "<InvalidXml>")

    /// 테스트 설정 파일 생성
    let createTestConfigFiles(outputDirectory: string) =
        let configDir = Path.Combine(outputDirectory, "Config")
        if not (Directory.Exists(configDir)) then
            Directory.CreateDirectory(configDir) |> ignore
        
        // 기본 테스트 설정
        let basicConfig = 
            TestConfigBuilder()
                .AddDeviceHint("MOTOR", "Motor")
                .AddDeviceHint("CYL", "Cylinder")
                .AddDeviceHint("SENSOR", "Sensor")
                .AddDeviceHint("LAMP", "Lamp")
                .AddApiCommand("START")
                .AddApiCommand("STOP")
                .AddApiCommand("EXTEND")
                .AddApiCommand("RETRACT")
                .AddApiStatus("RUNNING")
                .AddApiStatus("EXTENDED")
                .AddApiStatus("RETRACTED")
                .AddApiStatus("DETECT")
                .Build()
        
        let basicConfigJson = System.Text.Json.JsonSerializer.Serialize(basicConfig, 
            System.Text.Json.JsonSerializerOptions(WriteIndented = true))
        File.WriteAllText(Path.Combine(configDir, "test-config.json"), basicConfigJson)
        
        // 커스텀 테스트 설정
        let customConfig = 
            TestConfigBuilder()
                .AddDeviceHint("ROBOT", "Custom")
                .AddDeviceHint("AGV", "Conveyor")
                .AddApiCommand("UP")
                .AddApiCommand("DOWN")
                .Build()
        
        let customConfigJson = System.Text.Json.JsonSerializer.Serialize(customConfig, 
            System.Text.Json.JsonSerializerOptions(WriteIndented = true))
        File.WriteAllText(Path.Combine(configDir, "custom-config.json"), customConfigJson)