namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS.IR

type LogicalOperatorTest() =
    [<Test>]
    member _.``LogicalAnd_truth_table``() =
        logicalAnd (literal true ) (literal true ) |> _.TValue === true
        logicalAnd (literal true ) (literal false) |> _.TValue === false
        logicalAnd (literal false) (literal true ) |> _.TValue === false
        logicalAnd (literal false) (literal false) |> _.TValue === false

    [<Test>]
    member _.``LogicalOr_truth_table``() =
        logicalOr (literal true ) (literal true )  |> _.TValue === true
        logicalOr (literal true ) (literal false)  |> _.TValue === true
        logicalOr (literal false) (literal true )  |> _.TValue === true
        logicalOr (literal false) (literal false)  |> _.TValue === false

    [<Test>]
    member _.``LogicalNot_basic``() =
        logicalNot (literal true)  |> _.TValue === false
        logicalNot (literal false) |> _.TValue === true

    [<Test>]
    member _.``Logical_composition``() =
        // (true && false) |> not |> (|| false) = true
        let andExpr = logicalAnd (literal true) (literal false)
        let notExpr = logicalNot andExpr
        logicalOr notExpr (literal false) |> _.TValue === true

        // 단일 식으로 평가
        logicalOr
            (logicalNot (
                logicalAnd (literal true) (literal false)))
            (literal false)
        |> _.TValue === true
