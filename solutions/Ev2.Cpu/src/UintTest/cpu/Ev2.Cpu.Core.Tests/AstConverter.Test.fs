namespace Ev2.Cpu.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Ast

// ─────────────────────────────────────────────────────────────────────
// AstConverter 모듈 단위 테스트
// ─────────────────────────────────────────────────────────────────────
// Ast.DsExpr ↔ Core.DsExpr 변환 로직 검증
// 기본 변환, 복잡한 표현식, 에러 케이스, 왕복 변환 테스트
// ─────────────────────────────────────────────────────────────────────

[<Collection("Sequential")>]
type AstConverterTest() =

    // 각 테스트 전에 TagRegistry 초기화
    do DsTagRegistry.clear()

    // ═════════════════════════════════════════════════════════════════
    // 기본 변환 테스트: Ast.DsExpr → Core.DsExpr
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``toRuntimeExpr - EConst를 Expression.Const로 변환``() =
        DsTagRegistry.clear()

        let intConst = EConst(box 42, typeof<int>)
        let result = AstConverter.toRuntimeExpr intConst

        match result with
        | Ok (Expression.Const(value, typ)) ->
            unbox<int> value |> should equal 42
            typ |> should equal typeof<int>
        | _ -> failwith "Expected Expression.Const"

    [<Fact>]
    member _.``toRuntimeExpr - EConst 여러 타입 변환``() =
        DsTagRegistry.clear()

        let boolConst = EConst(box true, typeof<bool>)
        let doubleConst = EConst(box 3.14, typeof<double>)
        let stringConst = EConst(box "Hello", typeof<string>)

        match AstConverter.toRuntimeExpr boolConst with
        | Ok (Expression.Const(v, t)) ->
            unbox<bool> v |> should equal true
            t |> should equal typeof<bool>
        | _ -> failwith "Expected bool Const"

        match AstConverter.toRuntimeExpr doubleConst with
        | Ok (Expression.Const(v, t)) ->
            unbox<float> v |> should equal 3.14
            t |> should equal typeof<double>
        | _ -> failwith "Expected double Const"

        match AstConverter.toRuntimeExpr stringConst with
        | Ok (Expression.Const(v, t)) ->
            unbox<string> v |> should equal "Hello"
            t |> should equal typeof<string>
        | _ -> failwith "Expected string Const"


    [<Fact>]
    member _.``toRuntimeExpr - EVar 타입 일치 확인``() =
        DsTagRegistry.clear()

        // 먼저 TDouble로 등록
        let tag = DsTag.Create("Tank_Level", typeof<double>)
        DsTagRegistry.register tag |> ignore

        // 동일한 타입으로 변환 시도 - 성공해야 함
        let varExpr = EVar("Tank_Level", typeof<double>)
        let result = AstConverter.toRuntimeExpr varExpr

        match result with
        | Ok (Expression.Terminal t) ->
            t.Name |> should equal "Tank_Level"
            t.DataType |> should equal typeof<double>
        | _ -> failwith "Expected successful conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EVar 타입 불일치 에러``() =
        DsTagRegistry.clear()

        // TDouble로 등록
        let tag = DsTag.Create("Tank_Level", typeof<double>)
        DsTagRegistry.register tag |> ignore

        // TInt로 변환 시도 - 에러 발생해야 함
        let varExpr = EVar("Tank_Level", typeof<int>)
        let result = AstConverter.toRuntimeExpr varExpr

        match result with
        | Error (ConversionError.TypeMismatch(expected, actual)) ->
            expected |> should equal typeof<int>
            actual |> should equal typeof<double>
        | Ok _ -> failwith "Expected TypeMismatch error"
        | Error e -> failwithf "Unexpected error: %s" (e.Format())

    [<Fact>]
    member _.``toRuntimeExpr - ETerminal을 Expression.Terminal로 변환``() =
        DsTagRegistry.clear()

        let terminalExpr = ETerminal("%I0.0", typeof<bool>)
        let result = AstConverter.toRuntimeExpr terminalExpr

        match result with
        | Ok (Expression.Terminal tag) ->
            tag.Name |> should equal "%I0.0"
            tag.DataType |> should equal typeof<bool>
        | _ -> failwith "Expected Expression.Terminal"

    [<Fact>]
    member _.``toRuntimeExpr - EUnary 단항 연산 변환``() =
        DsTagRegistry.clear()

        let unaryExpr = EUnary(DsOp.Not, EConst(box true, typeof<bool>))
        let result = AstConverter.toRuntimeExpr unaryExpr

        match result with
        | Ok (Expression.Unary(op, expr)) ->
            op |> should equal DsOp.Not
            match expr with
            | Expression.Const(v, t) ->
                unbox<bool> v |> should equal true
                t |> should equal typeof<bool>
            | _ -> failwith "Expected Const"
        | _ -> failwith "Expected Expression.Unary"

    [<Fact>]
    member _.``toRuntimeExpr - EBinary 이항 연산 변환``() =
        DsTagRegistry.clear()

        let binaryExpr = EBinary(DsOp.Add, EConst(box 10, typeof<int>), EConst(box 20, typeof<int>))
        let result = AstConverter.toRuntimeExpr binaryExpr

        match result with
        | Ok (Expression.Binary(op, left, right)) ->
            op |> should equal DsOp.Add
            match left, right with
            | Expression.Const(lv, lt), Expression.Const(rv, rt) ->
                unbox<int> lv |> should equal 10
                unbox<int> rv |> should equal 20
                lt |> should equal typeof<int>
                rt |> should equal typeof<int>
            | _ -> failwith "Expected Const operands"
        | _ -> failwith "Expected Expression.Binary"

    [<Fact>]
    member _.``toRuntimeExpr - ECall 함수 호출 변환``() =
        DsTagRegistry.clear()

        let callExpr = ECall("ABS", [EConst(box -5, typeof<int>)])
        let result = AstConverter.toRuntimeExpr callExpr

        match result with
        | Ok (Expression.Function(funcName, args)) ->
            funcName |> should equal "ABS"
            args.Length |> should equal 1
            match args.[0] with
            | Expression.Const(v, t) ->
                unbox<int> v |> should equal -5
                t |> should equal typeof<int>
            | _ -> failwith "Expected Const argument"
        | _ -> failwith "Expected Expression.Function"

    [<Fact>]
    member _.``toRuntimeExpr - ECall 여러 인자 변환``() =
        DsTagRegistry.clear()

        let callExpr = ECall("MAX", [
            EConst(box 10, typeof<int>)
            EConst(box 20, typeof<int>)
            EConst(box 15, typeof<int>)
        ])
        let result = AstConverter.toRuntimeExpr callExpr

        match result with
        | Ok (Expression.Function(funcName, args)) ->
            funcName |> should equal "MAX"
            args.Length |> should equal 3
        | _ -> failwith "Expected Expression.Function"

    [<Fact>]
    member _.``toRuntimeExpr - EMeta 메타데이터는 에러 반환``() =
        DsTagRegistry.clear()

        let metaExpr = EMeta("debug", Map.empty)
        let result = AstConverter.toRuntimeExpr metaExpr

        match result with
        | Error (UnsupportedFeature(feature, _)) ->
            feature |> should equal "EMeta"
        | _ -> failwith "Expected UnsupportedFeature error for EMeta"

    // ═════════════════════════════════════════════════════════════════
    // 복잡한 표현식 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``toRuntimeExpr - 중첩 이항 연산 (a + b) * c``() =
        DsTagRegistry.clear()

        // (10 + 20) * 2
        let complexExpr = EBinary(
            DsOp.Mul,
            EBinary(DsOp.Add, EConst(box 10, typeof<int>), EConst(box 20, typeof<int>)),
            EConst(box 2, typeof<int>)
        )

        let result = AstConverter.toRuntimeExpr complexExpr

        match result with
        | Ok (Expression.Binary(DsOp.Mul, left, right)) ->
            match left with
            | Expression.Binary(DsOp.Add, _, _) -> ()
            | _ -> failwith "Expected nested Binary"
            match right with
            | Expression.Const(v, _) -> unbox<int> v |> should equal 2
            | _ -> failwith "Expected Const"
        | _ -> failwith "Expected Expression.Binary"

    [<Fact>]
    member _.``toRuntimeExpr - 변수와 상수 혼합 연산``() =
        DsTagRegistry.clear()

        // Motor_Speed + 100
        let mixedExpr = EBinary(
            DsOp.Add,
            EVar("Motor_Speed", typeof<int>),
            EConst(box 100, typeof<int>)
        )

        let result = AstConverter.toRuntimeExpr mixedExpr

        match result with
        | Ok (Expression.Binary(DsOp.Add, left, right)) ->
            match left with
            | Expression.Terminal tag -> tag.Name |> should equal "Motor_Speed"
            | _ -> failwith "Expected Terminal"
            match right with
            | Expression.Const(v, _) -> unbox<int> v |> should equal 100
            | _ -> failwith "Expected Const"
        | _ -> failwith "Expected Expression.Binary"

    [<Fact>]
    member _.``toRuntimeExpr - 함수 내 복잡한 표현식``() =
        DsTagRegistry.clear()

        // MAX(a + 1, b * 2)
        let complexCall = ECall("MAX", [
            EBinary(DsOp.Add, EVar("a", typeof<int>), EConst(box 1, typeof<int>))
            EBinary(DsOp.Mul, EVar("b", typeof<int>), EConst(box 2, typeof<int>))
        ])

        let result = AstConverter.toRuntimeExpr complexCall

        match result with
        | Ok (Expression.Function(funcName, args)) ->
            funcName |> should equal "MAX"
            args.Length |> should equal 2
            match args.[0] with
            | Expression.Binary(DsOp.Add, _, _) -> ()
            | _ -> failwith "Expected Add operation"
            match args.[1] with
            | Expression.Binary(DsOp.Mul, _, _) -> ()
            | _ -> failwith "Expected Mul operation"
        | _ -> failwith "Expected Expression.Function"

    [<Fact>]
    member _.``toRuntimeExpr - 깊이 중첩된 표현식``() =
        DsTagRegistry.clear()

        // ((a + b) * (c - d)) / 2
        let deepNested = EBinary(
            DsOp.Div,
            EBinary(
                DsOp.Mul,
                EBinary(DsOp.Add, EVar("a", typeof<int>), EVar("b", typeof<int>)),
                EBinary(DsOp.Sub, EVar("c", typeof<int>), EVar("d", typeof<int>))
            ),
            EConst(box 2, typeof<int>)
        )

        let result = AstConverter.toRuntimeExpr deepNested

        match result with
        | Ok _ -> ()
        | Error e -> failwithf "Conversion failed: %s" (e.Format())

    // ═════════════════════════════════════════════════════════════════
    // 역변환 테스트: Core.DsExpr → Ast.DsExpr
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``fromRuntimeExpr - Expression.Const를 EConst로 역변환``() =
        DsTagRegistry.clear()

        let runtimeExpr = Expression.Const(box 42, typeof<int>)
        let astExpr = AstConverter.fromRuntimeExpr runtimeExpr

        match astExpr with
        | EConst(value, typ) ->
            unbox<int> value |> should equal 42
            typ |> should equal typeof<int>
        | _ -> failwith "Expected EConst"

    [<Fact>]
    member _.``fromRuntimeExpr - Expression.Terminal을 EVar로 역변환``() =
        DsTagRegistry.clear()

        let tag = DsTag.Create("Motor_Speed", typeof<int>)
        let runtimeExpr = Expression.Terminal tag
        let astExpr = AstConverter.fromRuntimeExpr runtimeExpr

        match astExpr with
        | EVar(name, typ) ->
            name |> should equal "Motor_Speed"
            typ |> should equal typeof<int>
        | _ -> failwith "Expected EVar"

    [<Fact>]
    member _.``fromRuntimeExpr - Expression.Unary 역변환``() =
        DsTagRegistry.clear()

        let runtimeExpr = Expression.Unary(DsOp.Not, Expression.Const(box true, typeof<bool>))
        let astExpr = AstConverter.fromRuntimeExpr runtimeExpr

        match astExpr with
        | EUnary(op, expr) ->
            op |> should equal DsOp.Not
            match expr with
            | EConst(v, t) ->
                unbox<bool> v |> should equal true
                t |> should equal typeof<bool>
            | _ -> failwith "Expected EConst"
        | _ -> failwith "Expected EUnary"

    [<Fact>]
    member _.``fromRuntimeExpr - Expression.Binary 역변환``() =
        DsTagRegistry.clear()

        let runtimeExpr = Expression.Binary(DsOp.Add, Expression.Const(box 10, typeof<int>), Expression.Const(box 20, typeof<int>))
        let astExpr = AstConverter.fromRuntimeExpr runtimeExpr

        match astExpr with
        | EBinary(op, left, right) ->
            op |> should equal DsOp.Add
        | _ -> failwith "Expected EBinary"

    [<Fact>]
    member _.``fromRuntimeExpr - Expression.Function 역변환``() =
        DsTagRegistry.clear()

        let runtimeExpr = Expression.Function("ABS", [Expression.Const(box -5, typeof<int>)])
        let astExpr = AstConverter.fromRuntimeExpr runtimeExpr

        match astExpr with
        | ECall(funcName, args) ->
            funcName |> should equal "ABS"
            args.Length |> should equal 1
        | _ -> failwith "Expected ECall"

    // ═════════════════════════════════════════════════════════════════
    // 왕복 변환 테스트 (Round-trip)
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``왕복 변환 - EConst → Expression.Const → EConst``() =
        DsTagRegistry.clear()

        let original = EConst(box 42, typeof<int>)
        let runtime = AstConverter.toRuntimeExprUnsafe original
        let restored = AstConverter.fromRuntimeExpr runtime

        match restored with
        | EConst(v, t) ->
            unbox<int> v |> should equal 42
            t |> should equal typeof<int>
        | _ -> failwith "Round-trip failed"

    [<Fact>]
    member _.``왕복 변환 - 복잡한 표현식 의미 보존``() =
        DsTagRegistry.clear()

        // (a_int + 10) * 2
        let original = EBinary(
            DsOp.Mul,
            EBinary(DsOp.Add, EVar("a_int", typeof<int>), EConst(box 10, typeof<int>)),
            EConst(box 2, typeof<int>)
        )

        let runtime = AstConverter.toRuntimeExprUnsafe original
        let restored = AstConverter.fromRuntimeExpr runtime

        match restored with
        | EBinary(DsOp.Mul, left, right) ->
            match left with
            | EBinary(DsOp.Add, _, _) -> ()
            | _ -> failwith "Expected nested Add"
            match right with
            | EConst(v, _) -> unbox<int> v |> should equal 2
            | _ -> failwith "Expected Const"
        | _ -> failwith "Round-trip failed"

    // ═════════════════════════════════════════════════════════════════
    // 헬퍼 함수 테스트
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``toRuntimeExprUnsafe - 성공 케이스``() =
        DsTagRegistry.clear()

        let astExpr = EConst(box 42, typeof<int>)
        let runtime = AstConverter.toRuntimeExprUnsafe astExpr

        match runtime with
        | Expression.Const(v, t) ->
            unbox<int> v |> should equal 42
            t |> should equal typeof<int>
        | _ -> failwith "Expected Expression.Const"

    [<Fact>]
    member _.``toRuntimeExprUnsafe - 실패 시 예외 발생``() =
        DsTagRegistry.clear()

        // 타입 불일치 케이스
        let tag = DsTag.Create("TypeCheck_Tag", typeof<double>)
        DsTagRegistry.register tag |> ignore
        let astExpr = EVar("TypeCheck_Tag", typeof<int>)  // TInt로 시도

        (fun () -> AstConverter.toRuntimeExprUnsafe astExpr |> ignore)
        |> should throw typeof<Exception>

    [<Fact>]
    member _.``toRuntimeExprs - 여러 표현식 일괄 변환``() =
        DsTagRegistry.clear()

        let exprs = [
            EConst(box 1, typeof<int>)
            EConst(box 2, typeof<int>)
            EConst(box 3, typeof<int>)
        ]

        let result = AstConverter.toRuntimeExprs exprs

        match result with
        | Ok runtimeExprs ->
            runtimeExprs.Length |> should equal 3
            match runtimeExprs.[0] with
            | Expression.Const(v, _) -> unbox<int> v |> should equal 1
            | _ -> failwith "Expected Const"
        | Error e -> failwithf "Conversion failed: %s" (e.Format())



    [<Fact>]
    member _.``canConvertToRuntime - 변환 가능 케이스``() =
        DsTagRegistry.clear()

        let astExpr = EConst(box 42, typeof<int>)
        AstConverter.canConvertToRuntime astExpr |> should equal true

    [<Fact>]
    member _.``canConvertToRuntime - 변환 불가능 케이스``() =
        DsTagRegistry.clear()

        // 타입 불일치
        let tag = DsTag.Create("TypeCheck_Tag_2", typeof<double>)
        DsTagRegistry.register tag |> ignore
        let astExpr = EVar("TypeCheck_Tag_2", typeof<int>)

        AstConverter.canConvertToRuntime astExpr |> should equal false



    [<Fact>]
    member _.``ConversionError.Format - UnsupportedFeature 메시지``() =
        let error = UnsupportedFeature("CustomFeature", "Not implemented yet")
        let message = error.Format()
        message.Contains("CustomFeature") |> should be True
        message.Contains("Not implemented") |> should be True

    [<Fact>]
    member _.``ConversionError.Format - InvalidArgumentCount 메시지``() =
        let error = InvalidArgumentCount("MAX", 2, 3)
        let message = error.Format()
        message.Contains("MAX") |> should be True
        message.Contains("2") |> should be True
        message.Contains("3") |> should be True

    // ═════════════════════════════════════════════════════════════════
    // Phase 2 Enhanced Tests - Boundary Values, Error Cases & Edge Cases
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``toRuntimeExpr - EConst boundary Int32 MinMax``() =
        DsTagRegistry.clear()

        let minInt = EConst(box System.Int32.MinValue, typeof<int>)
        let maxInt = EConst(box System.Int32.MaxValue, typeof<int>)

        match AstConverter.toRuntimeExpr minInt with
        | Ok (Expression.Const(v, t)) when t = typeof<int> -> unbox<int> v |> should equal System.Int32.MinValue
        | _ -> failwith "Expected Int32.MinValue conversion"

        match AstConverter.toRuntimeExpr maxInt with
        | Ok (Expression.Const(v, t)) when t = typeof<int> -> unbox<int> v |> should equal System.Int32.MaxValue
        | _ -> failwith "Expected Int32.MaxValue conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EConst boundary Double special values``() =
        DsTagRegistry.clear()

        let nan = EConst(box System.Double.NaN, typeof<double>)
        let posInf = EConst(box System.Double.PositiveInfinity, typeof<double>)
        let negInf = EConst(box System.Double.NegativeInfinity, typeof<double>)

        match AstConverter.toRuntimeExpr nan with
        | Ok (Expression.Const(v, t)) when t = typeof<double> -> System.Double.IsNaN(unbox<float> v) |> should be True
        | _ -> failwith "Expected NaN conversion"

        match AstConverter.toRuntimeExpr posInf with
        | Ok (Expression.Const(v, t)) when t = typeof<double> -> unbox<float> v |> should equal System.Double.PositiveInfinity
        | _ -> failwith "Expected +Infinity conversion"

        match AstConverter.toRuntimeExpr negInf with
        | Ok (Expression.Const(v, t)) when t = typeof<double> -> unbox<float> v |> should equal System.Double.NegativeInfinity
        | _ -> failwith "Expected -Infinity conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EConst empty string``() =
        DsTagRegistry.clear()

        let emptyStr = EConst(box "", typeof<string>)

        match AstConverter.toRuntimeExpr emptyStr with
        | Ok (Expression.Const(v, t)) when t = typeof<string> -> unbox<string> v |> should equal ""
        | _ -> failwith "Expected empty string conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EConst very long string``() =
        DsTagRegistry.clear()

        let longStr = System.String('X', 10000)
        let longConst = EConst(box longStr, typeof<string>)

        match AstConverter.toRuntimeExpr longConst with
        | Ok (Expression.Const(v, t)) when t = typeof<string> ->
            let s = unbox<string> v
            s.Length |> should equal 10000
        | _ -> failwith "Expected long string conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EVar with very long name``() =
        DsTagRegistry.clear()

        let longName = System.String('A', 1000)
        let tag = DsTag.Create(longName, typeof<int>)
        DsTagRegistry.register tag |> ignore

        let varExpr = EVar(longName, typeof<int>)
        let result = AstConverter.toRuntimeExpr varExpr

        match result with
        | Ok (Expression.Terminal t) ->
            t.Name.Length |> should equal 1000
            t.DataType |> should equal typeof<int>
        | _ -> failwith "Expected conversion with long name"

    [<Fact>]
    member _.``toRuntimeExpr - EBinary deeply nested``() =
        DsTagRegistry.clear()

        // Create deeply nested expression: ((((1 + 2) + 3) + 4) + 5)
        let rec createNested depth =
            if depth = 0 then
                EConst(box 1, typeof<int>)
            else
                EBinary(Add, createNested (depth - 1), EConst(box depth, typeof<int>))

        let deepExpr = createNested 10
        let result = AstConverter.toRuntimeExpr deepExpr

        match result with
        | Ok _ -> () // Should successfully convert
        | Error e -> failwithf "Deep nesting failed: %s" (e.Format())

    [<Fact>]
    member _.``toRuntimeExpr - EUnary all unary operators``() =
        DsTagRegistry.clear()

        let tag = DsTag.Create("flag", typeof<bool>)
        DsTagRegistry.register tag |> ignore

        let flagVar = EVar("flag", typeof<bool>)

        // Test Not, Rising, Falling
        let notExpr = EUnary(Not, flagVar)
        let risingExpr = EUnary(Rising, flagVar)
        let fallingExpr = EUnary(Falling, flagVar)

        match AstConverter.toRuntimeExpr notExpr with
        | Ok (Expression.Unary(DsOp.Not, _)) -> ()
        | _ -> failwith "Expected Not conversion"

        match AstConverter.toRuntimeExpr risingExpr with
        | Ok (Expression.Unary(DsOp.Rising, _)) -> ()
        | _ -> failwith "Expected Rising conversion"

        match AstConverter.toRuntimeExpr fallingExpr with
        | Ok (Expression.Unary(DsOp.Falling, _)) -> ()
        | _ -> failwith "Expected Falling conversion"

    [<Fact>]
    member _.``toRuntimeExpr - EBinary all arithmetic operators``() =
        DsTagRegistry.clear()

        let tagX = DsTag.Create("x", typeof<int>)
        let tagY = DsTag.Create("y", typeof<int>)
        DsTagRegistry.register tagX |> ignore
        DsTagRegistry.register tagY |> ignore

        let x = EVar("x", typeof<int>)
        let y = EVar("y", typeof<int>)

        // Test Add, Sub, Mul, Div, Mod
        let operators = [Add; Sub; Mul; Div; Mod]
        let expectedOps = [DsOp.Add; DsOp.Sub; DsOp.Mul; DsOp.Div; DsOp.Mod]

        List.zip operators expectedOps
        |> List.iter (fun (astOp, coreOp) ->
            let expr = EBinary(astOp, x, y)
            match AstConverter.toRuntimeExpr expr with
            | Ok (Expression.Binary(op, _, _)) -> op |> should equal coreOp
            | _ -> failwithf "Expected %A conversion" coreOp)

    [<Fact>]
    member _.``toRuntimeExpr - EBinary all comparison operators``() =
        DsTagRegistry.clear()

        let tagX = DsTag.Create("x", typeof<int>)
        let tagY = DsTag.Create("y", typeof<int>)
        DsTagRegistry.register tagX |> ignore
        DsTagRegistry.register tagY |> ignore

        let x = EVar("x", typeof<int>)
        let y = EVar("y", typeof<int>)

        // Test Eq, Ne, Gt, Ge, Lt, Le
        let operators = [Eq; Ne; Gt; Ge; Lt; Le]
        let expectedOps = [DsOp.Eq; DsOp.Ne; DsOp.Gt; DsOp.Ge; DsOp.Lt; DsOp.Le]

        List.zip operators expectedOps
        |> List.iter (fun (astOp, coreOp) ->
            let expr = EBinary(astOp, x, y)
            match AstConverter.toRuntimeExpr expr with
            | Ok (Expression.Binary(op, _, _)) -> op |> should equal coreOp
            | _ -> failwithf "Expected %A conversion" coreOp)

    [<Fact>]
    member _.``toRuntimeExpr - EBinary all logical operators``() =
        DsTagRegistry.clear()

        let tagA = DsTag.Create("a", typeof<bool>)
        let tagB = DsTag.Create("b", typeof<bool>)
        DsTagRegistry.register tagA |> ignore
        DsTagRegistry.register tagB |> ignore

        let a = EVar("a", typeof<bool>)
        let b = EVar("b", typeof<bool>)

        // Test And, Or, Xor
        let operators = [And; Or; Xor]
        let expectedOps = [DsOp.And; DsOp.Or; DsOp.Xor]

        List.zip operators expectedOps
        |> List.iter (fun (astOp, coreOp) ->
            let expr = EBinary(astOp, a, b)
            match AstConverter.toRuntimeExpr expr with
            | Ok (Expression.Binary(op, _, _)) -> op |> should equal coreOp
            | _ -> failwithf "Expected %A conversion" coreOp)

    [<Fact>]
    member _.``toRuntimeExpr - ECall with no arguments``() =
        DsTagRegistry.clear()

        let callExpr = ECall("GetTime", [])
        let result = AstConverter.toRuntimeExpr callExpr

        match result with
        | Ok (Expression.Function("GetTime", args)) -> args.Length |> should equal 0
        | _ -> failwith "Expected GetTime conversion"

    [<Fact>]
    member _.``toRuntimeExpr - ECall with many arguments``() =
        DsTagRegistry.clear()

        let manyArgs = [for i in 1..20 -> EConst(box i, typeof<int>)]
        let callExpr = ECall("ManyParams", manyArgs)
        let result = AstConverter.toRuntimeExpr callExpr

        match result with
        | Ok (Expression.Function("ManyParams", args)) -> args.Length |> should equal 20
        | _ -> failwith "Expected ManyParams conversion"

    [<Fact>]
    member _.``toRuntimeExprs - Empty list``() =
        DsTagRegistry.clear()

        let result = AstConverter.toRuntimeExprs []

        match result with
        | Ok exprs -> exprs.Length |> should equal 0
        | Error e -> failwithf "Empty list conversion failed: %s" (e.Format())

    [<Fact>]
    member _.``toRuntimeExprs - Large list 100 items``() =
        DsTagRegistry.clear()

        let largeList = [for i in 1..100 -> EConst(box i, typeof<int>)]
        let result = AstConverter.toRuntimeExprs largeList

        match result with
        | Ok exprs -> exprs.Length |> should equal 100
        | Error e -> failwithf "Large list conversion failed: %s" (e.Format())

    [<Fact>]
    member _.``canConvertToRuntime - Deeply nested expression``() =
        DsTagRegistry.clear()

        let rec createNested depth =
            if depth = 0 then
                EConst(box 1, typeof<int>)
            else
                EBinary(Add, createNested (depth - 1), EConst(box 1, typeof<int>))

        let deepExpr = createNested 20
        AstConverter.canConvertToRuntime deepExpr |> should equal true

    [<Fact>]
    member _.``ConversionError.Format - TypeMismatch message``() =
        let error = ConversionError.TypeMismatch(typeof<int>, typeof<double>)
        let message = error.Format()
        message.Contains("typeof<Int32>") |> should be True  // Updated to match TypeHelpers change
        message.Contains("typeof<Double>") |> should be True

    [<Fact>]
    member _.``ConversionError.Format - UndefinedVariable message``() =
        let error = ConversionError.UndefinedVariable("MissingVar")
        let message = error.Format()
        message.Contains("MissingVar") |> should be True

    [<Fact>]
    member _.``toRuntimeExpr - Complex nested expression``() =
        DsTagRegistry.clear()

        // Register variables
        let tagA = DsTag.Create("cpx_a", typeof<int>)
        let tagB = DsTag.Create("cpx_b", typeof<int>)
        let tagC = DsTag.Create("cpx_c", typeof<int>)
        DsTagRegistry.register tagA |> ignore
        DsTagRegistry.register tagB |> ignore
        DsTagRegistry.register tagC |> ignore

        // Complex: ((a + b) * c) / 2
        let complex =
            EBinary(Div,
                EBinary(Mul,
                    EBinary(Add, EVar("cpx_a", typeof<int>), EVar("cpx_b", typeof<int>)),
                    EVar("cpx_c", typeof<int>)),
                EConst(box 2, typeof<int>))

        let result = AstConverter.toRuntimeExpr complex

        match result with
        | Ok _ -> () // Successful conversion
        | Error e -> failwithf "Complex expression conversion failed: %s" (e.Format())

    [<Fact>]
    member _.``toRuntimeExpr - ETerminal conversion``() =
        DsTagRegistry.clear()

        let terminal = ETerminal("%I0.0", typeof<bool>)
        let result = AstConverter.toRuntimeExpr terminal

        match result with
        | Ok (Expression.Terminal t) ->
            t.Name |> should equal "%I0.0"
            t.DataType |> should equal typeof<bool>
        | _ -> failwith "Expected ETerminal conversion"
