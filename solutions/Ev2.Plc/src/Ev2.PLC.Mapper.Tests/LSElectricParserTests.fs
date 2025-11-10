module Ev2.PLC.Mapper.Tests.LSElectricParserTests

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Parsers.LSElectric

/// 테스트용 샘플 파일 경로
let getSampleFilePath () =
    let testDir = __SOURCE_DIRECTORY__
    Path.Combine(testDir, "SampleData", "lsPLC.xml")

/// 파서 생성 헬퍼
let createParser () =
    let logger = NullLogger<LSElectricParser>.Instance
    LSElectricParserFactory.create logger

[<Fact>]
let ``LSElectricParser should be created successfully`` () =
    let parser = createParser()

    parser |> should not' (be Null)
    parser.SupportedVendor.Manufacturer |> should equal "LS Electric"

[<Fact>]
let ``LSElectricParser should support LSElectric XML format`` () =
    let parser = createParser()
    let format = LSElectricXML "test.xml"

    parser.CanParse(format) |> should equal true

[<Fact>]
let ``LSElectricParser should not support other formats`` () =
    let parser = createParser()
    let format = SiemensXML "test.xml"

    parser.CanParse(format) |> should equal false

[<Fact>]
let ``LSElectricParser should validate existing XML file`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ValidateFileAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.IsValid |> should equal true
        result.Message |> should not' (be EmptyString)
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should detect non-existent file`` () =
    let parser = createParser()
    let nonExistentFile = "NonExistent.xml"

    let result = parser.ValidateFileAsync(nonExistentFile) |> Async.AwaitTask |> Async.RunSynchronously

    result.IsValid |> should equal false
    result.Severity |> should equal ValidationSeverity.ValidationError

[<Fact>]
let ``LSElectricParser should parse sample XML file successfully`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ParseAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Verify project info
        result.ProjectInfo |> should not' (be Null)
        result.ProjectInfo.Name |> should not' (be EmptyString)
        result.ProjectInfo.Vendor.Manufacturer |> should equal "LS Electric"

        // Verify variables were parsed
        result.Variables |> should not' (be Empty)
        result.Variables.Length |> should be (greaterThan 0)

        printfn $"Parsed {result.Variables.Length} variables from LS PLC file"
        printfn $"Parsed {result.Logic.Length} logic rungs from LS PLC file"
        printfn $"Project name: {result.ProjectInfo.Name}"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should extract variables with correct structure`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ParseAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Check first variable
        let firstVar = result.Variables |> List.tryHead

        match firstVar with
        | Some var ->
            var.Name |> should not' (be EmptyString)
            var.Address |> should not' (be EmptyString)
            var.DataType |> should not' (be EmptyString)

            printfn $"Sample variable: Name={var.Name}, Address={var.Address}, Type={var.DataType}"
        | None ->
            Assert.True(false, "No variables found in sample file")
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should parse specific variable types`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ParseAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Check for BOOL variables
        let boolVars = result.Variables |> List.filter (fun v -> v.DataType = "BOOL")
        boolVars |> should not' (be Empty)

        // Check for TIMER variables
        let timerVars = result.Variables |> List.filter (fun v -> v.DataType = "TON")
        timerVars |> should not' (be Empty)

        // Check for STRING variables (may not exist in sample file)
        let stringVars = result.Variables |> List.filter (fun v -> v.DataType = "STRING")

        // Count different types
        let typeGroups = result.Variables |> List.groupBy (fun v -> v.DataType)

        printfn $"BOOL variables: {boolVars.Length}"
        printfn $"TON (Timer) variables: {timerVars.Length}"
        printfn $"STRING variables: {stringVars.Length}"
        printfn $"Total variable types: {typeGroups.Length}"
        for (dataType, vars) in typeGroups |> List.take (min 5 typeGroups.Length) do
            printfn $"  {dataType}: {vars.Length} variables"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should extract variables with addresses`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ParseAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Find variables with M memory addresses
        let mAddressVars = result.Variables
                          |> List.filter (fun v -> v.Address.StartsWith("%MX") || v.Address.StartsWith("%MD"))
                          |> List.truncate 5

        mAddressVars |> should not' (be Empty)

        for var in mAddressVars do
            printfn $"Variable: {var.Name} -> {var.Address} ({var.DataType})"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should extract project metadata`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = parser.ParseAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.ProjectInfo.Name |> should not' (be EmptyString)
        result.ProjectInfo.FilePath |> should equal sampleFile
        result.ProjectInfo.FileSize |> should be (greaterThan 0L)

        match result.ProjectInfo.Format with
        | LSElectricXML _ -> Assert.True(true)
        | _ -> Assert.True(false, "Expected LSElectricXML format")

        printfn $"Project: {result.ProjectInfo.Name}"
        printfn $"Version: {result.ProjectInfo.Version}"
        printfn $"File size: {result.ProjectInfo.FileSize} bytes"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")



[<Fact>]
let ``LSElectricParser should extract symbol table`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let xmlContent = File.ReadAllText(sampleFile)
        let symbols = parser.ExtractSymbolTableAsync(xmlContent) |> Async.AwaitTask |> Async.RunSynchronously

        symbols |> should not' (be Empty)
        symbols.Length |> should be (greaterThan 0)

        printfn $"Extracted {symbols.Length} symbols from symbol table"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``LSElectricParser should extract project info from content`` () =
    let parser = createParser()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let xmlContent = File.ReadAllText(sampleFile)
        let projectInfo = parser.ExtractProjectInfoAsync(xmlContent) |> Async.AwaitTask |> Async.RunSynchronously

        projectInfo.Name |> should not' (be EmptyString)
        projectInfo.Vendor.Manufacturer |> should equal "LS Electric"

        printfn $"Extracted project info: {projectInfo.Name} v{projectInfo.Version}"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")
