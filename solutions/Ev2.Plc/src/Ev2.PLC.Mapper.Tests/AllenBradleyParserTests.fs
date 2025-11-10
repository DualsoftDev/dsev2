namespace Ev2.PLC.Mapper.Tests

open System
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Mapper.Core.Parser
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

module AllenBradleyParserTests =

    /// Test logger
    let private createLogger() =
        (NullLoggerFactory.Instance :> ILoggerFactory).CreateLogger<AllenBradleyParser>()

    /// Sample L5K content
    let private sampleL5K = """
CONTROLLER TestController (ProcessorType := "1756-L75",
                          MajorRev := 32,
                          MinorRev := 11)

PROGRAM MainProgram

ROUTINE TestRoutine

RUNG 0
XIC(StartButton) XIO(StopButton) OTE(Motor);
// Motor control logic
END_RUNG

RUNG 1
TON(Timer1, 5000) XIC(Timer1.DN) OTE(AlarmLight);
// Timer alarm logic
END_RUNG

RUNG 2
CTU(Counter1, 100) EQU(Counter1.ACC, 50) OTE(HalfwayIndicator);
// Counter monitoring
END_RUNG

RUNG 3
XIC(ESTOP) OTE(SafetyRelay);
// Emergency stop logic
END_RUNG

END_ROUTINE

TAG
StartButton : BOOL;
StopButton : BOOL;
Motor : BOOL;
Timer1 : TIMER;
AlarmLight : BOOL;
Counter1 : COUNTER;
HalfwayIndicator : BOOL;
ESTOP : BOOL;
SafetyRelay : BOOL;
END_TAG

END_PROGRAM
"""

    [<Fact>]
    let ``AllenBradleyParser should parse L5K file content`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleL5K)

            result |> should not' (be Empty)
            result.Length |> should equal 4
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract rung numbers correctly`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Number |> should equal 1
            rung0.Content |> should haveSubstring "StartButton"
            rung0.Content |> should haveSubstring "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect logic flow types`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleL5K)

            // Timer rung
            let timerRung = result |> List.find (fun r -> r.Content.Contains("TON"))
            timerRung.Type |> should equal (Some LogicFlowType.Timer)

            // Counter rung
            let counterRung = result |> List.find (fun r -> r.Content.Contains("CTU"))
            counterRung.Type |> should equal (Some LogicFlowType.Counter)

            // Safety rung
            let safetyRung = result |> List.find (fun r -> r.Content.Contains("ESTOP"))
            safetyRung.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract variables from rungs`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Variables |> should contain "StartButton"
            rung0.Variables |> should contain "StopButton"
            rung0.Variables |> should contain "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should extract comments`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleL5K)

            let rung0 = result |> List.find (fun r -> r.Number = 1)
            rung0.Comments |> should contain "Motor control logic"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should handle structured text logic type`` () =
        let stContent = """
ROUTINE STRoutine
IF Temperature > 100 THEN
    HeaterOn := FALSE;
    CoolerOn := TRUE;
ELSE
    HeaterOn := TRUE;
    CoolerOn := FALSE;
END_IF;
END_ROUTINE
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(stContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.StructuredText
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should support L5K and L5X extensions`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        parser.SupportedFileExtensions |> should contain ".L5K"
        parser.SupportedFileExtensions |> should contain ".l5k"
        parser.SupportedFileExtensions |> should contain ".L5X"
        parser.SupportedFileExtensions |> should contain ".l5x"

    [<Fact>]
    let ``AllenBradleyParser should check if it can parse file`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        parser.CanParse("test.L5K") |> should be True
        parser.CanParse("test.l5k") |> should be True
        parser.CanParse("test.L5X") |> should be True
        parser.CanParse("test.xml") |> should be False
        parser.CanParse("test.csv") |> should be False

    [<Fact>]
    let ``AllenBradleyParser should handle empty file gracefully`` () =
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync("")

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should handle malformed content gracefully`` () =
        let malformed = "This is not a valid L5K file content"
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(malformed)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect function block logic type`` () =
        let fbContent = """
ROUTINE FBRoutine
RUNG 0
MOV(SourceA, DestB) ADD(Value1, Value2, Result) MUL(Result, Factor, Output);
END_RUNG
END_ROUTINE
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(fbContent)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.FunctionBlock
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should handle complex rung with multiple instructions`` () =
        let complexRung = """
RUNG 0
XIC(Input1) [XIC(Input2) , XIO(Input3)] OTE(Output1) OTL(Output2) OTU(Output3);
// Complex logic with multiple outputs
END_RUNG
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(complexRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "Input1"
            logic.Variables |> should contain "Input2"
            logic.Variables |> should contain "Input3"
            logic.Variables |> should contain "Output1"
            logic.Variables |> should contain "Output2"
            logic.Variables |> should contain "Output3"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect math flow type`` () =
        let mathRung = """
RUNG 0
ADD(Value1, Value2, Sum) SUB(Sum, Offset, Result) MUL(Result, Factor, Output) DIV(Output, Divisor, Final);
END_RUNG
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(mathRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect conditional flow type`` () =
        let jumpRung = """
RUNG 0
EQU(Counter, 10) JMP(Label1);
LBL(Label1) XIC(Input) OTE(Output);
JSR(Subroutine1);
END_RUNG
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(jumpRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Conditional)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect sequence flow type`` () =
        let seqRung = """
RUNG 0
SQO(Array, Mask, Dest, Control, Length, Position);
SQI(Source, Mask, Array, Control, Length, Position);
END_RUNG
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(seqRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Sequence)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``AllenBradleyParser should detect interrupt flow type`` () =
        let intRung = """
RUNG 0
ONS(OneShot) OSR(RisingEdge) OSF(FallingEdge) OTE(Output);
END_RUNG
"""
        let logger = createLogger()
        let parser = AllenBradleyParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(intRung)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Interrupt)
        } |> Async.RunSynchronously