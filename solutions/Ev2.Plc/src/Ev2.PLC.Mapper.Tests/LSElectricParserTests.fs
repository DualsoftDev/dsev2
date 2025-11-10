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

module LSElectricParserTests =

    /// Test logger
    let private createLogger() =
        (NullLoggerFactory.Instance :> ILoggerFactory).CreateLogger<LSElectricParser>()

    /// Sample XML content for LS Electric
    let private sampleXML = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project version="2.0">
    <Program Name="MainProgram" Language="LD">
        <Rung Number="0">
            <Comment>Motor Start/Stop Control</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="StartButton" />
                <Element Type="NC_CONTACT" Name="StopButton" />
                <Element Type="COIL" Name="Motor" />
            </Elements>
        </Rung>
        <Rung Number="1">
            <Comment>Timer Operation</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="Motor" />
                <Element Type="TON" Name="Timer1" Parameter="T#5s" />
                <Element Type="COIL" Name="TimerDone" />
            </Elements>
        </Rung>
        <Rung Number="2">
            <Comment>Counter Logic</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="CountPulse" />
                <Element Type="CTU" Name="Counter1" Parameter="100" />
                <Element Type="NO_CONTACT" Name="Counter1.Q" />
                <Element Type="COIL" Name="CounterReached" />
            </Elements>
        </Rung>
        <Rung Number="3">
            <Comment>Safety Circuit</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="EMERGENCY_STOP" />
                <Element Type="NC_CONTACT" Name="SafetyGate" />
                <Element Type="COIL" Name="SafetyRelay" />
            </Elements>
        </Rung>
        <Rung Number="4">
            <Comment>Math Operations</Comment>
            <Elements>
                <Element Type="ADD" Name="AddBlock" Input1="Value1" Input2="Value2" Output="Result" />
                <Element Type="MUL" Name="MultiplyBlock" Input1="Result" Input2="Factor" Output="FinalValue" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

    [<Fact>]
    let ``LSElectricParser should parse XML file content`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            result |> should not' (be Empty)
            result.Length |> should equal 5
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should extract rung numbers correctly`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            let rung0 = result |> List.find (fun r -> r.Number = 0)
            rung0.Number |> should equal 0
            rung0.Comment |> should equal (Some "Motor Start/Stop Control")
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect logic flow types`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            // Timer rung
            let timerRung = result |> List.find (fun r -> r.Comment = Some "Timer Operation")
            timerRung.Type |> should equal (Some LogicFlowType.Timer)

            // Counter rung
            let counterRung = result |> List.find (fun r -> r.Comment = Some "Counter Logic")
            counterRung.Type |> should equal (Some LogicFlowType.Counter)

            // Safety rung
            let safetyRung = result |> List.find (fun r -> r.Comment = Some "Safety Circuit")
            safetyRung.Type |> should equal (Some LogicFlowType.Safety)

            // Math rung
            let mathRung = result |> List.find (fun r -> r.Comment = Some "Math Operations")
            mathRung.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should extract variables from rungs`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            let rung0 = result |> List.find (fun r -> r.Number = 0)
            rung0.Variables |> should contain "StartButton"
            rung0.Variables |> should contain "StopButton"
            rung0.Variables |> should contain "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle structured text content`` () =
        let stXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="STProgram" Language="ST">
        <Code>
            IF Temperature > 100 THEN
                HeaterOn := FALSE;
                CoolerOn := TRUE;
            ELSE
                HeaterOn := TRUE;
                CoolerOn := FALSE;
            END_IF;
        </Code>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(stXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.StructuredText
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should support XML extension`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        parser.SupportedFileExtensions |> should contain ".xml"
        parser.SupportedFileExtensions |> should contain ".XML"

    [<Fact>]
    let ``LSElectricParser should check if it can parse file`` () =
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        parser.CanParse("project.xml") |> should be True
        parser.CanParse("project.XML") |> should be True
        parser.CanParse("project.csv") |> should be False
        parser.CanParse("project.txt") |> should be False

    [<Fact>]
    let ``LSElectricParser should handle empty XML gracefully`` () =
        let emptyXml = """<?xml version="1.0" encoding="UTF-8"?><XG5000Project></XG5000Project>"""
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(emptyXml)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle malformed XML gracefully`` () =
        let malformed = "<This is not valid XML"
        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(malformed)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect function block logic type`` () =
        let fbXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="FBProgram" Language="FBD">
        <FunctionBlock Number="0">
            <Elements>
                <Element Type="FB" Name="PID_Controller" />
                <Element Type="FB" Name="Motor_Control" />
            </Elements>
        </FunctionBlock>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(fbXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.FunctionBlock
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should extract parameters from elements`` () =
        let paramXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="ParamProgram">
        <Rung Number="0">
            <Elements>
                <Element Type="TON" Name="Timer1" Parameter="T#10s" />
                <Element Type="CTU" Name="Counter1" Parameter="50" Preset="100" />
                <Element Type="MOV" Name="Move1" Source="D100" Destination="D200" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(paramXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "Timer1"
            logic.Variables |> should contain "Counter1"
            logic.Variables |> should contain "Move1"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect sequential flow type`` () =
        let seqXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="SeqProgram">
        <Rung Number="0">
            <Comment>Sequential Operation</Comment>
            <Elements>
                <Element Type="STEP" Name="Step1" />
                <Element Type="TRANSITION" Name="Trans1" />
                <Element Type="STEP" Name="Step2" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(seqXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Sequential)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect interrupt flow type`` () =
        let intXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="IntProgram">
        <Rung Number="0">
            <Comment>Interrupt Handler</Comment>
            <Elements>
                <Element Type="INTERRUPT" Name="INT1" />
                <Element Type="P_EDGE" Name="Edge1" />
                <Element Type="N_EDGE" Name="Edge2" />
                <Element Type="COIL" Name="Output" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(intXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Interrupt)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle complex nested structures`` () =
        let nestedXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="ComplexProgram">
        <Rung Number="0">
            <Comment>Complex Logic with Nesting</Comment>
            <Elements>
                <Branch Type="Parallel">
                    <Elements>
                        <Element Type="NO_CONTACT" Name="Input1" />
                        <Element Type="NO_CONTACT" Name="Input2" />
                    </Elements>
                </Branch>
                <Branch Type="Series">
                    <Elements>
                        <Element Type="NC_CONTACT" Name="Input3" />
                        <Element Type="NO_CONTACT" Name="Input4" />
                    </Elements>
                </Branch>
                <Element Type="COIL" Name="Output" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(nestedXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "Input1"
            logic.Variables |> should contain "Input2"
            logic.Variables |> should contain "Input3"
            logic.Variables |> should contain "Input4"
            logic.Variables |> should contain "Output"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect safety flow type for alarm`` () =
        let alarmXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="AlarmProgram">
        <Rung Number="0">
            <Comment>Alarm Handling</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="ALARM_HIGH" />
                <Element Type="NO_CONTACT" Name="ALARM_LOW" />
                <Element Type="ALARM" Name="AlarmHandler" />
                <Element Type="COIL" Name="AlarmActive" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let logger = createLogger()
        let parser = LSElectricParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(alarmXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously