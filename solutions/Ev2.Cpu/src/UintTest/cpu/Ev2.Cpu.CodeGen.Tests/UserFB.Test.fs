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
    builder.AddInput("fb_enable", DsDataType.TBool)
    builder.AddOutput("fb_output", DsDataType.TBool)

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
    builder.AddInput("fb_trigger", DsDataType.TBool)
    builder.AddOutput("fb_count", DsDataType.TInt)
    builder.AddStatic("fb_counter", DsDataType.TInt)

    let stmt = Assign(0, DsTag.Int("fb_count"), Terminal(DsTag.Int("fb_counter")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        fb.Statics.Length |> should equal 1
        let (staticName, staticType, initVal) = fb.Statics.[0]
        staticName |> should equal "fb_counter"
        staticType |> should equal DsDataType.TInt
        initVal |> should equal None
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - Static 변수 초기값`` () =
    let builder = FBBuilder("InitStaticFB")
    builder.AddInput("fb_reset", DsDataType.TBool)
    builder.AddOutput("fb_value", DsDataType.TInt)
    builder.AddStaticWithInit("fb_initial", DsDataType.TInt, box 100)

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
    builder.AddInput("fb_a", DsDataType.TInt)
    builder.AddOutput("fb_result", DsDataType.TInt)
    builder.AddTemp("fb_intermediate", DsDataType.TInt)

    let stmt = Assign(0, DsTag.Int("fb_result"), Terminal(DsTag.Int("fb_a")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        fb.Temps.Length |> should equal 1
        let (tempName, tempType) = fb.Temps.[0]
        tempName |> should equal "fb_intermediate"
        tempType |> should equal DsDataType.TInt
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``createFBInstance - FB 인스턴스 생성`` () =
    let builder = FBBuilder("CounterFB")
    builder.AddInput("fb_enable2", DsDataType.TBool)
    builder.AddOutput("fb_count2", DsDataType.TInt)
    builder.AddStatic("fb_counter2", DsDataType.TInt)

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
        Inputs = [{ Name = "fb_input"; DataType = DsDataType.TBool; Direction = ParamDirection.Input; DefaultValue = None; Description = None; IsOptional = false }]
        Outputs = [{ Name = "fb_output2"; DataType = DsDataType.TBool; Direction = ParamDirection.Output; DefaultValue = None; Description = None; IsOptional = false }]
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
    builder.AddInput("fb_in", DsDataType.TBool)
    builder.AddOutput("fb_out", DsDataType.TBool)
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
        builder.AddInput(sprintf "fb_input%d" i, DsDataType.TInt)
    builder.AddOutput("fb_sum", DsDataType.TInt)
    builder.AddStatement(Assign(0, DsTag.Int("fb_sum"), intExpr 0))

    match builder.Build() with
    | Ok fb ->
        fb.Inputs.Length |> should equal 100
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 100 outputs`` () =
    let builder = FBBuilder("ManyOutputFB")
    builder.AddInput("fb_trigger", DsDataType.TBool)
    for i in 1..100 do
        builder.AddOutput(sprintf "fb_output%d" i, DsDataType.TInt)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_trigger"), boolExpr false))

    match builder.Build() with
    | Ok fb ->
        fb.Outputs.Length |> should equal 100
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 50 static variables`` () =
    let builder = FBBuilder("ManyStaticFB")
    builder.AddInput("fb_reset", DsDataType.TBool)
    builder.AddOutput("fb_status", DsDataType.TBool)
    for i in 1..50 do
        builder.AddStatic(sprintf "fb_static%d" i, DsDataType.TInt)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_status"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Statics.Length |> should equal 50
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 50 temp variables`` () =
    let builder = FBBuilder("ManyTempFB")
    builder.AddInput("fb_data", DsDataType.TInt)
    builder.AddOutput("fb_result4", DsDataType.TInt)
    for i in 1..50 do
        builder.AddTemp(sprintf "fb_temp%d" i, DsDataType.TInt)
    builder.AddStatement(Assign(0, DsTag.Int("fb_result4"), intExpr 0))

    match builder.Build() with
    | Ok fb ->
        fb.Temps.Length |> should equal 50
    | Error msg ->
        failwithf "FB build failed: %s" msg

[<Fact>]
let ``FBBuilder - FB with 1000 statements`` () =
    let builder = FBBuilder("ManyStmtFB")
    builder.AddInput("fb_enable3", DsDataType.TBool)
    builder.AddOutput("fb_done", DsDataType.TBool)
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
    builder.AddInput("fb_in2", DsDataType.TBool)
    builder.AddOutput("fb_out2", DsDataType.TInt)
    builder.AddStaticWithInit("fb_max", DsDataType.TInt, box System.Int32.MaxValue)
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
    builder.AddInput("fb_in3", DsDataType.TBool)
    builder.AddOutput("fb_out3", DsDataType.TInt)
    builder.AddStaticWithInit("fb_min", DsDataType.TInt, box System.Int32.MinValue)
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
    builder.AddInput("fb_bool", DsDataType.TBool)
    builder.AddInput("fb_int", DsDataType.TInt)
    builder.AddInput("fb_double", DsDataType.TDouble)
    builder.AddInput("fb_string", DsDataType.TString)
    builder.AddOutput("fb_status2", DsDataType.TBool)
    builder.AddStatement(Assign(0, DsTag.Bool("fb_status2"), boolExpr true))

    match builder.Build() with
    | Ok fb ->
        fb.Inputs.Length |> should equal 4
        fb.Inputs |> List.map (fun p -> p.DataType)
                  |> should equal [DsDataType.TBool; DsDataType.TInt; DsDataType.TDouble; DsDataType.TString]
    | Error msg ->
        failwithf "FB build failed: %s" msg
