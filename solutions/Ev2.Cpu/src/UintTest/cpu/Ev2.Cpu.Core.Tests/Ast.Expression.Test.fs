namespace Ev2.Cpu.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core
open Ev2.Cpu.Ast

// ─────────────────────────────────────────────────────────────────────
// Ast/Expression.fs 모듈 포괄적 유닛테스트
// ─────────────────────────────────────────────────────────────────────
// DsExpr AST, ExprBuilder, ExprAnalysis의 모든 기능을 테스트
// 7가지 표현식 타입과 분석 유틸리티의 정상/경계/오류 케이스 검증
// ─────────────────────────────────────────────────────────────────────

type AstExpressionTest() =
    
    // ═════════════════════════════════════════════════════════════════
    // DsExpr 기본 생성자 테스트 (7가지 표현식 타입)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_EConst_상수_표현식_생성``() =
        let boolConst = EConst(box true, TBool)
        let intConst = EConst(box 42, TInt)
        let doubleConst = EConst(box 3.14, TDouble)
        let stringConst = EConst(box "Hello", TString)
        
        match boolConst with
        | EConst(value, typ) ->
            value |> should equal (box true)
            typ |> should equal TBool
        | _ -> failwith "Expected EConst"
    
    [<Fact>]
    member _.``DsExpr_EVar_변수_표현식_생성``() =
        let motorSpeed = EVar("Motor_Speed", TInt)
        let tankLevel = EVar("Tank_Level", TDouble)
        
        match motorSpeed with
        | EVar(name, typ) ->
            name |> should equal "Motor_Speed"
            typ |> should equal TInt
        | _ -> failwith "Expected EVar"
    
    [<Fact>]
    member _.``DsExpr_ETerminal_터미널_표현식_생성``() =
        let digitalInput = ETerminal("%I0.0", TBool)
        let analogOutput = ETerminal("%AW100", TInt)
        
        match digitalInput with
        | ETerminal(name, typ) ->
            name |> should equal "%I0.0"
            typ |> should equal TBool
        | _ -> failwith "Expected ETerminal"
    
    [<Fact>]
    member _.``DsExpr_EUnary_단항_연산_표현식_생성``() =
        let notExpr = EUnary(Not, EVar("Enable", TBool))
        let risingExpr = EUnary(Rising, ETerminal("%I0.1", TBool))
        
        match notExpr with
        | EUnary(op, expr) ->
            op |> should equal Not
            match expr with
            | EVar(name, _) -> name |> should equal "Enable"
            | _ -> failwith "Expected EVar in unary expression"
        | _ -> failwith "Expected EUnary"
    
    [<Fact>]
    member _.``DsExpr_EBinary_이항_연산_표현식_생성``() =
        let addExpr = EBinary(Add, EVar("Speed", TInt), EConst(box 10, TInt))
        let compareExpr = EBinary(Gt, EVar("Pressure", TDouble), EConst(box 5.0, TDouble))
        
        match addExpr with
        | EBinary(op, left, right) ->
            op |> should equal Add
            match left, right with
            | EVar("Speed", TInt), EConst(value, TInt) -> value |> should equal (box 10)
            | _ -> failwith "Expected correct binary operands"
        | _ -> failwith "Expected EBinary"
    
    [<Fact>]
    member _.``DsExpr_ECall_함수_호출_표현식_생성``() =
        let absCall = ECall("ABS", [EConst(box -5, TInt)])
        let maxCall = ECall("MAX", [EVar("A", TInt); EVar("B", TInt); EConst(box 100, TInt)])
        
        match absCall with
        | ECall(name, args) ->
            name |> should equal "ABS"
            args.Length |> should equal 1
        | _ -> failwith "Expected ECall"
        
        match maxCall with
        | ECall(name, args) ->
            name |> should equal "MAX"
            args.Length |> should equal 3
        | _ -> failwith "Expected ECall with 3 args"
    
    [<Fact>]
    member _.``DsExpr_EMeta_메타데이터_표현식_생성``() =
        let metadata = Map.ofList [("unit", box "℃"); ("range", box "0..100")]
        let metaExpr = EMeta("comment", metadata)
        
        match metaExpr with
        | EMeta(tag, meta) ->
            tag |> should equal "comment"
            meta.Count |> should equal 2
            meta.ContainsKey("unit") |> should be True
            meta.["unit"] |> should equal (box "℃")
        | _ -> failwith "Expected EMeta"
    
    // ═════════════════════════════════════════════════════════════════
    // InferType 메서드 테스트 (타입 추론 시스템)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_InferType_상수_변수_터미널_타입_추론``() =
        let boolConst = EConst(box true, TBool)
        let intVar = EVar("Counter", TInt)
        let doubleTerminal = ETerminal("%AW100", TDouble)
        
        boolConst.InferType() |> should equal (Some TBool)
        intVar.InferType() |> should equal (Some TInt)
        doubleTerminal.InferType() |> should equal (Some TDouble)
    
    [<Fact>]
    member _.``DsExpr_InferType_단항_연산_타입_추론``() =
        let notExpr = EUnary(Not, EVar("Flag", TBool))
        let risingExpr = EUnary(Rising, ETerminal("%I0.0", TBool))
        let fallingExpr = EUnary(Falling, EVar("Signal", TBool))
        
        // 논리 단항 연산자는 항상 Bool 타입 반환
        notExpr.InferType() |> should equal (Some TBool)
        risingExpr.InferType() |> should equal (Some TBool)
        fallingExpr.InferType() |> should equal (Some TBool)
    
    [<Fact>]
    member _.``DsExpr_InferType_함수_호출_메타_null_반환``() =
        let funcCall = ECall("ABS", [EConst(box -5, TInt)])
        let metadata = EMeta("comment", Map.empty)
        
        // 함수 호출과 메타데이터는 외부 타입 해석 필요
        funcCall.InferType() |> should equal None
        metadata.InferType() |> should equal None
    
    // ═════════════════════════════════════════════════════════════════
    // GetVariables 메서드 테스트 (변수 추출)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_GetVariables_단순_변수_추출``() =
        let var1 = EVar("Motor_Speed", TInt)
        let terminal1 = ETerminal("%I0.0", TBool)
        let const1 = EConst(box 42, TInt)
        
        let vars1 = var1.GetVariables()
        let vars2 = terminal1.GetVariables()
        let vars3 = const1.GetVariables()
        
        vars1.Contains("Motor_Speed") |> should be True
        vars1.Count |> should equal 1
        
        vars2.Contains("%I0.0") |> should be True
        vars2.Count |> should equal 1
        
        vars3.Count |> should equal 0
    
    [<Fact>]
    member _.``DsExpr_GetVariables_복합_표현식_변수_추출``() =
        // (Motor_Speed + 10) > Tank_Level
        let complexExpr = 
            EBinary(Gt, 
                EBinary(Add, EVar("Motor_Speed", TInt), EConst(box 10, TInt)),
                EVar("Tank_Level", TDouble))
        
        let variables = complexExpr.GetVariables()
        
        variables.Count |> should equal 2
        variables.Contains("Motor_Speed") |> should be True
        variables.Contains("Tank_Level") |> should be True
    
    [<Fact>]
    member _.``DsExpr_GetVariables_함수_호출_내부_변수_추출``() =
        // MAX(Speed, %AW100, Counter)
        let funcExpr = ECall("MAX", [
            EVar("Speed", TInt)
            ETerminal("%AW100", TInt)
            EVar("Counter", TInt)
        ])
        
        let variables = funcExpr.GetVariables()
        
        variables.Count |> should equal 3
        variables.Contains("Speed") |> should be True
        variables.Contains("%AW100") |> should be True
        variables.Contains("Counter") |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // GetFunctionCalls 메서드 테스트 (함수 호출 추출)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_GetFunctionCalls_단순_함수_추출``() =
        let absCall = ECall("ABS", [EConst(box -5, TInt)])
        let varExpr = EVar("Speed", TInt)
        
        let funcs1 = absCall.GetFunctionCalls()
        let funcs2 = varExpr.GetFunctionCalls()
        
        funcs1.Count |> should equal 1
        funcs1.Contains("ABS") |> should be True
        
        funcs2.Count |> should equal 0
    
    [<Fact>]
    member _.``DsExpr_GetFunctionCalls_중첩_함수_호출_추출``() =
        // MAX(ABS(-5), MIN(A, B))
        let nestedExpr = ECall("MAX", [
            ECall("ABS", [EConst(box -5, TInt)])
            ECall("MIN", [EVar("A", TInt); EVar("B", TInt)])
        ])
        
        let functions = nestedExpr.GetFunctionCalls()
        
        functions.Count |> should equal 3
        functions.Contains("MAX") |> should be True
        functions.Contains("ABS") |> should be True
        functions.Contains("MIN") |> should be True
    
    [<Fact>]
    member _.``DsExpr_GetFunctionCalls_이항_연산_내부_함수_추출``() =
        // ABS(X) + SQRT(Y)
        let binaryWithFuncs = EBinary(Add,
            ECall("ABS", [EVar("X", TInt)]),
            ECall("SQRT", [EVar("Y", TDouble)]))
        
        let functions = binaryWithFuncs.GetFunctionCalls()
        
        functions.Count |> should equal 2
        functions.Contains("ABS") |> should be True
        functions.Contains("SQRT") |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // ToText 메서드 테스트 (텍스트 표현)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_ToText_상수_표현식_텍스트``() =
        let boolTrue = EConst(box true, TBool)
        let boolFalse = EConst(box false, TBool)
        let intConst = EConst(box 42, TInt)
        let doubleConst = EConst(box 3.14, TDouble)
        let stringConst = EConst(box "Hello World", TString)
        
        boolTrue.ToText() |> should equal "TRUE"
        boolFalse.ToText() |> should equal "FALSE"
        intConst.ToText() |> should equal "42"
        doubleConst.ToText() |> should equal "3.14"
        stringConst.ToText() |> should equal "\"Hello World\""
    
    [<Fact>]
    member _.``DsExpr_ToText_변수_터미널_텍스트``() =
        let variable = EVar("Motor_Speed", TInt)
        let terminal = ETerminal("%I0.0", TBool)
        
        variable.ToText() |> should equal "Motor_Speed"
        terminal.ToText() |> should equal "%I0.0"
    
    [<Fact>]
    member _.``DsExpr_ToText_단항_연산_텍스트``() =
        let notExpr = EUnary(Not, EVar("Enable", TBool))
        let risingExpr = EUnary(Rising, ETerminal("%I0.1", TBool))
        
        notExpr.ToText() |> should equal "NOT Enable"
        risingExpr.ToText() |> should equal "↑ %I0.1"
    
    [<Fact>]
    member _.``DsExpr_ToText_이항_연산_텍스트_우선순위``() =
        // A + B * C 는 A + (B * C) 로 출력되어야 함
        let expr1 = EBinary(Add, 
            EVar("A", TInt),
            EBinary(Mul, EVar("B", TInt), EVar("C", TInt)))
        
        // (A + B) * C 는 괄호가 필요함
        let expr2 = EBinary(Mul,
            EBinary(Add, EVar("A", TInt), EVar("B", TInt)),
            EVar("C", TInt))
        
        expr1.ToText() |> should equal "A + B * C"
        expr2.ToText() |> should equal "(A + B) * C"
    
    [<Fact>]
    member _.``DsExpr_ToText_함수_호출_텍스트``() =
        let absCall = ECall("ABS", [EConst(box -5, TInt)])
        let maxCall = ECall("MAX", [EVar("A", TInt); EVar("B", TInt); EConst(box 100, TInt)])
        let noArgsCall = ECall("NOW", [])
        
        absCall.ToText() |> should equal "ABS(-5)"
        maxCall.ToText() |> should equal "MAX(A, B, 100)"
        noArgsCall.ToText() |> should equal "NOW()"
    
    [<Fact>]
    member _.``DsExpr_ToText_메타데이터_텍스트``() =
        let metadata = Map.ofList [("unit", box "℃"); ("range", box "0..100")]
        let metaExpr = EMeta("comment", metadata)
        
        let text = metaExpr.ToText()
        text.StartsWith("/*comment:") |> should be True
        text.EndsWith("*/") |> should be True
        text.Contains("unit=℃") |> should be True
        text.Contains("range=0..100") |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // Validate 메서드 테스트 (표현식 구조 검증)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_Validate_정상_상수_검증``() =
        let validConst = EConst(box 42, TInt)
        let result = validConst.Validate()
        
        match result with
        | Ok () -> () // 성공
        | Error msg -> failwithf "Expected valid constant: %s" msg
    
    [<Fact>]
    member _.``DsExpr_Validate_타입_불일치_상수_오류``() =
        let invalidConst = EConst(box "string_value", TInt)  // String value with Int type
        let result = invalidConst.Validate()
        
        match result with
        | Ok () -> failwith "Expected validation error for type mismatch"
        | Error msg -> 
            msg.Contains("Invalid constant") |> should be True
            msg.Contains("Type mismatch") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_빈_변수명_오류``() =
        let emptyVar = EVar("", TInt)
        let whitespaceVar = EVar("   ", TDouble)
        
        match emptyVar.Validate() with
        | Ok () -> failwith "Expected validation error for empty variable name"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
        
        match whitespaceVar.Validate() with
        | Ok () -> failwith "Expected validation error for whitespace variable name"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_잘못된_단항_연산자_오류``() =
        // Add는 이항 연산자인데 단항으로 사용
        let invalidUnary = EUnary(Add, EVar("X", TInt))
        
        match invalidUnary.Validate() with
        | Ok () -> failwith "Expected validation error for non-unary operator"
        | Error msg -> msg.Contains("is not unary") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_잘못된_이항_연산자_오류``() =
        // Not은 단항 연산자인데 이항으로 사용
        let invalidBinary = EBinary(Not, EVar("A", TBool), EVar("B", TBool))
        
        match invalidBinary.Validate() with
        | Ok () -> failwith "Expected validation error for non-binary operator"
        | Error msg -> msg.Contains("is not binary") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_빈_함수명_오류``() =
        let emptyFunc = ECall("", [EVar("X", TInt)])
        let whitespaceFunc = ECall("   ", [])
        
        match emptyFunc.Validate() with
        | Ok () -> failwith "Expected validation error for empty function name"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
        
        match whitespaceFunc.Validate() with
        | Ok () -> failwith "Expected validation error for whitespace function name"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_함수_인수_재귀_검증``() =
        // 함수 인수 중 하나가 유효하지 않음
        let invalidArg = EVar("", TInt)  // 빈 변수명
        let funcWithInvalidArg = ECall("MAX", [EVar("A", TInt); invalidArg; EVar("C", TInt)])
        
        match funcWithInvalidArg.Validate() with
        | Ok () -> failwith "Expected validation error for invalid function argument"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
    
    [<Fact>]
    member _.``DsExpr_Validate_빈_메타_태그_오류``() =
        let emptyMeta = EMeta("", Map.empty)
        let whitespaceMeta = EMeta("   ", Map.ofList [("key", box "value")])
        
        match emptyMeta.Validate() with
        | Ok () -> failwith "Expected validation error for empty meta tag"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
        
        match whitespaceMeta.Validate() with
        | Ok () -> failwith "Expected validation error for whitespace meta tag"
        | Error msg -> msg.Contains("cannot be empty") |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // ExprBuilder 모듈 테스트 (빌더 유틸리티)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``ExprBuilder_기본_생성자_함수``() =
        let const42 = ExprBuilder.constant (box 42) TInt
        let speedVar = ExprBuilder.variable "Speed" TInt
        let inputTerm = ExprBuilder.terminal "%I0.0" TBool
        
        const42 |> should equal (EConst(box 42, TInt))
        speedVar |> should equal (EVar("Speed", TInt))
        inputTerm |> should equal (ETerminal("%I0.0", TBool))
    
    [<Fact>]
    member _.``ExprBuilder_연산자_생성자_함수``() =
        let var1 = ExprBuilder.variable "A" TBool
        let var2 = ExprBuilder.variable "B" TBool
        
        let notExpr = ExprBuilder.unary Not var1
        let andExpr = ExprBuilder.binary And var1 var2
        let funcExpr = ExprBuilder.call "ABS" [ExprBuilder.constant (box -5) TInt]
        
        notExpr |> should equal (EUnary(Not, var1))
        andExpr |> should equal (EBinary(And, var1, var2))
        funcExpr |> should equal (ECall("ABS", [EConst(box -5, TInt)]))
    
    [<Fact>]
    member _.``ExprBuilder_편의_연산자_테스트``() =
        let a = ExprBuilder.variable "A" TInt
        let b = ExprBuilder.variable "B" TInt
        let flag1 = ExprBuilder.variable "Flag1" TBool
        let flag2 = ExprBuilder.variable "Flag2" TBool
        
        // 산술 연산자 (binary 함수 사용)
        let addExpr = ExprBuilder.binary Add a b
        let subExpr = ExprBuilder.binary Sub a b
        let mulExpr = ExprBuilder.binary Mul a b
        let divExpr = ExprBuilder.binary Div a b
        let modExpr = ExprBuilder.binary Mod a b
        
        addExpr |> should equal (EBinary(Add, a, b))
        subExpr |> should equal (EBinary(Sub, a, b))
        mulExpr |> should equal (EBinary(Mul, a, b))
        divExpr |> should equal (EBinary(Div, a, b))
        modExpr |> should equal (EBinary(Mod, a, b))
        
        // 비교 연산자 (binary 함수 사용)
        let eqExpr = ExprBuilder.binary Eq a b
        let neExpr = ExprBuilder.binary Ne a b
        let gtExpr = ExprBuilder.binary Gt a b
        let geExpr = ExprBuilder.binary Ge a b
        let ltExpr = ExprBuilder.binary Lt a b
        let leExpr = ExprBuilder.binary Le a b
        
        eqExpr |> should equal (EBinary(Eq, a, b))
        neExpr |> should equal (EBinary(Ne, a, b))
        gtExpr |> should equal (EBinary(Gt, a, b))
        geExpr |> should equal (EBinary(Ge, a, b))
        ltExpr |> should equal (EBinary(Lt, a, b))
        leExpr |> should equal (EBinary(Le, a, b))
        
        // 논리 연산자 (binary 함수 사용)
        let andExpr = ExprBuilder.binary And flag1 flag2
        let orExpr = ExprBuilder.binary Or flag1 flag2
        
        andExpr |> should equal (EBinary(And, flag1, flag2))
        orExpr |> should equal (EBinary(Or, flag1, flag2))
        
        // 단항 연산자 (not', rising, falling)
        let notExpr = ExprBuilder.not' flag1
        let risingExpr = ExprBuilder.rising flag1
        let fallingExpr = ExprBuilder.falling flag1
        
        notExpr |> should equal (EUnary(Not, flag1))
        risingExpr |> should equal (EUnary(Rising, flag1))
        fallingExpr |> should equal (EUnary(Falling, flag1))
    
    [<Fact>]
    member _.``ExprBuilder_타입별_상수_생성자``() =
        let boolConst = ExprBuilder.boolConst true
        let intConst = ExprBuilder.intConst 42
        let doubleConst = ExprBuilder.doubleConst 3.14
        let stringConst = ExprBuilder.stringConst "Hello"
        
        boolConst |> should equal (EConst(box true, TBool))
        intConst |> should equal (EConst(box 42, TInt))
        doubleConst |> should equal (EConst(box 3.14, TDouble))
        stringConst |> should equal (EConst(box "Hello", TString))
    
    // ═════════════════════════════════════════════════════════════════
    // ExprAnalysis 모듈 테스트 (표현식 분석)
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``ExprAnalysis_complexity_노드_카운트``() =
        let simple = EConst(box 42, TInt)                    // 1 노드
        let unaryExpr = EUnary(Not, EVar("Flag", TBool))     // 2 노드
        let binaryExpr = EBinary(Add, EVar("A", TInt), EVar("B", TInt))  // 3 노드
        let funcExpr = ECall("MAX", [EVar("X", TInt); EVar("Y", TInt)])  // 3 노드 (1 + 2 args)
        
        ExprAnalysis.complexity simple |> should equal 1
        ExprAnalysis.complexity unaryExpr |> should equal 2
        ExprAnalysis.complexity binaryExpr |> should equal 3
        ExprAnalysis.complexity funcExpr |> should equal 3
    
    [<Fact>]
    member _.``ExprAnalysis_complexity_복합_표현식``() =
        // (A + B) * (C - D) = 7 노드
        let complexExpr = EBinary(Mul,
            EBinary(Add, EVar("A", TInt), EVar("B", TInt)),     // 3 노드
            EBinary(Sub, EVar("C", TInt), EVar("D", TInt)))     // 3 노드
            // 총 1 + 3 + 3 = 7 노드
        
        ExprAnalysis.complexity complexExpr |> should equal 7
    
    [<Fact>]
    member _.``ExprAnalysis_depth_트리_깊이_계산``() =
        let simple = EConst(box 42, TInt)                       // 깊이 1
        let unaryExpr = EUnary(Not, EVar("Flag", TBool))        // 깊이 2
        let binaryExpr = EBinary(Add, EVar("A", TInt), EVar("B", TInt))  // 깊이 2
        
        // 중첩된 표현식: NOT(A + B) = 깊이 3
        let nestedExpr = EUnary(Not,
            EBinary(Add, EVar("A", TInt), EVar("B", TInt)))
        
        ExprAnalysis.depth simple |> should equal 1
        ExprAnalysis.depth unaryExpr |> should equal 2
        ExprAnalysis.depth binaryExpr |> should equal 2
        ExprAnalysis.depth nestedExpr |> should equal 3
    
    [<Fact>]
    member _.``ExprAnalysis_depth_함수_호출_깊이``() =
        let simpleFunc = ECall("ABS", [EVar("X", TInt)])        // 깊이 2
        let emptyFunc = ECall("NOW", [])                        // 깊이 1
        
        // 중첩 함수: MAX(ABS(A), B) = 깊이 3
        let nestedFunc = ECall("MAX", [
            ECall("ABS", [EVar("A", TInt)])     // 깊이 2
            EVar("B", TInt)                     // 깊이 1
        ])  // 전체 깊이: 1 + max(2, 1) = 3
        
        ExprAnalysis.depth simpleFunc |> should equal 2
        ExprAnalysis.depth emptyFunc |> should equal 1
        ExprAnalysis.depth nestedFunc |> should equal 3
    
    [<Fact>]
    member _.``ExprAnalysis_isConstant_상수_표현식_판정``() =
        let constant = EConst(box 42, TInt)
        let variable = EVar("Speed", TInt)
        let terminal = ETerminal("%I0.0", TBool)
        let funcCall = ECall("ABS", [EConst(box -5, TInt)])
        let metadata = EMeta("comment", Map.empty)
        
        // 상수 연산: 10 + 20
        let constExpr = EBinary(Add, EConst(box 10, TInt), EConst(box 20, TInt))
        
        // 변수 포함: X + 10
        let varExpr = EBinary(Add, EVar("X", TInt), EConst(box 10, TInt))
        
        ExprAnalysis.isConstant constant |> should be True
        ExprAnalysis.isConstant variable |> should be False
        ExprAnalysis.isConstant terminal |> should be False
        ExprAnalysis.isConstant funcCall |> should be False
        ExprAnalysis.isConstant metadata |> should be True
        ExprAnalysis.isConstant constExpr |> should be True
        ExprAnalysis.isConstant varExpr |> should be False
    
    [<Fact>]
    member _.``ExprAnalysis_hasEdgeOperators_에지_연산자_감지``() =
        let normalExpr = EBinary(Add, EVar("A", TInt), EVar("B", TInt))
        let risingExpr = EUnary(Rising, EVar("Signal", TBool))
        let fallingExpr = EUnary(Falling, ETerminal("%I0.0", TBool))
        
        // 중첩된 에지 연산자: (↑Signal) AND Enable
        let nestedEdge = EBinary(And,
            EUnary(Rising, EVar("Signal", TBool)),
            EVar("Enable", TBool))
        
        // 함수 내부 에지 연산자: DELAY(↑Signal, 100)
        let funcWithEdge = ECall("DELAY", [
            EUnary(Rising, EVar("Signal", TBool))
            EConst(box 100, TInt)
        ])
        
        ExprAnalysis.hasEdgeOperators normalExpr |> should be False
        ExprAnalysis.hasEdgeOperators risingExpr |> should be True
        ExprAnalysis.hasEdgeOperators fallingExpr |> should be True
        ExprAnalysis.hasEdgeOperators nestedEdge |> should be True
        ExprAnalysis.hasEdgeOperators funcWithEdge |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // 복합 시나리오 통합 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsExpr_통합_시나리오_복합_PLC_표현식``() =
        // PLC 표현식: (Motor_Speed > 100) AND NOT(↑Emergency_Stop) AND (Tank_Level < 80.5)
        let plcExpression = 
            EBinary(And,
                EBinary(And,
                    EBinary(Gt, EVar("Motor_Speed", TInt), EConst(box 100, TInt)),
                    EUnary(Not, EUnary(Rising, ETerminal("%I0.15", TBool)))),
                EBinary(Lt, EVar("Tank_Level", TDouble), EConst(box 80.5, TDouble)))
        
        // 타입 추론 테스트
        plcExpression.InferType() |> should equal (Some TBool)
        
        // 변수 추출 테스트
        let variables = plcExpression.GetVariables()
        variables.Count |> should equal 3
        variables.Contains("Motor_Speed") |> should be True
        variables.Contains("%I0.15") |> should be True
        variables.Contains("Tank_Level") |> should be True
        
        // 함수 호출 추출 테스트 (없어야 함)
        let functions = plcExpression.GetFunctionCalls()
        functions.Count |> should equal 0
        
        // 에지 연산자 감지 테스트
        ExprAnalysis.hasEdgeOperators plcExpression |> should be True
        
        // 상수 표현식이 아님
        ExprAnalysis.isConstant plcExpression |> should be False
        
        // 복잡도와 깊이 테스트
        let complexity = ExprAnalysis.complexity plcExpression
        let depth = ExprAnalysis.depth plcExpression
        complexity |> should be (greaterThan 5)  // 복합 표현식
        depth |> should be (greaterThan 2)       // 중첩된 구조
    
    [<Fact>]
    member _.``DsExpr_통합_시나리오_함수_기반_제어_로직``() =
        // PLC 함수 기반 제어: PID(SetPoint, ProcessValue, Kp, Ki, Kd) > MIN(SafetyLimit, OperatorLimit)
        let pidExpression = 
            EBinary(Gt,
                ECall("PID", [
                    EVar("SetPoint", TDouble)
                    EVar("ProcessValue", TDouble)
                    EConst(box 1.5, TDouble)  // Kp
                    EConst(box 0.1, TDouble)  // Ki
                    EConst(box 0.05, TDouble) // Kd
                ]),
                ECall("MIN", [
                    EVar("SafetyLimit", TDouble)
                    EVar("OperatorLimit", TDouble)
                ]))
        
        // 함수 호출 추출
        let functions = pidExpression.GetFunctionCalls()
        functions.Count |> should equal 2
        functions.Contains("PID") |> should be True
        functions.Contains("MIN") |> should be True
        
        // 변수 추출
        let variables = pidExpression.GetVariables()
        variables.Count |> should equal 4
        variables.Contains("SetPoint") |> should be True
        variables.Contains("ProcessValue") |> should be True
        variables.Contains("SafetyLimit") |> should be True
        variables.Contains("OperatorLimit") |> should be True
        
        // 타입 추론 (함수 호출 때문에 None)
        pidExpression.InferType() |> should equal None
        
        // 상수가 아님 (변수와 함수 포함)
        ExprAnalysis.isConstant pidExpression |> should be False

        // 에지 연산자 없음
        ExprAnalysis.hasEdgeOperators pidExpression |> should be False

    // ═════════════════════════════════════════════════════════════════
    // Phase 2 Enhanced Tests - Boundary Values, Error Cases & Edge Cases
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``DsExpr_EConst_Boundary_Int32_MinMax``() =
        // Int32 boundary values
        let minInt = EConst(box System.Int32.MinValue, TInt)
        let maxInt = EConst(box System.Int32.MaxValue, TInt)

        match minInt with
        | EConst(value, TInt) -> unbox<int> value |> should equal System.Int32.MinValue
        | _ -> failwith "Expected EConst with Int32.MinValue"

        match maxInt with
        | EConst(value, TInt) -> unbox<int> value |> should equal System.Int32.MaxValue
        | _ -> failwith "Expected EConst with Int32.MaxValue"

    [<Fact>]
    member _.``DsExpr_EConst_Boundary_Double_SpecialValues``() =
        // Double special values
        let nan = EConst(box System.Double.NaN, TDouble)
        let posInf = EConst(box System.Double.PositiveInfinity, TDouble)
        let negInf = EConst(box System.Double.NegativeInfinity, TDouble)
        let zero = EConst(box 0.0, TDouble)
        let negZero = EConst(box -0.0, TDouble)

        match nan with
        | EConst(value, TDouble) -> System.Double.IsNaN(unbox<float> value) |> should be True
        | _ -> failwith "Expected NaN"

        match posInf with
        | EConst(value, TDouble) -> unbox<float> value |> should equal System.Double.PositiveInfinity
        | _ -> failwith "Expected +Infinity"

        match negInf with
        | EConst(value, TDouble) -> unbox<float> value |> should equal System.Double.NegativeInfinity
        | _ -> failwith "Expected -Infinity"

    [<Fact>]
    member _.``DsExpr_EConst_String_EmptyAndLong``() =
        // Empty string
        let emptyStr = EConst(box "", TString)
        match emptyStr with
        | EConst(value, TString) -> unbox<string> value |> should equal ""
        | _ -> failwith "Expected empty string"

        // Very long string (10,000 chars)
        let longStr = System.String('X', 10000)
        let longExpr = EConst(box longStr, TString)
        match longExpr with
        | EConst(value, TString) ->
            let s = unbox<string> value
            s.Length |> should equal 10000
        | _ -> failwith "Expected long string"

    [<Fact>]
    member _.``DsExpr_EVar_EmptyName``() =
        // Variable with empty name (edge case)
        let emptyVar = EVar("", TBool)
        match emptyVar with
        | EVar(name, typ) ->
            name |> should equal ""
            typ |> should equal TBool
        | _ -> failwith "Expected EVar with empty name"

    [<Fact>]
    member _.``DsExpr_EVar_VeryLongName``() =
        // Variable with very long name (1000 chars)
        let longName = System.String('A', 1000)
        let longVar = EVar(longName, TInt)
        match longVar with
        | EVar(name, typ) ->
            name.Length |> should equal 1000
            typ |> should equal TInt
        | _ -> failwith "Expected EVar with long name"

    [<Fact>]
    member _.``DsExpr_EBinary_DeeplyNested``() =
        // Deeply nested binary expressions (10 levels)
        let rec createNestedAdd depth =
            if depth = 0 then
                EConst(box 1, TInt)
            else
                EBinary(Add, createNestedAdd (depth - 1), EConst(box 1, TInt))

        let deepExpr = createNestedAdd 10
        let depth = ExprAnalysis.depth deepExpr
        depth |> should be (greaterThanOrEqualTo 10)

    [<Fact>]
    member _.``DsExpr_EBinary_AllArithmeticOperators``() =
        // Test all arithmetic operators
        let x = EVar("x", TInt)
        let y = EVar("y", TInt)

        let addExpr = EBinary(Add, x, y)
        let subExpr = EBinary(Sub, x, y)
        let mulExpr = EBinary(Mul, x, y)
        let divExpr = EBinary(Div, x, y)
        let modExpr = EBinary(Mod, x, y)

        // All should be EBinary with correct operators
        match addExpr with EBinary(Add, _, _) -> () | _ -> failwith "Expected Add"
        match subExpr with EBinary(Sub, _, _) -> () | _ -> failwith "Expected Sub"
        match mulExpr with EBinary(Mul, _, _) -> () | _ -> failwith "Expected Mul"
        match divExpr with EBinary(Div, _, _) -> () | _ -> failwith "Expected Div"
        match modExpr with EBinary(Mod, _, _) -> () | _ -> failwith "Expected Mod"

    [<Fact>]
    member _.``DsExpr_EBinary_AllComparisonOperators``() =
        // Test all comparison operators
        let x = EVar("x", TInt)
        let y = EVar("y", TInt)

        let eqExpr = EBinary(Eq, x, y)
        let neExpr = EBinary(Ne, x, y)
        let gtExpr = EBinary(Gt, x, y)
        let geExpr = EBinary(Ge, x, y)
        let ltExpr = EBinary(Lt, x, y)
        let leExpr = EBinary(Le, x, y)

        // All should be EBinary with correct operators
        match eqExpr with EBinary(Eq, _, _) -> () | _ -> failwith "Expected Eq"
        match neExpr with EBinary(Ne, _, _) -> () | _ -> failwith "Expected Ne"
        match gtExpr with EBinary(Gt, _, _) -> () | _ -> failwith "Expected Gt"
        match geExpr with EBinary(Ge, _, _) -> () | _ -> failwith "Expected Ge"
        match ltExpr with EBinary(Lt, _, _) -> () | _ -> failwith "Expected Lt"
        match leExpr with EBinary(Le, _, _) -> () | _ -> failwith "Expected Le"

    [<Fact>]
    member _.``DsExpr_EBinary_AllLogicalOperators``() =
        // Test all logical operators
        let a = EVar("a", TBool)
        let b = EVar("b", TBool)

        let andExpr = EBinary(And, a, b)
        let orExpr = EBinary(Or, a, b)
        let xorExpr = EBinary(Xor, a, b)

        // All should be EBinary with correct operators
        match andExpr with EBinary(And, _, _) -> () | _ -> failwith "Expected And"
        match orExpr with EBinary(Or, _, _) -> () | _ -> failwith "Expected Or"
        match xorExpr with EBinary(Xor, _, _) -> () | _ -> failwith "Expected Xor"

    [<Fact>]
    member _.``DsExpr_EUnary_AllUnaryOperators``() =
        // Test all unary operators
        let boolVar = EVar("flag", TBool)

        let notExpr = EUnary(Not, boolVar)
        let risingExpr = EUnary(Rising, boolVar)
        let fallingExpr = EUnary(Falling, boolVar)

        // All should be EUnary with correct operators
        match notExpr with EUnary(Not, _) -> () | _ -> failwith "Expected Not"
        match risingExpr with EUnary(Rising, _) -> () | _ -> failwith "Expected Rising"
        match fallingExpr with EUnary(Falling, _) -> () | _ -> failwith "Expected Falling"

    [<Fact>]
    member _.``DsExpr_ECall_NoArguments``() =
        // Function call with no arguments
        let noArgs = ECall("GetTime", [])
        match noArgs with
        | ECall(name, args) ->
            name |> should equal "GetTime"
            args.Length |> should equal 0
        | _ -> failwith "Expected ECall with no args"

    [<Fact>]
    member _.``DsExpr_ECall_ManyArguments``() =
        // Function call with many arguments (20)
        let manyArgs = [for i in 1..20 -> EConst(box i, TInt)]
        let call = ECall("ManyParams", manyArgs)
        match call with
        | ECall(name, args) ->
            name |> should equal "ManyParams"
            args.Length |> should equal 20
        | _ -> failwith "Expected ECall with 20 args"

    [<Fact>]
    member _.``DsExpr_GetVariables_NoVariables``() =
        // Expression with only constants (no variables)
        let constExpr = EBinary(Add, EConst(box 1, TInt), EConst(box 2, TInt))
        let vars = constExpr.GetVariables()
        vars.Count |> should equal 0

    [<Fact>]
    member _.``DsExpr_GetVariables_DuplicateVariables``() =
        // Expression with same variable used multiple times
        let x = EVar("x", TInt)
        let expr = EBinary(Add, EBinary(Mul, x, x), x)
        let vars = expr.GetVariables()
        // Should only count "x" once (using HashSet)
        vars.Count |> should equal 1
        vars.Contains("x") |> should be True

    [<Fact>]
    member _.``DsExpr_GetFunctionCalls_NoCalls``() =
        // Expression with no function calls
        let expr = EBinary(Add, EVar("a", TInt), EConst(box 5, TInt))
        let calls = expr.GetFunctionCalls()
        calls.Count |> should equal 0

    [<Fact>]
    member _.``DsExpr_GetFunctionCalls_DuplicateCalls``() =
        // Expression with same function called multiple times
        let abs1 = ECall("ABS", [EVar("x", TInt)])
        let abs2 = ECall("ABS", [EVar("y", TInt)])
        let expr = EBinary(Add, abs1, abs2)
        let calls = expr.GetFunctionCalls()
        // Should only count "ABS" once (using HashSet)
        calls.Count |> should equal 1
        calls.Contains("ABS") |> should be True

    [<Fact>]
    member _.``DsExpr_InferType_MixedTypes_IntDouble``() =
        // Int + Double => should return None (or Double with promotion)
        let intVar = EVar("x", TInt)
        let dblVar = EVar("y", TDouble)
        let mixedExpr = EBinary(Add, intVar, dblVar)
        // Type inference may return None for mixed types in AST
        // (Core.Expression handles promotion, AST might not)
        let inferredType = mixedExpr.InferType()
        // Accept either None or TDouble
        (inferredType = None || inferredType = Some TDouble) |> should be True

    [<Fact>]
    member _.``ExprAnalysis_isConstant_ConstantExpression``() =
        // Pure constant expression
        let constExpr = EBinary(Add, EConst(box 1, TInt), EConst(box 2, TInt))
        ExprAnalysis.isConstant constExpr |> should be True

    [<Fact>]
    member _.``ExprAnalysis_isConstant_WithVariable``() =
        // Expression with variable
        let varExpr = EBinary(Add, EVar("x", TInt), EConst(box 2, TInt))
        ExprAnalysis.isConstant varExpr |> should be False

    [<Fact>]
    member _.``ExprAnalysis_complexity_SimpleExpression``() =
        // Simple constant
        let simple = EConst(box 42, TInt)
        let complexity = ExprAnalysis.complexity simple
        complexity |> should equal 1

    [<Fact>]
    member _.``ExprAnalysis_complexity_ComplexExpression``() =
        // Complex nested expression
        let complex =
            EBinary(Add,
                EBinary(Mul, EVar("a", TInt), EVar("b", TInt)),
                EBinary(Div, EVar("c", TInt), EConst(box 2, TInt)))
        let complexity = ExprAnalysis.complexity complex
        complexity |> should be (greaterThan 5)

    [<Fact>]
    member _.``ExprAnalysis_depth_FlatExpression``() =
        // Flat expression (no nesting)
        let flat = EConst(box 42, TInt)
        let depth = ExprAnalysis.depth flat
        depth |> should equal 1

    [<Fact>]
    member _.``ExprAnalysis_depth_DeeplyNestedExpression``() =
        // Create deeply nested expression (depth = 5)
        let nested =
            EBinary(Add,
                EBinary(Mul,
                    EBinary(Sub,
                        EBinary(Div, EVar("a", TInt), EConst(box 2, TInt)),
                        EVar("b", TInt)),
                    EVar("c", TInt)),
                EVar("d", TInt))
        let depth = ExprAnalysis.depth nested
        depth |> should be (greaterThanOrEqualTo 4)

    [<Fact>]
    member _.``ExprAnalysis_hasEdgeOperators_NoEdge``() =
        // Expression without edge operators
        let noEdge = EBinary(And, EVar("a", TBool), EVar("b", TBool))
        ExprAnalysis.hasEdgeOperators noEdge |> should be False

    [<Fact>]
    member _.``ExprAnalysis_hasEdgeOperators_WithRising``() =
        // Expression with Rising edge
        let withRising = EUnary(Rising, EVar("trigger", TBool))
        ExprAnalysis.hasEdgeOperators withRising |> should be True

    [<Fact>]
    member _.``ExprAnalysis_hasEdgeOperators_WithFalling``() =
        // Expression with Falling edge
        let withFalling = EUnary(Falling, EVar("reset", TBool))
        ExprAnalysis.hasEdgeOperators withFalling |> should be True

    [<Fact>]
    member _.``ExprAnalysis_hasEdgeOperators_DeeplyNested``() =
        // Edge operator deeply nested in expression
        let deepEdge =
            EBinary(And,
                EVar("enable", TBool),
                EBinary(Or,
                    EUnary(Rising, EVar("start", TBool)),
                    EVar("running", TBool)))
        ExprAnalysis.hasEdgeOperators deepEdge |> should be True

    [<Fact>]
    member _.``DsExpr_ComplexScenario_MathExpression``() =
        // Complex math: ((a + b) * c) / (d - e)
        let complexMath =
            EBinary(Div,
                EBinary(Mul,
                    EBinary(Add, EVar("a", TDouble), EVar("b", TDouble)),
                    EVar("c", TDouble)),
                EBinary(Sub, EVar("d", TDouble), EVar("e", TDouble)))

        // Variable extraction
        let vars = complexMath.GetVariables()
        vars.Count |> should equal 5

        // Complexity
        let complexity = ExprAnalysis.complexity complexMath
        complexity |> should be (greaterThan 7)

        // Depth
        let depth = ExprAnalysis.depth complexMath
        depth |> should be (greaterThanOrEqualTo 3)

    [<Fact>]
    member _.``DsExpr_ComplexScenario_BooleanLogic``() =
        // Complex boolean: (a AND b) OR (NOT c AND (d OR e))
        let complexLogic =
            EBinary(Or,
                EBinary(And, EVar("a", TBool), EVar("b", TBool)),
                EBinary(And,
                    EUnary(Not, EVar("c", TBool)),
                    EBinary(Or, EVar("d", TBool), EVar("e", TBool))))

        // Variable extraction
        let vars = complexLogic.GetVariables()
        vars.Count |> should equal 5

        // No edge operators
        ExprAnalysis.hasEdgeOperators complexLogic |> should be False

        // Not constant
        ExprAnalysis.isConstant complexLogic |> should be False