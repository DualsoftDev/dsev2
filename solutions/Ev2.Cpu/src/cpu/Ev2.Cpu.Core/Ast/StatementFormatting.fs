module Ev2.Cpu.Core.StatementFormatting

open System
open Ev2.Cpu.Core


/// 문장 확장 메서드 - 포매팅 관련
type Stmt with
    
    /// 문장의 텍스트 표현 (들여쓰기 포함)
    member this.ToText(indent: int) : string =
        let indentStr = String.replicate indent "  "
        
        let rec stmtToText stmt currentIndent =
            let currentIndentStr = String.replicate currentIndent "  "
            
            match stmt with
            | Assignment(tag, expr) ->
                sprintf "%s%s := %s" currentIndentStr tag.Name (expr.ToText())
            
            | Conditional(cond, thenStmts, elseStmts) ->
                let thenText = 
                    thenStmts 
                    |> List.map (fun s -> stmtToText s (currentIndent + 1))
                    |> String.concat "\n"
                
                let elseText = 
                    if elseStmts.IsEmpty then ""
                    else
                        let elseContent = 
                            elseStmts 
                            |> List.map (fun s -> stmtToText s (currentIndent + 1))
                            |> String.concat "\n"
                        sprintf "\n%sELSE\n%s" currentIndentStr elseContent
                
                sprintf "%sIF %s THEN\n%s%s\n%sEND_IF" 
                    currentIndentStr (cond.ToText()) thenText elseText currentIndentStr
            
            | Loop(cond, stmts) ->
                let bodyText = 
                    stmts 
                    |> List.map (fun s -> stmtToText s (currentIndent + 1))
                    |> String.concat "\n"
                
                sprintf "%sWHILE %s DO\n%s\n%sEND_WHILE" 
                    currentIndentStr (cond.ToText()) bodyText currentIndentStr
            
            | Block(stmts) ->
                stmts 
                |> List.map (fun s -> stmtToText s currentIndent)
                |> String.concat "\n"
            
            | FunctionCall(name, args) ->
                let argsText = args |> List.map (fun e -> e.ToText()) |> String.concat ", "
                sprintf "%s%s(%s)" currentIndentStr name argsText
            
            | Return(Some expr) ->
                sprintf "%sRETURN %s" currentIndentStr (expr.ToText())
            
            | Return(None) ->
                sprintf "%sRETURN" currentIndentStr
            
            | Break ->
                sprintf "%sBREAK" currentIndentStr
            
            | Continue ->
                sprintf "%sCONTINUE" currentIndentStr
            
            | Comment(text) ->
                sprintf "%s// %s" currentIndentStr text
            
            | Empty ->
                sprintf "%s;" currentIndentStr
            
            | TimerCall(name, preset, enable) ->
                sprintf "%s%s(PRESET:=%s, ENABLE:=%s)" 
                    currentIndentStr name (preset.ToText()) (enable.ToText())
            
            | CounterCall(name, preset, count, resetLoad) ->
                sprintf "%s%s(PRESET:=%s, COUNT:=%s, RESET_LOAD:=%s)" 
                    currentIndentStr name (preset.ToText()) (count.ToText()) (resetLoad.ToText())
            
            | SystemCall(name, args) ->
                let argsText = args |> List.map (fun e -> e.ToText()) |> String.concat ", "
                sprintf "%s%s(%s)" currentIndentStr name argsText
        
        stmtToText this indent
    
    /// 기본 텍스트 표현 (들여쓰기 0)
    member this.ToText() : string =
        this.ToText(0)
    
    /// 간결한 표현 (디버깅용)
    member this.ToShortText(maxLength: int) : string =
        let fullText = this.ToText()
        let singleLine = fullText.Replace("\n", " ").Replace("\r", "")
        if singleLine.Length <= maxLength then
            singleLine
        else
            let truncated = singleLine.Substring(0, maxLength - 3)
            truncated + "..."
    
    /// 디버그 표현
    member this.ToDebugText() : string =
        let rec toDebugRec stmt =
            match stmt with
            | Assignment(tag, expr) ->
                sprintf "Assign(%s:%A, %s)" tag.Name tag.StructType (expr.ToDebugText())
            
            | Conditional(cond, thenStmts, elseStmts) ->
                let thenText = thenStmts |> List.map toDebugRec |> String.concat "; "
                let elseText = elseStmts |> List.map toDebugRec |> String.concat "; "
                sprintf "If(%s, [%s], [%s])" (cond.ToDebugText()) thenText elseText
            
            | Loop(cond, stmts) ->
                let bodyText = stmts |> List.map toDebugRec |> String.concat "; "
                sprintf "While(%s, [%s])" (cond.ToDebugText()) bodyText
            
            | Block(stmts) ->
                let bodyText = stmts |> List.map toDebugRec |> String.concat "; "
                sprintf "Block([%s])" bodyText
            
            | FunctionCall(name, args) ->
                let argsText = args |> List.map (fun e -> e.ToDebugText()) |> String.concat ", "
                sprintf "Call(%s, [%s])" name argsText
            
            | Return(Some expr) ->
                sprintf "Return(%s)" (expr.ToDebugText())
            
            | Return(None) ->
                "Return()"
            
            | Break -> "Break"
            | Continue -> "Continue"
            | Comment(text) -> sprintf "Comment(\"%s\")" text
            | Empty -> "Empty"
            
            | TimerCall(name, preset, enable) ->
                sprintf "Timer(%s, %s, %s)" name (preset.ToDebugText()) (enable.ToDebugText())
            
            | CounterCall(name, preset, count, resetLoad) ->
                sprintf "Counter(%s, %s, %s, %s)" name (preset.ToDebugText()) (count.ToDebugText()) (resetLoad.ToDebugText())
            
            | SystemCall(name, args) ->
                let argsText = args |> List.map (fun e -> e.ToDebugText()) |> String.concat ", "
                sprintf "System(%s, [%s])" name argsText
        
        toDebugRec this
