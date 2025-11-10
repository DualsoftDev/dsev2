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

module S7ParserTests =

    /// Test logger
    let private createLogger() =
        (NullLoggerFactory.Instance :> ILoggerFactory).CreateLogger<S7Parser>()

    /// Sample XML content for Siemens S7
    let private sampleXML = """<?xml version="1.0" encoding="UTF-8"?>
<Document>
    <Engineering version="V17">
        <SW.Blocks.FC Name="FC1">
            <AttributeList>
                <ProgrammingLanguage>LAD</ProgrammingLanguage>
            </AttributeList>
            <ObjectList>
                <SW.Blocks.CompileUnit>
                    <AttributeList>
                        <NetworkSource>
                            <Network Number="1">
                                <Title>Motor Control</Title>
                                <Comment>Start/Stop motor control logic</Comment>
                                <LAD>
                                    <Contact Name="I0.0" Type="NO" />
                                    <Contact Name="I0.1" Type="NC" />
                                    <Coil Name="Q0.0" />
                                </LAD>
                            </Network>
                            <Network Number="2">
                                <Title>Timer Operation</Title>
                                <Comment>5 second timer</Comment>
                                <STL>
                                    A I0.2
                                    L S5T#5S
                                    SD T1
                                    A T1
                                    = Q0.1
                                </STL>
                            </Network>
                            <Network Number="3">
                                <Title>Counter Logic</Title>
                                <STL>
                                    A I0.3
                                    CU C1
                                    L 100
                                    L C1
                                    >=I
                                    = Q0.2
                                </STL>
                            </Network>
                            <Network Number="4">
                                <Title>Safety Circuit</Title>
                                <Comment>Emergency stop logic</Comment>
                                <STL>
                                    A I1.0  // EMERGENCY_STOP
                                    = Q1.0  // SafetyRelay
                                </STL>
                            </Network>
                            <Network Number="5">
                                <Title>Math Operations</Title>
                                <STL>
                                    L MW100
                                    L MW102
                                    +I
                                    T MW104
                                    L MW104
                                    L 2
                                    *I
                                    T MW106
                                </STL>
                            </Network>
                        </NetworkSource>
                    </AttributeList>
                </SW.Blocks.CompileUnit>
            </ObjectList>
        </SW.Blocks.FC>
    </Engineering>
</Document>"""

    /// Sample AWL/STL content for Siemens
    let private sampleAWL = """
NETWORK 1
TITLE = Motor Start/Stop

// Motor control with start and stop buttons
      A     I 0.0       // Start button
      AN    I 0.1       // Stop button
      =     Q 0.0       // Motor output

NETWORK 2
TITLE = Timer Function

// 5 second timer operation
      A     I 0.2
      L     S5T#5S
      SD    T 1
      A     T 1
      =     Q 0.1

NETWORK 3
TITLE = Counter Operation

      A     I 0.3
      CU    C 1
      L     100
      L     C 1
      >=I
      =     Q 0.2

NETWORK 4
TITLE = Safety Logic

// Emergency stop circuit
      A     "EMERGENCY_STOP"
      =     "SafetyRelay"

NETWORK 5
TITLE = Math Calculation

      L     DB100.DBW0
      L     DB100.DBW2
      +I
      T     DB100.DBW4
"""

    /// Sample SCL content for Siemens
    let private sampleSCL = """
FUNCTION_BLOCK "MotorControl"
VAR_INPUT
    StartButton : BOOL;
    StopButton : BOOL;
    EmergencyStop : BOOL;
END_VAR

VAR_OUTPUT
    Motor : BOOL;
    Alarm : BOOL;
END_VAR

VAR
    Timer1 : TON;
    Counter1 : CTU;
    Temperature : INT;
END_VAR

BEGIN
    // Motor control logic
    IF StartButton AND NOT StopButton AND NOT EmergencyStop THEN
        Motor := TRUE;
    ELSE
        Motor := FALSE;
    END_IF;

    // Timer operation
    Timer1(IN := Motor, PT := T#5S);
    Alarm := Timer1.Q;

    // Temperature check
    IF Temperature > 100 THEN
        Motor := FALSE;
        Alarm := TRUE;
    END_IF;
END_FUNCTION_BLOCK
"""

    [<Fact>]
    let ``S7Parser should parse XML file content`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            result |> should not' (be Empty)
            result.Length |> should equal 5
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should parse AWL file content`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp AWL file
            let tempFile = Path.GetTempFileName() + ".awl"
            try
                File.WriteAllText(tempFile, sampleAWL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should equal 5
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should parse SCL file content`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp SCL file
            let tempFile = Path.GetTempFileName() + ".scl"
            try
                File.WriteAllText(tempFile, sampleSCL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 0)
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should extract network numbers correctly`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            let network1 = result |> List.find (fun r -> r.Number = 1)
            network1.Number |> should equal 1
            network1.Name |> should equal (Some "Motor Control")
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect logic flow types`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            // Timer network
            let timerNetwork = result |> List.find (fun r -> r.Name = Some "Timer Operation")
            timerNetwork.Type |> should equal (Some LogicFlowType.Timer)

            // Counter network
            let counterNetwork = result |> List.find (fun r -> r.Name = Some "Counter Logic")
            counterNetwork.Type |> should equal (Some LogicFlowType.Counter)

            // Safety network
            let safetyNetwork = result |> List.find (fun r -> r.Name = Some "Safety Circuit")
            safetyNetwork.Type |> should equal (Some LogicFlowType.Safety)

            // Math network
            let mathNetwork = result |> List.find (fun r -> r.Name = Some "Math Operations")
            mathNetwork.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should extract variables from networks`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleXML)

            let network1 = result |> List.find (fun r -> r.Number = 1)
            network1.Variables |> should contain "I0.0"
            network1.Variables |> should contain "I0.1"
            network1.Variables |> should contain "Q0.0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect STL logic type`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp STL file
            let tempFile = Path.GetTempFileName() + ".stl"
            try
                File.WriteAllText(tempFile, sampleAWL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                let logic = result.[0]
                logic.LogicType |> should equal LogicType.STL
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect SCL logic type`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp SCL file
            let tempFile = Path.GetTempFileName() + ".scl"
            try
                File.WriteAllText(tempFile, sampleSCL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                let logic = result.[0]
                logic.LogicType |> should equal LogicType.SCL
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should support multiple file extensions`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        parser.SupportedFileExtensions |> should contain ".awl"
        parser.SupportedFileExtensions |> should contain ".AWL"
        parser.SupportedFileExtensions |> should contain ".stl"
        parser.SupportedFileExtensions |> should contain ".STL"
        parser.SupportedFileExtensions |> should contain ".scl"
        parser.SupportedFileExtensions |> should contain ".SCL"
        parser.SupportedFileExtensions |> should contain ".xml"
        parser.SupportedFileExtensions |> should contain ".XML"

    [<Fact>]
    let ``S7Parser should check if it can parse file`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        parser.CanParse("program.awl") |> should be True
        parser.CanParse("program.AWL") |> should be True
        parser.CanParse("program.stl") |> should be True
        parser.CanParse("program.STL") |> should be True
        parser.CanParse("program.scl") |> should be True
        parser.CanParse("program.SCL") |> should be True
        parser.CanParse("program.xml") |> should be True
        parser.CanParse("program.csv") |> should be False
        parser.CanParse("program.txt") |> should be False

    [<Fact>]
    let ``S7Parser should extract comments from AWL`` () =
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp AWL file
            let tempFile = Path.GetTempFileName() + ".awl"
            try
                File.WriteAllText(tempFile, sampleAWL)
                let! result = parser.ParseFileAsync(tempFile)

                let network1 = result |> List.find (fun r -> r.Number = 1)
                network1.Comments |> should contain "Motor control with start and stop buttons"
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should handle empty XML gracefully`` () =
        let emptyXml = """<?xml version="1.0" encoding="UTF-8"?><Document></Document>"""
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(emptyXml)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should handle malformed XML gracefully`` () =
        let malformed = "<This is not valid XML"
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(malformed)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect conditional flow type in SCL`` () =
        let conditionalSCL = """
IF Temperature > 100 THEN
    HeaterOn := FALSE;
ELSIF Temperature < 50 THEN
    HeaterOn := TRUE;
END_IF;
"""
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(conditionalSCL)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Conditional)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect interrupt flow type`` () =
        let intAWL = """
NETWORK 1
      A     P#I0.0    // Positive edge
      FP    M10.0
      =     Q0.0

      A     I0.1
      FN    M10.1     // Negative edge
      =     Q0.1
"""
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp AWL file
            let tempFile = Path.GetTempFileName() + ".awl"
            try
                File.WriteAllText(tempFile, intAWL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                let logic = result.[0]
                logic.Type |> should equal (Some LogicFlowType.Interrupt)
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should extract data block variables`` () =
        let dbAWL = """
NETWORK 1
      L     DB100.DBW0
      L     DB100.DBW2
      +I
      T     DB100.DBW4

      L     DB200.DBD10
      L     DB200.DBD14
      *R
      T     DB200.DBD18
"""
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            // Create temp AWL file
            let tempFile = Path.GetTempFileName() + ".awl"
            try
                File.WriteAllText(tempFile, dbAWL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                let logic = result.[0]
                logic.Variables |> should contain "DB100.DBW0"
                logic.Variables |> should contain "DB100.DBW2"
                logic.Variables |> should contain "DB100.DBW4"
                logic.Variables |> should contain "DB200.DBD10"
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``S7Parser should detect sequential flow type in SCL`` () =
        let seqSCL = """
FUNCTION_BLOCK "Sequence"
BEGIN
    CASE State OF
        0:  // Initial state
            Output1 := TRUE;
            State := 1;
        1:  // Next state
            Output2 := TRUE;
            State := 2;
        2:  // Final state
            Output3 := TRUE;
            State := 0;
    END_CASE;
END_FUNCTION_BLOCK
"""
        let logger = createLogger()
        let parser = S7Parser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(seqSCL)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Sequential)
        } |> Async.RunSynchronously