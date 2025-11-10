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

module SiemensParserTests =

    let createParser() =
        let logger = createLogger<SiemensParser>()
        SiemensParser(logger) :> IPlcParser

    [<Fact>]
    let ``SiemensParser should parse AWL content successfully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleSiemensAWL)

            result |> should not' (be Empty)
            result.Length |> should equal 2
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should extract network numbers correctly`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleSiemensAWL)

            let network1 = result |> List.find (fun r -> r.Number = 1)
            network1.Number |> should equal 1
            network1.Comment |> should equal (Some "Motor Start/Stop")
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should extract variables from networks`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleSiemensAWL)

            let network1 = result |> List.find (fun r -> r.Number = 1)
            network1.Variables |> should contain "I 0.0"
            network1.Variables |> should contain "I 0.1"
            network1.Variables |> should contain "Q 0.0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should detect timer flow type`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleSiemensAWL)

            let timerNetwork = result |> List.find (fun r -> r.Comment = Some "Timer Function")
            timerNetwork.Type |> should equal (Some LogicFlowType.Timer)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should handle SCL structured text`` () =
        let sclContent = """
FUNCTION_BLOCK FB_Motor
VAR_INPUT
    Start : BOOL;
    Stop : BOOL;
END_VAR
VAR_OUTPUT
    Running : BOOL;
END_VAR

IF Start AND NOT Stop THEN
    Running := TRUE;
ELSIF Stop THEN
    Running := FALSE;
END_IF;
END_FUNCTION_BLOCK
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(sclContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.StructuredText
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should support multiple file extensions`` () =
        let parser = createParser()

        parser.SupportedFileExtensions |> should contain ".awl"
        parser.SupportedFileExtensions |> should contain ".AWL"
        parser.SupportedFileExtensions |> should contain ".scl"
        parser.SupportedFileExtensions |> should contain ".SCL"
        parser.SupportedFileExtensions |> should contain ".stl"
        parser.SupportedFileExtensions |> should contain ".STL"
        parser.SupportedFileExtensions |> should contain ".xml"

    [<Fact>]
    let ``SiemensParser should validate file extension`` () =
        let parser = createParser()

        parser.CanParse("program.awl") |> should be True
        parser.CanParse("program.SCL") |> should be True
        parser.CanParse("program.stl") |> should be True
        parser.CanParse("program.xml") |> should be True
        parser.CanParse("program.csv") |> should be False

    [<Fact>]
    let ``SiemensParser should handle STL format`` () =
        let stlContent = """
NETWORK 1
// Conveyor control
      A     I 1.0       // Sensor 1
      A     I 1.1       // Sensor 2
      =     Q 1.0       // Conveyor motor
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(stlContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "I 1.0"
            logic.Variables |> should contain "Q 1.0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should detect counter flow type`` () =
        let counterContent = """
NETWORK 1
      A     I 0.0
      CU    C 0
      A     C 0
      =     Q 0.0
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(counterContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Counter)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should detect safety flow type`` () =
        let safetyContent = """
NETWORK 1
// EMERGENCY STOP
      AN    I 0.0       // E-STOP button (NC)
      =     Q 0.0       // Safety relay
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(safetyContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should detect math operations`` () =
        let mathContent = """
NETWORK 1
      L     MW 0
      L     MW 2
      +I
      T     MW 4
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(mathContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should handle function blocks`` () =
        let fbContent = """
FUNCTION_BLOCK FB1
TITLE = 'PID Controller'
VAR_INPUT
    SetPoint : REAL;
    ProcessValue : REAL;
END_VAR
VAR_OUTPUT
    Output : REAL;
END_VAR
BEGIN
    // PID logic here
END_FUNCTION_BLOCK
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(fbContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.FunctionBlock
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should parse file from disk`` () =
        let parser = createParser()
        let tempFile = createTempFile ".awl" TestData.sampleSiemensAWL

        try
            async {
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should equal 2
            } |> Async.RunSynchronously
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``SiemensParser should parse directory with multiple formats`` () =
        let parser = createParser()
        let tempDir = createTempDirectory()

        try
            File.WriteAllText(Path.Combine(tempDir, "file1.awl"), TestData.sampleSiemensAWL)
            File.WriteAllText(Path.Combine(tempDir, "file2.scl"), "IF TRUE THEN Y := 1; END_IF;")

            async {
                let! result = parser.ParseDirectoryAsync(tempDir)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 1)
            } |> Async.RunSynchronously
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``SiemensParser should handle empty content gracefully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync("")
            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``SiemensParser should extract comments from AWL`` () =
        let awlWithComments = """
NETWORK 1
TITLE = Test Network
// This is a test comment
      A     I 0.0       // Input comment
      =     Q 0.0       // Output comment
"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(awlWithComments)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Comment |> should equal (Some "Test Network")
            logic.Comments |> should contain "This is a test comment"
        } |> Async.RunSynchronously