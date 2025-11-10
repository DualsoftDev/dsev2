namespace Ev2.PLC.Mapper.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Analyzer
open TestHelpers

module LogicAnalyzerTests =

    let createAnalyzer() =
        let logger = createLogger<LogicAnalyzer>()
        LogicAnalyzer(logger)

    [<Fact>]
    let ``LogicAnalyzer should detect logic flow types`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let flowTypes = analyzer.DetectFlowTypes(logic)

        flowTypes |> should contain LogicFlowType.Simple
        flowTypes |> should contain LogicFlowType.Timer

    [<Fact>]
    let ``LogicAnalyzer should count logic by type`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let counts = analyzer.CountLogicByType(logic)

        counts.[LogicType.LadderRung] |> should equal 2

    [<Fact>]
    let ``LogicAnalyzer should find connected logic blocks`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let connections = analyzer.FindConnectedLogic(logic, "Motor")

        connections |> should not' (be Empty)
        connections |> List.exists (fun l -> l.Variables |> List.contains "Motor") |> should be True

    [<Fact>]
    let ``LogicAnalyzer should detect logic dependencies`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let dependencies = analyzer.FindDependencies(logic.[0], logic)

        // Logic that uses variables from the first logic block
        dependencies |> should not' (be Empty)

    [<Fact>]
    let ``LogicAnalyzer should calculate complexity metrics`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic().[0]

        let complexity = analyzer.CalculateComplexity(logic)

        complexity.VariableCount |> should equal 3
        complexity.InstructionCount |> should be (greaterThan 0)
        complexity.CyclomaticComplexity |> should be (greaterThan 0)

    [<Fact>]
    let ``LogicAnalyzer should detect critical paths`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let criticalPaths = analyzer.FindCriticalPaths(logic)

        criticalPaths |> should not' (be Empty)

    [<Fact>]
    let ``LogicAnalyzer should optimize logic flow`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let optimized = analyzer.OptimizeLogicFlow(logic)

        optimized |> List.length |> should be (lessThanOrEqualTo (logic |> List.length))

    [<Fact>]
    let ``LogicAnalyzer should detect timer usage patterns`` () =
        let analyzer = createAnalyzer()
        let timerLogic = [
            { TestData.createSampleLogic().[1] with
                Content = "TON(Timer1, 5000)"
                Type = Some LogicFlowType.Timer }
            { TestData.createSampleLogic().[0] with
                Content = "TOF(Timer2, 3000)"
                Type = Some LogicFlowType.Timer }
        ]

        let patterns = analyzer.DetectTimerPatterns(timerLogic)

        patterns.TotalTimers |> should equal 2
        patterns.TimerTypes |> should contain "TON"
        patterns.TimerTypes |> should contain "TOF"

    [<Fact>]
    let ``LogicAnalyzer should detect counter usage patterns`` () =
        let analyzer = createAnalyzer()
        let counterLogic = [
            { TestData.createSampleLogic().[0] with
                Content = "CTU(Counter1, 100)"
                Type = Some LogicFlowType.Counter }
            { TestData.createSampleLogic().[0] with
                Content = "CTD(Counter2, 50)"
                Type = Some LogicFlowType.Counter }
        ]

        let patterns = analyzer.DetectCounterPatterns(counterLogic)

        patterns.TotalCounters |> should equal 2
        patterns.CounterTypes |> should contain "CTU"
        patterns.CounterTypes |> should contain "CTD"

    [<Fact>]
    let ``LogicAnalyzer should detect safety interlocks`` () =
        let analyzer = createAnalyzer()
        let safetyLogic = [
            { TestData.createSampleLogic().[0] with
                Content = "XIC(ESTOP) XIO(SafetyGate) OTE(SafetyRelay)"
                Variables = ["ESTOP"; "SafetyGate"; "SafetyRelay"]
                Type = Some LogicFlowType.Safety }
        ]

        let interlocks = analyzer.DetectSafetyInterlocks(safetyLogic)

        interlocks |> should not' (be Empty)
        interlocks |> List.exists (fun i -> i.Variables |> List.contains "ESTOP") |> should be True

    [<Fact>]
    let ``LogicAnalyzer should validate logic structure`` () =
        let analyzer = createAnalyzer()
        let validLogic = TestData.createSampleLogic().[0]
        let invalidLogic = { validLogic with Content = ""; Variables = [] }

        analyzer.ValidateLogicStructure(validLogic) |> should be True
        analyzer.ValidateLogicStructure(invalidLogic) |> should be False

    [<Fact>]
    let ``LogicAnalyzer should detect redundant logic`` () =
        let analyzer = createAnalyzer()
        let redundantLogic = [
            TestData.createSampleLogic().[0]
            TestData.createSampleLogic().[0]  // Duplicate
        ]

        let redundancies = analyzer.FindRedundantLogic(redundantLogic)

        redundancies |> should not' (be Empty)

    [<Fact>]
    let ``LogicAnalyzer should generate logic report`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let report = analyzer.GenerateLogicReport(logic)

        report |> should haveSubstring "Total Logic Blocks:"
        report |> should haveSubstring "Logic Types:"
        report |> should haveSubstring "Flow Types:"

    [<Fact>]
    let ``LogicAnalyzer should detect sequential flow patterns`` () =
        let analyzer = createAnalyzer()
        let sequentialLogic = [
            { TestData.createSampleLogic().[0] with
                Content = "SET(Step1) RST(Step0)"
                Type = Some LogicFlowType.Sequential }
            { TestData.createSampleLogic().[0] with
                Content = "SET(Step2) RST(Step1)"
                Type = Some LogicFlowType.Sequential }
        ]

        let patterns = analyzer.DetectSequentialPatterns(sequentialLogic)

        patterns.StepCount |> should equal 3  // Step0, Step1, Step2
        patterns.SequenceType |> should equal "Linear"

    [<Fact>]
    let ``LogicAnalyzer should calculate execution time estimates`` () =
        let analyzer = createAnalyzer()
        let logic = TestData.createSampleLogic()

        let estimates = analyzer.EstimateExecutionTime(logic)

        estimates |> List.iter (fun (l, time) ->
            time |> should be (greaterThan 0.0)
        )

    [<Fact>]
    let ``LogicAnalyzer should detect logic loops`` () =
        let analyzer = createAnalyzer()
        let loopLogic = [
            { TestData.createSampleLogic().[0] with
                Id = Some "1"
                Variables = ["A"; "B"] }
            { TestData.createSampleLogic().[0] with
                Id = Some "2"
                Variables = ["B"; "C"] }
            { TestData.createSampleLogic().[0] with
                Id = Some "3"
                Variables = ["C"; "A"] }  // Creates a loop
        ]

        let loops = analyzer.DetectLogicLoops(loopLogic)

        loops |> should not' (be Empty)