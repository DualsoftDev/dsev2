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

module MitsubishiParserTests =

    /// Test logger
    let private createLogger() =
        (NullLoggerFactory.Instance :> ILoggerFactory).CreateLogger<MitsubishiParser>()

    /// Sample CSV content for Mitsubishi
    let private sampleCSV = """Step,Instruction,Device,Comment
0,LD,X0,Start Button
1,AND,X1,Safety Switch
2,OUT,Y0,Motor Output
3,LD,Y0,
4,TON,T0,T#5s
5,LD,T0,Timer Done
6,OUT,Y1,Alarm
7,LD,X2,Count Input
8,CTU,C0,100
9,LD,C0,Counter Done
10,OUT,Y2,Complete Signal
11,LD,X10,Emergency Stop
12,OUT,Y10,Safety Relay
13,LD,D100,
14,ADD,D100,D101
15,MOV,D101,D200
16,END,,"""

    /// Sample AWL content for Mitsubishi
    let private sampleAWL = """
// Motor control logic
LD X0
AND X1
OUT Y0

// Timer operation
LD Y0
TON T0 K50
LD T0
OUT Y1

// Counter operation
LD X2
CTU C0 K100
LD C0
OUT Y2

// Safety circuit
LD X10
OUT Y10

// Math operations
LD D100
ADD D101 D102
MUL D102 K2 D103
MOV D103 D200

END
"""

    [<Fact>]
    let ``MitsubishiParser should parse CSV file content`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            result |> should not' (be Empty)
            result.Length |> should be (greaterThan 0)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should parse AWL file content`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            // Create temp AWL file
            let tempFile = Path.GetTempFileName() + ".awl"
            try
                File.WriteAllText(tempFile, sampleAWL)
                let! result = parser.ParseFileAsync(tempFile)

                result |> should not' (be Empty)
                result.Length |> should be (greaterThan 0)
            finally
                if File.Exists(tempFile) then File.Delete(tempFile)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should extract variables from CSV`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            let firstRung = result |> List.head
            firstRung.Variables |> should not' (be Empty)

            // Check for specific variables
            let allVariables = result |> List.collect (fun r -> r.Variables)
            allVariables |> should contain "X0"
            allVariables |> should contain "Y0"
            allVariables |> should contain "T0"
            allVariables |> should contain "C0"
            allVariables |> should contain "D100"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect timer flow type`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            let timerRung = result |> List.find (fun r -> r.Content.Contains("TON"))
            timerRung.Type |> should equal (Some LogicFlowType.Timer)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect counter flow type`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            let counterRung = result |> List.find (fun r -> r.Content.Contains("CTU"))
            counterRung.Type |> should equal (Some LogicFlowType.Counter)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect safety flow type`` () =
        let safetyCSV = """Step,Instruction,Device,Comment
0,LD,X_EMERGENCY,Emergency Stop
1,OUT,Y_SAFETY,Safety Relay"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(safetyCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Safety)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect math flow type`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            let mathRung = result |> List.find (fun r -> r.Content.Contains("ADD"))
            mathRung.Type |> should equal (Some LogicFlowType.Math)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should support CSV and AWL extensions`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        parser.SupportedFileExtensions |> should contain ".csv"
        parser.SupportedFileExtensions |> should contain ".CSV"
        parser.SupportedFileExtensions |> should contain ".awl"
        parser.SupportedFileExtensions |> should contain ".AWL"
        parser.SupportedFileExtensions |> should contain ".il"
        parser.SupportedFileExtensions |> should contain ".IL"

    [<Fact>]
    let ``MitsubishiParser should check if it can parse file`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        parser.CanParse("program.csv") |> should be True
        parser.CanParse("program.CSV") |> should be True
        parser.CanParse("program.awl") |> should be True
        parser.CanParse("program.AWL") |> should be True
        parser.CanParse("program.il") |> should be True
        parser.CanParse("program.xml") |> should be False
        parser.CanParse("program.txt") |> should be False

    [<Fact>]
    let ``MitsubishiParser should handle tab-separated CSV`` () =
        let tabCSV = "Step\tInstruction\tDevice\tComment
0\tLD\tX0\tStart
1\tOUT\tY0\tMotor"

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(tabCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "X0"
            logic.Variables |> should contain "Y0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should handle quoted CSV values`` () =
        let quotedCSV = """Step,Instruction,Device,Comment
"0","LD","X0","Start Button"
"1","OUT","Y0","Motor Output"
"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(quotedCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Variables |> should contain "X0"
            logic.Variables |> should contain "Y0"
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should extract comments from CSV`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            let rungs = result |> List.filter (fun r -> r.Comments.Length > 0)
            rungs |> should not' (be Empty)

            let motorRung = result |> List.find (fun r -> r.Comments |> List.exists (fun c -> c.Contains("Motor")))
            motorRung.Comments |> should not' (be Empty)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should handle empty CSV gracefully`` () =
        let emptyCSV = "Step,Instruction,Device,Comment"
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(emptyCSV)

            result |> should be Empty
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should handle malformed CSV gracefully`` () =
        let malformed = "This is not a valid CSV format"
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(malformed)

            // Should still try to parse, but may return empty or minimal results
            result |> ignore
            true |> should be True // Just check it doesn't throw
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect instruction list logic type`` () =
        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(sampleCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.LogicType |> should equal LogicType.InstructionList
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect conditional flow type`` () =
        let jumpCSV = """Step,Instruction,Device,Comment
0,LD,X0,
1,CJ,P10,Jump to label
2,LBL,P10,Label
3,OUT,Y0,"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(jumpCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Conditional)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect interrupt flow type`` () =
        let intCSV = """Step,Instruction,Device,Comment
0,LD,X0,
1,PLS,M0,Pulse
2,PLF,M1,Falling edge
3,OUT,Y0,"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(intCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Interrupt)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should detect sequential flow type`` () =
        let seqCSV = """Step,Instruction,Device,Comment
0,LD,X0,
1,MOV,D100,D200
2,BMOV,D200,D300
3,OUT,Y0,"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(seqCSV)

            result |> should not' (be Empty)
            let logic = result.[0]
            logic.Type |> should equal (Some LogicFlowType.Sequential)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``MitsubishiParser should group instructions into rungs correctly`` () =
        let multiRungCSV = """Step,Instruction,Device,Comment
0,LD,X0,First rung
1,OUT,Y0,
2,LD,X1,Second rung
3,OUT,Y1,
4,LD,X2,Third rung
5,OUT,Y2,
6,END,,"""

        let logger = createLogger()
        let parser = MitsubishiParser(logger) :> IPlcParser

        async {
            let! result = parser.ParseContentAsync(multiRungCSV)

            result.Length |> should equal 3

            let rung1 = result.[0]
            rung1.Variables |> should contain "X0"
            rung1.Variables |> should contain "Y0"

            let rung2 = result.[1]
            rung2.Variables |> should contain "X1"
            rung2.Variables |> should contain "Y1"

            let rung3 = result.[2]
            rung3.Variables |> should contain "X2"
            rung3.Variables |> should contain "Y2"
        } |> Async.RunSynchronously