namespace Ev2.PLC.Mapper.Test.Unit

open System
open System.IO
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Parsers.AllenBradley
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes
open Ev2.PLC.Mapper.Test.TestData.SampleDataProvider

/// Allen-Bradley 파서 테스트
module AllenBradleyParserTests =

    let createParser() =
        let logger = NullLogger<AllenBradleyParser>.Instance
        AllenBradleyParserFactory.create logger

    [<Fact>]
    let ``AllenBradleyParser should support correct vendor and formats`` () =
        // Arrange
        let parser = createParser()
        
        // Act & Assert
        parser.SupportedVendor |> should equal (PlcVendor.CreateAllenBradley())
        parser.SupportedFormats |> should haveLength 1
        
        match parser.SupportedFormats.Head with
        | AllenBradleyL5K _ -> ()
        | _ -> failwith "Should support AllenBradleyL5K format"

    [<Fact>]
    let ``AllenBradleyParser should identify compatible formats`` () =
        // Arrange
        let parser = createParser()
        
        // Act & Assert
        parser.CanParse(AllenBradleyL5K "test.l5k") |> should equal true
        parser.CanParse(LSElectricXML "test.xml") |> should equal false
        parser.CanParse(CustomFormat("test", "custom")) |> should equal false

    [<Fact>]
    let ``AllenBradleyParser should parse valid L5K content`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let l5kContent = createAllenBradleyL5KSample variables
        let format = AllenBradleyL5K "test.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(l5kContent, format)
        
        // Assert
        result.ProjectInfo.Name |> should equal "TestProject"
        result.Variables |> should haveLength (variables.Length)
        
        // 첫 번째 변수 확인
        let firstVar = result.Variables.Head
        firstVar.Name |> should equal "AREA1_MOTOR1_START"
        firstVar.DataType |> should equal "BOOL"
    }

    [<Fact>]
    let ``AllenBradleyParser should handle empty L5K content`` () = task {
        // Arrange
        let parser = createParser()
        let emptyL5K = """RSLogix 5000 Export File
L5K
Version: 1.0

PROJECT EmptyProject
  Description: Empty test project"""
        let format = AllenBradleyL5K "empty.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(emptyL5K, format)
        
        // Assert
        result.Variables |> should haveLength 0
        result.ProjectInfo.Name |> should equal "EmptyProject"
    }

    [<Fact>]
    let ``AllenBradleyParser should parse file correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let l5kContent = createAllenBradleyL5KSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".l5k")
        File.WriteAllText(tempFile, l5kContent)
        
        try
            // Act
            let! result = parser.ParseAsync(tempFile)
            
            // Assert
            result.ProjectInfo.FilePath |> should equal tempFile
            result.Variables |> should haveLength (variables.Length)
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``AllenBradleyParser should throw exception for non-existent file`` () = task {
        // Arrange
        let parser = createParser()
        let nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".l5k")
        
        // Act & Assert
        let! ex = Assert.ThrowsAsync<FileNotFoundException>(fun () ->
            parser.ParseAsync(nonExistentFile) :> Task
        )
        ex |> should not' (be null)
    }

    [<Fact>]
    let ``AllenBradleyParser should validate L5K file correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let l5kContent = createAllenBradleyL5KSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".l5k")
        File.WriteAllText(tempFile, l5kContent)
        
        try
            // Act
            let! result = parser.ValidateFileAsync(tempFile)
            
            // Assert
            result.IsValid |> should equal true
            result.Severity |> should equal ValidationSeverity.ValidationInfo
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``AllenBradleyParser should reject non-L5K file extension`` () = task {
        // Arrange
        let parser = createParser()
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt")
        File.WriteAllText(tempFile, "not l5k content")
        
        try
            // Act
            let! result = parser.ValidateFileAsync(tempFile)
            
            // Assert
            result.IsValid |> should equal true  // Warning but valid
            result.Severity |> should equal ValidationSeverity.ValidationWarning
            result.Message.Contains("Expected .L5K file extension") |> should equal true
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``AllenBradleyParser should extract tags correctly`` () = task {
        // Arrange
        let parser = createParser()
        
        let l5kWithTags = """RSLogix 5000 Export File
L5K
Version: 1.0

PROJECT TestProject
  Description: Test Project

TAG Motor1_Start Controller BOOL // Motor 1 Start Button
TAG Motor1_Speed Controller INT // Motor 1 Speed Setting
TAG Cylinder_Extend Controller BOOL
"""
        
        let format = AllenBradleyL5K "test.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(l5kWithTags, format)
        
        // Assert
        result.Variables |> should haveLength 3
        
        let motorStartTag = result.Variables |> List.find (fun v -> v.Name = "Motor1_Start")
        motorStartTag.DataType |> should equal "BOOL"
        motorStartTag.Comment |> should equal (Some "Motor 1 Start Button")
        
        let motorSpeedTag = result.Variables |> List.find (fun v -> v.Name = "Motor1_Speed")
        motorSpeedTag.DataType |> should equal "INT"
        motorSpeedTag.Comment |> should equal (Some "Motor 1 Speed Setting")
        
        let cylinderTag = result.Variables |> List.find (fun v -> v.Name = "Cylinder_Extend")
        cylinderTag.DataType |> should equal "BOOL"
        cylinderTag.Comment |> should equal None
    }

    [<Fact>]
    let ``AllenBradleyParser should extract project info correctly`` () = task {
        // Arrange
        let parser = createParser()
        
        let l5kWithProjectInfo = """RSLogix 5000 Export File
L5K
Version: 1.0

PROJECT MyTestProject
  Description: This is a test project for Allen-Bradley
  Revision: 2.1

CONTROLLER MainController
  ProcessorType: 1756-L73
  Revision: 33.11
"""
        
        let format = AllenBradleyL5K "test.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(l5kWithProjectInfo, format)
        
        // Assert
        result.ProjectInfo.Name |> should equal "MyTestProject"
        result.ProjectInfo.Vendor |> should equal (PlcVendor.CreateAllenBradley())
        
        match result.ProjectInfo.Format with
        | AllenBradleyL5K _ -> ()
        | _ -> failwith "Should have AllenBradleyL5K format"
    }

    [<Fact>]
    let ``AllenBradleyParser should handle malformed L5K content gracefully`` () = task {
        // Arrange
        let parser = createParser()
        
        let malformedL5K = """RSLogix 5000 Export File
L5K
This is not a valid L5K format
PROJECT IncompleteProject
  Description: Incomplete
"""
        
        let format = AllenBradleyL5K "malformed.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(malformedL5K, format)
        
        // Assert
        // 파서는 예외를 발생시키지 않고 가능한 만큼 파싱해야 함
        result.Variables |> should haveLength 0
        result.ProjectInfo.Name |> should equal "IncompleteProject"
    }

    [<Fact>]
    let ``AllenBradleyParser should parse tags with complex data types`` () = task {
        // Arrange
        let parser = createParser()
        
        let l5kWithComplexTypes = """RSLogix 5000 Export File
L5K
Version: 1.0

PROJECT ComplexProject
  Description: Complex Project

TAG MyTimer Controller TIMER // Timer instance
TAG MyCounter Controller COUNTER // Counter instance
TAG MyArray Controller DINT[10] // Integer array
TAG MyString Controller STRING // String variable
TAG MyUDT Controller MyCustomType // User defined type
"""
        
        let format = AllenBradleyL5K "complex.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(l5kWithComplexTypes, format)
        
        // Assert
        result.Variables |> should haveLength 5
        
        let timerTag = result.Variables |> List.find (fun v -> v.Name = "MyTimer")
        timerTag.DataType |> should equal "TIMER"
        
        let arrayTag = result.Variables |> List.find (fun v -> v.Name = "MyArray")
        arrayTag.DataType |> should equal "DINT[10]"
        
        let udtTag = result.Variables |> List.find (fun v -> v.Name = "MyUDT")
        udtTag.DataType |> should equal "MyCustomType"
    }

    [<Fact>]
    let ``AllenBradleyParser should handle Studio 5000 format variations`` () = task {
        // Arrange
        let parser = createParser()
        
        let studio5000Format = """Studio 5000 Logix Designer Export File
L5K
Version: 32.00

PROJECT Studio5000Project
  TargetType: Controller

CONTROLLER ControlLogix
  ProcessorType: 1756-L85E
  MajorRev: 32
  MinorRev: 11

TAG Process_Start Controller BOOL := 0 // Process start command
TAG Process_Value Controller REAL := 0.0 // Process value
"""
        
        let format = AllenBradleyL5K "studio5000.l5k"
        
        // Act
        let! result = parser.ParseContentAsync(studio5000Format, format)
        
        // Assert
        result.ProjectInfo.Name |> should equal "Studio5000Project"
        result.Variables |> should haveLength 2
        
        let startTag = result.Variables |> List.find (fun v -> v.Name = "Process_Start")
        startTag.DataType |> should equal "BOOL"
        startTag.InitialValue |> should equal (Some "0")
        
        let valueTag = result.Variables |> List.find (fun v -> v.Name = "Process_Value")
        valueTag.DataType |> should equal "REAL"
        valueTag.InitialValue |> should equal (Some "0.0")
    }