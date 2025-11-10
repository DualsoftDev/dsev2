namespace Ev2.PLC.Mapper.Test

open System
open System.Collections.Generic
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Engine
open TestHelpers

module MappingEngineTests =

    let createEngine() =
        let logger = createLogger<MappingEngine>()
        MappingEngine(logger)

    [<Fact>]
    let ``MappingEngine should map variables to device groups`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()

        let mapped = engine.MapVariables(variables, config)

        mapped.DeviceGroups |> should not' (be Empty)
        mapped.DeviceGroups.Count |> should be (greaterThan 0)

    [<Fact>]
    let ``MappingEngine should apply naming conventions`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()

        let mapped = engine.ApplyNamingConventions(variables, config.Rules.VariableNaming)

        mapped |> List.iter (fun v ->
            v.Name |> should startWith "PLC_"
        )

    [<Fact>]
    let ``MappingEngine should generate API definitions`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()

        let apis = engine.GenerateApiDefinitions(variables, config.Rules.ApiGeneration)

        apis |> should not' (be Empty)
        apis |> List.exists (fun a -> a.Type = ApiType.Read) |> should be True
        apis |> List.exists (fun a -> a.Type = ApiType.Write) |> should be True

    [<Fact>]
    let ``MappingEngine should create device groups by prefix`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()

        let groups = engine.CreateDeviceGroups(variables, config.Rules.DeviceGrouping)

        groups |> should not' (be Empty)
        groups |> List.exists (fun g -> g.Name = "Motor1") |> should be True
        groups |> List.exists (fun g -> g.Name = "Temperature") |> should be True

    [<Fact>]
    let ``MappingEngine should validate mapping rules`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let isValid = engine.ValidateMappingRules(config.Rules)

        isValid |> should be True

    [<Fact>]
    let ``MappingEngine should handle batch API generation`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables() @ TestData.createSampleVariables()  // Double for batch

        let apis = engine.GenerateBatchApis(variables, config.Rules.ApiGeneration)

        apis |> should not' (be Empty)
        apis |> List.forall (fun a -> a.Variables.Length <= config.Rules.ApiGeneration.MaxBatchSize) |> should be True

    [<Fact>]
    let ``MappingEngine should map logic to workflows`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let logic = TestData.createSampleLogic()

        let workflows = engine.MapLogicToWorkflows(logic, config)

        workflows |> should not' (be Empty)
        workflows |> List.exists (fun w -> w.Type = WorkflowType.Sequential) |> should be True

    [<Fact>]
    let ``MappingEngine should optimize device groups`` () =
        let engine = createEngine()
        let groups = [
            { Name = "Group1"; Variables = TestData.createSampleVariables() |> List.take 1 }
            { Name = "Group2"; Variables = TestData.createSampleVariables() |> List.take 1 }
        ]

        let optimized = engine.OptimizeDeviceGroups(groups, 2)

        optimized.Length |> should be (lessThan groups.Length)

    [<Fact>]
    let ``MappingEngine should generate mapping report`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()
        let logic = TestData.createSampleLogic()

        let report = engine.GenerateMappingReport(variables, logic, config)

        report |> should haveSubstring "Mapping Report"
        report |> should haveSubstring "Variables:"
        report |> should haveSubstring "Logic Blocks:"

    [<Fact>]
    let ``MappingEngine should handle vendor-specific mappings`` () =
        let engine = createEngine()

        let abConfig = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let lsConfig = TestData.createTestMappingConfig(PlcVendor.LSElectric)
        let variables = TestData.createSampleVariables()

        let abMapped = engine.MapVariables(variables, abConfig)
        let lsMapped = engine.MapVariables(variables, lsConfig)

        // Different vendors might produce different mappings
        abMapped.Vendor |> should equal PlcVendor.AllenBradley
        lsMapped.Vendor |> should equal PlcVendor.LSElectric

    [<Fact>]
    let ``MappingEngine should validate variable addresses`` () =
        let engine = createEngine()
        let validVariables = TestData.createSampleVariables()
        let invalidVariables = [
            { TestData.createSampleVariables().[0] with Address = "" }
            { TestData.createSampleVariables().[1] with Address = "INVALID" }
        ]

        engine.ValidateVariableAddresses(validVariables) |> should be True
        engine.ValidateVariableAddresses(invalidVariables) |> should be False

    [<Fact>]
    let ``MappingEngine should merge duplicate variables`` () =
        let engine = createEngine()
        let duplicates = [
            TestData.createSampleVariables().[0]
            TestData.createSampleVariables().[0]  // Duplicate
            TestData.createSampleVariables().[1]
        ]

        let merged = engine.MergeDuplicateVariables(duplicates)

        merged.Length |> should equal 2  // One duplicate removed

    [<Fact>]
    let ``MappingEngine should calculate mapping statistics`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let variables = TestData.createSampleVariables()
        let logic = TestData.createSampleLogic()

        let stats = engine.CalculateMappingStatistics(variables, logic, config)

        stats.TotalVariables |> should equal 5
        stats.TotalLogicBlocks |> should equal 2
        stats.MappingCoverage |> should be (greaterThan 0.0)

    [<Fact>]
    let ``MappingEngine should export mapping configuration`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let exported = engine.ExportConfiguration(config)

        exported |> should haveSubstring "AllenBradley"
        exported |> should haveSubstring "PLC_"

    [<Fact>]
    let ``MappingEngine should import mapping configuration`` () =
        let engine = createEngine()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let exported = engine.ExportConfiguration(config)

        let imported = engine.ImportConfiguration(exported)

        imported.Vendor |> should equal config.Vendor
        imported.Rules.VariableNaming.Prefix |> should equal config.Rules.VariableNaming.Prefix