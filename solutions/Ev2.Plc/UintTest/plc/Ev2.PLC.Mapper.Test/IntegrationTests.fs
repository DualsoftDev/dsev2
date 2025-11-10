namespace Ev2.PLC.Mapper.Test

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Parser
open Ev2.PLC.Mapper.Core.Analyzer
open Ev2.PLC.Mapper.Core.Engine
open Ev2.PLC.Mapper.Core.Configuration
open Ev2.PLC.Mapper
open TestHelpers

module IntegrationTests =

    [<Fact>]
    let ``End-to-end: Parse Allen-Bradley file and map to DS system`` () =
        // Setup
        let logger = createLogger<AllenBradleyParser>()
        let parser = AllenBradleyParser(logger) :> IPlcParser
        let analyzer = VariableAnalyzer(createLogger<VariableAnalyzer>())
        let engine = MappingEngine(createLogger<MappingEngine>())
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        async {
            // Parse
            let! logic = parser.ParseContentAsync(TestData.sampleL5K)
            logic |> should not' (be Empty)

            // Analyze
            let variables = TestData.createSampleVariables()
            let analysis = analyzer.AnalyzeVariables(variables)
            analysis.TotalCount |> should equal 5

            // Map
            let mapped = engine.MapVariables(variables, config)
            mapped.DeviceGroups |> should not' (be Empty)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``End-to-end: Parse LS Electric XML and generate APIs`` () =
        let parser = LSElectricParser(createLogger<LSElectricParser>()) :> IPlcParser
        let engine = MappingEngine(createLogger<MappingEngine>())
        let config = TestData.createTestMappingConfig(PlcVendor.LSElectric)

        async {
            // Parse
            let! logic = parser.ParseContentAsync(TestData.sampleLSXML)
            logic |> should not' (be Empty)

            // Generate APIs
            let variables = TestData.createSampleVariables()
            let apis = engine.GenerateApiDefinitions(variables, config.Rules.ApiGeneration)
            apis |> should not' (be Empty)
            apis |> List.exists (fun a -> a.Type = ApiType.Read) |> should be True
        } |> Async.RunSynchronously

    [<Fact>]
    let ``End-to-end: Multiple vendor files to unified model`` () =
        let tempDir = createTempDirectory()

        try
            // Create test files for different vendors
            File.WriteAllText(Path.Combine(tempDir, "ab.L5K"), TestData.sampleL5K)
            File.WriteAllText(Path.Combine(tempDir, "ls.xml"), TestData.sampleLSXML)
            File.WriteAllText(Path.Combine(tempDir, "mx.csv"), TestData.sampleMitsubishiCSV)
            File.WriteAllText(Path.Combine(tempDir, "s7.awl"), TestData.sampleSiemensAWL)

            // Parse all files
            let abParser = AllenBradleyParser(createLogger<AllenBradleyParser>()) :> IPlcParser
            let lsParser = LSElectricParser(createLogger<LSElectricParser>()) :> IPlcParser
            let mxParser = MitsubishiParser(createLogger<MitsubishiParser>()) :> IPlcParser
            let s7Parser = SiemensParser(createLogger<SiemensParser>()) :> IPlcParser

            async {
                let! abLogic = abParser.ParseFileAsync(Path.Combine(tempDir, "ab.L5K"))
                let! lsLogic = lsParser.ParseFileAsync(Path.Combine(tempDir, "ls.xml"))
                let! mxLogic = mxParser.ParseFileAsync(Path.Combine(tempDir, "mx.csv"))
                let! s7Logic = s7Parser.ParseFileAsync(Path.Combine(tempDir, "s7.awl"))

                // All parsers should produce results
                abLogic |> should not' (be Empty)
                lsLogic |> should not' (be Empty)
                mxLogic |> should not' (be Empty)
                s7Logic |> should not' (be Empty)

                // Combine all logic
                let allLogic = abLogic @ lsLogic @ mxLogic @ s7Logic
                allLogic |> List.length |> should be (greaterThan 4)
            } |> Async.RunSynchronously

        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``End-to-end: Configuration-driven mapping`` () =
        let configManager = ConfigurationManager(createLogger<ConfigurationManager>())
        let engine = MappingEngine(createLogger<MappingEngine>())
        let tempFile = Path.GetTempFileName() + ".json"

        try
            // Create and save configuration
            let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
            configManager.SaveConfiguration(config, tempFile)

            // Load configuration and apply mapping
            let loadedConfig = configManager.LoadConfiguration(tempFile)
            let variables = TestData.createSampleVariables()
            let mapped = engine.MapVariables(variables, loadedConfig)

            mapped.DeviceGroups |> should not' (be Empty)
            mapped.Vendor |> should equal PlcVendor.AllenBradley

        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``End-to-end: Factory pattern with auto-detection`` () =
        let factory = MapperFactory(createLogger<MapperFactory>())
        let tempDir = createTempDirectory()

        try
            // Create files with different extensions
            let files = [
                ("project.L5K", TestData.sampleL5K)
                ("project.xml", TestData.sampleLSXML)
                ("project.csv", TestData.sampleMitsubishiCSV)
            ]

            files |> List.iter (fun (name, content) ->
                File.WriteAllText(Path.Combine(tempDir, name), content)
            )

            // Auto-detect and create mappers
            files |> List.iter (fun (name, _) ->
                let filePath = Path.Combine(tempDir, name)
                let mapper = factory.CreateMapperFromFile(filePath)

                mapper |> Option.isSome |> should be True

                match name with
                | "project.L5K" -> mapper.Value.Vendor |> should equal PlcVendor.AllenBradley
                | "project.xml" -> mapper.Value.Vendor |> should equal PlcVendor.LSElectric
                | "project.csv" -> mapper.Value.Vendor |> should equal PlcVendor.Mitsubishi
                | _ -> ()
            )

        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``End-to-end: Complete analysis pipeline`` () =
        let parser = AllenBradleyParser(createLogger<AllenBradleyParser>()) :> IPlcParser
        let varAnalyzer = VariableAnalyzer(createLogger<VariableAnalyzer>())
        let nameAnalyzer = NamingAnalyzer(createLogger<NamingAnalyzer>())
        let logicAnalyzer = LogicAnalyzer(createLogger<LogicAnalyzer>())

        async {
            // Parse logic
            let! logic = parser.ParseContentAsync(TestData.sampleL5K)

            // Analyze variables
            let variables = TestData.createSampleVariables()
            let varAnalysis = varAnalyzer.AnalyzeVariables(variables)
            varAnalysis.TotalCount |> should equal 5

            // Analyze naming
            let names = variables |> List.map (fun v -> v.Name)
            let patterns = nameAnalyzer.DetectNamingPatterns(names)
            patterns.CommonPrefixes |> should not' (be Empty)

            // Analyze logic
            let flowTypes = logicAnalyzer.DetectFlowTypes(logic)
            flowTypes |> should not' (be Empty)

        } |> Async.RunSynchronously

    [<Fact>]
    let ``End-to-end: Batch processing multiple directories`` () =
        let rootDir = createTempDirectory()

        try
            // Create vendor-specific subdirectories
            let vendors = [
                ("AllenBradley", ".L5K", TestData.sampleL5K)
                ("LSElectric", ".xml", TestData.sampleLSXML)
                ("Mitsubishi", ".csv", TestData.sampleMitsubishiCSV)
                ("Siemens", ".awl", TestData.sampleSiemensAWL)
            ]

            vendors |> List.iter (fun (vendor, ext, content) ->
                let vendorDir = Path.Combine(rootDir, vendor)
                Directory.CreateDirectory(vendorDir) |> ignore
                File.WriteAllText(Path.Combine(vendorDir, "project" + ext), content)
            )

            // Process each vendor directory
            let factory = MapperFactory(createLogger<MapperFactory>())
            let results =
                vendors
                |> List.map (fun (vendor, ext, _) ->
                    let vendorDir = Path.Combine(rootDir, vendor)
                    let filePath = Path.Combine(vendorDir, "project" + ext)

                    match factory.CreateMapperFromFile(filePath) with
                    | Some mapper ->
                        let parser =
                            match mapper.Vendor with
                            | PlcVendor.AllenBradley -> AllenBradleyParser(createLogger<AllenBradleyParser>()) :> IPlcParser
                            | PlcVendor.LSElectric -> LSElectricParser(createLogger<LSElectricParser>()) :> IPlcParser
                            | PlcVendor.Mitsubishi -> MitsubishiParser(createLogger<MitsubishiParser>()) :> IPlcParser
                            | PlcVendor.Siemens -> SiemensParser(createLogger<SiemensParser>()) :> IPlcParser
                            | _ -> failwith "Unknown vendor"

                        async {
                            let! logic = parser.ParseFileAsync(filePath)
                            return (vendor, logic)
                        }
                    | None ->
                        async { return (vendor, []) }
                )
                |> Async.Parallel
                |> Async.RunSynchronously

            results |> Array.length |> should equal 4
            results |> Array.forall (fun (_, logic) -> not (List.isEmpty logic)) |> should be True

        finally
            cleanupTempDirectory rootDir

    [<Fact>]
    let ``End-to-end: Error handling and recovery`` () =
        let parser = AllenBradleyParser(createLogger<AllenBradleyParser>()) :> IPlcParser

        async {
            // Test with invalid content
            let! emptyResult = parser.ParseContentAsync("")
            emptyResult |> should be Empty

            // Test with malformed content
            let! malformedResult = parser.ParseContentAsync("This is not valid L5K content")
            malformedResult |> should be Empty

            // Test with non-existent file
            let! fileResult = parser.ParseFileAsync("non_existent_file.L5K")
            fileResult |> should be Empty

            // Test recovery with valid content after errors
            let! validResult = parser.ParseContentAsync(TestData.sampleL5K)
            validResult |> should not' (be Empty)

        } |> Async.RunSynchronously

    [<Fact>]
    let ``End-to-end: Performance test with large dataset`` () =
        let parser = AllenBradleyParser(createLogger<AllenBradleyParser>()) :> IPlcParser

        // Generate large dataset
        let largeContent =
            [1..100]
            |> List.map (fun i ->
                sprintf """RUNG %d
XIC(Input%d) OTE(Output%d);
END_RUNG
""" i i i)
            |> String.concat "\n"
            |> fun rungs -> sprintf """CONTROLLER LargeController
PROGRAM MainProgram
ROUTINE TestRoutine
%s
END_ROUTINE
END_PROGRAM""" rungs

        async {
            let startTime = DateTime.Now
            let! result = parser.ParseContentAsync(largeContent)
            let elapsed = DateTime.Now - startTime

            result |> List.length |> should equal 100
            elapsed.TotalSeconds |> should be (lessThan 5.0)  // Should complete within 5 seconds
        } |> Async.RunSynchronously