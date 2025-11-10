namespace Ev2.PLC.Mapper.Test.Integration

open System
open System.IO
open System.Diagnostics
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Mapper
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes
open Ev2.PLC.Mapper.Test.TestData.SampleDataProvider

/// 성능 테스트
module PerformanceTests =

    [<Fact>]
    let ``Large file processing should complete within reasonable time`` () = task {
        // Arrange
        let variables = 
            [1..1000] 
            |> List.map (fun i -> 
                TestVariableBuilder()
                    .WithName($"AREA{i % 10}_DEVICE{i}_STATUS")
                    .WithAddress($"X{i}")
                    .WithDataType("BOOL")
                    .Build())
        
        let xmlContent = createLSElectricXmlSample variables
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        File.WriteAllText(tempFile, xmlContent)
        
        try
            // Act
            let stopwatch = Stopwatch.StartNew()
            let! result = MapperApi.processFileAsync tempFile
            stopwatch.Stop()
            
            // Assert
            stopwatch.ElapsedMilliseconds |> should be (lessThan 5000L) // Should complete within 5 seconds
            
            if result.Success then
                result.Areas |> should not' (be Empty)
            else
                failwithf "Performance test should succeed but got errors: %A" result.Errors
                
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``Memory usage should remain stable during processing`` () = task {
        // Arrange
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        File.WriteAllText(tempFile, xmlContent)
        
        try
            // Get initial memory
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            let initialMemory = GC.GetTotalMemory(false)
            
            // Act - Process multiple times
            for i in 1..10 do
                let! result = MapperApi.processFileAsync tempFile
                if not result.Success then
                    failwithf "Processing iteration %d failed: %A" i result.Errors
            
            // Check memory after processing
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            let finalMemory = GC.GetTotalMemory(false)
            
            // Assert
            let memoryIncrease = finalMemory - initialMemory
            memoryIncrease |> should be (lessThan (10L * 1024L * 1024L)) // Less than 10MB increase
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``Concurrent processing should not cause conflicts`` () = task {
        // Arrange
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        let tasks = 
            [1..5] 
            |> List.map (fun i -> task {
                let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
                File.WriteAllText(tempFile, xmlContent)
                
                try
                    let! result = MapperApi.processFileAsync tempFile
                    return (i, result)
                finally
                    if File.Exists(tempFile) then
                        File.Delete(tempFile)
            })
        
        // Act
        let! results = Task.WhenAll(tasks)
        
        // Assert
        results |> Array.length |> should equal 5
        
        for (taskId, result) in results do
            if result.Success then
                result.Areas |> should not' (be Empty)
            else
                failwithf "Concurrent task %d should succeed but got errors: %A" taskId result.Errors
    }