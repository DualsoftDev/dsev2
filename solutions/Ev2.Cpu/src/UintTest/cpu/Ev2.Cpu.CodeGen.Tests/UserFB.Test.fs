module Ev2.Cpu.Test.UserFB

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

// ═══════════════════════════════════════════════════════════════════════
// FB 생성 및 빌드 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``FBBuilder - 기본 FB 생성`` () =
    let builder = FBBuilder("TestFB")
    builder.AddInput("fb_enable", typeof<bool>)
    builder.AddOutput("fb_output", typeof<bool>)

    let stmt = Assign(0, DsTag.Bool("fb_output"), Terminal(DsTag.Bool("fb_enable")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        fb.Name |> should equal "TestFB"
        fb.Inputs.Length |> should equal 1
        fb.Outputs.Length |> should equal 1
        fb.Body.Length |> should equal 1
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Static 변수 추가`` () =
    let builder = FBBuilder("StatefulFB")
    builder.AddInput("fb_trigger", typeof<bool>)
    builder.AddOutput("fb_count", typeof<int>)
    builder.AddStatic("fb_counter", typeof<int>)

    let stmt = Assign(0, DsTag.Int("fb_count"), Terminal(DsTag.Int("fb_counter")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        fb.Statics.Length |> should equal 1
        let (staticName, staticType, initVal) = fb.Statics.[0]
        staticName |> should equal "fb_counter"
        staticType |> should equal typeof<int>
        initVal |> should equal None
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Static 변수 초기값`` () =
    let builder = FBBuilder("InitStaticFB")
    builder.AddInput("fb_reset", typeof<bool>)
    builder.AddOutput("fb_value", typeof<int>)
    builder.AddStaticWithInit("fb_initial", typeof<int>, box 100)

    let stmt = Assign(0, DsTag.Int("fb_value"), Terminal(DsTag.Int("fb_initial")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        let (_, _, initVal) = fb.Statics.[0]
        initVal |> should equal (Some (box 100))
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Temp 변수 추가`` () =
    let builder = FBBuilder("TempFB")
    builder.AddInput("fb_a", typeof<int>)
    builder.AddOutput("fb_result", typeof<int>)
    builder.AddTemp("fb_intermediate", typeof<int>)

    let stmt = Assign(0, DsTag.Int("fb_result"), Terminal(DsTag.Int("fb_a")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        fb.Temps.Length |> should equal 1
        let (tempName, tempType) = fb.Temps.[0]
        tempName |> should equal "fb_intermediate"
        tempType |> should equal typeof<int>
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``createFBInstance - FB 인스턴스 생성`` () =
    let builder = FBBuilder("CounterFB")
    builder.AddInput("fb_enable2", typeof<bool>)
    builder.AddOutput("fb_count2", typeof<int>)
    builder.AddStatic("fb_counter2", typeof<int>)

    let stmt = Assign(0, DsTag.Int("fb_count2"), intExpr 0)
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        let instance = createFBInstance "Counter1" fb
        instance.Name |> should equal "Counter1"
        instance.FBType.Name |> should equal "CounterFB"
        instance.IsInitialized |> should equal false
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``UserFB.Validate - 본문 없는 FB는 검증 실패`` () =
    let fb = {
        Name = "EmptyFB"
        Inputs = [{ Name = "fb_input"; DataType = typeof<bool>; Direction = ParamDirection.Input; DefaultValue = None; Description = None; IsOptional = false }]
        Outputs = [{ Name = "fb_output2"; DataType = typeof<bool>; Direction = ParamDirection.Output; DefaultValue = None; Description = None; IsOptional = false }]
        InOuts = []
        Statics = []
        Temps = []
        Body = []
        Metadata = UserFCMetadata.Empty
    }

    match fb.Validate() with
    | Error msg ->
        msg.Contains("at least one statement") |> should equal true
    | Ok () ->
        failwith "Expected validation to fail for FB without body"

// ═══════════════════════════════════════════════════════════════════════
// Validation and Boundary Value Tests (Phase 5)
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``FBBuilder - FB with very long name (500 chars)`` () =
    let longName = String.replicate 500 "F"
    let builder = FBBuilder(longName)
    builder.AddInput("fb_in", typeof<bool>)
    builder.AddOutput("fb_out", typeof<bool>)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_out"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Name |> should equal longName
        fb.Name.Length |> should equal 500
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 100 inputs`` () =
    let builder = FBBuilder("ManyInputFB")
    for i in 1..100 do
        builder.AddInput(sprintf "fb_input%d" i, typeof<int>)
    builder.AddOutput("fb_sum", typeof<int>)
    builder.AddStatement(Assign(0, DsTag.Int("fb_sum"), intExpr 0))

    match builder.Build() with
    | Ok fb ->
        fb.Inputs.Length |> should equal 100
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 100 outputs`` () =
    let builder = FBBuilder("ManyOutputFB")
    builder.AddInput("fb_trigger", typeof<bool>)
    for i in 1..100 do
        builder.AddOutput(sprintf "fb_output%d" i, typeof<int>)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_trigger"), boolExpr false))

    match builder.Build() with
    | Ok fb ->
        fb.Outputs.Length |> should equal 100
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 50 static variables`` () =
    let builder = FBBuilder("ManyStaticFB")
    builder.AddInput("fb_reset", typeof<bool>)
    builder.AddOutput("fb_status", typeof<bool>)
    for i in 1..50 do
        builder.AddStatic(sprintf "fb_static%d" i, typeof<int>)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_status"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Statics.Length |> should equal 50
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 50 temp variables`` () =
    let builder = FBBuilder("ManyTempFB")
    builder.AddInput("fb_data", typeof<int>)
    builder.AddOutput("fb_result4", typeof<int>)
    for i in 1..50 do
        builder.AddTemp(sprintf "fb_temp%d" i, typeof<int>)
    builder.AddStatement(Assign(0, DsTag.Int("fb_result4"), intExpr 0))

    match builder.Build() with
    | Ok fb ->
        fb.Temps.Length |> should equal 50
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 1000 statements`` () =
    let builder = FBBuilder("ManyStmtFB")
    builder.AddInput("fb_enable3", typeof<bool>)
    builder.AddOutput("fb_done", typeof<bool>)
    for i in 1..1000 do
        builder.AddStatement(Assign(0, DsTag.Bool("fb_done"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Body.Length |> should equal 1000
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Static with Int32.MaxValue initial value`` () =
    let builder = FBBuilder("MaxValueFB")
    builder.AddInput("fb_in2", typeof<bool>)
    builder.AddOutput("fb_out2", typeof<int>)
    builder.AddStaticWithInit("fb_max", typeof<int>, box System.Int32.MaxValue)
    builder.AddStatement(Assign(0, DsTag.Int("fb_out2"), Terminal(DsTag.Int("fb_max"))))

    match builder.Build() with
    | Ok fb ->
        let (_, _, initVal) = fb.Statics.[0]
        initVal |> should equal (Some (box System.Int32.MaxValue))
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Static with Int32.MinValue initial value`` () =
    let builder = FBBuilder("MinValueFB")
    builder.AddInput("fb_in3", typeof<bool>)
    builder.AddOutput("fb_out3", typeof<int>)
    builder.AddStaticWithInit("fb_min", typeof<int>, box System.Int32.MinValue)
    builder.AddStatement(Assign(0, DsTag.Int("fb_out3"), Terminal(DsTag.Int("fb_min"))))

    match builder.Build() with
    | Ok fb ->
        let (_, _, initVal) = fb.Statics.[0]
        initVal |> should equal (Some (box System.Int32.MinValue))
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - All data types as inputs`` () =
    let builder = FBBuilder("AllTypesFB")
    builder.AddInput("fb_bool", typeof<bool>)
    builder.AddInput("fb_int", typeof<int>)
    builder.AddInput("fb_double", typeof<double>)
    builder.AddInput("fb_string", typeof<string>)
    builder.AddOutput("fb_status2", typeof<bool>)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_status2"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Inputs.Length |> should equal 4
        fb.Inputs |> List.map (fun p -> p.DataType)
                  |> should equal [typeof<bool>; typeof<int>; typeof<double>; typeof<string>]
    | Error msg ->
        failwithf "FB build failed: %s" msg
