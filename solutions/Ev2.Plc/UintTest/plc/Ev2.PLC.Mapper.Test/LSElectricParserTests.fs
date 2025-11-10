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

module LSElectricParserTests =

    let createParser() =
        let logger = createLogger<LSElectricParser>()
        LSElectricParser(logger) :> IPlcParser

    [<Fact>]
    let ``LSElectricParser should parse XML content successfully`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleLSXML)

            result |> should not' (be Empty)
            result.Length |> should equal 2
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should extract rung numbers correctly`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleLSXML)

            let rung0 = result |> List.find (fun r -> r.Number = 0)
            rung0.Number |> should equal 0
            rung0.Comment |> should equal (Some "Motor Start/Stop Control")
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should extract variables from rungs`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleLSXML)

            let rung0 = result |> List.find (fun r -> r.Number = 0)
            rung0.Variables |> should contain "StartButton"
            rung0.Variables |> should contain "StopButton"
            rung0.Variables |> should contain "Motor"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect timer flow type`` () =
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(TestData.sampleLSXML)

            let timerRung = result |> List.find (fun r -> r.Comment = Some "Timer Operation")
            timerRung.Type |> should equal (Some LogicFlowType.Timer)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle structured text programs`` () =
        let stXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="STProgram" Language="ST">
        <Code>
            IF Temperature > 100 THEN
                HeaterOn := FALSE;
            END_IF;
        </Code>
    </Program>
</XG5000Project>"""

        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(stXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.StructuredText
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle function block diagrams`` () =
        let fbdXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="FBDProgram" Language="FBD">
        <FunctionBlock Number="0">
            <Elements>
                <Element Type="FB" Name="PID_Controller" />
            </Elements>
        </FunctionBlock>
    </Program>
</XG5000Project>"""

        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(fbdXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.FunctionBlock
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should support XML file extensions`` () =
        let parser = createParser()

        parser.SupportedFileExtensions |> should contain ".xml"
        parser.SupportedFileExtensions |> should contain ".XML"

    [<Fact>]
    let ``LSElectricParser should validate file extension`` () =
        let parser = createParser()

        parser.CanParse("project.xml") |> should be True
        parser.CanParse("project.XML") |> should be True
        parser.CanParse("project.csv") |> should be False

    [<Fact>]
    let ``LSElectricParser should handle empty XML gracefully`` () =
        let emptyXml = """<?xml version="1.0" encoding="UTF-8"?><XG5000Project></XG5000Project>"""
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(emptyXml)
            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle malformed XML gracefully`` () =
        let malformed = "<This is not valid XML"
        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(malformed)
            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should parse file from disk`` () =
        let parser = createParser()
        let tempFile = createTempFile ".xml" TestData.sampleLSXML

        try
            async {
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should equal 2
            } |> Async.RunSynchronously
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``LSElectricParser should parse directory with multiple XML files`` () =
        let parser = createParser()
        let tempDir = createTempDirectory()

        try
            // Create multiple XML files
            File.WriteAllText(Path.Combine(tempDir, "file1.xml"), TestData.sampleLSXML)
            File.WriteAllText(Path.Combine(tempDir, "file2.xml"), TestData.sampleLSXML)

            async {
                let! result = parser.ParseDirectoryAsync(tempDir)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 2)
            } |> Async.RunSynchronously
        finally
            cleanupTempDirectory tempDir

    [<Fact>]
    let ``LSElectricParser should detect safety flow type`` () =
        let safetyXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="SafetyProgram">
        <Rung Number="0">
            <Comment>Emergency Stop Circuit</Comment>
            <Elements>
                <Element Type="NO_CONTACT" Name="EMERGENCY_STOP" />
                <Element Type="COIL" Name="SafetyRelay" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(safetyXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should detect counter flow type`` () =
        let counterXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="CounterProgram">
        <Rung Number="0">
            <Elements>
                <Element Type="CTU" Name="Counter1" Parameter="100" />
                <Element Type="COIL" Name="CounterDone" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(counterXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Counter)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``LSElectricParser should handle complex nested structures`` () =
        let nestedXml = """<?xml version="1.0" encoding="UTF-8"?>
<XG5000Project>
    <Program Name="ComplexProgram">
        <Rung Number="0">
            <Elements>
                <Branch Type="Parallel">
                    <Elements>
                        <Element Type="NO_CONTACT" Name="Input1" />
                        <Element Type="NO_CONTACT" Name="Input2" />
                    </Elements>
                </Branch>
                <Element Type="COIL" Name="Output" />
            </Elements>
        </Rung>
    </Program>
</XG5000Project>"""

        let parser = createParser()

        async {
            let! result = parser.ParseContentAsync(nestedXml)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "Input1"
            logic.Variables |> should contain "Input2"
            logic.Variables |> should contain "Output"
        } |> Async.RunSynchronously