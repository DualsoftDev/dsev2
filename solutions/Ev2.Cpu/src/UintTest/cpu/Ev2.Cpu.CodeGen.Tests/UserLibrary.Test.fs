module Ev2.Cpu.Test.UserLibrary

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

// ═══════════════════════════════════════════════════════════════════════
// UserFBRegistry 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``UserFBRegistry - FC 등록 및 조회`` () =
    let registry = UserFBRegistry()

    let builder = FCBuilder("TestFC")
    builder.AddInput("x", typeof<int>)
    builder.AddOutput("y", typeof<int>)
    builder.SetBody(add (Terminal(DsTag.Int("x"))) (intExpr 1))

    match builder.Build() with
    | Ok fc ->
        match registry.RegisterFC(fc) with
        | Ok () ->
            match registry.GetFC("TestFC") with
            | Some foundFC ->
                foundFC.Name |> should equal "TestFC"
            | None ->
                failwith "FC not found in registry"
        | Error msg ->
            failwithf "FC registration failed: %s" msg
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``UserFBRegistry - FB 등록 및 조회`` () =
    let registry = UserFBRegistry()

    let builder = FBBuilder("TestFB")
    builder.AddInput("enable", typeof<bool>)
    builder.AddOutput("output", typeof<bool>)

    let stmt = Assign(0, DsTag.Bool("output"), Terminal(DsTag.Bool("enable")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        match registry.RegisterFB(fb) with
        | Ok () ->
            match registry.GetFB("TestFB") with
            | Some foundFB ->
                foundFB.Name |> should equal "TestFB"
            | None ->
                failwith "FB not found in registry"
        | Error msg ->
            failwithf "FB registration failed: %s" msg
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``UserFBRegistry - 인스턴스 등록 및 조회`` () =
    let registry = UserFBRegistry()

    let builder = FBBuilder("CounterFB")
    builder.AddInput("enable", typeof<bool>)
    builder.AddOutput("count", typeof<int>)

    let stmt = Assign(0, DsTag.Int("count"), intExpr 0)
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        match registry.RegisterFB(fb) with
        | Ok () ->
            let instance = createFBInstance "Counter1" fb
            match registry.RegisterInstance(instance) with
            | Ok () ->
                match registry.TryFindInstance("Counter1") with
                | Some foundInst ->
                    foundInst.Name |> should equal "Counter1"
                    foundInst.FBType.Name |> should equal "CounterFB"
                | None ->
                    failwith "Instance not found in registry"
            | Error msg ->
                failwithf "Instance registration failed: %s" msg
        | Error msg ->
            failwithf "FB registration failed: %s" msg
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``UserFBRegistry - GetAllFCs 모든 FC 조회`` () =
    let registry = UserFBRegistry()

    let fc1 = FCBuilder("FC1")
    fc1.AddInput("x", typeof<int>)
    fc1.AddOutput("y", typeof<int>)
    fc1.SetBody(intExpr 1)

    let fc2 = FCBuilder("FC2")
    fc2.AddInput("a", typeof<bool>)
    fc2.AddOutput("b", typeof<bool>)
    fc2.SetBody(boolExpr true)

    match fc1.Build(), fc2.Build() with
    | Ok fcObj1, Ok fcObj2 ->
        let _ = registry.RegisterFC(fcObj1)
        let _ = registry.RegisterFC(fcObj2)

        let allFCs = registry.GetAllFCs()
        allFCs.Length |> should equal 2
        allFCs |> List.map (fun fc -> fc.Name) |> should contain "FC1"
        allFCs |> List.map (fun fc -> fc.Name) |> should contain "FC2"
    | _ ->
        failwith "FC build failed"

[<Fact>]
let ``UserFBRegistry - GetAllFBs 모든 FB 조회`` () =
    let registry = UserFBRegistry()

    let fb1 = FBBuilder("FB1")
    fb1.AddInput("input", typeof<bool>)
    fb1.AddOutput("output", typeof<bool>)
    fb1.AddStatement(Assign(0, DsTag.Bool("output"), boolExpr true))

    let fb2 = FBBuilder("FB2")
    fb2.AddInput("enable", typeof<bool>)
    fb2.AddOutput("done", typeof<bool>)
    fb2.AddStatement(Assign(0, DsTag.Bool("done"), boolExpr false))

    match fb1.Build(), fb2.Build() with
    | Ok fbObj1, Ok fbObj2 ->
        let _ = registry.RegisterFB(fbObj1)
        let _ = registry.RegisterFB(fbObj2)

        let allFBs = registry.GetAllFBs()
        allFBs.Length |> should equal 2
        allFBs |> List.map (fun fb -> fb.Name) |> should contain "FB1"
        allFBs |> List.map (fun fb -> fb.Name) |> should contain "FB2"
    | _ ->
        failwith "FB build failed"

[<Fact>]
let ``UserFBRegistry - ValidateAll 모든 FC/FB 검증`` () =
    let registry = UserFBRegistry()

    let fc = FCBuilder("ValidFC")
    fc.AddInput("x", typeof<int>)
    fc.AddOutput("y", typeof<int>)
    fc.SetBody(intExpr 1)

    let fb = FBBuilder("ValidFB")
    fb.AddInput("enable", typeof<bool>)
    fb.AddOutput("output", typeof<bool>)
    fb.AddStatement(Assign(0, DsTag.Bool("output"), boolExpr true))

    match fc.Build(), fb.Build() with
    | Ok fcObj, Ok fbObj ->
        let _ = registry.RegisterFC(fcObj)
        let _ = registry.RegisterFB(fbObj)

        match registry.ValidateAll() with
        | Ok () -> () // 성공
        | Error errors ->
            failwithf "Validation failed: %A" errors
    | _ ->
        failwith "Build failed"
