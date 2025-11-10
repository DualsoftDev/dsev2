namespace Ev2.PLC.Mapper.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Analyzer
open TestHelpers

module NamingAnalyzerTests =

    let createAnalyzer() =
        let logger = createLogger<NamingAnalyzer>()
        NamingAnalyzer(logger)

    [<Fact>]
    let ``NamingAnalyzer should detect naming patterns`` () =
        let analyzer = createAnalyzer()
        let names = ["Motor_Start"; "Motor_Stop"; "Motor_Running"; "Valve_Open"; "Valve_Close"]

        let patterns = analyzer.DetectNamingPatterns(names)

        patterns.CommonPrefixes |> should contain "Motor"
        patterns.CommonPrefixes |> should contain "Valve"
        patterns.CommonSuffixes |> should contain "Start"
        patterns.CommonSuffixes |> should contain "Stop"

    [<Fact>]
    let ``NamingAnalyzer should detect casing style`` () =
        let analyzer = createAnalyzer()

        analyzer.DetectCasingStyle("MotorStart") |> should equal "PascalCase"
        analyzer.DetectCasingStyle("motorStart") |> should equal "camelCase"
        analyzer.DetectCasingStyle("motor_start") |> should equal "snake_case"
        analyzer.DetectCasingStyle("MOTOR_START") |> should equal "UPPER_SNAKE_CASE"
        analyzer.DetectCasingStyle("motor-start") |> should equal "kebab-case"

    [<Fact>]
    let ``NamingAnalyzer should convert between casing styles`` () =
        let analyzer = createAnalyzer()

        analyzer.ConvertCasing("motor_start", "PascalCase") |> should equal "MotorStart"
        analyzer.ConvertCasing("MotorStart", "snake_case") |> should equal "motor_start"
        analyzer.ConvertCasing("motor_start", "camelCase") |> should equal "motorStart"
        analyzer.ConvertCasing("motorStart", "UPPER_SNAKE_CASE") |> should equal "MOTOR_START"

    [<Fact>]
    let ``NamingAnalyzer should validate naming conventions`` () =
        let analyzer = createAnalyzer()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let rules = config.Rules.VariableNaming

        let validNames = ["PLC_Motor_Start"; "PLC_Valve_1"]
        let invalidNames = ["Motor_Start"; "PLC_IF"; "PLC_x"]  // Missing prefix, reserved word, too short

        validNames
        |> List.iter (fun name ->
            analyzer.ValidateNamingConvention(name, rules) |> should be True
        )

        invalidNames
        |> List.iter (fun name ->
            analyzer.ValidateNamingConvention(name, rules) |> should be False
        )

    [<Fact>]
    let ``NamingAnalyzer should suggest name corrections`` () =
        let analyzer = createAnalyzer()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let rules = config.Rules.VariableNaming

        let suggestion = analyzer.SuggestNameCorrection("motor-start", rules)

        suggestion |> should equal "PLC_Motor_Start"

    [<Fact>]
    let ``NamingAnalyzer should find naming conflicts`` () =
        let analyzer = createAnalyzer()
        let names = ["Motor_Start"; "motor_start"; "MOTOR_START"; "Motor_Stop"]

        let conflicts = analyzer.FindNamingConflicts(names)

        conflicts |> should not' (be Empty)
        conflicts |> List.length |> should equal 3  // The three motor_start variants

    [<Fact>]
    let ``NamingAnalyzer should generate unique names`` () =
        let analyzer = createAnalyzer()
        let existingNames = ["Motor1"; "Motor2"; "Motor3"]

        let newName = analyzer.GenerateUniqueName("Motor", existingNames)

        newName |> should equal "Motor4"
        existingNames |> should not' (contain newName)

    [<Fact>]
    let ``NamingAnalyzer should extract base name from prefixed name`` () =
        let analyzer = createAnalyzer()

        analyzer.ExtractBaseName("PLC_Motor_Start", "PLC_") |> should equal "Motor_Start"
        analyzer.ExtractBaseName("Motor_Start_Button", "_Button") |> should equal "Motor_Start"
        analyzer.ExtractBaseName("NoPrefix", "PLC_") |> should equal "NoPrefix"

    [<Fact>]
    let ``NamingAnalyzer should detect abbreviations`` () =
        let analyzer = createAnalyzer()
        let names = ["Mtr_Start"; "Vlv_Open"; "Tmp_Sensor"; "Prs_High"]

        let abbreviations = analyzer.DetectAbbreviations(names)

        abbreviations |> should contain ("Mtr", "Motor")
        abbreviations |> should contain ("Vlv", "Valve")
        abbreviations |> should contain ("Tmp", "Temperature")
        abbreviations |> should contain ("Prs", "Pressure")

    [<Fact>]
    let ``NamingAnalyzer should expand abbreviations in names`` () =
        let analyzer = createAnalyzer()
        let abbreviations = Map.ofList [("Mtr", "Motor"); ("Vlv", "Valve")]

        analyzer.ExpandAbbreviations("Mtr_Start_Vlv", abbreviations)
        |> should equal "Motor_Start_Valve"

    [<Fact>]
    let ``NamingAnalyzer should calculate name similarity`` () =
        let analyzer = createAnalyzer()

        analyzer.CalculateSimilarity("Motor_Start", "Motor_Stop") |> should be (greaterThan 0.5f)
        analyzer.CalculateSimilarity("Motor_Start", "Valve_Open") |> should be (lessThan 0.5f)
        analyzer.CalculateSimilarity("ABC", "ABC") |> should equal 1.0f

    [<Fact>]
    let ``NamingAnalyzer should group similar names`` () =
        let analyzer = createAnalyzer()
        let names = ["Motor_Start"; "Motor_Stop"; "Motor_Speed"; "Valve_Open"; "Valve_Close"]

        let groups = analyzer.GroupSimilarNames(names, 0.5f)

        groups |> List.length |> should equal 2
        groups |> List.exists (fun g -> g |> List.contains "Motor_Start") |> should be True
        groups |> List.exists (fun g -> g |> List.contains "Valve_Open") |> should be True

    [<Fact>]
    let ``NamingAnalyzer should validate character usage`` () =
        let analyzer = createAnalyzer()
        let allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_"

        analyzer.ValidateCharacters("Motor_Start_1", allowedChars) |> should be True
        analyzer.ValidateCharacters("Motor-Start", allowedChars) |> should be False
        analyzer.ValidateCharacters("Motor Start", allowedChars) |> should be False
        analyzer.ValidateCharacters("Motor@Start", allowedChars) |> should be False

    [<Fact>]
    let ``NamingAnalyzer should check reserved words`` () =
        let analyzer = createAnalyzer()
        let reserved = ["IF"; "THEN"; "ELSE"; "END"; "FOR"; "WHILE"]

        analyzer.IsReservedWord("IF", reserved) |> should be True
        analyzer.IsReservedWord("Motor", reserved) |> should be False
        analyzer.IsReservedWord("END_Motor", reserved) |> should be False  // Contains but isn't exactly reserved

    [<Fact>]
    let ``NamingAnalyzer should generate naming report`` () =
        let analyzer = createAnalyzer()
        let names = TestData.createSampleVariables() |> List.map (fun v -> v.Name)

        let report = analyzer.GenerateNamingReport(names)

        report |> should haveSubstring "Total Names:"
        report |> should haveSubstring "Naming Patterns:"
        report |> should haveSubstring "Common Prefixes:"