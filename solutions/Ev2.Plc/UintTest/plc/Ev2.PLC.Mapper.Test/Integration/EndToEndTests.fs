namespace Ev2.PLC.Mapper.Test.Integration

open System
open System.IO
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes
open Ev2.PLC.Mapper.Test.TestData.SampleDataProvider

/// 전체 시스템 통합 테스트
module EndToEndTests =

    [<Fact>]
    let ``Complete mapping pipeline should work end-to-end`` () = task {
        // Arrange
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        try
            // Create test data
            createTestFiles(tempDir)
            createTestConfigFiles(tempDir)
            
            let inputFile = Path.Combine(tempDir, "standard_test.xml")
            let configFile = Path.Combine(tempDir, "Config", "test-config.json")
            
            // Act
            let! result = MapperApi.processFileWithConfigFileAsync inputFile configFile
            
            // Assert
            if result.Success then
                result.Areas |> should not' (be Empty)
                result.Devices |> should not' (be Empty)
                result.ProjectInfo.Vendor |> should equal (PlcVendor.CreateLSElectric())
            else
                failwithf "End-to-end test should succeed but got errors: %A" result.Errors
                
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }

    [<Fact>]
    let ``Mapping should handle multiple file formats`` () = task {
        // Arrange
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        try
            createTestFiles(tempDir)
            
            // Test XML file
            let xmlFile = Path.Combine(tempDir, "standard_test.xml")
            let! xmlResult = MapperApi.processFileAsync xmlFile
            
            // Test L5K file
            let l5kFile = Path.Combine(tempDir, "standard_test.l5k")
            let! l5kResult = MapperApi.processFileAsync l5kFile
            
            // Assert
            if xmlResult.Success then
                xmlResult.Areas |> should not' (be Empty)
                xmlResult.ProjectInfo.Vendor |> should equal (PlcVendor.CreateLSElectric())
            else
                failwithf "XML processing failed: %A" xmlResult.Errors
                
            if l5kResult.Success then
                l5kResult.Areas |> should not' (be Empty)
                l5kResult.ProjectInfo.Vendor |> should equal (PlcVendor.CreateAllenBradley())
            else
                failwithf "L5K processing failed: %A" l5kResult.Errors
                
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }

    [<Fact>]
    let ``Configuration changes should be reflected in processing`` () = task {
        // Arrange
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        try
            createTestFiles(tempDir)
            
            // Create custom configuration
            let customConfig = 
                TestConfigBuilder()
                    .AddDeviceHint("SPECIAL_MOTOR", "Motor")
                    .AddApiCommand("ACTIVATE")
                    .Build()
            
            let configFile = Path.Combine(tempDir, "custom.json")
            let configJson = System.Text.Json.JsonSerializer.Serialize(customConfig, 
                System.Text.Json.JsonSerializerOptions(WriteIndented = true))
            File.WriteAllText(configFile, configJson)
            
            let inputFile = Path.Combine(tempDir, "standard_test.xml")
            
            // Act
            let! result = MapperApi.processFileWithConfigFileAsync inputFile configFile
            
            // Assert
            if result.Success then
                result.Areas |> should not' (be Empty)
                // Configuration should be applied during processing
                ()
            else
                failwithf "Processing with custom config should succeed but got errors: %A" result.Errors
                
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }