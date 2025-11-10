namespace Ev2.Cpu.Ast

open System
open Ev2.Cpu.Core

// ─────────────────────────────────────────────────────────────────────
// 표현식 추상 구문 트리 (Expression Abstract Syntax Tree)
// ─────────────────────────────────────────────────────────────────────
// PLC 표현식을 구문 분석하여 실행 가능한 트리 구조로 변환
// 모든 PLC 표현식 (상수, 변수, 연산, 함수 호출)을 통합 관리
// 타입 정보와 함께 저장되어 컴파일 시점에 타입 검증 수행
// ─────────────────────────────────────────────────────────────────────

/// <summary>표현식 추상 구문 트리 (Expression AST)</summary>
/// <remarks>
/// PLC에서 사용되는 모든 표현식을 8가지 기본 타입으로 분류:
/// - EConst: 상수값 (리터럴)
/// - EVar: 메모리 변수 (읽기/쓰기 가능)
/// - EUnary: 단항 연산 (NOT, 에지 등)
/// - EBinary: 이항 연산 (산술, 논리, 비교)
/// - ECall: 내장 함수 호출
/// - EUserFC: 사용자 정의 함수 호출
/// - ETerminal: I/O 터미널 (입출력 점)
/// - EMeta: 메타데이터 (디버깅/문서화)
///
/// 타입 정보를 함께 저장하여 컴파일 시점 타입 검증이 가능합니다.
/// </remarks>
[<StructuralEquality; NoComparison>]
type DsExpr =
    /// 상수값 (Constant)
    /// 프로그램에 하드코딩된 값 - 실행 시 변경되지 않음
    /// 예: 123, 45.67, TRUE, "Hello"
    | EConst of value:obj * typ:Type

    /// 메모리 변수 (Variable)
    /// PLC 메모리에 저장된 값 - 런타임에 읽기/쓰기 가능
    /// 예: Motor_Speed, Tank_Level, Alarm_Active
    | EVar of name:string * typ:Type

    /// 단항 연산 (Unary Operation)
    /// 하나의 피연산자에 적용되는 연산 - NOT, 에지 검출 등
    /// 예: NOT Start_Button, ↑ Enable_Signal
    | EUnary of op:DsOp * expr:DsExpr

    /// 이항 연산 (Binary Operation)
    /// 두 개의 피연산자에 적용되는 연산 - 산술, 논리, 비교
    /// 예: Speed + 10, Pressure > 5.0, Enable AND Ready
    | EBinary of op:DsOp * left:DsExpr * right:DsExpr

    /// 함수 호출 (Function Call)
    /// 내장 함수 호출
    /// 예: ABS(-5), MAX(A, B, C), PID(SP, PV, Kp, Ki, Kd)
    | ECall of funcName:string * args:DsExpr list

    /// 사용자 정의 함수 호출 (User-Defined Function Call)
    /// UserFC 호출 - 이름, 인자, 반환 타입, 시그니처 포함
    /// 예: LinearScale(input, 0.0, 100.0) → 반환타입: DOUBLE
    | EUserFC of fcName:string * args:DsExpr list * returnType:Type option * signature:string

    /// I/O 터미널 (Terminal)
    /// 물리적 입출력 점에 연결된 신호
    /// 예: %I0.0 (디지털 입력), %AW100 (아날로그 출력)
    | ETerminal of termName:string * typ:Type

    /// 메타데이터 (Metadata)
    /// 디버깅, 문서화, 시뮬레이션을 위한 추가 정보
    /// 예: /*comment:설명*/, /*unit:℃*/, /*range:0..100*/
    | EMeta of tag:string * metadata:Map<string,obj>
    with
        /// <summary>표현식의 타입 추론</summary>
        /// <returns>추론된 데이터 타입 (추론 불가 시 None)</returns>
        /// <remarks>
        /// 타입 추론 규칙:
        /// - EConst, EVar, ETerminal: 저장된 타입 반환
        /// - EUnary (NOT, Rising, Falling): Bool
        /// - EBinary: 연산자와 피연산자 타입으로 결정
        /// - ECall: 외부 타입 resolution 필요 (None)
        /// - EUserFC: 저장된 반환 타입 반환
        /// - EMeta: 타입 없음 (None)
        /// </remarks>
        member this.InferType() : Type option =
            match this with
            | EConst(_, t) | EVar(_, t) | ETerminal(_, t) -> Some t

            | EUnary(op, expr) ->
                let exprType = expr.InferType()
                match op with
                | Not | Rising | Falling -> Some typeof<bool>
                | BitNot -> exprType  // Bitwise NOT preserves numeric type
                | _ -> None

            | EBinary(op, left, right) ->
                let leftType = left.InferType()
                let rightType = right.InferType()
                DsOp.validateForTypes op leftType rightType

            | ECall(funcName, args) ->
                // Cannot infer here - Functions module not yet compiled.
                // Type inference for function calls happens in AstValidation
                None
            | EUserFC(_, _, returnType, _) -> returnType  // UserFC has stored return type
            | EMeta _ -> None  // Metadata doesn't have runtime type

        /// <summary>표현식에서 참조되는 모든 변수 목록</summary>
        /// <returns>변수 이름 집합 (EVar 및 ETerminal)</returns>
        member this.GetVariables() : Set<string> =
            let rec collect acc expr =
                match expr with
                | EVar(name, _) -> Set.add name acc
                | ETerminal(name, _) -> Set.add name acc
                | EUnary(_, x) -> collect acc x
                | EBinary(_, l, r) -> collect (collect acc l) r
                | ECall(_, args) -> args |> List.fold collect acc
                | _ -> acc
            collect Set.empty this

        /// <summary>표현식에 포함된 모든 함수 호출 목록</summary>
        /// <returns>함수 이름 집합 (ECall 및 EUserFC)</returns>
        member this.GetFunctionCalls() : Set<string> =
            let rec collect acc expr =
                match expr with
                | ECall(name, args) ->
                    let childCalls = args |> List.fold collect acc
                    Set.add name childCalls
                | EUserFC(name, args, _, _) ->
                    let childCalls = args |> List.fold collect acc
                    Set.add name childCalls
                | EUnary(_, x) -> collect acc x
                | EBinary(_, l, r) -> collect (collect acc l) r
                | _ -> acc
            collect Set.empty this

        /// <summary>표현식을 사람이 읽을 수 있는 텍스트로 변환</summary>
        /// <param name="withParens">전체를 괄호로 감쌀지 여부 (optional, 기본값: false)</param>
        /// <returns>텍스트 표현 (예: "Speed + 10", "NOT Enable")</returns>
        /// <remarks>
        /// 연산자 우선순위를 고려하여 필요한 괄호만 추가합니다.
        /// </remarks>
        member this.ToText(?withParens: bool) : string =
            let needParens = defaultArg withParens false
            let wrap s = if needParens then sprintf "(%s)" s else s
            
            let rec go expr precedence =
                match expr with
                | EConst(v, _) ->
                    match v with
                    | :? string as s -> sprintf "\"%s\"" s
                    | :? bool as b -> if b then "TRUE" else "FALSE"
                    | _ -> string v

                | EVar(name, _) | ETerminal(name, _) -> name

                | EUnary(op, x) ->
                    let result = sprintf "%O %s" op (go x op.Priority)
                    if precedence > op.Priority then sprintf "(%s)" result else result

                | EBinary(op, l, r) ->
                    let leftExpr = go l op.Priority
                    let rightExpr = go r (op.Priority + 1)
                    let result = sprintf "%s %O %s" leftExpr op rightExpr
                    if precedence > op.Priority then sprintf "(%s)" result else result

                | ECall(fname, args) ->
                    let argTexts = args |> List.map (fun arg -> go arg 0) |> String.concat ", "
                    sprintf "%s(%s)" fname argTexts

                | EUserFC(fname, args, _, _) ->
                    let argTexts = args |> List.map (fun arg -> go arg 0) |> String.concat ", "
                    sprintf "UserFC_%s(%s)" fname argTexts

                | EMeta(tag, meta) ->
                    let metaText = 
                        meta 
                        |> Map.toSeq 
                        |> Seq.map (fun (k, v) -> sprintf "%s=%s" k (v.ToString())) 
                        |> String.concat "; "
                    sprintf "/*%s:%s*/" tag metaText

            wrap (go this 0)

        /// <summary>표현식 구조 및 타입 검증</summary>
        /// <returns>검증 성공 시 Ok (), 실패 시 Error with 오류 메시지</returns>
        /// <remarks>
        /// 검증 항목:
        /// - 상수값이 타입과 일치하는지
        /// - 변수/터미널/함수 이름이 비어있지 않은지
        /// - 연산자가 올바르게 사용되었는지 (unary/binary)
        /// - UserFC 시그니처가 유효한지
        /// </remarks>
        member this.Validate() : Result<unit, string> =
            let rec validate expr =
                match expr with
                | EConst(value, typ) ->
                    try
                        TypeHelpers.validateType typ value |> ignore
                        Ok ()
                    with
                    | ex -> Error (sprintf "Invalid constant: %s" ex.Message)

                | EVar(name, _) | ETerminal(name, _) ->
                    if String.IsNullOrWhiteSpace name then
                        Error "Variable/terminal name cannot be empty"
                    else Ok ()

                | EUnary(op, expr) ->
                    match validate expr with
                    | Error e -> Error e
                    | Ok () ->
                        if not op.IsUnary then
                            Error (sprintf "Operator %O is not unary" op)
                        else Ok ()

                | EBinary(op, left, right) ->
                    match validate left, validate right with
                    | Error e, _ | _, Error e -> Error e
                    | Ok (), Ok () ->
                        if not op.IsBinary then
                            Error (sprintf "Operator %O is not binary" op)
                        else Ok ()

                | ECall(name, args) ->
                    if String.IsNullOrWhiteSpace name then
                        Error "Function name cannot be empty"
                    else
                        args |> List.tryPick (fun arg ->
                            match validate arg with
                            | Error e -> Some e
                            | Ok () -> None
                        ) |> Option.map Error |> Option.defaultValue (Ok ())

                | EUserFC(name, args, _, signature) ->
                    if String.IsNullOrWhiteSpace name then
                        Error "UserFC name cannot be empty"
                    elif String.IsNullOrWhiteSpace signature then
                        Error (sprintf "UserFC '%s' has invalid signature" name)
                    else
                        args |> List.tryPick (fun arg ->
                            match validate arg with
                            | Error e -> Some e
                            | Ok () -> None
                        ) |> Option.map Error |> Option.defaultValue (Ok ())

                | EMeta(tag, _) ->
                    if String.IsNullOrWhiteSpace tag then
                        Error "Meta tag cannot be empty"
                    else Ok ()

            validate this

/// <summary>표현식 생성 유틸리티 모듈</summary>
/// <remarks>
/// DsExpr 생성을 위한 편리한 헬퍼 함수들을 제공합니다.
/// </remarks>
module ExprBuilder =

    /// <summary>상수 표현식 생성</summary>
    let constant (value: obj) (typ: Type) = EConst(value, typ)

    /// <summary>변수 표현식 생성</summary>
    let variable (name: string) (typ: Type) = EVar(name, typ)

    /// <summary>터미널 표현식 생성</summary>
    let terminal (name: string) (typ: Type) = ETerminal(name, typ)

    /// <summary>단항 연산 표현식 생성</summary>
    let unary (op: DsOp) (expr: DsExpr) = EUnary(op, expr)

    /// <summary>이항 연산 표현식 생성</summary>
    let binary (op: DsOp) (left: DsExpr) (right: DsExpr) = EBinary(op, left, right)

    /// <summary>내장 함수 호출 표현식 생성</summary>
    let call (name: string) (args: DsExpr list) = ECall(name, args)

    /// <summary>UserFC 호출 표현식 생성</summary>
    let userFCCall (name: string) (args: DsExpr list) (returnType: Type option) (signature: string) =
        EUserFC(name, args, returnType, signature)

    /// <summary>메타데이터 표현식 생성</summary>
    let meta (tag: string) (metadata: Map<string, obj>) = EMeta(tag, metadata)

    let not' expr = unary Not expr
    let rising expr = unary Rising expr
    let falling expr = unary Falling expr

    // Type-safe constant builders
    let boolConst (b: bool) = constant (box b) typeof<bool>
    let intConst (i: int) = constant (box i) typeof<int>
    let doubleConst (d: double) = constant (box d) typeof<double>
    let stringConst (s: string) = constant (box s) typeof<string>

/// <summary>표현식 분석 유틸리티 모듈</summary>
/// <remarks>
/// 표현식의 복잡도, 깊이, 특성을 분석하는 함수들을 제공합니다.
/// </remarks>
module ExprAnalysis =

    /// <summary>표현식 복잡도 계산 (노드 개수)</summary>
    /// <param name="expr">분석할 표현식</param>
    /// <returns>표현식 트리의 총 노드 개수</returns>
    let complexity (expr: DsExpr) : int =
        let rec count expr =
            match expr with
            | EConst _ | EVar _ | ETerminal _ | EMeta _ -> 1
            | EUnary(_, x) -> 1 + count x
            | EBinary(_, l, r) -> 1 + count l + count r
            | ECall(_, args) | EUserFC(_, args, _, _) -> 1 + (args |> List.sumBy count)
        count expr

    /// <summary>표현식 트리의 최대 깊이</summary>
    /// <param name="expr">분석할 표현식</param>
    /// <returns>표현식 트리의 최대 깊이 (루트부터 리프까지의 최대 경로)</returns>
    let depth (expr: DsExpr) : int =
        let rec getDepth expr =
            match expr with
            | EConst _ | EVar _ | ETerminal _ | EMeta _ -> 1
            | EUnary(_, x) -> 1 + getDepth x
            | EBinary(_, l, r) -> 1 + max (getDepth l) (getDepth r)
            | ECall(_, args) | EUserFC(_, args, _, _) ->
                if List.isEmpty args then 1
                else 1 + (args |> List.map getDepth |> List.max)
        getDepth expr

    /// <summary>표현식이 상수인지 확인</summary>
    /// <param name="expr">확인할 표현식</param>
    /// <returns>변수나 함수 호출이 없으면 true</returns>
    /// <remarks>EConst 및 EMeta만 포함된 표현식만 상수로 간주됩니다.</remarks>
    let isConstant (expr: DsExpr) : bool =
        let rec check expr =
            match expr with
            | EConst _ -> true
            | EVar _ | ETerminal _ | ECall _ | EUserFC _ -> false
            | EUnary(_, x) -> check x
            | EBinary(_, l, r) -> check l && check r
            | EMeta _ -> true
        check expr

    /// <summary>표현식에 에지 연산자 포함 여부 확인</summary>
    /// <param name="expr">확인할 표현식</param>
    /// <returns>Rising 또는 Falling 에지 연산자가 있으면 true</returns>
    let hasEdgeOperators (expr: DsExpr) : bool =
        let rec check expr =
            match expr with
            | EUnary(op, x) -> op.IsEdgeOp || check x
            | EBinary(_, l, r) -> check l || check r
            | ECall(_, args) | EUserFC(_, args, _, _) -> args |> List.exists check
            | _ -> false
        check expr