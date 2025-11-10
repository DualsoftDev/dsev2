module Ev2.PLC.Mapper.Tests.ABLogicAnalyzerTests

open System
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Core.Engine

/// 파서 생성 헬퍼
let createAnalyzer () =
    let logger = NullLogger<ABLogicAnalyzer>.Instance
    ABLogicAnalyzerFactory.create logger

[<Fact>]
let ``ABLogicAnalyzer should be created successfully`` () =
    let analyzer = createAnalyzer()

    analyzer |> should not' (be Null)
    analyzer.SupportedVendor.Manufacturer |> should equal "Allen-Bradley"

[<Fact>]
let ``ABLogicAnalyzer should parse XIC instruction`` () =
    let analyzer = createAnalyzer()
    let instruction = "XIC(Start_Button)"

    let result = analyzer.ParseXICAsync(instruction) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some condition ->
        condition.Variable |> should equal "Start_Button"
        condition.Operator |> should equal ConditionOperator.Equal
        condition.Value |> should equal "True"
    | None -> Assert.True(false, "Failed to parse XIC instruction")

[<Fact>]
let ``ABLogicAnalyzer should parse XIO instruction`` () =
    let analyzer = createAnalyzer()
    let instruction = "XIO(Stop_Button)"

    let result = analyzer.ParseXIOAsync(instruction) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some condition ->
        condition.Variable |> should equal "Stop_Button"
        condition.Operator |> should equal ConditionOperator.Not
    | None -> Assert.True(false, "Failed to parse XIO instruction")

[<Fact>]
let ``ABLogicAnalyzer should parse OTE instruction`` () =
    let analyzer = createAnalyzer()
    let instruction = "OTE(Motor_Run)"

    let result = analyzer.ParseOTEAsync(instruction) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some action ->
        action.Variable |> should equal "Motor_Run"
        action.Operation |> should equal ActionOperation.Assign
    | None -> Assert.True(false, "Failed to parse OTE instruction")

[<Fact>]
let ``ABLogicAnalyzer should parse MOV instruction`` () =
    let analyzer = createAnalyzer()
    let instruction = "MOV(100,Counter)"

    let result = analyzer.ParseMOVAsync(instruction) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some action ->
        action.Variable |> should equal "Counter"
        action.Operation |> should equal ActionOperation.Assign
        action.Value |> should equal (Some "100")
    | None -> Assert.True(false, "Failed to parse MOV instruction")

[<Fact>]
let ``ABLogicAnalyzer should parse comparison instructions`` () =
    let analyzer = createAnalyzer()

    // EQU
    let equ = analyzer.ParseComparisonAsync("EQU(11,Status)") |> Async.AwaitTask |> Async.RunSynchronously
    equ |> should not' (be None)
    match equ with
    | Some cond -> cond.Operator |> should equal ConditionOperator.Equal
    | None -> ()

    // GRT
    let grt = analyzer.ParseComparisonAsync("GRT(Temperature,50)") |> Async.AwaitTask |> Async.RunSynchronously
    grt |> should not' (be None)
    match grt with
    | Some cond -> cond.Operator |> should equal ConditionOperator.GreaterThan
    | None -> ()

[<Fact>]
let ``ABLogicAnalyzer should parse arithmetic instructions`` () =
    let analyzer = createAnalyzer()

    // ADD
    let add = analyzer.ParseArithmeticAsync("ADD(Value1,Value2,Result)") |> Async.AwaitTask |> Async.RunSynchronously
    add |> should not' (be None)
    match add with
    | Some action ->
        action.Variable |> should equal "Result"
        action.Operation |> should equal ActionOperation.Assign
    | None -> ()

[<Fact>]
let ``ABLogicAnalyzer should analyze simple rung`` () =
    let analyzer = createAnalyzer()

    // Simple rung: XIC(Start)OTE(Motor)
    let rung = {
        Id = Some "Rung1"
        Name = Some "Test Rung 1"
        Number = 1
        Content = "N: XIC(Start_Button)OTE(Motor_Run);"
        RawContent = Some "N: XIC(Start_Button)OTE(Motor_Run);"
        LogicType = LadderRung
        Type = Some LogicFlowType.Simple
        Variables = ["Start_Button"; "Motor_Run"]
        Comments = []
        LineNumber = Some 1
        Properties = Map.empty
        Comment = None
    }

    let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some logicFlow ->
        logicFlow.Conditions.Length |> should equal 1
        logicFlow.Actions.Length |> should equal 1
        logicFlow.Conditions.[0].Variable |> should equal "Start_Button"
        logicFlow.Actions.[0].Variable |> should equal "Motor_Run"
    | None -> Assert.True(false, "Failed to analyze rung")

[<Fact>]
let ``ABLogicAnalyzer should analyze complex rung`` () =
    let analyzer = createAnalyzer()

    // Complex rung from sample: EQU, MOV, XIC, OTE
    let rung = {
        Id = Some "Rung2"
        Name = Some "Complex Rung"
        Number = 2
        Content = "N: EQU(11,FAC_IF[BSOMS_Index+0])MOV(2000,BS_Timer[BSOMS_Moniter,0].PRE)XIC(BS_Timer[BSOMS_Moniter,0].DN)MOV(1,FAC_IF[BSOMS_Index+0]);"
        RawContent = Some "N: EQU(11,FAC_IF[BSOMS_Index+0])MOV(2000,BS_Timer[BSOMS_Moniter,0].PRE)XIC(BS_Timer[BSOMS_Moniter,0].DN)MOV(1,FAC_IF[BSOMS_Index+0]);"
        LogicType = LadderRung
        Type = Some LogicFlowType.Sequential
        Variables = []
        Comments = []
        LineNumber = Some 2
        Properties = Map.empty
        Comment = None
    }

    let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be None)
    match result with
    | Some logicFlow ->
        logicFlow.Conditions.Length |> should be (greaterThan 0)
        logicFlow.Actions.Length |> should be (greaterThan 0)
        printfn $"Complex rung analyzed: {logicFlow.Conditions.Length} conditions, {logicFlow.Actions.Length} actions"
    | None -> Assert.True(false, "Failed to analyze complex rung")

[<Fact>]
let ``ABLogicAnalyzer should analyze batch of rungs`` () =
    let analyzer = createAnalyzer()

    let rungs = [
        {
            Id = Some "Rung1"
            Name = Some "Rung 1"
            Number = 1
            Content = "N: XIC(Input1)OTE(Output1);"
            RawContent = Some "N: XIC(Input1)OTE(Output1);"
            LogicType = LadderRung
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }
        {
            Id = Some "Rung2"
            Name = Some "Rung 2"
            Number = 2
            Content = "N: XIO(Input2)OTL(Output2);"
            RawContent = Some "N: XIO(Input2)OTL(Output2);"
            LogicType = LadderRung
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 2
            Properties = Map.empty
            Comment = None
        }
        {
            Id = Some "Rung3"
            Name = Some "Rung 3"
            Number = 3
            Content = "N: MOV(100,Counter);"
            RawContent = Some "N: MOV(100,Counter);"
            LogicType = LadderRung
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 3
            Properties = Map.empty
            Comment = None
        }
    ]

    let result = analyzer.AnalyzeRungsBatchAsync(rungs) |> Async.AwaitTask |> Async.RunSynchronously

    result |> should not' (be Empty)
    result.Length |> should equal 3
    printfn $"Batch analyzed {result.Length} rungs successfully"

[<Fact>]
let ``ABLogicAnalyzer should detect safety interlocks`` () =
    let analyzer = createAnalyzer()

    let rung = {
        Id = Some "SafetyRung"
        Name = Some "Safety Interlock Test"
        Number = 1
        Content = "N: XIC(Emergency_Stop)XIC(Safety_Gate)OTE(Safety_Interlock);"
        RawContent = Some "N: XIC(Emergency_Stop)XIC(Safety_Gate)OTE(Safety_Interlock);"
        LogicType = LadderRung
        Type = Some LogicFlowType.Safety
        Variables = []
        Comments = []
        LineNumber = Some 1
        Properties = Map.empty
        Comment = None
    }

    let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | Some logicFlow ->
        logicFlow.Type |> should equal LogicFlowType.Safety
        printfn $"Safety rung detected: {logicFlow.Type}"
    | None -> Assert.True(false, "Failed to analyze safety rung")

[<Fact>]
let ``ABLogicAnalyzer should extract IO variables`` () =
    let analyzer = createAnalyzer()

    let rung = {
        Id = Some "Rung1"
        Name = Some "Multi IO Rung"
        Number = 1
        Content = "N: XIC(Input1)XIC(Input2)OTE(Output1)OTE(Output2);"
        RawContent = Some "N: XIC(Input1)XIC(Input2)OTE(Output1)OTE(Output2);"
        LogicType = LadderRung
        Type = Some LogicFlowType.Simple
        Variables = []
        Comments = []
        LineNumber = Some 1
        Properties = Map.empty
        Comment = None
    }

    let result = analyzer.ExtractIOVariablesAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously
    let (inputs, outputs) = result

    inputs.Length |> should equal 2
    outputs.Length |> should equal 2
    inputs |> should contain "Input1"
    inputs |> should contain "Input2"
    outputs |> should contain "Output1"
    outputs |> should contain "Output2"

[<Fact>]
let ``ABLogicAnalyzer should handle array and member access`` () =
    let analyzer = createAnalyzer()

    let rung = {
        Id = Some "ArrayRung"
        Name = Some "Array Access Test"
        Number = 1
        Content = "N: XIC(Timer[0].DN)MOV(100,Array[Index+1]);"
        RawContent = Some "N: XIC(Timer[0].DN)MOV(100,Array[Index+1]);"
        LogicType = LadderRung
        Type = Some LogicFlowType.Simple
        Variables = []
        Comments = []
        LineNumber = Some 1
        Properties = Map.empty
        Comment = None
    }

    let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

    match result with
    | Some logicFlow ->
        logicFlow.Conditions.Length |> should equal 1
        logicFlow.Actions.Length |> should equal 1
        // 배열 인덱스와 멤버 접근이 정상적으로 파싱되어야 함
        logicFlow.Conditions.[0].Variable |> should equal "Timer[0].DN"
        logicFlow.Actions.[0].Variable |> should equal "Array[Index+1]"
    | None -> Assert.True(false, "Failed to analyze rung with arrays")
