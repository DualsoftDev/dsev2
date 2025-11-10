namespace Ev2.Cpu.Core

open System

// ─────────────────────────────────────────────────────────────────────
// 연산자 타입 정의 (Operator Types)
// ─────────────────────────────────────────────────────────────────────
// CPU 연산자의 타입 정의만 포함
// IEC 61131-3 표준 기반 PLC 연산자
// ─────────────────────────────────────────────────────────────────────

/// 연산자 카테고리
[<RequireQualifiedAccess>]
type OperatorCategory =
    | Logical
    | Comparison
    | Arithmetic
    | Bitwise
    | Signal
    | Special

/// CPU 연산자 정의
/// PLC 프로그래밍에서 사용되는 모든 연산자를 카테고리별로 분류:
/// - 논리 연산자: AND, OR, NOT (릴레이 로직)
/// - 비교 연산자: =, <>, >, >=, <, <= (조건 판단)
/// - 산술 연산자: +, -, *, /, MOD (수치 계산)
/// - 에지 연산자: ↑, ↓ (신호 변화 감지)
/// - 데이터 이동: MOV (값 복사)
[<StructuralEquality; NoComparison>]
type DsOp =
    // 논리 연산자
    | And
    | Or
    | Not
    | Xor
    | Nand
    | Nor

    // 비교 연산자
    | Eq
    | Ne
    | Gt
    | Ge
    | Lt
    | Le

    // 산술 연산자
    | Add
    | Sub
    | Mul
    | Div
    | Mod
    | Pow

    // 비트 연산자
    | BitAnd
    | BitOr
    | BitXor
    | BitNot
    | ShiftLeft
    | ShiftRight

    // 신호/엣지 연산자
    | Rising
    | Falling
    | Edge

    // 특수 연산자
    | Assign
    | Move
    | Coalesce

        /// 문자열 표현
    override this.ToString() =
        match this with
        // 논리
        | And -> "AND" | Or -> "OR" | Not -> "NOT"
        | Xor -> "XOR" | Nand -> "NAND" | Nor -> "NOR"

        // 비교
        | Eq -> "=" | Ne -> "<>" | Gt -> ">"
        | Ge -> ">=" | Lt -> "<" | Le -> "<="

        // 산술
        | Add -> "+" | Sub -> "-" | Mul -> "*"
        | Div -> "/" | Mod -> "MOD" | Pow -> "^"

        // 비트
        | BitAnd -> "&" | BitOr -> "|" | BitXor -> "⊻"
        | BitNot -> "~" | ShiftLeft -> "<<" | ShiftRight -> ">>"

        // 신호
        | Rising -> "↑" | Falling -> "↓" | Edge -> "⇅"

        // 특수
        | Assign -> ":=" | Move -> "Move" | Coalesce -> "??"

    
// ─────────────────────────────────────────────────────────────────────
// 연산자 메타데이터 (Operator Metadata)
// ─────────────────────────────────────────────────────────────────────
// 연산자 우선순위, 카테고리, 속성 등 메타데이터 관리
// ─────────────────────────────────────────────────────────────────────

/// 연산자 메타데이터
type OperatorMetadata = {
    Symbol: string
    Aliases: string list
    Category: OperatorCategory
    Priority: int
    IsUnary: bool
    IsCommutative: bool
    Description: string
}

    
/// DsOp 타입 확장 - 메타데이터 및 속성
type DsOp with


    /// 우선순위 (높을수록 먼저 평가)
    member this.Priority =
        match this with
        // 낮은 우선순위
        | Assign -> 1
        | Coalesce -> 5
        | Or | BitOr -> 10
        | Xor | BitXor -> 15
        | And | BitAnd -> 20
        | Nor | Nand -> 25

        // 중간 우선순위
        | Eq | Ne | Gt | Ge | Lt | Le -> 30
        | ShiftLeft | ShiftRight -> 35

        // 높은 우선순위
        | Add | Sub -> 40
        | Mul | Div | Mod -> 50
        | Pow -> 55

        // 단항 연산자 (가장 높음)
        | Not | BitNot | Rising | Falling | Edge -> 60
        | Move -> 70

    /// 단항 연산자 여부
    member this.IsUnary =
        match this with
        | Not | BitNot | Rising | Falling | Edge -> true
        | _ -> false

    /// 이항 연산자 여부
    member this.IsBinary = not this.IsUnary

    /// 교환 법칙 성립 여부
    member this.IsCommutative =
        match this with
        | And | Or | Xor | Nand | Nor
        | Add | Mul
        | BitAnd | BitOr | BitXor
        | Eq | Ne -> true
        | _ -> false

    /// 연산자 카테고리
    member this.Category =
        match this with
        | And | Or | Not | Xor | Nand | Nor -> OperatorCategory.Logical
        | Eq | Ne | Gt | Ge | Lt | Le -> OperatorCategory.Comparison
        | Add | Sub | Mul | Div | Mod | Pow -> OperatorCategory.Arithmetic
        | BitAnd | BitOr | BitXor | BitNot | ShiftLeft | ShiftRight -> OperatorCategory.Bitwise
        | Rising | Falling | Edge -> OperatorCategory.Signal
        | Assign | Move | Coalesce -> OperatorCategory.Special

    /// Check if operator is logical
    member this.IsLogical =
        match this with
        | And | Or | Not | Xor | Nand | Nor -> true
        | _ -> false

    /// Check if operator is comparison
    member this.IsComparison =
        match this with
        | Eq | Ne | Gt | Ge | Lt | Le -> true
        | _ -> false

    /// Check if operator is arithmetic
    member this.IsArithmetic =
        match this with
        | Add | Sub | Mul | Div | Mod | Pow -> true
        | _ -> false

    /// Check if operator is edge detection
    member this.IsEdgeOp =
        match this with
        | Rising | Falling | Edge -> true
        | _ -> false

    /// Check if operator is right-associative
    /// Right-associative operators: a op b op c = a op (b op c)
    /// Examples: Pow (2^3^2 = 2^(3^2)), Assign (a:=b:=c means a:=(b:=c)), Coalesce (a??b??c means a??(b??c))
    member this.IsRightAssociative =
        match this with
        | Pow | Assign | Coalesce -> true
        | _ -> false

    /// Check if operator is edge detection (alias for backward compatibility)
    member this.IsEdgeDetection = this.IsEdgeOp

    /// 별칭 목록
    member this.Aliases =
        match this with
        | And -> ["AND"; "&&"; "&"]
        | Or -> ["OR"; "||"; "|"]
        | Not -> ["NOT"; "!"; "~"]
        | Xor -> ["XOR"; "⊕"]
        | Nand -> ["NAND"]
        | Nor -> ["NOR"]
        | Eq -> ["="; "=="; "EQ"]
        | Ne -> ["<>"; "!="; "NE"]
        | Gt -> [">"; "GT"]
        | Ge -> [">="; "GE"]
        | Lt -> ["<"; "LT"]
        | Le -> ["<="; "LE"]
        | Add -> ["+"; "ADD"]
        | Sub -> ["-"; "SUB"]
        | Mul -> ["*"; "MUL"]
        | Div -> ["/"; "DIV"]
        | Mod -> ["MOD"; "%"]
        | Pow -> ["^"; "POW"; "**"]
        | BitAnd -> ["BITAND"; "BAND"; "BIT_AND"]
        | BitOr -> ["BITOR"; "BOR"; "BIT_OR"]
        | BitXor -> ["BITXOR"; "BXOR"]
        | BitNot -> ["BITNOT"; "BNOT"]
        | ShiftLeft -> ["SHL"; "<<"]
        | ShiftRight -> ["SHR"; ">>"]
        | Rising -> ["RISING"; "↑"; "R_TRIG"; "RISE"]
        | Falling -> ["FALLING"; "↓"; "F_TRIG"; "FALL"]
        | Edge -> ["EDGE"; "⇅"; "ANY_EDGE"]
        | Assign -> [":="; "ASSIGN"]
        | Move -> ["MOVE"; "MOV"]
        | Coalesce -> ["??"; "COALESCE"]

    /// 설명
    member this.Description =
        match this with
        | And -> "Logical AND operation"
        | Or -> "Logical OR operation"
        | Not -> "Logical NOT operation"
        | Eq -> "Equality comparison"
        | Ne -> "Inequality comparison"
        | Gt -> "Greater than comparison"
        | Ge -> "Greater than or equal comparison"
        | Lt -> "Less than comparison"
        | Le -> "Less than or equal comparison"
        | Rising -> "Rising edge detection"
        | Falling -> "Falling edge detection"
        | Edge -> "Edge detection"
        | _ -> sprintf "%A operation" this

    /// 연산자 메타데이터
    member this.Metadata : OperatorMetadata =
        {
            Symbol = this.ToString()
            Aliases = this.Aliases
            Category = this.Category
            Priority = this.Priority
            IsUnary = this.IsUnary
            IsCommutative = this.IsCommutative
            Description = this.Description
        }




    /// 타입 검증 (타입 추론에서 사용)
    static member validateForTypes (op: DsOp) (leftType: Type option) (rightType: Type option) : Type option =
        match op with
        // None 타입이 있으면 결과도 None
        | _ when leftType.IsNone || (op.IsBinary && rightType.IsNone) -> None

        // 논리 연산자는 Bool 타입에서만 동작
        | And | Or | Xor | Nand | Nor ->
            match leftType, rightType with
            | Some t1, Some t2 when t1 = typeof<bool> && t2 = typeof<bool> -> Some typeof<bool>
            | _ -> None
        | Not ->
            match leftType with
            | Some t when t = typeof<bool> -> Some typeof<bool>
            | _ -> None

        // 비교 연산자는 호환 타입 비교시 Bool 반환
        | Eq | Ne | Gt | Ge | Lt | Le ->
            match leftType, rightType with
            // Boolean은 순서 비교 불가 (Gt, Ge, Lt, Le)
            | Some t1, Some t2 when t1 = typeof<bool> && t2 = typeof<bool> && (op = Gt || op = Ge || op = Lt || op = Le) -> None
            // 같은 타입끼리 비교
            | Some l, Some r when l = r -> Some typeof<bool>
            // 숫자 타입 간 비교 (Int ↔ Double) - 양방향 호환
            | Some t1, Some t2 when (t1 = typeof<int> && t2 = typeof<double>) || (t1 = typeof<double> && t2 = typeof<int>) -> Some typeof<bool>
            | _ -> None

        // 산술 연산자는 수치 타입에서만 동작, 타입 승격 지원
        | Add | Sub | Mul | Div | Mod | Pow ->
            match leftType, rightType with
            | Some t1, Some t2 when t1 = typeof<int> && t2 = typeof<int> -> Some typeof<int>
            | Some t1, Some t2 when t1 = typeof<double> && t2 = typeof<double> -> Some typeof<double>
            | Some t1, Some t2 when (t1 = typeof<int> && t2 = typeof<double>) || (t1 = typeof<double> && t2 = typeof<int>) -> Some typeof<double>
            | _ -> None

        // 비트 연산자는 정수 타입에서만 동작
        | BitAnd | BitOr | BitXor | ShiftLeft | ShiftRight ->
            match leftType, rightType with
            | Some t1, Some t2 when t1 = typeof<int> && t2 = typeof<int> -> Some typeof<int>
            | _ -> None
        | BitNot ->
            match leftType with
            | Some t when t = typeof<int> -> Some typeof<int>
            | _ -> None

        // 신호 연산자는 Bool 타입에서만 동작
        | Rising | Falling | Edge ->
            match leftType with
            | Some t when t = typeof<bool> -> Some typeof<bool>
            | _ -> None

        // 특수 연산자
        | Assign | Move ->
            match leftType, rightType with
            | Some l, Some r when TypeHelpers.areTypesCompatible r l -> rightType
            | _ -> None
        | Coalesce -> leftType
