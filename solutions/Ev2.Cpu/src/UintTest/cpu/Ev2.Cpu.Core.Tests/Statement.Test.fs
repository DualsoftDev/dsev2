module Ev2.Cpu.Test.Statement
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

[<Fact>]
let ``Statement - Assign 생성`` () =
    clearVariableRegistry()
    let tag = DsTag.Bool "output"
    let expr = bool true
    
    let stmt = tag := expr
    match stmt with
    | Assign(_, t, e) ->
        t |> should equal tag
        e |> should equal expr
    | _ -> failwith "Expected Assign statement"

[<Fact>]
let ``Statement - Command 생성`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let action = fn "SET" [boolVar "output"]
    
    let stmt = cond --> action
    match stmt with
    | Command(_, c, a) ->
        c |> should equal cond
        a |> should equal action
    | _ -> failwith "Expected Command statement"

[<Fact>]
let ``Statement - 복합 조건 Command`` () =
    clearVariableRegistry()
    let start = boolVar "start"
    let ready = boolVar "ready"
    let motor = boolVar "motor"
    
    let stmt = (start &&. ready) --> (fn "SET" [motor])
    match stmt with
    | Command(_, cond, action) ->
        match cond with
        | Binary(DsOp.And, _, _) -> ()
        | _ -> failwith "Expected AND condition"
        match action with
        | Function("SET", _) -> ()
        | _ -> failwith "Expected SET function"
    | _ -> failwith "Expected Command statement"

[<Fact>]
let ``Statement - 산술 연산 래더`` () =
    clearVariableRegistry()
    let cond = boolVar "calc"
    let x = intVar "x"
    let y = intVar "y"
    let resultTarget = "result"
    
    // 조건부 ADD
    let addStmt = (cond, x, y) --+ resultTarget
    match addStmt with
    | Command(_, _, Function("Move", _)) -> ()
    | _ -> failwith "Expected conditional ADD"
    
    // 조건부 SUB  
    let subStmt = (cond, x, y) --- resultTarget
    match subStmt with
    | Command(_, _, Function("Move", _)) -> ()
    | _ -> failwith "Expected conditional SUB"

[<Fact>]
let ``Statement - 조건부 복사`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let source = intVar "input"
    let target = "output"
    
    let stmt = (cond, source) -~> target
    match stmt with
    | Command(_, c, Function("Move", [src; dst])) ->
        c |> should equal cond
        src |> should equal source
    | _ -> failwith "Expected conditional Move"

[<Fact>]
let ``Statement - ToText 변환`` () =
    clearVariableRegistry()
    let tag = DsTag.Bool "flag"
    let expr = bool true
    
    let assignStmt = tag := expr
    let assignText = assignStmt.ToText()
    
    // 방법 1: 문자열 포함 여부를 직접 확인
    assignText.Contains("flag") |> should be True
    assignText.Contains(":=") |> should be True
    
    // 또는 방법 2: haveSubstring 사용
    // assignText |> should haveSubstring "flag"
    // assignText |> should haveSubstring ":="
    
    let cmdStmt = (boolVar "cond") --> (fn "SET" [boolVar "out"])
    let cmdText = cmdStmt.ToText()
    
    cmdText.Contains("IF") |> should be True
    cmdText.Contains("THEN") |> should be True
    
    // 또는
    // cmdText |> should haveSubstring "IF"
    // cmdText |> should haveSubstring "THEN"

[<Fact>]
let ``Statement - 참조 변수 추출`` () =
    clearVariableRegistry()
    let x = boolVar "x"
    let y = boolVar "y"
    let z = DsTag.Bool "z"
    
    let stmt = z := (x &&. y)
    let refs = stmt.ReferencedVars
    
    // Set 또는 List로 변환하여 검사
    let refsList = refs |> Set.ofSeq
    refsList |> should contain "x"
    refsList |> should contain "y"
    refsList |> should not' (contain "z")

[<Fact>]
let ``Statement - 타이머 연산`` () =
    clearVariableRegistry()
    let enable = boolVar "enable"
    let timer = "T1"
    let preset = 5000

    let stmt = enable --@ (timer, preset)
    match stmt with
    | Command(_, _, Function("TON", args)) ->
        args.Length |> should equal 3
    | _ -> failwith "Expected timer operation"

[<Fact>]
let ``Statement - 카운터 연산`` () =
    clearVariableRegistry()
    let trigger = boolVar "count_up"
    let counter = "C1"
    let preset = 100

    let stmt = trigger --% (counter, preset)
    match stmt with
    | Command(_, _, Function("CTU", args)) ->
        args.Length |> should equal 4
    | _ -> failwith "Expected counter operation"

// ═════════════════════════════════════════════════════════════════
// Phase 2 Enhanced Tests - Edge Cases & Boundary Values
// ═════════════════════════════════════════════════════════════════

open FsCheck
open FsCheck.Xunit

[<Fact>]
let ``Statement - Assign with boundary value Int32`` () =
    clearVariableRegistry()
    let maxTag = DsTag.Int "maxValue"
    let minTag = DsTag.Int "minValue"

    // Int32.MaxValue assignment
    let stmtMax = maxTag := num System.Int32.MaxValue
    match stmtMax with
    | Assign(_, t, e) ->
        t |> should equal maxTag
        e.InferType() |> should equal DsDataType.TInt
    | _ -> failwith "Expected Assign"

    // Int32.MinValue assignment
    let stmtMin = minTag := num System.Int32.MinValue
    match stmtMin with
    | Assign(_, t, e) ->
        t |> should equal minTag
        e.InferType() |> should equal DsDataType.TInt
    | _ -> failwith "Expected Assign"

[<Fact>]
let ``Statement - Assign with boundary value Double`` () =
    clearVariableRegistry()
    let nanTag = DsTag.Double "nanValue"
    let infTag = DsTag.Double "infValue"

    // NaN assignment
    let stmtNaN = nanTag := dbl System.Double.NaN
    match stmtNaN with
    | Assign(_, t, e) ->
        t |> should equal nanTag
        e.InferType() |> should equal DsDataType.TDouble
    | _ -> failwith "Expected Assign"

    // Infinity assignment
    let stmtInf = infTag := dbl System.Double.PositiveInfinity
    match stmtInf with
    | Assign(_, t, e) ->
        t |> should equal infTag
        e.InferType() |> should equal DsDataType.TDouble
    | _ -> failwith "Expected Assign"

[<Fact>]
let ``Statement - Assign with empty string`` () =
    clearVariableRegistry()
    let tag = DsTag.String "emptyStr"

    let stmt = tag := str ""
    match stmt with
    | Assign(_, t, e) ->
        t |> should equal tag
        e.InferType() |> should equal DsDataType.TString
    | _ -> failwith "Expected Assign"

[<Fact>]
let ``Statement - Command with complex nested condition`` () =
    clearVariableRegistry()
    let a = boolVar "a"
    let b = boolVar "b"
    let c = boolVar "c"
    let output = boolVar "output"

    // ((a AND b) OR c) --> SET output
    let complexCond = (a &&. b) ||. c
    let stmt = complexCond --> (fn "SET" [output])
    match stmt with
    | Command(_, cond, action) ->
        cond.InferType() |> should equal DsDataType.TBool
        match action with
        | Function("SET", _) -> ()
        | _ -> failwith "Expected SET function"
    | _ -> failwith "Expected Command"

[<Fact>]
let ``Statement - Arithmetic with zero divisor`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let x = intVar "x"
    let zero = num 0
    let resultTarget = "result"

    // Division by zero is syntactically valid (runtime error)
    let stmt = (cond, x, zero) --/ resultTarget
    match stmt with
    | Command(_, _, Function("Move", _)) -> ()
    | _ -> failwith "Expected conditional DIV"

[<Fact>]
let ``Statement - Timer with zero preset`` () =
    clearVariableRegistry()
    let enable = boolVar "enable"
    let timer = "T0"
    let preset = 0

    // Timer with 0 preset is syntactically valid
    let stmt = enable --@ (timer, preset)
    match stmt with
    | Command(_, _, Function("TON", args)) ->
        args.Length |> should equal 3
    | _ -> failwith "Expected timer operation"

[<Fact>]
let ``Statement - Timer with large preset`` () =
    clearVariableRegistry()
    let enable = boolVar "enable"
    let timer = "T_Max"
    let preset = System.Int32.MaxValue

    // Timer with max preset
    let stmt = enable --@ (timer, preset)
    match stmt with
    | Command(_, _, Function("TON", args)) ->
        args.Length |> should equal 3
    | _ -> failwith "Expected timer operation"

[<Fact>]
let ``Statement - Counter with zero preset`` () =
    clearVariableRegistry()
    let trigger = boolVar "trigger"
    let counter = "C0"
    let preset = 0

    // Counter with 0 preset is syntactically valid
    let stmt = trigger --% (counter, preset)
    match stmt with
    | Command(_, _, Function("CTU", args)) ->
        args.Length |> should equal 4
    | _ -> failwith "Expected counter operation"

[<Fact>]
let ``Statement - Counter with large preset`` () =
    clearVariableRegistry()
    let trigger = boolVar "trigger"
    let counter = "C_Max"
    let preset = System.Int32.MaxValue

    // Counter with max preset
    let stmt = trigger --% (counter, preset)
    match stmt with
    | Command(_, _, Function("CTU", args)) ->
        args.Length |> should equal 4
    | _ -> failwith "Expected counter operation"

[<Fact>]
let ``Statement - Conditional Move with constant`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let constant = num 42
    let target = "output"

    // Move constant to target
    let stmt = (cond, constant) -~> target
    match stmt with
    | Command(_, c, Function("Move", [src; dst])) ->
        c |> should equal cond
        src |> should equal constant
    | _ -> failwith "Expected conditional Move"

[<Fact>]
let ``Statement - Chained assignments`` () =
    clearVariableRegistry()
    let tag1 = DsTag.Int "x"
    let tag2 = DsTag.Int "y"
    let tag3 = DsTag.Int "z"

    // Create multiple assignments
    let stmt1 = tag1 := num 1
    let stmt2 = tag2 := num 2
    let stmt3 = tag3 := num 3

    // All should be Assign statements
    match stmt1, stmt2, stmt3 with
    | Assign(_, _, _), Assign(_, _, _), Assign(_, _, _) -> ()
    | _ -> failwith "Expected all Assign statements"

[<Fact>]
let ``Statement - ReferencedVars with no variables`` () =
    clearVariableRegistry()
    let tag = DsTag.Int "output"
    let constant = num 42

    // Assignment with only constant (no variable references)
    let stmt = tag := constant
    let refs = stmt.ReferencedVars

    // Should be empty
    refs |> Seq.isEmpty |> should be True

[<Fact>]
let ``Statement - ReferencedVars with multiple variables`` () =
    clearVariableRegistry()
    let a = intVar "refvar_a"
    let b = intVar "refvar_b"
    let c = intVar "refvar_c"
    let target = DsTag.Int "refvar_result"

    // result := (a + b) + c
    let expr = (a .+. b) .+. c
    let stmt = target := expr
    let refs = stmt.ReferencedVars |> Set.ofSeq

    // Should contain a, b, c but not result
    refs |> should contain "refvar_a"
    refs |> should contain "refvar_b"
    refs |> should contain "refvar_c"
    refs |> should not' (contain "refvar_result")

[<Fact>]
let ``Statement - ReferencedVars in complex condition`` () =
    clearVariableRegistry()
    let x = intVar "x"
    let y = intVar "y"
    let flag = boolVar "flag"
    let output = boolVar "output"

    // flag AND (x > y) --> SET output
    let cond = flag &&. (x >>. y)
    let stmt = cond --> (fn "SET" [output])
    let refs = stmt.ReferencedVars |> Set.ofSeq

    // Should contain flag, x, y, output
    refs |> should contain "flag"
    refs |> should contain "x"
    refs |> should contain "y"
    refs |> should contain "output"

[<Fact>]
let ``Statement - ToText handles special characters`` () =
    clearVariableRegistry()
    let tag = DsTag.String "str_var"
    let specialStr = str "Test\nWith\tSpecial\rChars"

    let stmt = tag := specialStr
    let text = stmt.ToText()

    // Should contain assignment and variable name
    text.Contains("str_var") |> should be True
    text.Contains(":=") |> should be True

[<Fact>]
let ``Statement - Arithmetic operations with mixed types`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let intVal = intVar "x"
    let dblVal = dblVar "y"
    let resultTarget = "result"

    // Int + Double => Double (type promotion)
    let stmt = (cond, intVal, dblVal) --+ resultTarget
    match stmt with
    | Command(_, _, Function("Move", _)) -> ()
    | _ -> failwith "Expected conditional ADD"

[<Fact>]
let ``Statement - Multiple operations on same variable`` () =
    clearVariableRegistry()
    let cond = boolVar "enable"
    let x = intVar "x"
    let result1 = "r1"
    let result2 = "r2"

    // Use x in multiple operations
    let stmt1 = (cond, x, num 10) --+ result1
    let stmt2 = (cond, x, num 20) --* result2

    // Both should be valid Command statements
    match stmt1, stmt2 with
    | Command(_, _, _), Command(_, _, _) -> ()
    | _ -> failwith "Expected Command statements"

[<Property>]
let ``Statement - Assign preserves type information`` (value: int) =
    clearVariableRegistry()
    let tag = DsTag.Int "x"
    let stmt = tag := num value
    match stmt with
    | Assign(_, t, e) ->
        t.DsDataType = DsDataType.TInt && e.InferType() = DsDataType.TInt
    | _ -> false

[<Property>]
let ``Statement - Command condition must be boolean`` (value: bool) =
    clearVariableRegistry()
    let cond = bool value
    let action = fn "NOP" []
    let stmt = cond --> action
    match stmt with
    | Command(_, c, _) -> c.InferType() = DsDataType.TBool
    | _ -> false

do ()
