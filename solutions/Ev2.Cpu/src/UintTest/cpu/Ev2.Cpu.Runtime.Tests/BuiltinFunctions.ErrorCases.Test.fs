module Ev2.Cpu.Runtime.Test.BuiltinFunctionErrors

open Xunit
open Ev2.Cpu.Runtime
open Ev2.Cpu.Core.Common

/// <summary>
/// 내장 함수 에러 케이스 테스트
/// - 잘못된 인자 개수
/// - 잘못된 타입
/// - 경계값 테스트
/// </summary>

// ═══════════════════════════════════════════════════════════════════════
// 인자 개수 검증 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``ADD - 인자 없으면 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "ADD" [] None |> ignore)

[<Fact>]
let ``ABS - 인자 2개면 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "ABS" [box 10; box 20] None |> ignore)

[<Fact>]
let ``DIV - 인자 1개면 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "DIV" [box 10] None |> ignore)

[<Fact>]
let ``CLAMP - 인자 2개면 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "CLAMP" [box 50; box 0] None |> ignore)

[<Fact>]
let ``IF - 인자 2개면 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "IF" [box true; box 100] None |> ignore)

// ═══════════════════════════════════════════════════════════════════════
// 알 수 없는 함수 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``알 수 없는 함수 호출 시 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "UNKNOWN_FUNCTION" [box 10] None |> ignore)

[<Fact>]
let ``빈 함수 이름 호출 시 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "" [box 10] None |> ignore)

// ═══════════════════════════════════════════════════════════════════════
// 경계값 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``DIV - 0으로 나누기`` () =
    // Division by zero는 런타임에서 IntegerDivisionByZeroException 발생
    Assert.Throws<IntegerDivisionByZeroException>(fun () ->
        BuiltinFunctions.call "DIV" [box 10; box 0] None |> ignore)

[<Fact>]
let ``SQRT - 음수 루트`` () =
    // 음수의 제곱근은 NaN 반환 (에러는 아님)
    let result = BuiltinFunctions.call "SQRT" [box -4.0] None
    let value = unbox<float> result
    Assert.True(System.Double.IsNaN(value))

[<Fact>]
let ``MAX - 빈 리스트 방지 (최소 1개)`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "MAX" [] None |> ignore)

[<Fact>]
let ``MIN - 빈 리스트 방지 (최소 1개)`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "MIN" [] None |> ignore)

// ═══════════════════════════════════════════════════════════════════════
// Range 함수 경계 테스트
// ═══════════════════════════════════════════════════════════════════════

[<Fact>]
let ``ROUND - 0개 인자 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "ROUND" [] None |> ignore)

[<Fact>]
let ``ROUND - 3개 인자 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "ROUND" [box 3.14; box 2; box 0] None |> ignore)

[<Fact>]
let ``SUBSTR - 1개 인자 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "SUBSTR" [box "hello"] None |> ignore)

[<Fact>]
let ``SUBSTR - 4개 인자 실패`` () =
    Assert.Throws<System.Exception>(fun () ->
        BuiltinFunctions.call "SUBSTR" [box "hello"; box 0; box 2; box "extra"] None |> ignore)
