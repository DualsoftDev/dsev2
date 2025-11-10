namespace Ev2.PLC.Mapper.Tests

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Engine
open Ev2.PLC.Mapper.Core.Configuration
open Ev2.PLC.Mapper.Core.Interfaces

module VariableAnalyzerTests =

    /// Test logger and configuration
    let private createAnalyzer() =
        let loggerFactory = NullLoggerFactory.Instance :> ILoggerFactory
        let logger = loggerFactory.CreateLogger<VariableAnalyzer>()
        let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
        let configProvider = ConfigurationFactory.createProvider configLogger
        VariableAnalyzerFactory.create logger configProvider

    /// Sample raw variables
    let private createSampleVariables() = [
        { Name = "Motor1_Start"; Address = "%IX0.0"; DataType = "BOOL"; Comment = Some "Motor 1 start button"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Motor1_Stop"; Address = "%IX0.1"; DataType = "BOOL"; Comment = Some "Motor 1 stop button"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Motor1_Running"; Address = "%QX0.0"; DataType = "BOOL"; Comment = Some "Motor 1 running status"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Temperature_Sensor1"; Address = "%IW10"; DataType = "INT"; Comment = Some "Temperature sensor 1"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Temperature_Setpoint"; Address = "%MW20"; DataType = "INT"; Comment = Some "Temperature setpoint"; InitialValue = Some "25"; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Alarm_High"; Address = "%MX30.0"; DataType = "BOOL"; Comment = Some "High temperature alarm"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Counter1_Value"; Address = "%MW40"; DataType = "DWORD"; Comment = Some "Counter 1 current value"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Timer1"; Address = "%T1"; DataType = "TON"; Comment = Some "Timer 1"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "Safety_Emergency_Stop"; Address = "%IX1.0"; DataType = "BOOL"; Comment = Some "Emergency stop button"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        { Name = "ProcessData"; Address = "%DB100.0"; DataType = "STRUCT"; Comment = Some "Process data structure"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
    ]

    [<Fact>]
    let ``VariableAnalyzer should analyze single variable`` () =
        let analyzer = createAnalyzer()
        let variable = { Name = "Motor1_Start"; Address = "%IX0.0"; DataType = "BOOL"; Comment = Some "Motor 1 start button"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! results = analyzer.AnalyzeVariablesBatchAsync([variable], config)
            let result = results |> List.head

            result.Variable.Name |> should equal "Motor1_Start"
            result.IsValid |> should be True
            result.Device |> should not' (be None)
            result.Api |> should not' (be None)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should analyze batch of variables`` () =
        let analyzer = createAnalyzer()
        let variables = createSampleVariables()
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! results = analyzer.AnalyzeVariablesBatchAsync(variables, config)

            results.Length |> should equal variables.Length
            results |> List.forall (fun r -> r.IsValid) |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should extract areas from variables`` () =
        let analyzer = createAnalyzer()
        let variables = createSampleVariables()

        async {
            let! areas = analyzer.ExtractAreasAsync(variables)

            areas |> should not' (be Empty)
            areas |> List.exists (fun a -> a.Name = "Motor") |> should be True
            areas |> List.exists (fun a -> a.Name = "Temperature") |> should be True
            areas |> List.exists (fun a -> a.Name = "Safety") |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should extract devices from variables`` () =
        let analyzer = createAnalyzer()
        let variables = createSampleVariables()

        async {
            let! areas = analyzer.ExtractAreasAsync(variables)
            let! devices = analyzer.ExtractDevicesAsync(variables, areas)

            devices |> should not' (be Empty)
            devices |> List.exists (fun d -> d.Name.Contains("Motor")) |> should be True
            devices |> List.exists (fun d -> d.Name.Contains("Temperature")) |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should generate API definitions`` () =
        let analyzer = createAnalyzer()
        let variables = createSampleVariables()

        async {
            let! areas = analyzer.ExtractAreasAsync(variables)
            let! devices = analyzer.ExtractDevicesAsync(variables, areas)
            let! apiDefs = analyzer.GenerateApiDefinitionsAsync(devices)

            apiDefs |> should not' (be Empty)
            apiDefs |> List.forall (fun api -> not (String.IsNullOrEmpty(api.Name))) |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should detect input variables`` () =
        let analyzer = createAnalyzer()
        let inputVar = { Name = "Input1"; Address = "%IX0.0"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(inputVar, config)

            result.Variable.Address |> should haveSubstring "IX"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should detect output variables`` () =
        let analyzer = createAnalyzer()
        let outputVar = { Name = "Output1"; Address = "%QX0.0"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(outputVar, config)

            result.Variable.Address |> should haveSubstring "QX"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should detect memory variables`` () =
        let analyzer = createAnalyzer()
        let memVar = { Name = "Memory1"; Address = "%MW100"; DataType = "INT"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(memVar, config)

            result.Variable.Address |> should haveSubstring "MW"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle timer variables`` () =
        let analyzer = createAnalyzer()
        let timerVar = { Name = "Timer1"; Address = "%T1"; DataType = "TON"; Comment = Some "Timer"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(timerVar, config)

            result.Variable.DataType |> should equal "TON"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle counter variables`` () =
        let analyzer = createAnalyzer()
        let counterVar = { Name = "Counter1"; Address = "%C1"; DataType = "CTU"; Comment = Some "Counter"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(counterVar, config)

            result.Variable.DataType |> should equal "CTU"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should group variables by prefix`` () =
        let analyzer = createAnalyzer()
        let variables = [
            { Name = "Motor1_Start"; Address = "%IX0.0"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
            { Name = "Motor1_Stop"; Address = "%IX0.1"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
            { Name = "Motor2_Start"; Address = "%IX0.2"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
            { Name = "Motor2_Stop"; Address = "%IX0.3"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        ]

        async {
            let! areas = analyzer.ExtractAreasAsync(variables)
            let! devices = analyzer.ExtractDevicesAsync(variables, areas)

            // Should have Motor1 and Motor2 devices
            devices |> List.filter (fun d -> d.Name.Contains("Motor1")) |> should not' (be Empty)
            devices |> List.filter (fun d -> d.Name.Contains("Motor2")) |> should not' (be Empty)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle variables with initial values`` () =
        let analyzer = createAnalyzer()
        let varWithInit = { Name = "Setpoint"; Address = "%MW100"; DataType = "INT"; Comment = None; InitialValue = Some "100"; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(varWithInit, config)

            result.Variable.InitialValue |> should equal (Some "100")
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should validate variable names`` () =
        let analyzer = createAnalyzer()
        let invalidVar = { Name = ""; Address = "%IX0.0"; DataType = "BOOL"; Comment = None; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(invalidVar, config)

            result.IsValid |> should be False
            result.Issues |> should not' (be Empty)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle data block variables`` () =
        let analyzer = createAnalyzer()
        let dbVar = { Name = "ProcessData"; Address = "%DB100.DBW0"; DataType = "INT"; Comment = Some "Process data"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateSiemens())

        async {
            let! result = analyzer.AnalyzeVariableAsync(dbVar, config)

            result.Variable.Address |> should haveSubstring "DB"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should extract safety-related variables`` () =
        let analyzer = createAnalyzer()
        let variables = [
            { Name = "EMERGENCY_STOP"; Address = "%IX0.0"; DataType = "BOOL"; Comment = Some "Emergency stop"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
            { Name = "SAFETY_GATE"; Address = "%IX0.1"; DataType = "BOOL"; Comment = Some "Safety gate"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
            { Name = "ESTOP_RELAY"; Address = "%QX0.0"; DataType = "BOOL"; Comment = Some "E-stop relay"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        ]

        async {
            let! areas = analyzer.ExtractAreasAsync(variables)

            areas |> List.exists (fun a -> a.Name.ToLower().Contains("safety") || a.Name.ToLower().Contains("emergency")) |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle array variables`` () =
        let analyzer = createAnalyzer()
        let arrayVar = { Name = "DataArray[10]"; Address = "%MW100"; DataType = "ARRAY[0..9] OF INT"; Comment = Some "Data array"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())

        async {
            let! result = analyzer.AnalyzeVariableAsync(arrayVar, config)

            result.Variable.DataType |> should haveSubstring "ARRAY"
            result.IsValid |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``VariableAnalyzer should handle structure variables`` () =
        let analyzer = createAnalyzer()
        let structVar = { Name = "MotorControl"; Address = "%DB1.0"; DataType = "UDT_MotorControl"; Comment = Some "Motor control structure"; InitialValue = None; Scope = Some "Global"; AccessLevel = None; Properties = Map.empty }
        let config = MappingConfiguration.Default(PlcVendor.CreateSiemens())

        async {
            let! result = analyzer.AnalyzeVariableAsync(structVar, config)

            result.Variable.DataType |> should haveSubstring "UDT"
            result.IsValid |> should be True
        } |> Async.RunSynchronously