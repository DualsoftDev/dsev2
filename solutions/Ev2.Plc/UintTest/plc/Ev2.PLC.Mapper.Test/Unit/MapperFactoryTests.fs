namespace Ev2.PLC.Mapper.Test.Unit

open System
open System.IO
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open DSPLCServer.Common
open Ev2.PLC.Mapper
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes
open Ev2.PLC.Mapper.Test.TestData.SampleDataProvider

/// MapperFactory 테스트
module MapperFactoryTests =

    [<Fact>]
    let ``MapperApi should create factory without configuration`` () =
        // Act
        let factory = MapperApi.createFactory(None)
        
        // Assert
        factory |> should not' (be null)

    [<Fact>]
    let ``MapperFactory should create instance with configuration file`` () =
        // Arrange
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        let configFile = Path.Combine(tempDir, "test-config.json")
        
        let config = 
            TestConfigBuilder()
                .AddDeviceHint("MOTOR", "Motor")
                .Build()
        
        let configJson = System.Text.Json.JsonSerializer.Serialize(config, 
            System.Text.Json.JsonSerializerOptions(WriteIndented = true))
        File.WriteAllText(configFile, configJson)
        
        try
            // Act
            let factory = MapperApi.createFactory(Some configFile)
            
            // Assert
            factory |> should not' (be null)
            
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)

    [<Fact>]
    let ``MapperFactory should process LSElectric file correctly`` () = task {
        // Arrange
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        File.WriteAllText(tempFile, xmlContent)
        
        try
            // Act
            let! result = MapperApi.processFileAsync tempFile
            
            // Assert
            result |> should be (instanceOfType<MappingResult>)
            if result.Success then
                result.Areas |> should not' (be Empty)
                result.ProjectInfo.Vendor |> should equal PlcVendor.LSElectric
            else
                failwithf "Processing should succeed but got errors: %A" result.Errors
                
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``MapperFactory should process AllenBradley file correctly`` () = task {
        // Arrange
        let variables = createStandardTestVariables()
        let l5kContent = createAllenBradleyL5KSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".l5k")
        File.WriteAllText(tempFile, l5kContent)
        
        try
            // Act
            let! result = MapperApi.processFileAsync tempFile
            
            // Assert
            result |> should be (instanceOfType<MappingResult>)
            if result.Success then
                result.Areas |> should not' (be Empty)
                result.ProjectInfo.Vendor |> should equal PlcVendor.AllenBradley
            else
                failwithf "Processing should succeed but got errors: %A" result.Errors
                
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``MapperFactory should return error for unsupported file type`` () = task {
        // Arrange
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt")
        File.WriteAllText(tempFile, "unsupported content")
        
        try
            // Act
            let! result = MapperApi.processFileAsync tempFile
            
            // Assert
            if result.Success then
                failwith "Should return error for unsupported file type"
            else
                result.Errors |> should not' (be Empty)
                
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``MapperFactory should return error for non-existent file`` () = task {
        // Arrange
        let nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        
        // Act
        let! result = MapperApi.processFileAsync nonExistentFile
        
        // Assert
        if result.Success then
            failwith "Should return error for non-existent file"
        else
            result.Errors |> should not' (be Empty)
    }

    [<Fact>]
    let ``MapperFactory should process file with custom configuration`` () = task {
        // Arrange
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        let configFile = Path.Combine(tempDir, "custom-config.json")
        let config = 
            TestConfigBuilder()
                .AddDeviceHint("MOTOR", "Motor")
                .AddDeviceHint("CUSTOM_DEVICE", "Custom")
                .Build()
        
        let configJson = System.Text.Json.JsonSerializer.Serialize(config, 
            System.Text.Json.JsonSerializerOptions(WriteIndented = true))
        File.WriteAllText(configFile, configJson)
        
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        let dataFile = Path.Combine(tempDir, "test.xml")
        File.WriteAllText(dataFile, xmlContent)
        
        try
            // Act
            let! result = MapperApi.processFileWithConfigFileAsync dataFile configFile
            
            // Assert
            if result.Success then
                result.Areas |> should not' (be Empty)
            else
                failwithf "Processing with custom config should succeed but got errors: %A" result.Errors
                
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }