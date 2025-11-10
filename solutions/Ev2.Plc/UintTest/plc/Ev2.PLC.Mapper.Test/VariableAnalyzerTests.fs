namespace Ev2.PLC.Mapper.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Analyzer
open TestHelpers

module VariableAnalyzerTests =

    let createAnalyzer() =
        let logger = createLogger<VariableAnalyzer>()
        VariableAnalyzer(logger)

    [<Fact>]
    let ``VariableAnalyzer should analyze variable types correctly`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let analysis = analyzer.AnalyzeVariables(variables)

        analysis.TotalCount |> should equal 5
        analysis.TypeDistribution.["BOOL"] |> should equal 3
        analysis.TypeDistribution.["INT"] |> should equal 2

    [<Fact>]
    let ``VariableAnalyzer should detect input variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let inputs = analyzer.GetInputVariables(variables)

        inputs |> should not' (be Empty)
        inputs |> List.map (fun v -> v.Name) |> should contain "Motor1_Start"
        inputs |> List.map (fun v -> v.Name) |> should contain "Motor1_Stop"
        inputs |> List.map (fun v -> v.Name) |> should contain "Temperature_Sensor1"

    [<Fact>]
    let ``VariableAnalyzer should detect output variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let outputs = analyzer.GetOutputVariables(variables)

        outputs |> should not' (be Empty)
        outputs |> List.map (fun v -> v.Name) |> should contain "Motor1_Running"

    [<Fact>]
    let ``VariableAnalyzer should detect memory variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let memory = analyzer.GetMemoryVariables(variables)

        memory |> should not' (be Empty)
        memory |> List.map (fun v -> v.Name) |> should contain "Temperature_Setpoint"

    [<Fact>]
    let ``VariableAnalyzer should group variables by prefix`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let groups = analyzer.GroupByPrefix(variables, "_")

        groups.ContainsKey("Motor1") |> should be True
        groups.["Motor1"] |> List.length |> should equal 3
        groups.ContainsKey("Temperature") |> should be True
        groups.["Temperature"] |> List.length |> should equal 2

    [<Fact>]
    let ``VariableAnalyzer should find duplicate addresses`` () =
        let analyzer = createAnalyzer()
        let variables = [
            { TestData.createSampleVariables().[0] with Name = "Var1"; Address = "%IX0.0" }
            { TestData.createSampleVariables().[0] with Name = "Var2"; Address = "%IX0.0" }
            { TestData.createSampleVariables().[0] with Name = "Var3"; Address = "%IX0.1" }
        ]

        let duplicates = analyzer.FindDuplicateAddresses(variables)

        duplicates |> should not' (be Empty)
        duplicates.["%IX0.0"] |> should contain "Var1"
        duplicates.["%IX0.0"] |> should contain "Var2"

    [<Fact>]
    let ``VariableAnalyzer should validate variable names`` () =
        let analyzer = createAnalyzer()

        let validNames = ["Motor_Start"; "Valve123"; "_Counter"; "PLC_IO_1"]
        let invalidNames = ["123Motor"; "Motor-Start"; "Motor Start"; "IF"]

        validNames
        |> List.iter (fun name ->
            analyzer.IsValidVariableName(name) |> should be True
        )

        invalidNames
        |> List.iter (fun name ->
            analyzer.IsValidVariableName(name) |> should be False
        )

    [<Fact>]
    let ``VariableAnalyzer should detect unused variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()
        let logic = TestData.createSampleLogic()

        let unused = analyzer.FindUnusedVariables(variables, logic)

        unused |> should not' (be Empty)
        // Temperature variables are not used in the sample logic
        unused |> List.map (fun v -> v.Name) |> should contain "Temperature_Sensor1"

    [<Fact>]
    let ``VariableAnalyzer should calculate memory usage`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let usage = analyzer.CalculateMemoryUsage(variables)

        usage.TotalBytes |> should be (greaterThan 0)
        usage.BoolCount |> should equal 3
        usage.IntCount |> should equal 2

    [<Fact>]
    let ``VariableAnalyzer should generate variable report`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let report = analyzer.GenerateVariableReport(variables)

        report |> should haveSubstring "Total Variables: 5"
        report |> should haveSubstring "BOOL"
        report |> should haveSubstring "INT"

    [<Fact>]
    let ``VariableAnalyzer should detect global variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let globals = analyzer.GetGlobalVariables(variables)

        globals |> should not' (be Empty)
        globals |> List.length |> should equal 5  // All sample variables are global

    [<Fact>]
    let ``VariableAnalyzer should detect local variables`` () =
        let analyzer = createAnalyzer()
        let variables = [
            { TestData.createSampleVariables().[0] with Scope = Some "Local" }
            { TestData.createSampleVariables().[1] with Scope = Some "Global" }
        ]

        let locals = analyzer.GetLocalVariables(variables)

        locals |> List.length |> should equal 1
        locals.[0].Scope |> should equal (Some "Local")

    [<Fact>]
    let ``VariableAnalyzer should find variables by data type`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let boolVars = analyzer.FindByDataType(variables, "BOOL")
        let intVars = analyzer.FindByDataType(variables, "INT")

        boolVars |> List.length |> should equal 3
        intVars |> List.length |> should equal 2

    [<Fact>]
    let ``VariableAnalyzer should detect read-only variables`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let readOnly = analyzer.GetReadOnlyVariables(variables)

        readOnly |> should not' (be Empty)
        readOnly |> List.map (fun v -> v.Name) |> should contain "Temperature_Sensor1"

    [<Fact>]
    let ``VariableAnalyzer should find variables with initial values`` () =
        let analyzer = createAnalyzer()
        let variables = TestData.createSampleVariables()

        let withInitial = analyzer.GetVariablesWithInitialValues(variables)

        withInitial |> should not' (be Empty)
        withInitial |> List.map (fun v -> v.Name) |> should contain "Temperature_Setpoint"
        withInitial.[0].InitialValue |> should equal (Some "25")