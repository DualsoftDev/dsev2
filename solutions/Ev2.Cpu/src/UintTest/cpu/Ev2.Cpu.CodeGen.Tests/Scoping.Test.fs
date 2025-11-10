module Ev2.Cpu.Test.Scoping

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

// ═══════════════════════════════════════════════════════════════════════
// ScopeManager 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``ScopeManager - FC 변수 스코핑`` () =
    let manager = ScopeManager()

    let scopedName = manager.ScopeFC("TempConverter", "celsius")
    scopedName |> should equal "FC_TempConverter_celsius"

[<Fact>]
let ``ScopeManager - FB 인스턴스 변수 스코핑`` () =
    let manager = ScopeManager()

    let scopedName = manager.ScopeFBInstance("Motor1", "speed")
    scopedName |> should equal "FB_Motor1_speed"

[<Fact>]
let ``ScopeManager - FB Static 변수 스코핑`` () =
    let manager = ScopeManager()

    let scopedName = manager.ScopeFBStatic("Counter1", "value")
    scopedName |> should equal "FB_Counter1_Static_value"

[<Fact>]
let ``ScopeManager - FB Temp 변수 스코핑`` () =
    let manager = ScopeManager()

    let scopedName = manager.ScopeFBTemp("Motor1", "tempResult")
    scopedName |> should equal "FB_Motor1_Temp_tempResult"

[<Fact>]
let ``ScopeManager - Unscope FC 변수`` () =
    let manager = ScopeManager()

    let scopedName = "FC_TempConverter_celsius"
    match manager.UnscopeName(scopedName) with
    | Some originalName ->
        originalName |> should equal "celsius"
    | None ->
        failwith "Failed to unscope FC variable"

[<Fact>]
let ``ScopeManager - Unscope FB Static 변수`` () =
    let manager = ScopeManager()

    let scopedName = "FB_Counter1_Static_value"
    match manager.UnscopeName(scopedName) with
    | Some originalName ->
        originalName |> should equal "value"
    | None ->
        failwith "Failed to unscope FB static variable"

[<Fact>]
let ``NamespaceManager - 네임스페이스와 이름 결합`` () =
    let fullName = NamespaceManager.makeFullName (Some "MyLib") "MotorControl"
    fullName |> should equal "MyLib.MotorControl"

[<Fact>]
let ``NamespaceManager - 네임스페이스 없으면 이름만`` () =
    let fullName = NamespaceManager.makeFullName None "MotorControl"
    fullName |> should equal "MotorControl"

[<Fact>]
let ``NamespaceManager - 네임스페이스 분리`` () =
    let (ns, name) = NamespaceManager.splitNamespace "MyLib.SubLib.MotorControl"

    ns |> should equal (Some "MyLib.SubLib")
    name |> should equal "MotorControl"

[<Fact>]
let ``NamespaceManager - 유효한 네임스페이스 검증`` () =
    match NamespaceManager.validateNamespace "MyLib.SubLib" with
    | Ok () -> ()
    | Error msg ->
        failwithf "Expected validation to succeed: %s" msg

[<Fact>]
let ``NamespaceManager - 빈 네임스페이스 검증 실패`` () =
    match NamespaceManager.validateNamespace "" with
    | Error msg ->
        msg.Contains("empty") |> should equal true
    | Ok () ->
        failwith "Expected validation to fail"

[<Fact>]
let ``ScopeManager - UserFC의 모든 변수에 스코프 적용`` () =
    let manager = ScopeManager()

    let builder = FCBuilder("MathFC")
    builder.AddInput("scope_a", typeof<int>)
    builder.AddInput("scope_b", typeof<int>)
    builder.AddOutput("scope_result", typeof<int>)
    builder.SetBody(add (Terminal(DsTag.Int("scope_a"))) (Terminal(DsTag.Int("scope_b"))))

    match builder.Build() with
    | Ok fc ->
        let scopedNames = manager.ScopeUserFC(fc)

        scopedNames.Length |> should equal 3 // 2 inputs + 1 output

        let scopedInputA = scopedNames |> List.find (fun sn -> sn.OriginalName = "scope_a")
        scopedInputA.ScopedName |> should equal "FC_MathFC_scope_a"

        let scopedOutput = scopedNames |> List.find (fun sn -> sn.OriginalName = "scope_result")
        scopedOutput.ScopedName |> should equal "FC_MathFC_scope_result"
    | Error msg ->
        failwithf "FC build failed: %s" msg

[<Fact>]
let ``ScopeManager - UserFB 인스턴스의 모든 변수에 스코프 적용`` () =
    let manager = ScopeManager()

    let builder = FBBuilder("MotorFB")
    builder.AddInput("scope_start", typeof<bool>)
    builder.AddOutput("scope_running", typeof<bool>)
    builder.AddStaticWithInit("scope_state", typeof<bool>, box false)

    let stmt = Assign(0, DsTag.Bool("scope_running"), Terminal(DsTag.Bool("scope_state")))
    builder.AddStatement(stmt)

    match builder.Build() with
    | Ok fb ->
        let instance = createFBInstance "Motor1" fb
        let scopedNames = manager.ScopeFBInstance(instance)

        // 입력 1개 + 출력 1개 + Static 1개 = 3개
        scopedNames.Length |> should equal 3

        let scopedStart = scopedNames |> List.find (fun sn -> sn.OriginalName = "scope_start")
        scopedStart.ScopedName |> should equal "FB_Motor1_scope_start"

        let scopedState = scopedNames |> List.find (fun sn -> sn.OriginalName = "scope_state")
        scopedState.ScopedName |> should equal "FB_Motor1_Static_scope_state"
    | Error msg ->
        failwithf "FB build failed: %s" msg
