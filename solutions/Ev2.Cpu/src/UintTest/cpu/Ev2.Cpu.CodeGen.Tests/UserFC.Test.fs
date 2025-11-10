module Ev2.Cpu.Test.UserFC

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

// ═══════════════════════════════════════════════════════════════════════
// FC 생성 및 빌드 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``FCBuilder - 기본 FC 생성`` () =
    let builder = FCBuilder("TestFC")
    builder.AddInput("fc_input1", typeof<int>)
    builder.AddOutput("fc_output1", typeof<int>)

    let body = add (Terminal(DsTag.Int("fc_input1"))) (intExpr 10)
    builder.SetBody(body)

    match builder.Build() with
    | Ok fc ->
        fc.Name |> should equal "TestFC"
        fc.Inputs.Length |> should equal 1
        fc.Outputs.Length |> should equal 1
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - 여러 입력 파라미터`` () =
    let builder = FCBuilder("MultiParamFC")
    builder.AddInput("fc_a", typeof<int>)
    builder.AddInput("fc_b", typeof<int>)
    builder.AddOutput("fc_result", typeof<int>)

    let body = add (Terminal(DsTag.Int("fc_a"))) (Terminal(DsTag.Int("fc_b")))
    builder.SetBody(body)

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.Length |> should equal 2
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - 기본값이 있는 입력 파라미터`` () =
    let builder = FCBuilder("DefaultParamFC")
    builder.AddInput("fc_required", typeof<int>)
    builder.AddInputWithDefault("fc_optional", typeof<int>, box 42)
    builder.AddOutput("fc_result2", typeof<int>)

    let body = Terminal(DsTag.Int("fc_required"))
    builder.SetBody(body)

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.Length |> should equal 2
        fc.Inputs.[1].DefaultValue |> should equal (Some (box 42))
        fc.Inputs.[1].IsOptional |> should equal true
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - 설명 메타데이터 설정`` () =
    let builder = FCBuilder("DocumentedFC")
    builder.AddInput("fc_value", typeof<double>)
    builder.AddOutput("fc_result3", typeof<double>)
    builder.SetDescription("테스트용 FC입니다")

    let body = mul (Terminal(DsTag.Double("fc_value"))) (doubleExpr 2.0)
    builder.SetBody(body)

    match builder.Build() with
    | Ok fc ->
        fc.Metadata.Description |> should equal (Some "테스트용 FC입니다")
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``UserFC - ReturnType 속성 검증`` () =
    let builder = FCBuilder("ReturnTypeFC")
    builder.AddInput("fc_x", typeof<double>)
    builder.AddOutput("fc_y", typeof<double>)
    builder.SetBody(Terminal(DsTag.Double("fc_x")))

    match builder.Build() with
    | Ok fc ->
        fc.ReturnType |> should equal typeof<double>
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``UserFC.Validate - 출력 없는 FC는 검증 실패`` () =
    let fc = {
        Name = "NoOutputFC"
        Inputs = [{ Name = "fc_xvar"; DataType = typeof<int>; Direction = ParamDirection.Input; DefaultValue = None; Description = None; IsOptional = false }]
        Outputs = []
        Body = Terminal(DsTag.Int("fc_xvar"))
        Metadata = UserFCMetadata.Empty
    }

    match fc.Validate() with
    | Error msg ->
        msg.Contains("at least one output") |> should equal true
    | Ok () ->
        failwith "Expected validation to fail for FC without outputs"

// ═══════════════════════════════════════════════════════════════════════
// Validation and Boundary Value Tests (Phase 5)
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``FCBuilder - FC with very long name (500 chars)`` () =
    let longName = String.replicate 500 "C"
    let builder = FCBuilder(longName)
    builder.AddInput("fc_x2", typeof<int>)
    builder.AddOutput("fc_y2", typeof<int>)
    builder.SetBody(Terminal(DsTag.Int("fc_x2")))

    match builder.Build() with
    | Ok fc ->
        fc.Name |> should equal longName
        fc.Name.Length |> should equal 500
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with 100 input parameters`` () =
    let builder = FCBuilder("ManyParamFC")
    for i in 1..100 do
        builder.AddInput(sprintf "fc_param%d" i, typeof<int>)
    builder.AddOutput("fc_result4", typeof<int>)
    builder.SetBody(intExpr 0)

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.Length |> should equal 100
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with 50 optional parameters`` () =
    let builder = FCBuilder("OptionalParamFC")
    builder.AddInput("fc_required2", typeof<int>)
    for i in 1..50 do
        builder.AddInputWithDefault(sprintf "fc_opt%d" i, typeof<int>, box i)
    builder.AddOutput("fc_out4", typeof<int>)
    builder.SetBody(intExpr 0)

    match builder.Build() with
    | Ok fc ->
        let optionals = fc.Inputs |> List.filter (fun p -> p.IsOptional)
        optionals.Length |> should equal 50
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with Int32.MaxValue default`` () =
    let builder = FCBuilder("MaxDefaultFC")
    builder.AddInput("fc_a2", typeof<int>)
    builder.AddInputWithDefault("fc_max", typeof<int>, box System.Int32.MaxValue)
    builder.AddOutput("fc_result5", typeof<int>)
    builder.SetBody(Terminal(DsTag.Int("fc_max")))

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.[1].DefaultValue |> should equal (Some (box System.Int32.MaxValue))
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with Int32.MinValue default`` () =
    let builder = FCBuilder("MinDefaultFC")
    builder.AddInput("fc_b2", typeof<int>)
    builder.AddInputWithDefault("fc_min", typeof<int>, box System.Int32.MinValue)
    builder.AddOutput("fc_result6", typeof<int>)
    builder.SetBody(Terminal(DsTag.Int("fc_min")))

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.[1].DefaultValue |> should equal (Some (box System.Int32.MinValue))
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with very long description (1000 chars)`` () =
    let longDesc = String.replicate 1000 "D"
    let builder = FCBuilder("LongDescFC")
    builder.AddInput("fc_in4", typeof<int>)
    builder.AddOutput("fc_out5", typeof<int>)
    builder.SetDescription(longDesc)
    builder.SetBody(intExpr 0)

    match builder.Build() with
    | Ok fc ->
        fc.Metadata.Description |> should equal (Some longDesc)
        fc.Metadata.Description.Value.Length |> should equal 1000
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC with all data types`` () =
    let builder = FCBuilder("AllTypesFC")
    builder.AddInput("fc_bool2", typeof<bool>)
    builder.AddInput("fc_int2", typeof<int>)
    builder.AddInput("fc_double2", typeof<double>)
    builder.AddInput("fc_string2", typeof<string>)
    builder.AddOutput("fc_result7", typeof<bool>)
    builder.SetBody(boolExpr true)

    match builder.Build() with
    | Ok fc ->
        fc.Inputs.Length |> should equal 4
        fc.Inputs |> List.map (fun p -> p.DataType)
                  |> should equal [typeof<bool>; typeof<int>; typeof<double>; typeof<string>]
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC returning Double`` () =
    let builder = FCBuilder("DoubleFC")
    builder.AddInput("fc_val", typeof<double>)
    builder.AddOutput("fc_dbl", typeof<double>)
    builder.SetBody(mul (Terminal(DsTag.Double("fc_val"))) (doubleExpr 3.14))

    match builder.Build() with
    | Ok fc ->
        fc.ReturnType |> should equal typeof<double>
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC returning Bool`` () =
    let builder = FCBuilder("BoolFC")
    builder.AddInput("fc_flag", typeof<bool>)
    builder.AddOutput("fc_bool", typeof<bool>)
    builder.SetBody(Terminal(DsTag.Bool("fc_flag")))

    match builder.Build() with
    | Ok fc ->
        fc.ReturnType |> should equal typeof<bool>
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``FCBuilder - FC returning String`` () =
    let builder = FCBuilder("StringFC")
    builder.AddInput("fc_str", typeof<string>)
    builder.AddOutput("fc_string", typeof<string>)
    builder.SetBody(Terminal(DsTag.String("fc_str")))

    match builder.Build() with
    | Ok fc ->
        fc.ReturnType |> should equal typeof<string>
    | Error msg ->
        failwithf "FC build failed: %s" msg
