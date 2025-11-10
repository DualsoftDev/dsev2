namespace Ev2.Cpu.Core.UserDefined

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// User-Defined Types for FC/FB
// ═════════════════════════════════════════════════════════════════════════════
// UserFC/FB 전용 AST 타입 정의
// 기존 DsExpr/DsStmt와 독립적이지만 상호 변환 가능
// ═════════════════════════════════════════════════════════════════════════════

/// UserFC/FB에서 사용되는 표현식 타입
/// FC/FB 파라미터, Static 변수, 다른 UserFC 호출 등을 지원
[<StructuralEquality; NoComparison>]
type UserExpr =
    /// 상수 값
    /// 예: 123, 45.67, TRUE, "Hello"
    | UConst of value:obj * dataType:Type

    /// 파라미터 참조 (Input/Output/InOut)
    /// FC/FB의 파라미터를 참조
    /// 예: UParam("temperature", typeof<double>)
    | UParam of name:string * dataType:Type

    /// Static 변수 참조 (FB only)
    /// FB의 상태 변수를 참조
    /// 예: UStatic("counter", typeof<int>)
    | UStatic of name:string * dataType:Type

    /// Temp 변수 참조 (FB only)
    /// FB의 임시 변수를 참조
    /// 예: UTemp("tempResult", typeof<double>)
    | UTemp of name:string * dataType:Type

    /// 단항 연산
    /// 예: NOT start, -value
    | UUnary of op:DsOp * expr:UserExpr

    /// 이항 연산
    /// 예: temp + 10, pressure > threshold
    | UBinary of op:DsOp * left:UserExpr * right:UserExpr

    /// Built-in 함수 호출
    /// 시스템 제공 함수 (ABS, MAX, TON 등)
    /// 예: UCall("ABS", [UParam("value", typeof<double>)])
    | UCall of funcName:string * args:UserExpr list

    /// 다른 UserFC 호출
    /// 사용자 정의 함수 호출 (재귀 포함)
    /// 예: UUserFCCall("LinearScale", [input; minVal; maxVal])
    | UUserFCCall of fcName:string * args:UserExpr list

    /// 조건부 표현식 (IF-THEN-ELSE)
    /// 예: UConditional(condition, trueExpr, falseExpr)
    | UConditional of condition:UserExpr * trueExpr:UserExpr * falseExpr:UserExpr

    with
        /// 표현식의 타입 추론
        member this.InferType() : Type option =
            match this with
            | UConst(_, dt) -> Some dt
            | UParam(_, dt) -> Some dt
            | UStatic(_, dt) -> Some dt
            | UTemp(_, dt) -> Some dt

            | UUnary(op, expr) ->
                match op with
                | DsOp.Not | DsOp.Rising | DsOp.Falling -> Some typeof<bool>
                | _ -> expr.InferType()

            | UBinary(op, left, right) ->
                let leftType = left.InferType()
                let rightType = right.InferType()
                match op with
                // 논리 연산: BOOL
                | DsOp.And | DsOp.Or | DsOp.Xor -> Some typeof<bool>
                // 비교 연산: BOOL
                | DsOp.Eq | DsOp.Ne | DsOp.Gt | DsOp.Ge | DsOp.Lt | DsOp.Le -> Some typeof<bool>
                // 산술 연산: 타입 승격
                | DsOp.Add ->
                    // Add는 문자열 연결도 지원
                    match leftType, rightType with
                    | Some t, _ when t = typeof<string> -> Some typeof<string>
                    | _, Some t when t = typeof<string> -> Some typeof<string>
                    | Some t, _ when t = typeof<double> -> Some typeof<double>
                    | _, Some t when t = typeof<double> -> Some typeof<double>
                    | Some t1, Some t2 when t1 = typeof<int> && t2 = typeof<int> -> Some typeof<int>
                    | _ -> None
                | DsOp.Sub | DsOp.Mul | DsOp.Div | DsOp.Pow ->
                    match leftType, rightType with
                    | Some t, _ when t = typeof<double> -> Some typeof<double>
                    | _, Some t when t = typeof<double> -> Some typeof<double>
                    | Some t1, Some t2 when t1 = typeof<int> && t2 = typeof<int> -> Some typeof<int>
                    | _ -> None
                | DsOp.Mod -> Some typeof<int>
                | _ -> None

            | UCall(name, _) ->
                // Built-in 함수의 반환 타입은 외부에서 결정
                None

            | UUserFCCall(_, _) ->
                // UserFC의 반환 타입은 레지스트리에서 조회
                None

            | UConditional(_, trueExpr, _) ->
                // IF 표현식은 true 분기의 타입 사용
                trueExpr.InferType()

        /// 표현식을 텍스트로 변환
        member this.ToText() : string =
            match this with
            | UConst(v, dt) ->
                if dt = typeof<bool> then
                    if (v :?> bool) then "TRUE" else "FALSE"
                elif dt = typeof<int> then
                    sprintf "%d" (v :?> int)
                elif dt = typeof<double> then
                    sprintf "%f" (v :?> float)
                elif dt = typeof<string> then
                    sprintf "\"%s\"" (v :?> string)
                else
                    string v

            | UParam(name, _) -> name
            | UStatic(name, _) -> name
            | UTemp(name, _) -> name

            | UUnary(op, expr) -> sprintf "(%O %s)" op (expr.ToText())
            | UBinary(op, left, right) -> sprintf "(%s %O %s)" (left.ToText()) op (right.ToText())
            | UCall(name, args) ->
                let argsText = args |> List.map (fun a -> a.ToText()) |> String.concat ", "
                sprintf "%s(%s)" name argsText
            | UUserFCCall(name, args) ->
                let argsText = args |> List.map (fun a -> a.ToText()) |> String.concat ", "
                sprintf "%s(%s)" name argsText
            | UConditional(cond, trueExpr, falseExpr) ->
                sprintf "IF %s THEN %s ELSE %s" (cond.ToText()) (trueExpr.ToText()) (falseExpr.ToText())

        /// 표현식에 포함된 모든 변수 이름 추출
        member this.GetVariables() : Set<string> =
            let rec collect acc expr =
                match expr with
                | UConst _ -> acc
                | UParam(name, _) | UStatic(name, _) | UTemp(name, _) -> Set.add name acc
                | UUnary(_, e) -> collect acc e
                | UBinary(_, l, r) -> collect (collect acc l) r
                | UCall(_, args) | UUserFCCall(_, args) -> args |> List.fold collect acc
                | UConditional(c, t, f) -> collect (collect (collect acc c) t) f
            collect Set.empty this

        /// 표현식의 복잡도 (노드 개수)
        member this.Complexity() : int =
            let rec count expr =
                match expr with
                | UConst _ | UParam _ | UStatic _ | UTemp _ -> 1
                | UUnary(_, e) -> 1 + count e
                | UBinary(_, l, r) -> 1 + count l + count r
                | UCall(_, args) | UUserFCCall(_, args) -> 1 + (args |> List.sumBy count)
                | UConditional(c, t, f) -> 1 + count c + count t + count f
            count this

/// UserFB에서 사용되는 명령문 타입
/// 상태 변화, 조건부 실행, 시퀀스 등을 지원
[<StructuralEquality; NoComparison>]
type UserStmt =
    /// 변수 할당
    /// target: 파라미터, Static, Temp 변수 이름
    /// 예: UAssign("output", expr)
    | UAssign of target:string * expr:UserExpr

    /// 조건부 실행
    /// condition이 TRUE일 때 action 실행
    /// 예: UWhen(UParam("enable", typeof<bool>), UAssign("output", value))
    | UWhen of condition:UserExpr * action:UserStmt

    /// 명령문 시퀀스
    /// 순차적으로 실행
    /// 예: USequence([stmt1; stmt2; stmt3])
    | USequence of stmts:UserStmt list

    /// FB 호출
    /// 다른 FB의 인스턴스를 호출
    /// 예: UFBCall("Motor1", "MotorControl", inputs)
    | UFBCall of instanceName:string * fbName:string * inputs:Map<string, UserExpr>

    /// NOOP (아무 동작 안함)
    /// 플레이스홀더 또는 최적화로 제거된 명령문
    | UNoop

    with
        /// 명령문을 텍스트로 변환
        member this.ToText() : string =
            match this with
            | UAssign(target, expr) -> sprintf "%s := %s;" target (expr.ToText())
            | UWhen(cond, action) -> sprintf "IF %s THEN %s END_IF;" (cond.ToText()) (action.ToText())
            | USequence(stmts) ->
                stmts
                |> List.map (fun s -> s.ToText())
                |> String.concat "\n"
            | UFBCall(inst, fb, inputs) ->
                let inputsText =
                    inputs
                    |> Map.toSeq
                    |> Seq.map (fun (k, v) -> sprintf "%s := %s" k (v.ToText()))
                    |> String.concat ", "
                sprintf "%s(%s);" inst inputsText
            | UNoop -> ""

        /// 명령문에 포함된 모든 변수 이름 추출
        member this.GetVariables() : Set<string> =
            let rec collect acc stmt =
                match stmt with
                | UAssign(target, expr) -> Set.add target (Set.union acc (expr.GetVariables()))
                | UWhen(cond, action) -> collect (Set.union acc (cond.GetVariables())) action
                | USequence(stmts) -> stmts |> List.fold collect acc
                | UFBCall(inst, _, inputs) ->
                    let inputVars = inputs |> Map.toSeq |> Seq.map snd |> Seq.fold (fun s e -> Set.union s (e.GetVariables())) Set.empty
                    Set.add inst (Set.union acc inputVars)
                | UNoop -> acc
            collect Set.empty this

        /// 명령문의 복잡도 (노드 개수)
        member this.Complexity() : int =
            let rec count stmt =
                match stmt with
                | UAssign(_, expr) -> 1 + expr.Complexity()
                | UWhen(cond, action) -> 1 + cond.Complexity() + count action
                | USequence(stmts) -> 1 + (stmts |> List.sumBy count)
                | UFBCall(_, _, inputs) -> 1 + (inputs |> Map.toSeq |> Seq.sumBy (snd >> (fun e -> e.Complexity())))
                | UNoop -> 0
            count this

/// UserExpr 생성 헬퍼
module UserExprBuilder =

    /// 상수 생성
    let uconst value dataType = UConst(value, dataType)
    let ubool b = UConst(box b, typeof<bool>)
    let uint i = UConst(box i, typeof<int>)
    let udouble d = UConst(box d, typeof<double>)
    let ustring s = UConst(box s, typeof<string>)

    /// 파라미터 참조
    let uparam name dataType = UParam(name, dataType)

    /// Static 변수 참조
    let ustatic name dataType = UStatic(name, dataType)

    /// Temp 변수 참조
    let utemp name dataType = UTemp(name, dataType)

    /// 단항 연산
    let unot expr = UUnary(DsOp.Not, expr)
    let uneg expr = UUnary(DsOp.Sub, expr)

    /// 이항 연산
    let uadd l r = UBinary(DsOp.Add, l, r)
    let usub l r = UBinary(DsOp.Sub, l, r)
    let umul l r = UBinary(DsOp.Mul, l, r)
    let udiv l r = UBinary(DsOp.Div, l, r)
    let umod l r = UBinary(DsOp.Mod, l, r)

    let ueq l r = UBinary(DsOp.Eq, l, r)
    let une l r = UBinary(DsOp.Ne, l, r)
    let ugt l r = UBinary(DsOp.Gt, l, r)
    let uge l r = UBinary(DsOp.Ge, l, r)
    let ult l r = UBinary(DsOp.Lt, l, r)
    let ule l r = UBinary(DsOp.Le, l, r)

    let uand l r = UBinary(DsOp.And, l, r)
    let uor l r = UBinary(DsOp.Or, l, r)
    let uxor l r = UBinary(DsOp.Xor, l, r)

    /// 함수 호출
    let ucall name args = UCall(name, args)
    let uuserfccall name args = UUserFCCall(name, args)

    /// 조건부 표현식
    let uif condition trueExpr falseExpr = UConditional(condition, trueExpr, falseExpr)

/// UserStmt 생성 헬퍼
module UserStmtBuilder =

    /// 할당
    let uassign target expr = UAssign(target, expr)

    /// 조건부 실행
    let uwhen condition action = UWhen(condition, action)

    /// 시퀀스
    let useq stmts = USequence(stmts)

    /// FB 호출
    let ufbcall instanceName fbName inputs = UFBCall(instanceName, fbName, inputs)

    /// NOOP
    let unoop = UNoop
