namespace Ev2.Cpu.Core

open System
open System.Runtime.CompilerServices

/// 표현식 확장 메서드 - 포매팅 관련
[<Extension>]
type ExprExt =
    /// 표현식의 텍스트 표현
    [<Extension>]
    static member ToText(this: Expr) : string =
        let rec toTextRec expr needsParens =
            match expr with
            | Constant(value, _) ->
                match value with
                | :? bool as b -> b.ToString().ToLower()
                | :? string as s -> sprintf "\"%s\"" (s.Replace("\"", "\"\""))
                | _ -> value.ToString()
            
            | Variable(tag) -> tag.Name
            
            | UnaryOp(op, operand) ->
                let text = sprintf "%s %s" (op.ToString()) (toTextRec operand true)
                if needsParens then sprintf "(%s)" text else text
            
            | BinaryOp(op, left, right) ->
                let leftText = toTextRec left (ExpressionHelpers.isLowerPriority op left)
                let rightText = toTextRec right (ExpressionHelpers.isLowerPriority op right)
                let text = sprintf "%s %s %s" leftText (op.ToString()) rightText
                if needsParens then sprintf "(%s)" text else text
            
            | FunctionCall(name, args) ->
                let argsText = args |> List.map (fun e -> toTextRec e false) |> String.concat ", "
                sprintf "%s(%s)" name argsText
            
            | Conditional(cond, thenExpr, elseExpr) ->
                sprintf "IF %s THEN %s ELSE %s" 
                    (toTextRec cond false) (toTextRec thenExpr false) (toTextRec elseExpr false)
        
        toTextRec this false
    
    /// 간결한 텍스트 표현 (디버깅용)
    [<Extension>]
    static member ToShortText(this: Expr, maxLength: int) : string =
        let fullText = this.ToText()
        if fullText.Length <= maxLength then
            fullText
        else
            let truncated = fullText.Substring(0, maxLength - 3)
            truncated + "..."
    
    /// 디버그 표현 (타입 정보 포함)
    [<Extension>]
    static member ToDebugText(this: Expr) : string =
        let rec toDebugRec expr =
            match expr with
            | Constant(value, dtype) ->
                sprintf "Const(%A:%A)" value dtype
            
            | Variable(tag) ->
                sprintf "Var(%s:%A)" tag.Name tag.DsDataType
            
            | UnaryOp(op, operand) ->
                sprintf "UnaryOp(%A, %s)" op (toDebugRec operand)
            
            | BinaryOp(op, left, right) ->
                sprintf "BinaryOp(%A, %s, %s)" op (toDebugRec left) (toDebugRec right)
            
            | FunctionCall(name, args) ->
                let argsText = args |> List.map toDebugRec |> String.concat ", "
                sprintf "FuncCall(%s, [%s])" name argsText
            
            | Conditional(cond, thenExpr, elseExpr) ->
                sprintf "Conditional(%s, %s, %s)" 
                    (toDebugRec cond) (toDebugRec thenExpr) (toDebugRec elseExpr)
        
        toDebugRec this
