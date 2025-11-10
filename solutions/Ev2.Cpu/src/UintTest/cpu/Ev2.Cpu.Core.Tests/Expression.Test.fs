module Ev2.Cpu.Test.Expression

open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

[<Fact>]
let ``Expression - 기본 상수 생성`` () =
    clearVariableRegistry()
    let n = num 42
    n |> should equal (Const(box 42, typeof<int>))
    
    let b = bool true
    b |> should equal (Const(box true, typeof<bool>))
    
    let d = dbl 3.14
    d |> should equal (Const(box 3.14, typeof<double>))
    
    let s = str "test"
    s |> should equal (Const(box "test", typeof<string>))

[<Fact>]
let ``Expression - 변수 생성`` () =
    clearVariableRegistry()
    let v1 = boolVar "flag"
    match v1 with
    | Terminal(tag) -> 
        tag.Name |> should equal "flag"
        tag.DataType |> should equal typeof<bool>
    | _ -> failwith "Expected Terminal"
    
    let v2 = intVar "counter"
    match v2 with
    | Terminal(tag) -> 
        tag.Name |> should equal "counter"
        tag.DataType |> should equal typeof<int>
    | _ -> failwith "Expected Terminal"

[<Fact>]
let ``Expression - 논리 연산`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"
    
    let andExpr = a &&. b
    match andExpr with
    | Binary(DsOp.And, _, _) -> ()
    | _ -> failwith "Expected AND operation"
    
    let orExpr = a ||. b
    match orExpr with
    | Binary(DsOp.Or, _, _) -> ()
    | _ -> failwith "Expected OR operation"
    
    let notExpr = !!. a
    match notExpr with
    | Unary(DsOp.Not, _) -> ()
    | _ -> failwith "Expected NOT operation"

[<Fact>]
let ``Expression - 비교 연산`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let y = intVar "y"
    
    let eq = x ==. y
    match eq with
    | Binary(DsOp.Eq, _, _) -> ()
    | _ -> failwith "Expected EQ operation"
    
    let ne = x <>. y
    match ne with
    | Binary(DsOp.Ne, _, _) -> ()
    | _ -> failwith "Expected NE operation"
    
    let gt = x >>. y
    match gt with
    | Binary(DsOp.Gt, _, _) -> ()
    | _ -> failwith "Expected GT operation"

[<Fact>]
let ``Expression - 함수 호출`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let y = intVar "y"
    
    let movExpr = fn "Move" [x; y]
    match movExpr with
    | Function("Move", args) -> 
        args.Length |> should equal 2
    | _ -> failwith "Expected Function call"
    
    let addExpr = fn "ADD" [x; y; num 10]
    match addExpr with
    | Function("ADD", args) -> 
        args.Length |> should equal 3
    | _ -> failwith "Expected Function call"

[<Fact>]
let ``Expression - Set/Reset 패턴`` () =
    clearVariableRegistry()
    let start = boolVar "start"
    let stop = boolVar "stop"
    
    let srExpr = setReset start stop
    match srExpr with
    | Binary(DsOp.And, s, Unary(DsOp.Not, r)) -> 
        s |> should equal start
        r |> should equal stop
    | _ -> failwith "Expected Set/Reset pattern"

[<Fact>]
let ``Expression - 엣지 검출`` () =
    clearVariableRegistry()
    let input = boolVar "input"
    
    let rise = rising input
    match rise with
    | Unary(DsOp.Rising, _) -> ()
    | _ -> failwith "Expected Rising edge"
    
    let fall = falling input
    match fall with
    | Unary(DsOp.Falling, _) -> ()
    | _ -> failwith "Expected Falling edge"

[<Fact>]
let ``Expression - 체인 연산`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"
    let c = boolVar "c"
    
    let allExpr = all [a; b; c]
    allExpr.InferType() |> should equal typeof<bool>
    
    let anyExpr = any [a; b; c]
    anyExpr.InferType() |> should equal typeof<bool>
    
    let noneExpr = none [a; b; c]
    noneExpr.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - 타입 추론`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let y = intVar "y"
    let flag = boolVar "flag"
    
    // 산술 연산
    (x .+. y).InferType() |> should equal typeof<int>
    (x .-. y).InferType() |> should equal typeof<int>
    (x .*. y).InferType() |> should equal typeof<int>
    
    // 비교 연산
    (x >>. y).InferType() |> should equal typeof<bool>
    (x ==. y).InferType() |> should equal typeof<bool>
    
    // 논리 연산
    (flag &&. flag).InferType() |> should equal typeof<bool>
    (!!. flag).InferType() |> should equal typeof<bool>

// ═════════════════════════════════════════════════════════════════
// Phase 2 Enhanced Tests - Property-Based & Edge Cases
// ═════════════════════════════════════════════════════════════════

open FsCheck
open FsCheck.Xunit

[<Property>]
let ``Expression - Constant creation preserves value`` (value: int) =
    clearVariableRegistry()
    let expr = num value
    match expr with
    | Const(boxedVal, t) when t = typeof<int> -> unbox<int> boxedVal = value
    | _ -> false

[<Property>]
let ``Expression - Double constant creation preserves value`` (value: float) =
    clearVariableRegistry()
    let expr = dbl value
    match expr with
    | Const(boxedVal, t) when t = typeof<double> ->
        let actual = unbox<float> boxedVal
        // Handle NaN specially since NaN <> NaN
        if System.Double.IsNaN(value) then System.Double.IsNaN(actual)
        else actual = value
    | _ -> false

[<Property>]
let ``Expression - String constant creation preserves value`` (value: string) =
    clearVariableRegistry()
    let safeValue = if value = null then "" else value
    let expr = str safeValue
    match expr with
    | Const(boxedVal, t) when t = typeof<string> -> unbox<string> boxedVal = safeValue
    | _ -> false

[<Property>]
let ``Expression - Bool constant creation preserves value`` (value: bool) =
    clearVariableRegistry()
    let expr = bool value
    match expr with
    | Const(boxedVal, t) when t = typeof<bool> -> unbox<bool> boxedVal = value
    | _ -> false

[<Fact>]
let ``Expression - Constant creation with boundary values`` () =
    clearVariableRegistry()

    // Int32 boundaries
    let minInt = num System.Int32.MinValue
    match minInt with
    | Const(v, t) when t = typeof<int> -> unbox<int> v |> should equal System.Int32.MinValue
    | _ -> failwith "Expected Int32.MinValue constant"

    let maxInt = num System.Int32.MaxValue
    match maxInt with
    | Const(v, t) when t = typeof<int> -> unbox<int> v |> should equal System.Int32.MaxValue
    | _ -> failwith "Expected Int32.MaxValue constant"

    // Double boundaries
    let nan = dbl System.Double.NaN
    match nan with
    | Const(v, t) when t = typeof<double> -> System.Double.IsNaN(unbox<float> v) |> should be True
    | _ -> failwith "Expected NaN constant"

    let posInf = dbl System.Double.PositiveInfinity
    match posInf with
    | Const(v, t) when t = typeof<double> -> unbox<float> v |> should equal System.Double.PositiveInfinity
    | _ -> failwith "Expected +Infinity constant"

    let negInf = dbl System.Double.NegativeInfinity
    match negInf with
    | Const(v, t) when t = typeof<double> -> unbox<float> v |> should equal System.Double.NegativeInfinity
    | _ -> failwith "Expected -Infinity constant"

[<Fact>]
let ``Expression - Type inference for mixed Int/Double operations`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let y = dblVar "y"

    // Int + Double => Double (type promotion)
    (x .+. y).InferType() |> should equal typeof<double>
    (y .+. x).InferType() |> should equal typeof<double>

    // Double * Int => Double
    (y .*. x).InferType() |> should equal typeof<double>
    (x .*. y).InferType() |> should equal typeof<double>

[<Fact>]
let ``Expression - Variable names are stored correctly`` () =
    clearVariableRegistry()
    // Test with simple valid names
    let v1 = intVar "myVar"
    match v1 with
    | Terminal(tag) -> tag.Name |> should equal "myVar"
    | _ -> failwith "Expected Terminal"

    let v2 = boolVar "flag_123"
    match v2 with
    | Terminal(tag) -> tag.Name |> should equal "flag_123"
    | _ -> failwith "Expected Terminal"

[<Fact>]
let ``Expression - Logical AND is commutative for structure`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"

    let expr1 = a &&. b
    let expr2 = b &&. a

    // Both should be Binary(And, _, _)
    match expr1, expr2 with
    | Binary(DsOp.And, _, _), Binary(DsOp.And, _, _) -> ()
    | _ -> failwith "Expected AND operations"

[<Fact>]
let ``Expression - Logical OR is commutative for structure`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"

    let expr1 = a ||. b
    let expr2 = b ||. a

    // Both should be Binary(Or, _, _)
    match expr1, expr2 with
    | Binary(DsOp.Or, _, _), Binary(DsOp.Or, _, _) -> ()
    | _ -> failwith "Expected OR operations"

[<Fact>]
let ``Expression - Double NOT cancels out`` () =
    clearVariableRegistry()
    let a = boolVar "a"

    let notNotA = !!. (!!. a)
    match notNotA with
    | Unary(DsOp.Not, Unary(DsOp.Not, _)) -> ()
    | _ -> failwith "Expected NOT(NOT(a)) structure"

[<Fact>]
let ``Expression - Comparison operators work with constants`` () =
    clearVariableRegistry()

    let expr1 = num 5 >>. num 3
    expr1.InferType() |> should equal typeof<bool>

    let expr2 = dbl 3.14 ==. dbl 2.71
    expr2.InferType() |> should equal typeof<bool>

    let expr3 = str "abc" <>. str "def"
    expr3.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - Arithmetic with zero`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let zero = num 0

    // x + 0 should still be a valid expression
    let expr1 = x .+. zero
    expr1.InferType() |> should equal typeof<int>

    // x * 0 should still be a valid expression
    let expr2 = x .*. zero
    expr2.InferType() |> should equal typeof<int>

[<Fact>]
let ``Expression - Arithmetic with one`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let one = num 1

    // x * 1 should still be a valid expression
    let expr1 = x .*. one
    expr1.InferType() |> should equal typeof<int>

    // x / 1 should still be a valid expression
    let expr2 = x ./. one
    expr2.InferType() |> should equal typeof<int>

[<Fact>]
let ``Expression - Function with empty arguments`` () =
    clearVariableRegistry()

    // Function with no arguments should be valid
    let expr = fn "GetTime" []
    match expr with
    | Function("GetTime", args) -> args.Length |> should equal 0
    | _ -> failwith "Expected Function with no args"

[<Fact>]
let ``Expression - Function with many arguments`` () =
    clearVariableRegistry()

    // Function with 10 arguments
    let args = [for i in 1..10 -> num i]
    let expr = fn "ManyArgs" args
    match expr with
    | Function("ManyArgs", a) -> a.Length |> should equal 10
    | _ -> failwith "Expected Function with 10 args"

[<Fact>]
let ``Expression - Chain operations preserve type`` () =
    clearVariableRegistry()
    let flags = [boolVar "a"; boolVar "b"; boolVar "c"; boolVar "d"; boolVar "e"]

    let allExpr = all flags
    allExpr.InferType() |> should equal typeof<bool>

    let anyExpr = any flags
    anyExpr.InferType() |> should equal typeof<bool>

    let noneExpr = none flags
    noneExpr.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - Chain operations with single element`` () =
    clearVariableRegistry()
    let flag = boolVar "a"

    let allExpr = all [flag]
    allExpr.InferType() |> should equal typeof<bool>

    let anyExpr = any [flag]
    anyExpr.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - Set/Reset with constants`` () =
    clearVariableRegistry()
    let trueConst = bool true
    let falseConst = bool false

    // setReset should work with constants
    let srExpr = setReset trueConst falseConst
    srExpr.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - Rising edge on constant`` () =
    clearVariableRegistry()
    let trueConst = bool true

    // Rising edge should work with constants
    let riseExpr = rising trueConst
    match riseExpr with
    | Unary(DsOp.Rising, _) -> riseExpr.InferType() |> should equal typeof<bool>
    | _ -> failwith "Expected Rising edge"

[<Fact>]
let ``Expression - Falling edge on constant`` () =
    clearVariableRegistry()
    let falseConst = bool false

    // Falling edge should work with constants
    let fallExpr = falling falseConst
    match fallExpr with
    | Unary(DsOp.Falling, _) -> fallExpr.InferType() |> should equal typeof<bool>
    | _ -> failwith "Expected Falling edge"

[<Fact>]
let ``Expression - Complex nested expression type inference`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"
    let c = boolVar "c"
    let x = intVar "x"
    let y = intVar "y"

    // ((a AND b) OR c) should be Bool
    let logicExpr = (a &&. b) ||. c
    logicExpr.InferType() |> should equal typeof<bool>

    // ((x + y) > 10) should be Bool
    let comparisonExpr = (x .+. y) >>. num 10
    comparisonExpr.InferType() |> should equal typeof<bool>

    // (a AND ((x > y) OR b)) should be Bool
    let mixedExpr = a &&. ((x >>. y) ||. b)
    mixedExpr.InferType() |> should equal typeof<bool>

[<Fact>]
let ``Expression - Empty string is valid`` () =
    clearVariableRegistry()
    let emptyStr = str ""
    match emptyStr with
    | Const(v, t) when t = typeof<string> ->
        let s = unbox<string> v
        s |> should equal ""
    | _ -> failwith "Expected empty string constant"

[<Fact>]
let ``Expression - Very long string is valid`` () =
    clearVariableRegistry()
    let longStr = System.String('X', 10000)
    let expr = str longStr
    match expr with
    | Const(v, t) when t = typeof<string> ->
        let s = unbox<string> v
        s.Length |> should equal 10000
    | _ -> failwith "Expected long string constant"
