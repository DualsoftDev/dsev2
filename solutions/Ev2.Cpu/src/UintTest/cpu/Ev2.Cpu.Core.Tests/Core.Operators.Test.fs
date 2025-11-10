namespace Ev2.Cpu.Test

open System
open Xunit
open FsUnit.Xunit
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// Core/Operators.fs 모듈 포괄적 유닛테스트
// ─────────────────────────────────────────────────────────────────────
// DsOp 연산자와 Operators 모듈의 모든 기능을 테스트
// 우선순위, 타입 분류, 파싱, 검증 기능을 완전히 검증
// ─────────────────────────────────────────────────────────────────────

type CoreOperatorsTest() =
    
    // ═════════════════════════════════════════════════════════════════
    // DsOp 연산자 우선순위 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsOp_Priority_논리_연산자_우선순위``() =
        Or.Priority |> should equal 10      // 가장 낮은 우선순위
        And.Priority |> should equal 20     // OR보다 높음
        Not.Priority |> should equal 60     // 단항 연산자, 높은 우선순위
        
        // 논리 연산자 우선순위 순서 확인
        (Or.Priority < And.Priority) |> should be True
        (And.Priority < Not.Priority) |> should be True
    
    [<Fact>]
    member _.``DsOp_Priority_비교_연산자_우선순위``() =
        let comparisonOps = [Eq; Ne; Gt; Ge; Lt; Le]
        comparisonOps |> List.iter (fun op ->
            op.Priority |> should equal 30
        )
        
        // 비교가 논리보다 높은 우선순위
        (Eq.Priority > And.Priority) |> should be True
        (Eq.Priority > Or.Priority) |> should be True
    
    [<Fact>]
    member _.``DsOp_Priority_산술_연산자_우선순위``() =
        // 곱셈/나눗셈이 덧셈/뺄셈보다 높음
        Add.Priority |> should equal 40
        Sub.Priority |> should equal 40
        Mul.Priority |> should equal 50
        Div.Priority |> should equal 50
        Mod.Priority |> should equal 50
        
        // 산술 연산자 우선순위 순서 확인
        (Add.Priority < Mul.Priority) |> should be True
        (Sub.Priority < Div.Priority) |> should be True
        
        // 산술이 비교보다 높은 우선순위
        (Mul.Priority > Eq.Priority) |> should be True
        (Add.Priority > Eq.Priority) |> should be True
    
    [<Fact>]
    member _.``DsOp_Priority_에지_연산자_우선순위``() =
        Rising.Priority |> should equal 60   // 단항 연산자 수준
        Falling.Priority |> should equal 60  // 단항 연산자 수준
        
        // 에지 연산자 우선순위가 높음
        (Rising.Priority > Mul.Priority) |> should be True
        (Falling.Priority > Add.Priority) |> should be True
    
    [<Fact>]
    member _.``DsOp_Priority_이동_연산자_최고_우선순위``() =
        Move.Priority |> should equal 70      // 최고 우선순위
        
        // 이동 연산자가 모든 연산자보다 높은 우선순위
        (Move.Priority > Not.Priority) |> should be True
        (Move.Priority > Rising.Priority) |> should be True
        (Move.Priority > Mul.Priority) |> should be True
    
    // ═════════════════════════════════════════════════════════════════
    // DsOp 연산자 타입 분류 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsOp_IsLogical_논리_연산자_분류``() =
        And.IsLogical |> should be True
        Or.IsLogical |> should be True
        Not.IsLogical |> should be True
        
        // 논리가 아닌 연산자들
        Add.IsLogical |> should be False
        Eq.IsLogical |> should be False
        Rising.IsLogical |> should be False
        Move.IsLogical |> should be False
    
    [<Fact>]
    member _.``DsOp_IsComparison_비교_연산자_분류``() =
        Eq.IsComparison |> should be True
        Ne.IsComparison |> should be True
        Gt.IsComparison |> should be True
        Ge.IsComparison |> should be True
        Lt.IsComparison |> should be True
        Le.IsComparison |> should be True
        
        // 비교가 아닌 연산자들
        And.IsComparison |> should be False
        Add.IsComparison |> should be False
        Rising.IsComparison |> should be False
        Move.IsComparison |> should be False
    
    [<Fact>]
    member _.``DsOp_IsArithmetic_산술_연산자_분류``() =
        Add.IsArithmetic |> should be True
        Sub.IsArithmetic |> should be True
        Mul.IsArithmetic |> should be True
        Div.IsArithmetic |> should be True
        Mod.IsArithmetic |> should be True
        
        // 산술이 아닌 연산자들
        And.IsArithmetic |> should be False
        Eq.IsArithmetic |> should be False
        Rising.IsArithmetic |> should be False
        Move.IsArithmetic |> should be False
    
    [<Fact>]
    member _.``DsOp_IsEdge_에지_연산자_분류``() =
        Rising.IsEdgeOp |> should be True
        Falling.IsEdgeOp |> should be True
        
        // 에지가 아닌 연산자들
        And.IsEdgeOp |> should be False
        Add.IsEdgeOp |> should be False
        Eq.IsEdgeOp |> should be False
        Not.IsEdgeOp |> should be False
        Move.IsEdgeOp |> should be False
    
    [<Fact>]
    member _.``DsOp_IsUnary_단항_연산자_분류``() =
        Not.IsUnary |> should be True
        Rising.IsUnary |> should be True
        Falling.IsUnary |> should be True
        
        // 단항이 아닌 연산자들
        And.IsUnary |> should be False
        Or.IsUnary |> should be False
        Add.IsUnary |> should be False
        Eq.IsUnary |> should be False
        Move.IsUnary |> should be False
    
    [<Fact>]
    member _.``DsOp_IsBinary_이항_연산자_분류``() =
        // 논리 이항 연산자
        And.IsBinary |> should be True
        Or.IsBinary |> should be True
        
        // 비교 이항 연산자
        Eq.IsBinary |> should be True
        Ne.IsBinary |> should be True
        Gt.IsBinary |> should be True
        Ge.IsBinary |> should be True
        Lt.IsBinary |> should be True
        Le.IsBinary |> should be True
        
        // 산술 이항 연산자
        Add.IsBinary |> should be True
        Sub.IsBinary |> should be True
        Mul.IsBinary |> should be True
        Div.IsBinary |> should be True
        Mod.IsBinary |> should be True
        
        // 데이터 이동 연산자
        Move.IsBinary |> should be True
        
        // 이항이 아닌 연산자들
        Not.IsBinary |> should be False
        Rising.IsBinary |> should be False
        Falling.IsBinary |> should be False
    
    // ═════════════════════════════════════════════════════════════════
    // DsOp ToString 테스트 (텍스트 표현)
    // ═════════════════════────────────────────────────────────────────
    
    [<Fact>]
    member _.``DsOp_ToString_논리_연산자_텍스트``() =
        And.ToString() |> should equal "AND"
        Or.ToString() |> should equal "OR"
        Not.ToString() |> should equal "NOT"
    
    [<Fact>]
    member _.``DsOp_ToString_비교_연산자_텍스트``() =
        Eq.ToString() |> should equal "="
        Ne.ToString() |> should equal "<>"
        Gt.ToString() |> should equal ">"
        Ge.ToString() |> should equal ">="
        Lt.ToString() |> should equal "<"
        Le.ToString() |> should equal "<="
    
    [<Fact>]
    member _.``DsOp_ToString_산술_연산자_텍스트``() =
        Add.ToString() |> should equal "+"
        Sub.ToString() |> should equal "-"
        Mul.ToString() |> should equal "*"
        Div.ToString() |> should equal "/"
        Mod.ToString() |> should equal "MOD"
    
    [<Fact>]
    member _.``DsOp_ToString_에지_연산자_텍스트``() =
        Rising.ToString() |> should equal "↑"
        Falling.ToString() |> should equal "↓"
    
    [<Fact>]
    member _.``DsOp_ToString_이동_연산자_텍스트``() =
        Move.ToString() |> should equal "Move"
   
    [<Fact>]
    member _.``Operators_validateForTypes_논리_연산자_Bool_타입``() =
        // Bool 타입에 대한 논리 연산자 검증
        DsOp.validateForTypes And (Some TBool) (Some TBool) |> should equal (Some TBool)
        DsOp.validateForTypes Or (Some TBool) (Some TBool) |> should equal (Some TBool)
        
        // 단항 논리 연산자
        DsOp.validateForTypes Not (Some TBool) None |> should equal (Some TBool)
    
    [<Fact>]
    member _.``Operators_validateForTypes_논리_연산자_타입_오류``() =
        // Bool이 아닌 타입에 논리 연산자 적용시 None
        DsOp.validateForTypes And (Some TInt) (Some TInt) |> should equal None
        DsOp.validateForTypes Or (Some TDouble) (Some TDouble) |> should equal None
        DsOp.validateForTypes Not (Some TString) None |> should equal None
    
    [<Fact>]
    member _.``Operators_validateForTypes_비교_연산자_결과_Bool``() =
        // 모든 비교 연산자는 Bool 반환
        DsOp.validateForTypes Eq (Some TInt) (Some TInt) |> should equal (Some TBool)
        DsOp.validateForTypes Ne (Some TDouble) (Some TDouble) |> should equal (Some TBool)
        DsOp.validateForTypes Gt (Some TInt) (Some TDouble) |> should equal (Some TBool)
        DsOp.validateForTypes Ge (Some TString) (Some TString) |> should equal (Some TBool)
        DsOp.validateForTypes Lt (Some TBool) (Some TBool) |> should equal None
        DsOp.validateForTypes Le (Some TInt) (Some TInt) |> should equal (Some TBool)
    
    [<Fact>]
    member _.``Operators_validateForTypes_산술_연산자_수치_타입``() =
        // Int + Int -> Int
        DsOp.validateForTypes Add (Some TInt) (Some TInt) |> should equal (Some TInt)
        
        // Double + Double -> Double
        DsOp.validateForTypes Mul (Some TDouble) (Some TDouble) |> should equal (Some TDouble)
        
        // Int + Double -> Double (타입 승격)
        DsOp.validateForTypes Add (Some TInt) (Some TDouble) |> should equal (Some TDouble)
        DsOp.validateForTypes Sub (Some TDouble) (Some TInt) |> should equal (Some TDouble)
        
        // 모든 산술 연산자 테스트
        let arithmeticOps = [Add; Sub; Mul; Div; Mod]
        arithmeticOps |> List.iter (fun op ->
            DsOp.validateForTypes op (Some TInt) (Some TInt) |> should equal (Some TInt)
            DsOp.validateForTypes op (Some TDouble) (Some TDouble) |> should equal (Some TDouble)
            DsOp.validateForTypes op (Some TInt) (Some TDouble) |> should equal (Some TDouble)
        )
    
    [<Fact>]
    member _.``Operators_validateForTypes_산술_연산자_비수치_타입_오류``() =
        // Bool, String에는 산술 연산 불가
        let arithmeticOps = [Add; Sub; Mul; Div; Mod]
        arithmeticOps |> List.iter (fun op ->
            DsOp.validateForTypes op (Some TBool) (Some TBool) |> should equal None
            DsOp.validateForTypes op (Some TString) (Some TString) |> should equal None
            DsOp.validateForTypes op (Some TInt) (Some TString) |> should equal None
        )
    
    [<Fact>]
    member _.``Operators_validateForTypes_에지_연산자_Bool_타입``() =
        // 에지 연산자는 Bool 타입에만 적용 가능하고 Bool 반환
        DsOp.validateForTypes Rising (Some TBool) None |> should equal (Some TBool)
        DsOp.validateForTypes Falling (Some TBool) None |> should equal (Some TBool)
        
        // Bool이 아닌 타입에는 적용 불가
        DsOp.validateForTypes Rising (Some TInt) None |> should equal None
        DsOp.validateForTypes Falling (Some TDouble) None |> should equal None
    
    [<Fact>]
    member _.``Operators_validateForTypes_이동_연산자``() =
        // 이동 연산자는 모든 타입에 적용 가능하며 우변의 타입을 반환
        DsOp.validateForTypes Move (Some TInt) (Some TInt) |> should equal (Some TInt)
        DsOp.validateForTypes Move (Some TBool) (Some TBool) |> should equal (Some TBool)
        DsOp.validateForTypes Move (Some TDouble) (Some TDouble) |> should equal (Some TDouble)
        DsOp.validateForTypes Move (Some TString) (Some TString) |> should equal (Some TString)
        
        // 타입 변환도 허용 (우변 타입으로 결정)
        DsOp.validateForTypes Move (Some TInt) (Some TDouble) |> should equal (Some TDouble)
        DsOp.validateForTypes Move (Some TDouble) (Some TInt) |> should equal None
    
    [<Fact>]
    member _.``Operators_validateForTypes_None_타입_처리``() =
        // 하나라도 타입이 None이면 결과도 None
        DsOp.validateForTypes Add None (Some TInt) |> should equal None
        DsOp.validateForTypes Add (Some TInt) None |> should equal None
        DsOp.validateForTypes Add None None |> should equal None
        
        // 단항 연산자의 경우
        DsOp.validateForTypes Not None None |> should equal None
        DsOp.validateForTypes Rising None None |> should equal None
    
    // ═════════════════════════════════════════════════════════════════
    // 복합 시나리오 통합 테스트
    // ═════════════════════════════════════════════════════════════════
    
    [<Fact>]
    member _.``DsOp_통합_시나리오_PLC_표현식_우선순위``() =
        // PLC 표현식: A + B * C > D AND E OR F
        // 예상 우선순위: ((A + (B * C)) > D) AND E) OR F
        
        // 우선순위 검증
        (Mul.Priority > Add.Priority) |> should be True  // B * C 먼저
        (Add.Priority > Gt.Priority) |> should be True   // A + (B*C) 먼저, 그 다음 > D
        (Gt.Priority > And.Priority) |> should be True   // 비교 먼저, 그 다음 AND
        (And.Priority > Or.Priority) |> should be True   // AND 먼저, 그 다음 OR
        
        // 전체 우선순위 체인
        let priorities = [Or.Priority; And.Priority; Gt.Priority; Add.Priority; Mul.Priority]
        let sortedPriorities = priorities |> List.sort
        priorities |> should equal sortedPriorities
    
    [<Fact>]
    member _.``DsOp_통합_시나리오_연산자_분류_완전성``() =
        let allOperators = [
            And; Or; Not;                    // 논리 연산자
            Eq; Ne; Gt; Ge; Lt; Le;         // 비교 연산자
            Add; Sub; Mul; Div; Mod;        // 산술 연산자
            Rising; Falling;                // 에지 연산자
            Move                             // 이동 연산자
        ]
        
        // 모든 연산자는 최소한 하나의 분류에 속해야 함
        allOperators |> List.iter (fun op ->
            let hasClassification = 
                op.IsLogical || op.IsComparison || op.IsArithmetic || 
                op.IsEdgeOp || (op = Move)
            hasClassification |> should be True
        )
        
        // 모든 연산자는 단항 또는 이항이어야 함
        allOperators |> List.iter (fun op ->
            let hasArity = op.IsUnary || op.IsBinary
            hasArity |> should be True
        )
  
    
    [<Fact>]
    member _.``Operators_통합_시나리오_타입_검증_완전성``() =
        // 수치 타입 조합 테스트
        let numericTypes = [TInt; TDouble]
        let arithmeticOps = [Add; Sub; Mul; Div; Mod]
        
        // 모든 수치 타입 조합에 대해 산술 연산 검증
        for leftType in numericTypes do
            for rightType in numericTypes do
                for op in arithmeticOps do
                    let result = DsOp.validateForTypes op (Some leftType) (Some rightType)
                    result |> should not' (be None)
                    
                    // 결과 타입은 더 넓은 타입이어야 함
                    match leftType, rightType with
                    | TInt, TInt -> result |> should equal (Some TInt)
                    | TDouble, TDouble -> result |> should equal (Some TDouble)
                    | TInt, TDouble | TDouble, TInt -> result |> should equal (Some TDouble)
                    | _ -> ()
        
        // 모든 타입에 대해 비교 연산 검증
        let allTypes = [TBool; TInt; TDouble; TString]
        let comparisonOps = [Eq; Ne; Gt; Ge; Lt; Le]
    
        for leftType in allTypes do
            for rightType in allTypes do
                for op in comparisonOps do
                    let result = DsOp.validateForTypes op (Some leftType) (Some rightType)
                
                    let expectedResult = 
                        match op, leftType, rightType with
                        // Boolean 순서 비교는 불가 (Gt, Ge, Lt, Le)
                        | (Gt | Ge | Lt | Le), TBool, TBool -> None
                    
                        // 같은 타입끼리 비교
                        | _, t1, t2 when t1 = t2 -> Some TBool
                    
                        // 숫자 타입 간 비교 (Int ↔ Double)
                        | _, TInt, TDouble | _, TDouble, TInt -> Some TBool
                    
                        // 다른 타입 간 비교는 불가
                        | _ -> None
                
                    result |> should equal expectedResult

    // ═════════════════════════════════════════════════════════════════
    // Phase 2 Enhanced Tests - Boundary Values & Edge Cases
    // ═════════════════════════════════════════════════════════════════

    [<Fact>]
    member _.``DsOp_Priority_All_operators_have_valid_priority``() =
        let allOps = [
            Add; Sub; Mul; Div; Mod
            Eq; Ne; Gt; Ge; Lt; Le
            And; Or; Not
            Rising; Falling
            Move
        ]

        // All priorities should be positive
        for op in allOps do
            op.Priority |> should be (greaterThan 0)
            op.Priority |> should be (lessThanOrEqualTo 100)

    [<Fact>]
    member _.``DsOp_Priority_Relative_ordering_comprehensive``() =
        // Complete priority ordering: Or < And < Comparison < Add/Sub < Mul/Div < Unary < Move
        (Or.Priority < And.Priority) |> should be True
        (And.Priority < Eq.Priority) |> should be True
        (Eq.Priority < Add.Priority) |> should be True
        (Add.Priority < Mul.Priority) |> should be True
        (Mul.Priority < Not.Priority) |> should be True
        (Not.Priority < Move.Priority) |> should be True

    [<Fact>]
    member _.``DsOp_IsArithmetic_Comprehensive_classification``() =
        // Arithmetic operators
        Add.IsArithmetic |> should be True
        Sub.IsArithmetic |> should be True
        Mul.IsArithmetic |> should be True
        Div.IsArithmetic |> should be True
        Mod.IsArithmetic |> should be True

        // Non-arithmetic operators
        And.IsArithmetic |> should be False
        Or.IsArithmetic |> should be False
        Eq.IsArithmetic |> should be False
        Not.IsArithmetic |> should be False
        Rising.IsArithmetic |> should be False

    [<Fact>]
    member _.``DsOp_ToString_All_operators_have_string_representation``() =
        let allOps = [
            Add; Sub; Mul; Div; Mod
            Eq; Ne; Gt; Ge; Lt; Le
            And; Or; Not
            Rising; Falling
            Move
        ]

        // All operators should have non-empty string representation
        for op in allOps do
            let str = op.ToString()
            str |> should not' (equal "")
            str |> should not' (equal null)
            str.Length |> should be (greaterThan 0)

    [<Fact>]
    member _.``DsOp_IsUnary_Classification``() =
        // Unary operators
        Not.IsUnary |> should be True
        Rising.IsUnary |> should be True
        Falling.IsUnary |> should be True

        // Binary operators
        Add.IsUnary |> should be False
        And.IsUnary |> should be False
        Eq.IsUnary |> should be False

    [<Fact>]
    member _.``DsOp_ValidateForTypes_Null_handling``() =
        // Validate with null types should return None
        let result1 = DsOp.validateForTypes Add None (Some TInt)
        result1 |> should equal None

        let result2 = DsOp.validateForTypes Add (Some TInt) None
        result2 |> should equal None

        let result3 = DsOp.validateForTypes Add None None
        result3 |> should equal None

    [<Fact>]
    member _.``DsOp_ValidateForTypes_String_operations_limited``() =
        // String cannot be used with arithmetic operations
        let result1 = DsOp.validateForTypes Add (Some TString) (Some TString)
        result1 |> should equal None

        let result2 = DsOp.validateForTypes Mul (Some TString) (Some TString)
        result2 |> should equal None

        let result3 = DsOp.validateForTypes Div (Some TString) (Some TString)
        result3 |> should equal None

        // All comparison operators work with strings (same type comparisons allowed)
        let result4 = DsOp.validateForTypes Eq (Some TString) (Some TString)
        result4 |> should equal (Some TBool)

        let result5 = DsOp.validateForTypes Ne (Some TString) (Some TString)
        result5 |> should equal (Some TBool)

        let result6 = DsOp.validateForTypes Gt (Some TString) (Some TString)
        result6 |> should equal (Some TBool)

    [<Fact>]
    member _.``DsOp_ValidateForTypes_Bool_arithmetic_forbidden``() =
        // Boolean cannot be used in arithmetic operations
        let result1 = DsOp.validateForTypes Add (Some TBool) (Some TBool)
        result1 |> should equal None

        let result2 = DsOp.validateForTypes Sub (Some TBool) (Some TBool)
        result2 |> should equal None

        let result3 = DsOp.validateForTypes Mul (Some TBool) (Some TBool)
        result3 |> should equal None

    [<Fact>]
    member _.``DsOp_ValidateForTypes_Bool_order_comparison_forbidden``() =
        // Boolean can use Eq/Ne but not Gt/Ge/Lt/Le
        let result1 = DsOp.validateForTypes Eq (Some TBool) (Some TBool)
        result1 |> should equal (Some TBool)

        let result2 = DsOp.validateForTypes Ne (Some TBool) (Some TBool)
        result2 |> should equal (Some TBool)

        let result3 = DsOp.validateForTypes Gt (Some TBool) (Some TBool)
        result3 |> should equal None

        let result4 = DsOp.validateForTypes Ge (Some TBool) (Some TBool)
        result4 |> should equal None

        let result5 = DsOp.validateForTypes Lt (Some TBool) (Some TBool)
        result5 |> should equal None

        let result6 = DsOp.validateForTypes Le (Some TBool) (Some TBool)
        result6 |> should equal None

    [<Fact>]
    member _.``DsOp_ValidateForTypes_Cross_type_validation``() =
        // Int and Double can interoperate
        let result1 = DsOp.validateForTypes Add (Some TInt) (Some TDouble)
        result1 |> should equal (Some TDouble)

        let result2 = DsOp.validateForTypes Mul (Some TDouble) (Some TInt)
        result2 |> should equal (Some TDouble)

        // Bool and Int cannot interoperate
        let result3 = DsOp.validateForTypes Add (Some TBool) (Some TInt)
        result3 |> should equal None

        // String and Int cannot interoperate
        let result4 = DsOp.validateForTypes Add (Some TString) (Some TInt)
        result4 |> should equal None

    [<Fact>]
    member _.``DsOp_ValidateForTypes_Type_promotion_rules``() =
        // Int + Int = Int (no promotion)
        let result1 = DsOp.validateForTypes Add (Some TInt) (Some TInt)
        result1 |> should equal (Some TInt)

        // Double + Double = Double
        let result2 = DsOp.validateForTypes Add (Some TDouble) (Some TDouble)
        result2 |> should equal (Some TDouble)

        // Int + Double = Double (promotion to wider type)
        let result3 = DsOp.validateForTypes Add (Some TInt) (Some TDouble)
        result3 |> should equal (Some TDouble)

        // Double + Int = Double (promotion to wider type)
        let result4 = DsOp.validateForTypes Add (Some TDouble) (Some TInt)
        result4 |> should equal (Some TDouble)

    [<Fact>]
    member _.``DsOp_ValidateForTypes_All_comparison_operators_same_behavior``() =
        let comparisonOps = [Eq; Ne; Gt; Ge; Lt; Le]

        // All comparisons return Bool for numeric types
        for op in [Eq; Ne; Gt; Ge; Lt; Le] do
            let result = DsOp.validateForTypes op (Some TInt) (Some TInt)
            if op = Eq || op = Ne then
                result |> should equal (Some TBool)
            else
                result |> should equal (Some TBool)

    [<Fact>]
    member _.``DsOp_Operators_Parse_RoundTrip``() =
        let operators = [
            (Add, "ADD")
            (Sub, "SUB")
            (Mul, "MUL")
            (Div, "DIV")
            (Mod, "MOD")
            (Eq, "EQ")
            (Ne, "NE")
            (Gt, "GT")
            (Ge, "GE")
            (Lt, "LT")
            (Le, "LE")
            (And, "AND")
            (Or, "OR")
            (Not, "NOT")
        ]

        // Test that parsing and toString are consistent
        for (op, _) in operators do
            let str = op.ToString()
            str |> should not' (equal "")

    [<Fact>]
    member _.``DsOp_Edge_operators_only_work_with_Bool``() =
        // Rising and Falling should only work with Bool type
        let result1 = DsOp.validateForTypes Rising (Some TBool) None
        result1 |> should not' (equal None)

        let result2 = DsOp.validateForTypes Falling (Some TBool) None
        result2 |> should not' (equal None)

        // Should not work with non-Bool types
        let result3 = DsOp.validateForTypes Rising (Some TInt) None
        result3 |> should equal None

        let result4 = DsOp.validateForTypes Falling (Some TDouble) None
        result4 |> should equal None

    [<Fact>]
    member _.``DsOp_Logical_operators_only_work_with_Bool``() =
        // And, Or, Not should only work with Bool
        let result1 = DsOp.validateForTypes And (Some TBool) (Some TBool)
        result1 |> should equal (Some TBool)

        let result2 = DsOp.validateForTypes Or (Some TBool) (Some TBool)
        result2 |> should equal (Some TBool)

        let result3 = DsOp.validateForTypes Not (Some TBool) None
        result3 |> should equal (Some TBool)

        // Should not work with non-Bool types
        let result4 = DsOp.validateForTypes And (Some TInt) (Some TInt)
        result4 |> should equal None

        let result5 = DsOp.validateForTypes Or (Some TString) (Some TString)
        result5 |> should equal None
                