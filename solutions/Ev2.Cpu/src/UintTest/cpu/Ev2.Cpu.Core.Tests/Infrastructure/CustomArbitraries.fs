namespace Ev2.Cpu.Tests.Infrastructure

open System
open FsCheck
open Ev2.Cpu.Core

// ═══════════════════════════════════════════════════════════════════════
// Custom Arbitraries Module - FsCheck 커스텀 생성기
// ═══════════════════════════════════════════════════════════════════════
// Phase 1: 기반 인프라
// 속성 기반 테스트를 위한 도메인 특화 랜덤 값 생성기
// ═══════════════════════════════════════════════════════════════════════

module CustomArbitraries =

    // ───────────────────────────────────────────────────────────────────
    // Basic Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for safe integers (avoid overflow)</summary>
    let safeIntGen =
        Gen.choose (-1000000, 1000000)

    /// <summary>Generator for small positive integers</summary>
    let smallPositiveIntGen =
        Gen.choose (1, 1000)

    /// <summary>Generator for safe doubles (avoid infinity, NaN)</summary>
    let safeDoubleGen =
        Gen.choose (-10000000, 10000000)
        |> Gen.map (fun x -> float x / 1000.0)

    /// <summary>Generator for valid variable names</summary>
    let validNameGen =
        gen {
            let! firstChar = Gen.elements (['a'..'z'] @ ['A'..'Z'] @ ['_'])
            let! length = Gen.choose(0, 20)
            let! restChars =
                Gen.listOfLength length
                    (Gen.elements (['a'..'z'] @ ['A'..'Z'] @ ['0'..'9'] @ ['_']))
            let name = String(Array.ofList (firstChar::restChars))
            return name
        }

    /// <summary>Generator for non-empty strings</summary>
    let nonEmptyStringGen =
        Arb.generate<string>
        |> Gen.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))

    // ───────────────────────────────────────────────────────────────────
    // Type Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for Type</summary>
    let typeGen =
        Gen.elements [
            typeof<int>
            typeof<double>
            typeof<bool>
            typeof<string>
        ]

    /// <summary>Arbitrary for Type</summary>
    type ArbitraryType =
        static member Type() =
            Arb.fromGen typeGen

    /// <summary>Generator for values matching a Type</summary>
    let valueForTypeGen (dtype: Type) =
        if dtype = typeof<int> then safeIntGen |> Gen.map box
        elif dtype = typeof<double> then safeDoubleGen |> Gen.map box
        elif dtype = typeof<bool> then Arb.generate<bool> |> Gen.map box
        elif dtype = typeof<string> then nonEmptyStringGen |> Gen.map box
        else Gen.constant (box null)

    // ───────────────────────────────────────────────────────────────────
    // DsTag Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for DsTag of any type</summary>
    let dsTagGen =
        gen {
            let! name = validNameGen
            let! dtype = typeGen
            return DsTag.Create(name, dtype)
        }

    /// <summary>Generator for DsTag.Int</summary>
    let intTagGen =
        validNameGen |> Gen.map DsTag.Int

    /// <summary>Generator for DsTag.Double</summary>
    let doubleTagGen =
        validNameGen |> Gen.map DsTag.Double

    /// <summary>Generator for DsTag.Bool</summary>
    let boolTagGen =
        validNameGen |> Gen.map DsTag.Bool

    /// <summary>Generator for DsTag.String</summary>
    let stringTagGen =
        validNameGen |> Gen.map DsTag.String

    /// <summary>Arbitrary for DsTag</summary>
    type ArbitraryDsTag =
        static member DsTag() =
            Arb.fromGen dsTagGen

    // ───────────────────────────────────────────────────────────────────
    // DsExpr Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for simple DsExpr (no nesting)</summary>
    let simpleExprGen =
        Gen.oneof [
            // Constant expressions
            safeIntGen |> Gen.map (fun i -> DsExpr.Const(box i, typeof<int>))
            safeDoubleGen |> Gen.map (fun d -> DsExpr.Const(box d, typeof<double>))
            Arb.generate<bool> |> Gen.map (fun b -> DsExpr.Const(box b, typeof<bool>))
            nonEmptyStringGen |> Gen.map (fun s -> DsExpr.Const(box s, typeof<string>))

            // Terminal expressions
            intTagGen |> Gen.map DsExpr.Terminal
            doubleTagGen |> Gen.map DsExpr.Terminal
            boolTagGen |> Gen.map DsExpr.Terminal
            stringTagGen |> Gen.map DsExpr.Terminal
        ]

    /// <summary>Generator for DsExpr with limited depth to avoid stack overflow</summary>
    let rec exprGenWithDepth maxDepth =
        if maxDepth <= 0 then
            simpleExprGen
        else
            let subExprGen = exprGenWithDepth (maxDepth - 1)
            Gen.oneof [
                simpleExprGen

                // Binary operations
                Gen.map2 (fun e1 e2 -> DsExpr.Function("ADD", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("SUB", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("MUL", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("DIV", [e1; e2])) subExprGen subExprGen

                // Comparison
                Gen.map2 (fun e1 e2 -> DsExpr.Function("EQ", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("LT", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("GT", [e1; e2])) subExprGen subExprGen

                // Logical
                Gen.map2 (fun e1 e2 -> DsExpr.Function("AND", [e1; e2])) subExprGen subExprGen
                Gen.map2 (fun e1 e2 -> DsExpr.Function("OR", [e1; e2])) subExprGen subExprGen
                Gen.map (fun e -> DsExpr.Function("NOT", [e])) subExprGen

                // Unary
                Gen.map (fun e -> DsExpr.Function("ABS", [e])) subExprGen
                Gen.map (fun e -> DsExpr.Function("NEG", [e])) subExprGen
            ]

    /// <summary>Default expr generator (max depth = 3)</summary>
    let exprGen = exprGenWithDepth 3

    /// <summary>Arbitrary for DsExpr</summary>
    type ArbitraryDsExpr =
        static member DsExpr() =
            Arb.fromGen exprGen

    // ───────────────────────────────────────────────────────────────────
    // DsStmt Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for step numbers</summary>
    let stepGen =
        Gen.choose (0, 1000)
        |> Gen.map (fun i -> i * 10) // Steps in multiples of 10

    /// <summary>Generator for simple DsStmt (no nesting)</summary>
    let simpleStmtGen =
        gen {
            let! step = stepGen
            let! tag = dsTagGen
            let! expr = simpleExprGen
            return DsStmt.Assign(step, tag, expr)
        }

    /// <summary>Generator for Command statement</summary>
    let commandStmtGen =
        gen {
            let! step = stepGen
            let! condition = exprGen
            let! action = exprGen
            return DsStmt.Command(step, condition, action)
        }

    /// <summary>Generator for Break statement</summary>
    let breakStmtGen =
        stepGen |> Gen.map DsStmt.Break

    /// <summary>Generator for DsStmt with limited nesting</summary>
    let rec stmtGenWithDepth maxDepth =
        if maxDepth <= 0 then
            Gen.oneof [simpleStmtGen; commandStmtGen; breakStmtGen]
        else
            let subStmtGen = stmtGenWithDepth (maxDepth - 1)
            Gen.oneof [
                simpleStmtGen
                commandStmtGen
                breakStmtGen

                // For loop
                gen {
                    let! step = stepGen
                    let! loopVar = intTagGen
                    let! startExpr = safeIntGen |> Gen.map (fun i -> DsExpr.Const(box i, typeof<int>))
                    let! endExpr = safeIntGen |> Gen.map (fun i -> DsExpr.Const(box i, typeof<int>))
                    let! bodySize = Gen.choose(0, 5)
                    let! body = Gen.listOfLength bodySize subStmtGen
                    return DsStmt.For(step, loopVar, startExpr, endExpr, None, body)
                }

                // While loop
                gen {
                    let! step = stepGen
                    let! condition = exprGen
                    let! bodySize = Gen.choose(0, 5)
                    let! body = Gen.listOfLength bodySize subStmtGen
                    return DsStmt.While(step, condition, body, Some 100) // Max iterations to avoid infinite loops
                }
            ]

    /// <summary>Default stmt generator (max depth = 2)</summary>
    let stmtGen = stmtGenWithDepth 2

    /// <summary>Arbitrary for DsStmt</summary>
    type ArbitraryDsStmt =
        static member DsStmt() =
            Arb.fromGen stmtGen

    // ───────────────────────────────────────────────────────────────────
    // Program Generators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Generator for small statement lists</summary>
    let smallStmtListGen =
        gen {
            let! length = Gen.choose(1, 10)
            return! Gen.listOfLength length simpleStmtGen
        }

    /// <summary>Generator for medium statement lists</summary>
    let mediumStmtListGen =
        gen {
            let! length = Gen.choose(10, 100)
            return! Gen.listOfLength length simpleStmtGen
        }

    // ───────────────────────────────────────────────────────────────────
    // Shrinkers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Shrinker for DsExpr (simplify failing test cases)</summary>
    let rec shrinkExpr expr =
        match expr with
        | DsExpr.Const _ -> Seq.empty
        | DsExpr.Terminal _ -> Seq.empty
        | DsExpr.Function(_, args) ->
            seq {
                // Try removing arguments
                for arg in args do
                    yield arg
                // Try simplifying arguments
                for i = 0 to args.Length - 1 do
                    for shrunk in shrinkExpr args.[i] do
                        let newArgs = List.mapi (fun j a -> if i = j then shrunk else a) args
                        yield DsExpr.Function("ADD", newArgs)
            }
        | _ -> Seq.empty

    /// <summary>Shrinker for DsStmt</summary>
    let shrinkStmt stmt =
        match stmt with
        | DsStmt.Assign(step, tag, expr) ->
            shrinkExpr expr
            |> Seq.map (fun e -> DsStmt.Assign(step, tag, e))
        | DsStmt.Command(step, cond, action) ->
            Seq.append
                (shrinkExpr cond |> Seq.map (fun c -> DsStmt.Command(step, c, action)))
                (shrinkExpr action |> Seq.map (fun a -> DsStmt.Command(step, cond, a)))
        | DsStmt.For(step, loopVar, start, end', stepExpr, body) ->
            seq {
                // Try empty body
                yield DsStmt.For(step, loopVar, start, end', stepExpr, [])
                // Try smaller body
                if body.Length > 1 then
                    yield DsStmt.For(step, loopVar, start, end', stepExpr, List.take 1 body)
            }
        | DsStmt.While(step, condition, body, maxIter) ->
            seq {
                // Try empty body
                yield DsStmt.While(step, condition, [], maxIter)
                // Try smaller body
                if body.Length > 1 then
                    yield DsStmt.While(step, condition, List.take 1 body, maxIter)
            }
        | _ -> Seq.empty

    // ───────────────────────────────────────────────────────────────────
    // Configuration
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Register all custom arbitraries</summary>
    type CustomArbitrariesConfig =
        static member Arbitrary =
            [
                typeof<ArbitraryType>
                typeof<ArbitraryDsTag>
                typeof<ArbitraryDsExpr>
                typeof<ArbitraryDsStmt>
            ]

    /// <summary>FsCheck configuration with custom arbitraries</summary>
    let fsCheckConfig =
        { Config.Quick with
            Arbitrary = CustomArbitrariesConfig.Arbitrary
            MaxTest = 100
            QuietOnSuccess = true }

    /// <summary>FsCheck configuration for verbose testing</summary>
    let fsCheckVerboseConfig =
        { fsCheckConfig with
            QuietOnSuccess = false
            Replay = None }

    /// <summary>FsCheck configuration for thorough testing</summary>
    let fsCheckThoroughConfig =
        { fsCheckConfig with
            MaxTest = 1000
            EndSize = 100 }
