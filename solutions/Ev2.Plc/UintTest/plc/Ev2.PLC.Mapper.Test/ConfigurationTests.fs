namespace Ev2.PLC.Mapper.Test

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Configuration
open TestHelpers

module ConfigurationTests =

    let createManager() =
        let logger = createLogger<ConfigurationManager>()
        ConfigurationManager(logger)

    [<Fact>]
    let ``ConfigurationManager should load configuration from file`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let configJson = Newtonsoft.Json.JsonConvert.SerializeObject(config)
        let tempFile = createTempFile ".json" configJson

        try
            let loaded = manager.LoadConfiguration(tempFile)

            loaded.Vendor |> should equal PlcVendor.AllenBradley
            loaded.Rules.VariableNaming.Prefix |> should equal (Some "PLC_")
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``ConfigurationManager should save configuration to file`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let tempFile = Path.GetTempFileName() + ".json"

        try
            manager.SaveConfiguration(config, tempFile)

            File.Exists(tempFile) |> should be True
            let content = File.ReadAllText(tempFile)
            content |> should haveSubstring "AllenBradley"
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``ConfigurationManager should validate configuration`` () =
        let manager = createManager()
        let validConfig = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let invalidConfig = { validConfig with
            Rules = { validConfig.Rules with
                DeviceGrouping = { validConfig.Rules.DeviceGrouping with
                    MinGroupSize = -1 }}}

        manager.ValidateConfiguration(validConfig) |> should be True
        manager.ValidateConfiguration(invalidConfig) |> should be False

    [<Fact>]
    let ``ConfigurationManager should merge configurations`` () =
        let manager = createManager()
        let config1 = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let config2 = { config1 with
            Rules = { config1.Rules with
                VariableNaming = { config1.Rules.VariableNaming with
                    Suffix = Some "_END" }}}

        let merged = manager.MergeConfigurations(config1, config2)

        merged.Rules.VariableNaming.Prefix |> should equal (Some "PLC_")
        merged.Rules.VariableNaming.Suffix |> should equal (Some "_END")

    [<Fact>]
    let ``ConfigurationManager should apply default configuration`` () =
        let manager = createManager()

        let defaults = manager.GetDefaultConfiguration(PlcVendor.AllenBradley)

        defaults.Vendor |> should equal PlcVendor.AllenBradley
        defaults.Rules |> should not' (be null)

    [<Fact>]
    let ``ConfigurationManager should detect configuration changes`` () =
        let manager = createManager()
        let config1 = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let config2 = { config1 with
            Rules = { config1.Rules with
                ApiGeneration = { config1.Rules.ApiGeneration with
                    UseAsync = false }}}

        let changes = manager.DetectChanges(config1, config2)

        changes |> should not' (be Empty)
        changes |> should contain "ApiGeneration.UseAsync"

    [<Fact>]
    let ``ConfigurationManager should backup configuration`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let tempDir = createTempDirectory()

        try
            let backupPath = manager.BackupConfiguration(config, tempDir)

            File.Exists(backupPath) |> should be True
            backupPath |> should haveSubstring ".backup"
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``ConfigurationManager should restore configuration from backup`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
        let tempDir = createTempDirectory()

        try
            let backupPath = manager.BackupConfiguration(config, tempDir)
            let restored = manager.RestoreConfiguration(backupPath)

            restored.Vendor |> should equal config.Vendor
            restored.Rules.VariableNaming.Prefix |> should equal config.Rules.VariableNaming.Prefix
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``ConfigurationManager should list available configurations`` () =
        let manager = createManager()
        let tempDir = createTempDirectory()

        try
            // Create multiple config files
            for vendor in [PlcVendor.AllenBradley; PlcVendor.LSElectric; PlcVendor.Mitsubishi] do
                let config = TestData.createTestMappingConfig(vendor)
                let fileName = Path.Combine(tempDir, sprintf "%A.config.json" vendor)
                manager.SaveConfiguration(config, fileName)

            let configs = manager.ListConfigurations(tempDir)

            configs |> List.length |> should equal 3
            configs |> List.map (fun c -> c.Vendor) |> should contain PlcVendor.AllenBradley
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``ConfigurationManager should export configuration as template`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let template = manager.ExportAsTemplate(config)

        template |> should haveSubstring "TEMPLATE"
        template |> should haveSubstring "VENDOR"
        template |> should haveSubstring "PREFIX"

    [<Fact>]
    let ``ConfigurationManager should import configuration from template`` () =
        let manager = createManager()
        let template = """
        {
            "VENDOR": "AllenBradley",
            "PREFIX": "TEST_",
            "SUFFIX": "_VAR",
            "ASYNC": true
        }
        """

        let config = manager.ImportFromTemplate(template, PlcVendor.AllenBradley)

        config.Vendor |> should equal PlcVendor.AllenBradley
        config.Rules.VariableNaming.Prefix |> should equal (Some "TEST_")
        config.Rules.VariableNaming.Suffix |> should equal (Some "_VAR")

    [<Fact>]
    let ``ConfigurationManager should validate naming rules`` () =
        let manager = createManager()
        let validRules = TestData.createTestMappingConfig(PlcVendor.AllenBradley).Rules.VariableNaming
        let invalidRules = { validRules with
            MinLength = Some 100
            MaxLength = Some 10 }  // Min > Max

        manager.ValidateNamingRules(validRules) |> should be True
        manager.ValidateNamingRules(invalidRules) |> should be False

    [<Fact>]
    let ``ConfigurationManager should generate configuration report`` () =
        let manager = createManager()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let report = manager.GenerateConfigurationReport(config)

        report |> should haveSubstring "Configuration Report"
        report |> should haveSubstring "Vendor: AllenBradley"
        report |> should haveSubstring "Naming Convention:"
        report |> should haveSubstring "API Generation:"

    [<Fact>]
    let ``ConfigurationManager should handle vendor-specific defaults`` () =
        let manager = createManager()

        let abDefaults = manager.GetVendorDefaults(PlcVendor.AllenBradley)
        let lsDefaults = manager.GetVendorDefaults(PlcVendor.LSElectric)

        abDefaults.Rules.VariableNaming.CasingStyle |> should not' (equal lsDefaults.Rules.VariableNaming.CasingStyle)

    [<Fact>]
    let ``ConfigurationManager should clone configuration`` () =
        let manager = createManager()
        let original = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let cloned = manager.CloneConfiguration(original)

        cloned.Vendor |> should equal original.Vendor
        cloned.Rules |> should not' (be (identical original.Rules))  // Different instance
        cloned.Rules.VariableNaming.Prefix |> should equal original.Rules.VariableNaming.Prefix