namespace Ev2.Cpu.Core

open System
open System.Runtime.CompilerServices

[<Extension>]
type StmtExt =
    [<Extension>]
    static member GetReferencedVariables(this: Stmt) : Set<string> =
        match this with
        | Assignment(tag, expr) ->
            Set.add tag.Name (expr.GetReferencedVariables())
        
        | Conditional(cond, thenStmts, elseStmts) ->
            let condVars = cond.GetReferencedVariables()
            let thenVars = thenStmts |> List.map (fun s -> s.GetReferencedVariables()) |> Set.unionMany
            let elseVars = elseStmts |> List.map (fun s -> s.GetReferencedVariables()) |> Set.unionMany
            Set.unionMany [condVars; thenVars; elseVars]
        
        | Loop(cond, stmts) ->
            let condVars = cond.GetReferencedVariables()
            let bodyVars = stmts |> List.map (fun s -> s.GetReferencedVariables()) |> Set.unionMany
            Set.union condVars bodyVars
        
        | Block(stmts) ->
            stmts |> List.map (fun s -> s.GetReferencedVariables()) |> Set.unionMany
        
        | FunctionCall(_, args) ->
            args |> List.map (fun e -> e.GetReferencedVariables()) |> Set.unionMany
        
        | Return(Some expr) -> expr.GetReferencedVariables()
        | Return(None) -> Set.empty
        
        | TimerCall(name, preset, enable) ->
            [preset; enable] 
            |> List.map (fun e -> e.GetReferencedVariables()) 
            |> Set.unionMany
            |> Set.add name
        
        | CounterCall(name, preset, count, resetLoad) ->
            [preset; count; resetLoad] 
            |> List.map (fun e -> e.GetReferencedVariables()) 
            |> Set.unionMany
            |> Set.add name
        
        | SystemCall(_, args) ->
            args |> List.map (fun e -> e.GetReferencedVariables()) |> Set.unionMany
        
        | Break | Continue | Comment(_) | Empty -> Set.empty
    
    /// 문장에서 수정하는 모든 변수 수집
    [<Extension>]
    static member GetModifiedVariables(this: Stmt) : Set<string> =
        match this with
        | Assignment(tag, _) -> Set.singleton tag.Name
        
        | Conditional(_, thenStmts, elseStmts) ->
            let thenVars = thenStmts |> List.map (fun s -> s.GetModifiedVariables()) |> Set.unionMany
            let elseVars = elseStmts |> List.map (fun s -> s.GetModifiedVariables()) |> Set.unionMany
            Set.union thenVars elseVars
        
        | Loop(_, stmts) | Block(stmts) ->
            stmts |> List.map (fun s -> s.GetModifiedVariables()) |> Set.unionMany
        
        | _ -> Set.empty
    
    /// 문장의 복잡도 계산
    [<Extension>]
    static member GetComplexity(this: Stmt) : int =
        match this with
        | Assignment(_, expr) -> 1 + expr.GetComplexity()
        
        | Conditional(cond, thenStmts, elseStmts) ->
            let condComplexity = cond.GetComplexity()
            let thenComplexity = thenStmts |> List.sumBy (fun s -> s.GetComplexity())
            let elseComplexity = elseStmts |> List.sumBy (fun s -> s.GetComplexity())
            1 + condComplexity + thenComplexity + elseComplexity
        
        | Loop(cond, stmts) ->
            let condComplexity = cond.GetComplexity()
            let bodyComplexity = stmts |> List.sumBy (fun s -> s.GetComplexity())
            2 + condComplexity + bodyComplexity  // 루프는 가중치 2
        
        | Block(stmts) ->
            stmts |> List.sumBy (fun s -> s.GetComplexity())
        
        | FunctionCall(_, args) ->
            1 + (args |> List.sumBy (fun e -> e.GetComplexity()))
        
        | Return(Some expr) -> 1 + expr.GetComplexity()
        
        | TimerCall(_, preset, enable) ->
            1 + preset.GetComplexity() + enable.GetComplexity()
        
        | CounterCall(_, preset, count, resetLoad) ->
            1 + preset.GetComplexity() + count.GetComplexity() + resetLoad.GetComplexity()
        
        | SystemCall(_, args) ->
            1 + (args |> List.sumBy (fun e -> e.GetComplexity()))
        
        | Break | Continue | Return(None) | Comment(_) | Empty -> 1
    
    /// 문장의 깊이 계산
    [<Extension>]
    static member GetDepth(this: Stmt) : int =
        match this with
        | Assignment(_, expr) -> 1 + expr.GetDepth()
        
        | Conditional(cond, thenStmts, elseStmts) ->
            let condDepth = cond.GetDepth()
            let thenDepth = if thenStmts.IsEmpty then 0 else thenStmts |> List.map (fun s -> s.GetDepth()) |> List.max
            let elseDepth = if elseStmts.IsEmpty then 0 else elseStmts |> List.map (fun s -> s.GetDepth()) |> List.max
            1 + max condDepth (max thenDepth elseDepth)
        
        | Loop(cond, stmts) ->
            let condDepth = cond.GetDepth()
            let bodyDepth = if stmts.IsEmpty then 0 else stmts |> List.map (fun s -> s.GetDepth()) |> List.max
            1 + max condDepth bodyDepth
        
        | Block(stmts) ->
            if stmts.IsEmpty then 1 else stmts |> List.map (fun s -> s.GetDepth()) |> List.max
        
        | FunctionCall(_, args) ->
            if args.IsEmpty then 1 else 1 + (args |> List.map (fun e -> e.GetDepth()) |> List.max)
        
        | Return(Some expr) -> 1 + expr.GetDepth()
        
        | TimerCall(_, preset, enable) ->
            1 + max (preset.GetDepth()) (enable.GetDepth())
        
        | CounterCall(_, preset, count, resetLoad) ->
            1 + ([preset; count; resetLoad] |> List.map (fun e -> e.GetDepth()) |> List.max)
        
        | SystemCall(_, args) ->
            if args.IsEmpty then 1 else 1 + (args |> List.map (fun e -> e.GetDepth()) |> List.max)
        
        | Break | Continue | Return(None) | Comment(_) | Empty -> 1
    
    /// 문장의 문장 수 계산
    [<Extension>]
    static member GetStatementCount(this: Stmt) : int =
        match this with
        | Conditional(_, thenStmts, elseStmts) ->
            let thenCount = thenStmts |> List.sumBy (fun s -> s.GetStatementCount())
            let elseCount = elseStmts |> List.sumBy (fun s -> s.GetStatementCount())
            1 + thenCount + elseCount
        
        | Loop(_, stmts) | Block(stmts) ->
            1 + (stmts |> List.sumBy (fun s -> s.GetStatementCount()))
        
        | _ -> 1