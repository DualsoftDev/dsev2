module Ev2.PLC.Mapper.Tests.MxLogicAnalyzerTests

open System
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Core.Engine

/// Mitsubishi 로직 분석기 테스트
type MxLogicAnalyzerTests() =

    let createAnalyzer() =
        let logger = NullLogger<MxLogicAnalyzer>.Instance
        MxLogicAnalyzerFactory.create logger

    [<Fact>]
    let ``Parse LD instruction should extract condition correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "1"
            Name = Some "Test LD"
            Number = 1
            Content = "LD X0"
            RawContent = Some "LD X0"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.IsSome |> should equal true
        match result with
        | Some flow ->
            flow.Conditions.Length |> should equal 1
            flow.Conditions.[0].Variable |> should equal "X0"
            flow.Conditions.[0].Operator |> should equal ConditionOperator.Equal
            flow.Conditions.[0].Value |> should equal "True"
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse LDI instruction should extract inverted condition correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "2"
            Name = Some "Test LDI"
            Number = 1
            Content = "LDI X1"
            RawContent = Some "LDI X1"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.ExtractConditionsAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.Length |> should equal 1
        result.[0].Variable |> should equal "X1"
        result.[0].Operator |> should equal ConditionOperator.Not

    [<Fact>]
    let ``Parse OUT instruction should extract action correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "3"
            Name = Some "Test OUT"
            Number = 1
            Content = "OUT Y0"
            RawContent = Some "OUT Y0"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.ExtractActionsAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.Length |> should equal 1
        result.[0].Variable |> should equal "Y0"
        result.[0].Operation |> should equal ActionOperation.Assign
        result.[0].Value |> should equal (Some "True")

    [<Fact>]
    let ``Parse complex ladder logic with multiple instructions``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "4"
            Name = Some "Complex Logic"
            Number = 1
            Content = "LD X0\nAND X1\nOR X2\nOUT Y0"
            RawContent = Some "LD X0\nAND X1\nOR X2\nOUT Y0"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Sequential
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.IsSome |> should equal true
        match result with
        | Some flow ->
            flow.Conditions.Length |> should equal 3
            flow.Actions.Length |> should equal 1
            flow.Actions.[0].Variable |> should equal "Y0"
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse MOV instruction should extract move action``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "5"
            Name = Some "Test MOV"
            Number = 1
            Content = "MOV D100 D200"
            RawContent = Some "MOV D100 D200"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Simple
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.ExtractActionsAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.Length |> should equal 1
        result.[0].Variable |> should equal "D200"
        result.[0].Operation |> should equal ActionOperation.Assign
        result.[0].Value |> should equal (Some "D100")

    [<Fact>]
    let ``Parse Timer instruction should extract timer condition and action``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "6"
            Name = Some "Test Timer"
            Number = 1
            Content = "TON T0 K1000"
            RawContent = Some "TON T0 K1000"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Timer
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.IsSome |> should equal true
        match result with
        | Some flow ->
            flow.Conditions.Length |> should equal 1
            flow.Conditions.[0].Variable |> should equal "T0.Q"
            flow.Actions.Length |> should equal 1
            flow.Actions.[0].Variable |> should equal "T0"
            flow.Type |> should equal LogicFlowType.Timer
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse Counter instruction should extract counter condition and action``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "7"
            Name = Some "Test Counter"
            Number = 1
            Content = "CTU C0 K100"
            RawContent = Some "CTU C0 K100"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Counter
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let result = analyzer.AnalyzeRungAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        result.IsSome |> should equal true
        match result with
        | Some flow ->
            flow.Conditions.Length |> should equal 1
            flow.Conditions.[0].Variable |> should equal "C0.Q"
            flow.Actions.Length |> should equal 1
            flow.Actions.[0].Variable |> should equal "C0"
            flow.Actions.[0].Operation |> should equal ActionOperation.Increment
            flow.Type |> should equal LogicFlowType.Counter
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Detect logic flow type correctly for arithmetic operations``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "8"
            Name = Some "Math Operations"
            Number = 1
            Content = "ADD D100 D101 D102"
            RawContent = Some "ADD D100 D101 D102"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Math
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let flowType = analyzer.DetectLogicFlowTypeAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        flowType |> should equal LogicFlowType.Math

    [<Fact>]
    let ``Extract IO variables from complex rung``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "9"
            Name = Some "IO Test"
            Number = 1
            Content = "LD X0\nAND X1\nOUT Y0\nSET Y1"
            RawContent = Some "LD X0\nAND X1\nOUT Y0\nSET Y1"
            LogicType = LogicType.IL
            Type = Some LogicFlowType.Sequential
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let inputs, outputs = analyzer.ExtractIOVariablesAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        inputs.Length |> should equal 2
        inputs |> should contain "X0"
        inputs |> should contain "X1"
        outputs.Length |> should equal 2
        outputs |> should contain "Y0"
        outputs |> should contain "Y1"

    [<Fact>]
    let ``Analyze sequence with dependencies``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rungs = [
            { Id = Some "10"; Name = Some "Rung1"; Number = 1; Content = "LD X0\nOUT M0"; RawContent = Some "LD X0\nOUT M0"; LogicType = LogicType.IL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 1; Properties = Map.empty; Comment = None }
            { Id = Some "11"; Name = Some "Rung2"; Number = 2; Content = "LD M0\nOUT Y0"; RawContent = Some "LD M0\nOUT Y0"; LogicType = LogicType.IL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 2; Properties = Map.empty; Comment = None }
            { Id = Some "12"; Name = Some "Rung3"; Number = 3; Content = "LD X1\nOUT Y1"; RawContent = Some "LD X1\nOUT Y1"; LogicType = LogicType.IL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 3; Properties = Map.empty; Comment = None }
        ]

        // Act
        let sequence = analyzer.AnalyzeSequenceAsync(rungs) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        sequence.LogicFlows.Length |> should equal 3
        sequence.Statistics.TotalRungs |> should equal 3
        sequence.Statistics.TotalConditions |> should equal 3
        sequence.Statistics.TotalActions |> should equal 3