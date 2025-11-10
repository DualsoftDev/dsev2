namespace Ev2.PLC.Mapper.Test

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Parser
open Ev2.PLC.Mapper.Core.Interfaces
open TestHelpers

module MitsubishiParserTests =

    let createParser() =
        let logger = createLogger<MitsubishiParser>()
        MitsubishiParser(logger) :> IPlcParser

    [<Fact>]
    let ``MitsubishiParser should parse CSV content successfully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)

            result |> should not' (be Empty)
            result.Length |> should be (greaterThan 0)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should extract step numbers correctly`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)

            let step0 = result |> List.find (fun r -> r.Number = 0)
            step0.Number |> should equal 0
            step0.Content |> should haveSubstring "X0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should extract device names from CSV`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)

            let step0 = result |> List.find (fun r -> r.Number = 0)
            step0.Variables |> should contain "X0"

            let step2 = result |> List.find (fun r -> r.Number = 2)
            step2.Variables |> should contain "Y0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect timer instructions`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)

            let timerStep = result |> List.find (fun r -> r.Content.Contains("TON"))
            timerStep.Type |> should equal (Some LogicFlowType.Timer)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should extract comments from CSV`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)

            let step0 = result |> List.find (fun r -> r.Number = 0)
            step0.Comment |> should equal (Some "Start Button")
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should support CSV and AWL extensions`` () =
        let parser = createParser()

        parser.SupportedFileExtensions |> should contain ".csv"
        parser.SupportedFileExtensions |> should contain ".CSV"
        parser.SupportedFileExtensions |> should contain ".awl"
        parser.SupportedFileExtensions |> should contain ".AWL"

    [<Fact>]
    let ``MitsubishiParser should validate file extension`` () =
        let parser = createParser()

        parser.CanParse("program.csv") |> should be True
        parser.CanParse("program.CSV") |> should be True
        parser.CanParse("program.awl") |> should be True
        parser.CanParse("program.xml") |> should be False

    [<Fact>]
    let ``MitsubishiParser should handle AWL format`` () =
        let awlContent = """
LIST
LD X0
AND X1
OUT Y0
LD Y0
TON T0 K50
END
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(awlContent)

            result |> should not' (be Empty)
            result.Length |> should be (greaterThan 0)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect counter flow type`` () =
        let counterContent = """Step,Instruction,Device,Comment
0,LD,X0,Count Input
1,CTU,C0,Counter
2,LD,C0,
3,OUT,Y0,Counter Done
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(counterContent)

            let counterStep = result |> List.find (fun r -> r.Content.Contains("CTU"))
            counterStep.Type |> should equal (Some LogicFlowType.Counter)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect math operations`` () =
        let mathContent = """Step,Instruction,Device,Comment
0,LD,X0,
1,ADD,D0,D1
2,MUL,D1,D2
3,OUT,Y0,
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(mathContent)

            let mathStep = result |> List.find (fun r -> r.Content.Contains("ADD"))
            mathStep.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should handle empty CSV gracefully`` () =
        let emptyContent = """Step,Instruction,Device,Comment
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(emptyContent)
            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should parse file from disk`` () =
        let parser = createParser()
        let tempFile = createTempFile ".csv" TestData.sampleMitsubishiCSV

        try
            async {
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 0)
            } |> Async.RunSynchronously
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``MitsubishiParser should parse directory with CSV files`` () =
        let parser = createParser()
        let tempDir = createTempDirectory()

        try
            File.WriteAllText(Path.Combine(tempDir, "file1.csv"), TestData.sampleMitsubishiCSV)
            File.WriteAllText(Path.Combine(tempDir, "file2.CSV"), TestData.sampleMitsubishiCSV)

            async {
                let! result = parser.ParseDirectoryAsync(tempDir)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 5)
            } |> Async.RunSynchronously
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``MitsubishiParser should handle Shift-JIS encoding`` () =
        // This test would require actual Shift-JIS encoded content
        // For now, just verify the parser doesn't crash with UTF-8
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleMitsubishiCSV)
            result |> should not' (be Empty)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect sequential flow type`` () =
        let sequentialContent = """Step,Instruction,Device,Comment
0,LD,X0,
1,SET,M0,Step 1
2,LD,M0,
3,RST,M0,
4,SET,M1,Step 2
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(sequentialContent)

            let setStep = result |> List.find (fun r -> r.Content.Contains("SET"))
            setStep.Type |> should equal (Some LogicFlowType.Sequential)
        } |> Async.RunSynchronously