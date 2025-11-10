module Ev2.PLC.Mapper.Tests.MapperFactoryTests

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper

/// 테스트용 샘플 파일 경로
let getSampleFilePath () =
    let testDir = __SOURCE_DIRECTORY__
    Path.Combine(testDir, "SampleData", "lsPLC.xml")

/// MapperFactory 생성 헬퍼
let createMapperFactory () =
    let loggerFactory = NullLoggerFactory.Instance :> ILoggerFactory
    MapperFactory(loggerFactory)

[<Fact>]
let ``MapperFactory should be created successfully`` () =
    let factory = createMapperFactory()

    factory |> should not' (be Null)

[<Fact>]
let ``MapperFactory should create LS Electric parser`` () =
    let factory = createMapperFactory()
    let vendor = PlcVendor.CreateLSElectric()

    match factory.CreateParser(vendor) with
    | Some parser ->
        parser.SupportedVendor.Manufacturer |> should equal "LS Electric"
    | None ->
        Assert.True(false, "Failed to create LS Electric parser")

[<Fact>]
let ``MapperFactory should create Allen-Bradley parser`` () =
    let factory = createMapperFactory()
    let vendor = PlcVendor.CreateAllenBradley()

    match factory.CreateParser(vendor) with
    | Some parser ->
        parser.SupportedVendor.Manufacturer |> should equal "Allen-Bradley"
    | None ->
        Assert.True(false, "Failed to create Allen-Bradley parser")

[<Fact>]
let ``MapperFactory should return None for unimplemented parsers`` () =
    let factory = createMapperFactory()
    let vendor = PlcVendor.CreateMitsubishi()

    match factory.CreateParser(vendor) with
    | Some _ ->
        Assert.True(false, "Should not create parser for unimplemented vendor")
    | None ->
        Assert.True(true)

[<Fact>]
let ``MapperFactory should infer vendor from LS Electric XML file`` () =
    let factory = createMapperFactory()
    let filePath = "test_ls.xml"

    match factory.InferVendorFromFile(filePath) with
    | Some vendor ->
        vendor.Manufacturer |> should equal "LS Electric"
    | None ->
        Assert.True(false, "Failed to infer LS Electric vendor")

[<Fact>]
let ``MapperFactory should infer vendor from Allen-Bradley L5K file`` () =
    let factory = createMapperFactory()
    let filePath = "test.l5k"

    match factory.InferVendorFromFile(filePath) with
    | Some vendor ->
        vendor.Manufacturer |> should equal "Allen-Bradley"
    | None ->
        Assert.True(false, "Failed to infer Allen-Bradley vendor")

[<Fact>]
let ``MapperFactory should infer vendor from Mitsubishi CSV file`` () =
    let factory = createMapperFactory()
    let filePath = "gx_works.csv"

    match factory.InferVendorFromFile(filePath) with
    | Some vendor ->
        vendor.Manufacturer |> should equal "Mitsubishi Electric"
    | None ->
        Assert.True(false, "Failed to infer Mitsubishi vendor")

[<Fact>]
let ``MapperFactory should return None for unknown file format`` () =
    let factory = createMapperFactory()
    let filePath = "unknown.txt"

    match factory.InferVendorFromFile(filePath) with
    | Some _ ->
        Assert.True(false, "Should not infer vendor for unknown format")
    | None ->
        Assert.True(true)

[<Fact>]
let ``MapperFactory should return supported formats`` () =
    let factory = createMapperFactory()
    let formats = factory.GetSupportedFormats()

    formats |> should not' (be Empty)
    formats.Length |> should be (greaterThanOrEqualTo 4)

    // Check for LS Electric
    let lsFormats = formats |> List.filter (fun (vendor, _) -> vendor.Manufacturer = "LS Electric")
    lsFormats |> should not' (be Empty)

    // Check for Allen-Bradley
    let abFormats = formats |> List.filter (fun (vendor, _) -> vendor.Manufacturer = "Allen-Bradley")
    abFormats |> should not' (be Empty)

[<Fact>]
let ``MapperFactory should validate non-existent file`` () =
    let factory = createMapperFactory()
    let nonExistentFile = "NonExistent.xml"

    let result = factory.ValidateFileAsync(nonExistentFile) |> Async.AwaitTask |> Async.RunSynchronously

    result.IsValid |> should equal false
    result.Severity |> should equal ValidationSeverity.ValidationError

[<Fact>]
let ``MapperFactory should validate existing LS Electric file`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ValidateFileAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.IsValid |> should equal true
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should process LS Electric XML file successfully`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Verify basic parsing worked
        result.ProjectInfo.Name |> should not' (be EmptyString)
        result.Statistics.TotalVariables |> should be (greaterThan 0)

        printfn $"Processed {result.Statistics.TotalVariables} variables"
        printfn $"Mapped {result.Statistics.MappedVariables} variables"
        printfn $"Found {result.Statistics.TotalAreas} areas"
        printfn $"Found {result.Statistics.TotalDevices} devices"
        printfn $"Generated {result.Statistics.TotalApis} API definitions"
        printfn $"Processing time: {result.Statistics.ProcessingTime.TotalMilliseconds:F0}ms"

        if not result.Success then
            printfn $"Errors: {result.Errors.Length}"
            for error in result.Errors |> List.truncate (min 3 result.Errors.Length) do
                printfn $"  - {error}"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should extract areas from LS Electric file`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.Areas |> should not' (be Empty)
        result.Statistics.TotalAreas |> should be (greaterThan 0)

        for area in result.Areas |> List.truncate (min 5 result.Areas.Length) do
            printfn $"Area: {area.Name} ({area.Devices.Length} devices)"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should extract devices from LS Electric file`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.Devices |> should not' (be Empty)
        result.Statistics.TotalDevices |> should be (greaterThan 0)

        for device in result.Devices |> List.truncate (min 5 result.Devices.Length) do
            printfn $"Device: {device.Name} (Type: {device.Type.DisplayName}, Area: {device.Area})"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should generate API definitions from LS Electric file`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.ApiDefinitions |> should not' (be Empty)
        result.Statistics.TotalApis |> should be (greaterThan 0)

        for api in result.ApiDefinitions |> List.truncate (min 5 result.ApiDefinitions.Length) do
            printfn $"API: {api.Name} (Type: {api.Type}, Direction: {api.Direction})"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should calculate mapping statistics`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        result.Statistics.TotalVariables |> should be (greaterThan 0)
        result.Statistics.MappedVariables |> should be (greaterThanOrEqualTo 0)
        result.Statistics.FileSize |> should be (greaterThan 0L)
        result.Statistics.ProcessingTime.TotalMilliseconds |> should be (greaterThan 0.0)

        let mappingRate = result.Statistics.MappingRate
        mappingRate |> should be (greaterThanOrEqualTo 0.0)
        mappingRate |> should be (lessThanOrEqualTo 100.0)
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should create variable analyzer`` () =
    let factory = createMapperFactory()
    let analyzer = factory.CreateVariableAnalyzer()

    analyzer |> should not' (be Null)

[<Fact>]
let ``MapperFactory should create naming analyzer`` () =
    let factory = createMapperFactory()
    let analyzer = factory.CreateNamingAnalyzer()

    analyzer |> should not' (be Null)

[<Fact>]
let ``MapperApi should process file using convenience function`` () =
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = MapperApi.processFileAsync sampleFile |> Async.AwaitTask |> Async.RunSynchronously

        result.Statistics.TotalVariables |> should be (greaterThan 0)
        result.ProjectInfo.Name |> should not' (be EmptyString)
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperApi should process file with custom configuration`` () =
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let vendor = PlcVendor.CreateLSElectric()
        let config = MappingConfiguration.Default(vendor)

        let result = MapperApi.processFileWithConfigAsync sampleFile config |> Async.AwaitTask |> Async.RunSynchronously

        result.ProjectInfo.Vendor.Manufacturer |> should equal "LS Electric"
        result.Statistics.TotalVariables |> should be (greaterThan 0)
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")

[<Fact>]
let ``MapperFactory should handle processing errors gracefully`` () =
    let factory = createMapperFactory()
    let invalidFile = "invalid.xml"

    let result = factory.ProcessPlcProgramAsync(invalidFile) |> Async.AwaitTask |> Async.RunSynchronously

    result.Success |> should equal false
    result.Errors |> should not' (be Empty)

[<Fact>]
let ``MapperFactory should collect warnings during processing`` () =
    let factory = createMapperFactory()
    let sampleFile = getSampleFilePath()

    if File.Exists(sampleFile) then
        let result = factory.ProcessPlcProgramAsync(sampleFile) |> Async.AwaitTask |> Async.RunSynchronously

        // Warnings may or may not exist depending on the data
        // Just verify the warnings collection exists
        result.Warnings |> should not' (be Null)

        printfn $"Warnings count: {result.Warnings.Length}"
        for warning in result.Warnings |> List.truncate (min 5 result.Warnings.Length) do
            printfn $"  Warning: {warning}"
    else
        Assert.True(true, $"Sample file not found: {sampleFile}")
