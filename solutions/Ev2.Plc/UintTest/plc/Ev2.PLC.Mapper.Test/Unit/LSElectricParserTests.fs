namespace Ev2.PLC.Mapper.Test.Unit

open System
open System.IO
open System.Threading.Tasks
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Parsers.LSElectric
open Ev2.PLC.Mapper.Test.TestData.TestDataTypes
open Ev2.PLC.Mapper.Test.TestData.SampleDataProvider

/// LS일렉트릭 파서 테스트
module LSElectricParserTests =

    let createParser() =
        let logger = NullLogger<LSElectricParser>.Instance
        LSElectricParserFactory.create logger

    [<Fact>]
    let ``LSElectricParser should support correct vendor and formats`` () =
        // Arrange
        let parser = createParser()
        
        // Act & Assert
        parser.SupportedVendor |> should equal (PlcVendor.CreateLSElectric())
        parser.SupportedFormats |> should haveLength 1
        
        match parser.SupportedFormats.Head with
        | LSElectricXML _ -> ()
        | _ -> failwith "Should support LSElectricXML format"

    [<Fact>]
    let ``LSElectricParser should identify compatible formats`` () =
        // Arrange
        let parser = createParser()
        
        // Act & Assert
        parser.CanParse(LSElectricXML "test.xml") |> should equal true
        parser.CanParse(AllenBradleyL5K "test.l5k") |> should equal false
        parser.CanParse(CustomFormat("test", "custom")) |> should equal false

    [<Fact>]
    let ``LSElectricParser should parse valid XML content`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        let format = LSElectricXML "test.xml"
        
        // Act
        let! result = parser.ParseContentAsync(xmlContent, format)
        
        // Assert
        result.ProjectInfo.Name |> should equal "TestProject"
        result.ProjectInfo.Version |> should equal "1.0.0"
        result.Variables |> should haveLength (variables.Length)
        result.Logic |> should haveLength 1
        
        // 첫 번째 변수 확인
        let firstVar = result.Variables.Head
        firstVar.Name |> should equal "AREA1_MOTOR1_START"
        firstVar.Address |> should equal "X100"
        firstVar.DataType |> should equal "BOOL"
    }

    [<Fact>]
    let ``LSElectricParser should handle empty XML content`` () = task {
        // Arrange
        let parser = createParser()
        let emptyXml = """<?xml version="1.0" encoding="UTF-8"?><Project></Project>"""
        let format = LSElectricXML "empty.xml"
        
        // Act
        let! result = parser.ParseContentAsync(emptyXml, format)
        
        // Assert
        result.Variables |> should haveLength 0
        result.Logic |> should haveLength 0
    }

    [<Fact>]
    let ``LSElectricParser should throw exception for invalid XML`` () = task {
        // Arrange
        let parser = createParser()
        let invalidXml = "<InvalidXml>"
        let format = LSElectricXML "invalid.xml"
        
        // Act & Assert
        let! ex = Assert.ThrowsAsync<System.Xml.XmlException>(fun () ->
            parser.ParseContentAsync(invalidXml, format) :> Task
        )
        ex |> should not' (be null)
    }

    [<Fact>]
    let ``LSElectricParser should parse file correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        File.WriteAllText(tempFile, xmlContent)
        
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
    let ``LSElectricParser should throw exception for non-existent file`` () = task {
        // Arrange
        let parser = createParser()
        let nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        
        // Act & Assert
        let! ex = Assert.ThrowsAsync<FileNotFoundException>(fun () ->
            parser.ParseAsync(nonExistentFile) :> Task
        )
        ex |> should not' (be null)
    }

    [<Fact>]
    let ``LSElectricParser should validate XML file correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xml")
        File.WriteAllText(tempFile, xmlContent)
        
        try
            // Act1
            let! result = parser.ValidateFileAsync(tempFile)
            
            // Assert
            result.IsValid |> should equal true
            result.Severity |> should equal ValidationSeverity.ValidationInfo
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``LSElectricParser should reject non-XML file extension`` () = task {
        // Arrange
        let parser = createParser()
        let tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt")
        File.WriteAllText(tempFile, "not xml content")
        
        try
            // Act
            let! result = parser.ValidateFileAsync(tempFile)
            
            // Assert
            result.IsValid |> should equal true  // Warning but valid
            result.Severity |> should equal ValidationSeverity.ValidationWarning
            result.Message.Contains("Expected .xml file extension") |> should equal true
            
        finally
            if File.Exists(tempFile) then
                File.Delete(tempFile)
    }

    [<Fact>]
    let ``LSElectricParser should extract symbol table correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        // Act
        let! symbols = parser.ExtractSymbolTableAsync(xmlContent)
        
        // Assert
        symbols |> should haveLength (variables.Length)
        
        let motorStartSymbol = symbols |> List.find (fun s -> s.Name = "AREA1_MOTOR1_START")
        motorStartSymbol.Address |> should equal "X100"
        motorStartSymbol.DataType |> should equal "BOOL"
        motorStartSymbol.Comment |> should equal (Some "Motor 1 Start Button")
    }

    [<Fact>]
    let ``LSElectricParser should extract ladder logic correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        // Act
        let! logic = parser.ExtractLadderLogicAsync(xmlContent)
        
        // Assert
        logic |> should haveLength 1
        
        let rung = logic.Head
        rung.Type |> should equal LadderRung
        rung.Variables |> should contain "AREA1_MOTOR1_START"
        rung.Variables |> should contain "AREA1_MOTOR1_RUNNING"
    }

    [<Fact>]
    let ``LSElectricParser should extract project info correctly`` () = task {
        // Arrange
        let parser = createParser()
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        
        // Act
        let! projectInfo = parser.ExtractProjectInfoAsync(xmlContent)
        
        // Assert
        projectInfo.Name |> should equal "TestProject"
        projectInfo.Version |> should equal "1.0.0"
        projectInfo.Vendor |> should equal (PlcVendor.CreateLSElectric())
        
        match projectInfo.Format with
        | LSElectricXML _ -> ()
        | _ -> failwith "Should have LSElectricXML format"
    }

    [<Fact>]
    let ``LSElectricParser should handle XG5000 project directory`` () = task {
        // Arrange
        let parser = createParser()
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        let variables = createStandardTestVariables()
        let xmlContent = createLSElectricXmlSample variables
        let mainFile = Path.Combine(tempDir, "main.xml")
        File.WriteAllText(mainFile, xmlContent)
        
        try
            // Act
            let! result = parser.ParseXG5000ProjectAsync(tempDir)
            
            // Assert
            result.Variables |> should haveLength (variables.Length)
            result.ProjectInfo.Name |> should equal "TestProject"
            
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }

    [<Fact>]
    let ``LSElectricParser should throw exception for empty project directory`` () = task {
        // Arrange
        let parser = createParser()
        let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory(tempDir) |> ignore
        
        try
            // Act & Assert
            let! ex = Assert.ThrowsAsync<InvalidOperationException>(fun () ->
                parser.ParseXG5000ProjectAsync(tempDir) :> Task
            )
            ex |> should not' (be null)
            
        finally
            if Directory.Exists(tempDir) then
                Directory.Delete(tempDir, true)
    }

    [<Fact>]
    let ``LSElectricParser should parse variables with all optional fields`` () = task {
        // Arrange
        let parser = createParser()
        
        let xmlWithOptionalFields = """<?xml version="1.0" encoding="UTF-8"?>
<Project Name="TestProject" Version="1.0.0">
  <SymbolTable>
    <Symbol Name="TEST_VAR" Address="X100" DataType="BOOL" Comment="Test Comment" InitialValue="FALSE" Scope="Global" />
  </SymbolTable>
</Project>"""
        
        let format = LSElectricXML "test.xml"
        
        // Act
        let! result = parser.ParseContentAsync(xmlWithOptionalFields, format)
        
        // Assert
        result.Variables |> should haveLength 1
        
        let variable = result.Variables.Head
        variable.Name |> should equal "TEST_VAR"
        variable.Address |> should equal "X100"
        variable.DataType |> should equal "BOOL"
        variable.Comment |> should equal (Some "Test Comment")
        variable.InitialValue |> should equal (Some "FALSE")
        variable.Scope |> should equal (Some "Global")
    }

    [<Fact>]
    let ``LSElectricParser should handle missing optional attributes gracefully`` () = task {
        // Arrange
        let parser = createParser()
        
        let xmlWithMissingFields = """<?xml version="1.0" encoding="UTF-8"?>
<Project>
  <SymbolTable>
    <Symbol Name="MINIMAL_VAR" Address="X100" DataType="BOOL" />
  </SymbolTable>
</Project>"""
        
        let format = LSElectricXML "test.xml"
        
        // Act
        let! result = parser.ParseContentAsync(xmlWithMissingFields, format)
        
        // Assert
        result.Variables |> should haveLength 1
        
        let variable = result.Variables.Head
        variable.Name |> should equal "MINIMAL_VAR"
        variable.Comment |> should equal None
        variable.InitialValue |> should equal None
        variable.Scope |> should equal None
        
        // 프로젝트 정보도 기본값으로 처리되어야 함
        result.ProjectInfo.Name |> should equal "test"  // 파일명에서 추론
        result.ProjectInfo.Version |> should equal "1.0.0"  // 기본값
    }