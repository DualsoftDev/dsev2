module Ev2.PLC.Mapper.Tests.S7LogicAnalyzerTests

open System
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper.Core.Engine

/// Siemens S7 로직 분석기 테스트
type S7LogicAnalyzerTests() =

    let createAnalyzer() =
        let logger = NullLogger<S7LogicAnalyzer>.Instance
        S7LogicAnalyzerFactory.create logger

    [<Fact>]
    let ``Parse A (AND) instruction should extract condition correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "1"
            Name = Some "Test AND"
            Number = 1
            Content = "A I0.0"
            RawContent = Some "A I0.0"
            LogicType = LogicType.STL
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
            flow.Conditions.[0].Variable |> should equal "I0.0"
            flow.Conditions.[0].Operator |> should equal ConditionOperator.Equal
            flow.Conditions.[0].Value |> should equal "True"
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse AN (AND NOT) instruction should extract inverted condition correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "2"
            Name = Some "Test AND NOT"
            Number = 1
            Content = "AN I0.1"
            RawContent = Some "AN I0.1"
            LogicType = LogicType.STL
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
        result.[0].Variable |> should equal "I0.1"
        result.[0].Operator |> should equal ConditionOperator.Not

    [<Fact>]
    let ``Parse = (Assign) instruction should extract action correctly``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "3"
            Name = Some "Test Assign"
            Number = 1
            Content = "= Q0.0"
            RawContent = Some "= Q0.0"
            LogicType = LogicType.STL
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
        result.[0].Variable |> should equal "Q0.0"
        result.[0].Operation |> should equal ActionOperation.Assign
        result.[0].Value |> should equal (Some "True")

    [<Fact>]
    let ``Parse STL logic with multiple instructions``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "4"
            Name = Some "Complex STL"
            Number = 1
            Content = "A I0.0\nAN I0.1\nO I0.2\n= Q0.0"
            RawContent = Some "A I0.0\nAN I0.1\nO I0.2\n= Q0.0"
            LogicType = LogicType.STL
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
            flow.Actions.[0].Variable |> should equal "Q0.0"
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse Set and Reset instructions``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "5"
            Name = Some "Test Set/Reset"
            Number = 1
            Content = "S Q0.0\nR Q0.1"
            RawContent = Some "S Q0.0\nR Q0.1"
            LogicType = LogicType.STL
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
        result.Length |> should equal 2
        result.[0].Variable |> should equal "Q0.0"
        result.[0].Operation |> should equal ActionOperation.Set
        result.[1].Variable |> should equal "Q0.1"
        result.[1].Operation |> should equal ActionOperation.Reset

    [<Fact>]
    let ``Parse Load and Transfer instructions``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "6"
            Name = Some "Test Load/Transfer"
            Number = 1
            Content = "L DB100.DBW0\nT DB101.DBW0"
            RawContent = Some "L DB100.DBW0\nT DB101.DBW0"
            LogicType = LogicType.STL
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
        result.[0].Variable |> should equal "DB101.DBW0"
        result.[0].Operation |> should equal ActionOperation.Assign
        result.[0].Value |> should equal (Some "DB100.DBW0")

    [<Fact>]
    let ``Parse SCL IF statement should extract condition``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "7"
            Name = Some "Test SCL IF"
            Number = 1
            Content = "IF #Temperature > 100 THEN"
            RawContent = Some "IF #Temperature > 100 THEN"
            LogicType = LogicType.SCL
            Type = Some LogicFlowType.Conditional
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
        result.[0].Variable |> should equal "#Temperature"

    [<Fact>]
    let ``Parse SCL assignment statement``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "8"
            Name = Some "Test SCL Assignment"
            Number = 1
            Content = "#Output := #Input * 2;"
            RawContent = Some "#Output := #Input * 2;"
            LogicType = LogicType.SCL
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
        result.[0].Variable |> should equal "#Output"
        result.[0].Operation |> should equal ActionOperation.Assign
        result.[0].Value |> should equal (Some "#Input * 2")

    [<Fact>]
    let ``Parse LAD contact and coil elements``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "9"
            Name = Some "Test LAD"
            Number = 1
            Content = "--[I0.0]----[/I0.1]----(Q0.0)--"
            RawContent = Some "--[I0.0]----[/I0.1]----(Q0.0)--"
            LogicType = LogicType.Ladder
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
            flow.Conditions.Length |> should equal 2
            flow.Conditions.[0].Variable |> should equal "I0.0"
            flow.Conditions.[0].Operator |> should equal ConditionOperator.Equal
            flow.Conditions.[1].Variable |> should equal "I0.1"
            flow.Conditions.[1].Operator |> should equal ConditionOperator.Not
            flow.Actions.Length |> should equal 1
            flow.Actions.[0].Variable |> should equal "Q0.0"
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Parse Timer instruction should extract timer condition and action``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "10"
            Name = Some "Test Timer"
            Number = 1
            Content = "TON T1"
            RawContent = Some "TON T1"
            LogicType = LogicType.STL
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
            flow.Conditions.[0].Variable |> should equal "T1.Q"
            flow.Actions.Length |> should equal 1
            flow.Actions.[0].Variable |> should equal "T1"
            flow.Type |> should equal LogicFlowType.Timer
        | None -> failwith "Expected Some result"

    [<Fact>]
    let ``Detect logic flow type correctly for conditional logic``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "11"
            Name = Some "Conditional Logic"
            Number = 1
            Content = "IF #Value > 50 THEN\n  #Output := TRUE;\nEND_IF;"
            RawContent = Some "IF #Value > 50 THEN\n  #Output := TRUE;\nEND_IF;"
            LogicType = LogicType.SCL
            Type = Some LogicFlowType.Conditional
            Variables = []
            Comments = []
            LineNumber = Some 1
            Properties = Map.empty
            Comment = None
        }

        // Act
        let flowType = analyzer.DetectLogicFlowTypeAsync(rung) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        flowType |> should equal LogicFlowType.Conditional

    [<Fact>]
    let ``Extract IO variables from STL network``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rung = {
            Id = Some "12"
            Name = Some "IO Test"
            Number = 1
            Content = "A I0.0\nAN I0.1\n= Q0.0\nS Q0.1"
            RawContent = Some "A I0.0\nAN I0.1\n= Q0.0\nS Q0.1"
            LogicType = LogicType.STL
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
        inputs |> should contain "I0.0"
        inputs |> should contain "I0.1"
        outputs.Length |> should equal 2
        outputs |> should contain "Q0.0"
        outputs |> should contain "Q0.1"

    [<Fact>]
    let ``Analyze sequence with data blocks``() =
        // Arrange
        let analyzer = createAnalyzer()
        let rungs = [
            { Id = Some "13"; Name = Some "Network1"; Number = 1; Content = "A I0.0\n= DB1.DBX0.0"; RawContent = Some "A I0.0\n= DB1.DBX0.0"; LogicType = LogicType.STL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 1; Properties = Map.empty; Comment = None }
            { Id = Some "14"; Name = Some "Network2"; Number = 2; Content = "A DB1.DBX0.0\n= Q0.0"; RawContent = Some "A DB1.DBX0.0\n= Q0.0"; LogicType = LogicType.STL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 2; Properties = Map.empty; Comment = None }
            { Id = Some "15"; Name = Some "Network3"; Number = 3; Content = "A I0.1\n= Q0.1"; RawContent = Some "A I0.1\n= Q0.1"; LogicType = LogicType.STL; Type = Some LogicFlowType.Simple; Variables = []; Comments = []; LineNumber = Some 3; Properties = Map.empty; Comment = None }
        ]

        // Act
        let sequence = analyzer.AnalyzeSequenceAsync(rungs) |> Async.AwaitTask |> Async.RunSynchronously

        // Assert
        sequence.LogicFlows.Length |> should equal 3
        sequence.Statistics.TotalRungs |> should equal 3
        sequence.Statistics.TotalConditions |> should equal 3
        sequence.Statistics.TotalActions |> should equal 3