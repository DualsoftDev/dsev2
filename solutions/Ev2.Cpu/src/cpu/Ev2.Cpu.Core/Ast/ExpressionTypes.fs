namespace Ev2.Cpu.Core

open System

/// 표현식 AST
[<StructuralEquality; NoComparison>]
type Expr =
    | Constant of obj * DsDataType              // 상수 값
    | Variable of DsTag                     // 변수/IO 참조
    | UnaryOp of DsOp * Expr          // 단항 연산
    | BinaryOp of DsOp * Expr * Expr  // 이항 연산
    | FunctionCall of string * Expr list    // 함수 호출
    | Conditional of Expr * Expr * Expr     // 조건 표현식 (if-then-else)
    
    /// 표현식 타입 추론
    member this.InferType() : DsDataType =
        match this with
        | Constant(_, t) -> t
        | Variable(tag) -> tag.DsDataType
        
        | UnaryOp(op, expr) ->
            let operandType = expr.InferType()
            OperatorValidation.validateUnary op operandType
        
        | BinaryOp(op, left, right) ->
            let leftType = left.InferType()
            let rightType = right.InferType()
            OperatorValidation.validateBinary op leftType rightType
        
        | FunctionCall(_name, args) ->
            let argTypes = args |> List.map (fun e -> e.InferType())
            // 함수 시그니처 검증은 별도 모듈에서 처리
            DsDataType.TBool // 임시로 기본 타입 반환
        
        | Conditional(cond, thenExpr, elseExpr) ->
            let condType = cond.InferType()
            if condType <> DsDataType.TBool then
                raise (InvalidOperationException("Condition must be boolean"))
            let thenType = thenExpr.InferType()
            let elseType = elseExpr.InferType()
            if thenType = elseType then thenType
            else raise (InvalidOperationException($"Conditional branches must have same type: {thenType} vs {elseType}"))
    
    /// 표현식에서 참조하는 모든 변수 수집
    member this.GetReferencedVariables() : Set<string> =
        match this with
        | Constant(_, _) -> Set.empty
        | Variable(tag) -> Set.singleton tag.Name
        | UnaryOp(_, expr) -> expr.GetReferencedVariables()
        | BinaryOp(_, left, right) -> 
            Set.union (left.GetReferencedVariables()) (right.GetReferencedVariables())
        | FunctionCall(_, args) -> 
            args |> List.map (fun e -> e.GetReferencedVariables()) |> Set.unionMany
        | Conditional(cond, thenExpr, elseExpr) ->
            [cond; thenExpr; elseExpr] 
            |> List.map (fun e -> e.GetReferencedVariables()) 
            |> Set.unionMany
    
    /// 표현식 상수 여부 확인
    member this.IsConstantValue =
        match this with
        | Constant(_, _) -> true
        | _ -> false
    
    /// 표현식 단순성 확인 (상수 또는 변수만)
    member this.IsSimple =
        match this with
        | Constant(_, _) | Variable(_) -> true
        | _ -> false
    
    /// 표현식 복잡도 계산
    member this.GetComplexity() : int =
        match this with
        | Constant(_, _) | Variable(_) -> 1
        | UnaryOp(_, expr) -> 1 + expr.GetComplexity()
        | BinaryOp(_, left, right) -> 1 + left.GetComplexity() + right.GetComplexity()
        | FunctionCall(_, args) -> 1 + (args |> List.sumBy (fun e -> e.GetComplexity()))
        | Conditional(cond, thenExpr, elseExpr) -> 
            1 + cond.GetComplexity() + thenExpr.GetComplexity() + elseExpr.GetComplexity()
    
    /// 표현식의 깊이 계산
    member this.GetDepth() : int =
        match this with
        | Constant(_, _) | Variable(_) -> 1
        | UnaryOp(_, expr) -> 1 + expr.GetDepth()
        | BinaryOp(_, left, right) -> 1 + max (left.GetDepth()) (right.GetDepth())
        | FunctionCall(_, args) -> 
            match args with
            | [] -> 1
            | _ -> 1 + (args |> List.map (fun e -> e.GetDepth()) |> List.max)
        | Conditional(cond, thenExpr, elseExpr) -> 
            1 + ([cond; thenExpr; elseExpr] 
                |> List.map (fun e -> e.GetDepth()) 
                |> List.max)

/// 표현식 헬퍼 모듈
module ExpressionHelpers =
    /// 연산자 우선순위 비교
    let isLowerPriority (op: DsOp) (expr: Expr) =
        match expr with
        | BinaryOp(exprOp, _, _) | UnaryOp(exprOp, _) ->
            exprOp.Priority < op.Priority
        | _ -> false
