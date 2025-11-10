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

module AllenBradleyParserTests =

    let createParser() =
        let logger = createLogger<AllenBradleyParser>()
        AllenBradleyParser(logger) :> IPlcParser

    [<Fact>]
    let ``AllenBradleyParser should parse L5K content successfully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleL5K)

            result |> should not' (be Empty)
            result.Length |> should equal 2
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract rung numbers correctly`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Number |> should equal 1
            rung0.Content |> should haveSubstring "StartButton"
            rung0.Content |> should haveSubstring "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract variables from rungs`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Variables |> should contain "StartButton"
            rung0.Variables |> should contain "StopButton"
            rung0.Variables |> should contain "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect timer flow type`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleL5K)

            let timerRung = result |> List.find (fun r -> r.Content.Contains("TON"))
            timerRung.Type |> should equal (Some LogicFlowType.Timer)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract comments`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Comments |> should contain "Motor control logic"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should support L5K and L5X extensions`` () =
        let parser = createParser()

        parser.SupportedFileExtensions |> should contain ".L5K"
        parser.SupportedFileExtensions |> should contain ".l5k"
        parser.SupportedFileExtensions |> should contain ".L5X"
        parser.SupportedFileExtensions |> should contain ".l5x"

    [<Fact>]
    let ``AllenBradleyParser should validate file extension`` () =
        let parser = createParser()

        parser.CanParse("test.L5K") |> should be True
        parser.CanParse("test.l5k") |> should be True
        parser.CanParse("test.L5X") |> should be True
        parser.CanParse("test.xml") |> should be False

    [<Fact>]
    let ``AllenBradleyParser should handle structured text`` () =
        let stContent = """
ROUTINE STRoutine
IF Temperature > 100 THEN
    HeaterOn := FALSE;
    CoolerOn := TRUE;
END_IF;
END_ROUTINE
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(stContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.StructuredText
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect counter flow type`` () =
        let counterContent = """
RUNG 0
CTU(Counter1, 100) EQU(Counter1.ACC, 50) OTE(HalfwayIndicator);
END_RUNG
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(counterContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Counter)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect safety flow type`` () =
        let safetyContent = """
RUNG 0
XIC(ESTOP) OTE(SafetyRelay);
// Emergency stop logic
END_RUNG
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(safetyContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect math flow type`` () =
        let mathContent = """
RUNG 0
ADD(Value1, Value2, Sum) MUL(Sum, Factor, Output);
END_RUNG
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(mathContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect conditional flow type`` () =
        let jumpContent = """
RUNG 0
EQU(Counter, 10) JMP(Label1);
END_RUNG
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(jumpContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Conditional)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should parse file from disk`` () =
        let parser = createParser()
        let tempFile = createTempFile ".L5K" TestData.sampleL5K

        try
            async {
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should equal 2
            } |> Async.RunSynchronously
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``AllenBradleyParser should parse directory with L5K files`` () =
        let parser = createParser()
        let tempDir = createTempDirectory()

        try
            File.WriteAllText(Path.Combine(tempDir, "file1.L5K"), TestData.sampleL5K)
            File.WriteAllText(Path.Combine(tempDir, "file2.L5K"), TestData.sampleL5K)

            async {
                let! result = parser.ParseDirectoryAsync(tempDir)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 2)
            } |> Async.RunSynchronously
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``AllenBradleyParser should handle complex rung with multiple outputs`` () =
        let complexRung = """
RUNG 0
XIC(Input1) [XIC(Input2) , XIO(Input3)] OTE(Output1) OTL(Output2) OTU(Output3);
END_RUNG
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(complexRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "Input1"
            logic.Variables |> should contain "Output1"
            logic.Variables |> should contain "Output2"
            logic.Variables |> should contain "Output3"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should handle empty content gracefully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync("")
            result |> should be Empty
        } |> Async.RunSynchronously