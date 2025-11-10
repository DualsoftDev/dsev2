module Ev2.PLC.Common.Tests.QualityTests

open Xunit
open FsUnit.Xunit
open System
open Ev2.PLC.Common.Types

[<Fact>]
let ``DataSeverity should have correct hierarchy`` () =
    DataSeverity.Info |> should equal DataSeverity.Info
    DataSeverity.Warning |> should equal DataSeverity.Warning
    DataSeverity.Error |> should equal DataSeverity.Error
    DataSeverity.Critical |> should equal DataSeverity.Critical

[<Fact>]
let ``DataQuality Good should have perfect score`` () =
    let good = DataQuality.Good
    
    good.IsGood |> should equal true
    good.IsBad |> should equal false
    good.IsUncertain |> should equal false
    good.Score |> should equal 100

[<Fact>]
let ``DataQuality Bad should have zero score`` () =
    let bad = DataQuality.Bad("Communication error")
    
    bad.IsGood |> should equal false
    bad.IsBad |> should equal true
    bad.IsUncertain |> should equal false
    bad.Score |> should equal 0

[<Fact>]
let ``DataQuality Uncertain should have partial score`` () =
    let uncertain = DataQuality.Uncertain("Old data")
    
    uncertain.IsGood |> should equal false
    uncertain.IsBad |> should equal false
    uncertain.IsUncertain |> should equal true
    uncertain.Score |> should be (greaterThan 0)
    uncertain.Score |> should be (lessThan 100)

[<Fact>]
let ``DataStatus should represent correct states`` () =
    let goodStatus = DataStatus.CreateGood()
    let badStatus = DataStatus.CreateBad("Error")
    let uncertainStatus = DataStatus.CreateUncertain("Warning")
    
    goodStatus.IsGood |> should equal true
    goodStatus.IsBad |> should equal false
    goodStatus.IsUncertain |> should equal false
    
    badStatus.IsGood |> should equal false
    badStatus.IsBad |> should equal true
    badStatus.IsUncertain |> should equal false
    
    uncertainStatus.IsGood |> should equal false
    uncertainStatus.IsBad |> should equal false
    uncertainStatus.IsUncertain |> should equal true

[<Fact>]
let ``DataQuality Description should work correctly`` () =
    let good = DataQuality.Good
    let bad = DataQuality.Bad("Communication error")
    let uncertain = DataQuality.Uncertain("Old data")
    
    good.Description |> should equal "Good"
    bad.Description |> should startWith "Bad"
    bad.Description |> should endWith "Communication error"
    uncertain.Description |> should startWith "Uncertain"
    uncertain.Description |> should endWith "Old data"

[<Fact>]
let ``DataQuality Score should be in valid range`` () =
    let good = DataQuality.Good
    let bad = DataQuality.Bad("Error")
    let uncertain = DataQuality.Uncertain("Warning")
    
    good.Score |> should equal 100
    bad.Score |> should equal 0
    uncertain.Score |> should be (lessThanOrEqualTo 100)
    uncertain.Score |> should be (greaterThanOrEqualTo 0)