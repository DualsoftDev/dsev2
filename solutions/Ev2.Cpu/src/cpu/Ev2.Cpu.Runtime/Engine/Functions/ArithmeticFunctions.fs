namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

// ─────────────────────────────────────────────────────────────────────
// Arithmetic Functions (Refactored with TypeHelpers)
// ─────────────────────────────────────────────────────────────────────
// 산술 연산: add, sub, mul, divide, modulo, power
// TypeHelpers.BinaryOperators를 사용하여 타입 매칭 로직 단순화
// ─────────────────────────────────────────────────────────────────────

module Arithmetic =

    /// <summary>덧셈 (수치, 문자열 지원)</summary>
    let add (a: obj) (b: obj) =
        match BinaryTypeMatcher.analyze a b with
        | BothString (s1, s2) -> box (s1 + s2)
        | _ ->
            match BinaryOperators.applyNumericBoxed (fun i1 i2 -> box (i1 + i2)) (fun d1 d2 -> box (d1 + d2)) a b with
            | Some result -> result
            | None -> raise (ArgumentException($"Cannot add {a} and {b}"))

    /// <summary>뺄셈</summary>
    let sub (a: obj) (b: obj) =
        match BinaryOperators.applyNumericBoxed (fun i1 i2 -> box (i1 - i2)) (fun d1 d2 -> box (d1 - d2)) a b with
        | Some result -> result
        | None -> raise (ArgumentException($"Cannot subtract {b} from {a}"))

    /// <summary>곱셈</summary>
    let mul (a: obj) (b: obj) =
        match BinaryOperators.applyNumericBoxed (fun i1 i2 -> box (i1 * i2)) (fun d1 d2 -> box (d1 * d2)) a b with
        | Some result -> result
        | None -> raise (ArgumentException($"Cannot multiply {a} and {b}"))

    /// <summary>나눗셈 (0으로 나누기 검사 포함)</summary>
    let divide (a: obj) (b: obj) =
        // Zero check
        match UnaryTypeMatcher.analyze b with
        | MatchInt 0 ->
            match UnaryTypeMatcher.analyze a with
            | MatchInt numerator -> RuntimeExceptions.raiseIntDivisionByZeroWith numerator
            | _ -> RuntimeExceptions.raiseIntDivisionByZero()
        | MatchDouble d when Math.Abs d <= Constants.Epsilon ->
            match UnaryTypeMatcher.analyze a with
            | MatchDouble numerator -> RuntimeExceptions.raiseFloatDivisionByZeroWithEpsilon numerator Constants.Epsilon
            | MatchInt numerator -> RuntimeExceptions.raiseFloatDivisionByZeroWith (float numerator)
            | _ -> RuntimeExceptions.raiseFloatDivisionByZero()
        | _ -> ()

        match BinaryOperators.applyNumericAsDouble (fun d1 d2 -> box (d1 / d2)) a b with
        | Some result -> result
        | None -> raise (ArgumentException($"Cannot divide {a} by {b}"))

    /// <summary>나머지 연산 (정수만 지원)</summary>
    let modulo (a: obj) (b: obj) =
        match BinaryTypeMatcher.analyze a b with
        | BothInt (i1, i2) when i2 <> 0 -> box (i1 % i2)
        | BothInt (i1, _) -> RuntimeExceptions.raiseModuloByZeroWith i1
        | _ -> raise (ArgumentException("Modulo requires two integers"))

    /// <summary>거듭제곱</summary>
    let power (a: obj) (b: obj) =
        match BinaryOperators.applyNumericAsDouble (fun d1 d2 -> box (Math.Pow(d1, d2))) a b with
        | Some result -> result
        | None -> raise (ArgumentException($"Cannot calculate power of {a} ^ {b}"))
