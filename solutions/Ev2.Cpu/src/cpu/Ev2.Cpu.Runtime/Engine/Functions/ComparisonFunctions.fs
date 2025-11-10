namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core.Common

// ─────────────────────────────────────────────────────────────────────
// Comparison Functions (Refactored with TypeHelpers)
// ─────────────────────────────────────────────────────────────────────
// 비교 연산: eq, lt, gt, le, ge, ne
// TypeHelpers.ComparisonOperators를 사용하여 타입 매칭 로직 단순화
// ─────────────────────────────────────────────────────────────────────

module Comparison =

    /// <summary>동등성 비교 (모든 타입 지원, float는 epsilon 사용)</summary>
    let eq (a: obj) (b: obj) =
        ComparisonOperators.equals a b

    /// <summary>Less than 비교 (수치, 문자열 지원)</summary>
    let lt (a: obj) (b: obj) =
        ComparisonOperators.lessThan a b

    /// <summary>Greater than 비교</summary>
    let inline gt a b  = lt b a

    /// <summary>Less than or equal 비교</summary>
    let inline le a b  = lt a b || eq a b

    /// <summary>Greater than or equal 비교</summary>
    let inline ge a b  = lt b a || eq a b

    /// <summary>Not equal 비교</summary>
    let inline ne a b  = not (eq a b)
