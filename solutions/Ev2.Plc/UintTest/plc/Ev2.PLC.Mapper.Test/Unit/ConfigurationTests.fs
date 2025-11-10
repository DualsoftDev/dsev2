namespace Ev2.PLC.Mapper.Test.Unit

open System
open System.IO
open System.Text.Json
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Mapper.Core.Configuration
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes

/// 설정 시스템 테스트
module ConfigurationTests =

    [<Fact>]
    let ``ConfigurationLoader should load valid JSON configuration`` () =
        // Arrange
        let logger = NullLogger<ConfigurationLoader>.Instance
        let loader = ConfigurationLoader(logger)
        
        let config = 
            TestConfigBuilder()
                .AddDeviceHint("MOTOR", "Motor")
                .AddApiCommand("START")
                .Build()
        
        let jsonContent = JsonSerializer.Serialize(config, JsonSerializerOptions(WriteIndented = true))
        
        // Act
        let result = loader.LoadFromJson(jsonContent)
        
        // Assert
        result |> should be (instanceOfType<Result<MapperConfigRoot, string>>)
        match result with
        | Ok loadedConfig ->
            loadedConfig.MappingConfiguration.DeviceTypeHints.["MOTOR"] |> should equal "Motor"
            loadedConfig.MappingConfiguration.ApiTypeHints.Commands.["START"] |> should equal "Command"
        | Error msg -> 
            failwithf "Should have succeeded but got error: %s" msg

    [<Fact>]
    let ``ConfigurationLoader should return error for invalid JSON`` () =
        // Arrange
        let logger = NullLogger<ConfigurationLoader>.Instance
        let loader = ConfigurationLoader(logger)
        let invalidJson = "{\"invalid\": json syntax"
        
        // Act
        let result = loader.LoadFromJson(invalidJson)
        
        // Assert
        match result with
        | Error msg -> 
            msg.Contains("Failed to parse configuration JSON") |> should equal true
        | Ok _ -> 
            failwith "Should have failed for invalid JSON"

    [<Fact>]
    let ``ConfigurationLoader should return error for non-existent file`` () =
        // Arrange
        let logger = NullLogger<ConfigurationLoader>.Instance
        let loader = ConfigurationLoader(logger)
        let nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json")
        
        // Act
        let result = loader.LoadFromFile(nonExistentPath)
        
        // Assert
        match result with
        | Error msg -> 
            msg.Contains("Configuration file not found") |> should equal true
        | Ok _ -> 
            failwith "Should have failed for non-existent file"

    [<Fact>]
    let ``ConfigurationProvider should use defaults when no config specified`` () =
        // Arrange
        let logger = NullLogger<ConfigurationProvider>.Instance
        let provider = ConfigurationProvider(logger) :> IConfigurationProvider
        
        // Act
        let config = provider.LoadConfiguration(None)
        
        // Assert
        config.MappingConfiguration.DeviceTypeHints.Count |> should be (greaterThan 0)
        config.MappingConfiguration.ApiTypeHints.Commands.Count |> should be (greaterThan 0)
        config.MappingOptions.ParallelProcessing |> should equal true

    [<Fact>]
    let ``ConfigurationConverter should convert device type hints correctly`` () =
        // Arrange
        let hints = System.Collections.Generic.Dictionary<string, string>()
        hints.["MOTOR"] <- "Motor"
        hints.["CYL"] <- "Cylinder"
        hints.["UNKNOWN"] <- "Custom"
        
        // Act
        let converted = ConfigurationConverter.convertDeviceTypeHints hints
        
        // Assert
        converted.["MOTOR"] |> should equal Motor
        converted.["CYL"] |> should equal Cylinder
        match converted.["UNKNOWN"] with
        | DeviceType.Custom "Custom" -> ()
        | _ -> failwith "Should convert unknown device type to Custom"

    [<Fact>]
    let ``ConfigurationConverter should convert API type hints correctly`` () =
        // Arrange
        let apiHints = {
            Commands = dict [("START", "Command"); ("STOP", "Command")] |> System.Collections.Generic.Dictionary
            Status = dict [("RUNNING", "Status")] |> System.Collections.Generic.Dictionary
            Parameters = dict [("SPEED", "Parameter")] |> System.Collections.Generic.Dictionary
            Feedback = dict [("VALUE", "Feedback")] |> System.Collections.Generic.Dictionary
        }
        
        // Act
        let converted = ConfigurationConverter.convertApiTypeHints apiHints
        
        // Assert
        converted.["START"] |> should equal Command
        converted.["STOP"] |> should equal Command
        converted.["RUNNING"] |> should equal Status
        converted.["SPEED"] |> should equal Parameter
        converted.["VALUE"] |> should equal Feedback

    [<Fact>]
    let ``ConfigurationConverter should convert naming conventions correctly`` () =
        // Arrange
        let deviceHints = System.Collections.Generic.Dictionary<string, string>()
        deviceHints.["MOTOR"] <- "Motor"
        
        let apiHints = System.Collections.Generic.Dictionary<string, string>()
        apiHints.["START"] <- "Command"
        
        let conventions : NamingConventionConfig[] = [|
            {
                Name = "Test"
                Pattern = @"^(?<area>[A-Z]+)_(?<device>[A-Z]+)_(?<api>[A-Z]+)$"
                Description = "Test pattern"
                DeviceTypeHints = deviceHints
                ApiTypeHints = apiHints
                Priority = 1
            }
        |]
        
        // Act
        let converted = ConfigurationConverter.convertNamingConventions conventions
        
        // Assert
        converted |> should haveLength 1
        let conv = converted.Head
        conv.Name |> should equal "Test"
        conv.Pattern |> should equal @"^(?<area>[A-Z]+)_(?<device>[A-Z]+)_(?<api>[A-Z]+)$"
        conv.DeviceTypeHints.["MOTOR"] |> should equal Motor
        conv.ApiTypeHints.["START"] |> should equal Command

    [<Fact>]
    let ``ConfigurationConverter should convert mapping options correctly`` () =
        // Arrange
        let options : MappingOptionsConfig = {
            AnalyzeLogicFlow = true
            GenerateApiDependencies = false
            OptimizeAddressAllocation = true
            ValidateNaming = false
            GenerateDocumentation = true
            IncludeStatistics = false
            ParallelProcessing = true
            MaxConcurrency = 4
        }
        
        // Act
        let converted = ConfigurationConverter.convertMappingOptions options
        
        // Assert
        converted.AnalyzeLogicFlow |> should equal true
        converted.GenerateApiDependencies |> should equal false
        converted.OptimizeAddressAllocation |> should equal true
        converted.ValidateNaming |> should equal false
        converted.GenerateDocumentation |> should equal true
        converted.IncludeStatistics |> should equal false
        converted.ParallelProcessing |> should equal true
        converted.MaxConcurrency |> should equal 4

    [<Fact>]
    let ``ConfigurationConverter should handle zero MaxConcurrency`` () =
        // Arrange
        let options : MappingOptionsConfig = {
            AnalyzeLogicFlow = true
            GenerateApiDependencies = true
            OptimizeAddressAllocation = true
            ValidateNaming = true
            GenerateDocumentation = false
            IncludeStatistics = true
            ParallelProcessing = true
            MaxConcurrency = 0  // Should be converted to Environment.ProcessorCount
        }
        
        // Act
        let converted = ConfigurationConverter.convertMappingOptions options
        
        // Assert
        converted.MaxConcurrency |> should equal Environment.ProcessorCount

    [<Fact>]
    let ``ConfigurationProvider should save and load configuration successfully`` () =
        // Arrange
        let logger = NullLogger<ConfigurationProvider>.Instance
        let provider = ConfigurationProvider(logger) :> IConfigurationProvider
        
        let config = 
            TestConfigBuilder()
                .AddDeviceHint("TEST_DEVICE", "Motor")
                .AddApiCommand("TEST_CMD")
                .Build()
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json")
        
        try
            // Act - Save
            let saveResult = provider.SaveConfiguration(config, tempFile)
            
            // Assert - Save
            match saveResult with
            | Ok () -> File.Exists(tempFile) |> should equal true
            | Error msg -> failwithf "Save failed: %s" msg
            
            // Act - Load
            let loadedConfig = provider.LoadConfiguration(Some tempFile)
            
            // Assert - Load
            loadedConfig.MappingConfiguration.DeviceTypeHints.["TEST_DEVICE"] |> should equal "Motor"
            loadedConfig.MappingConfiguration.ApiTypeHints.Commands.["TEST_CMD"] |> should equal "Command"
        
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)