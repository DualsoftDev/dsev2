namespace Ev2.Cpu.Core

open Ev2.Cpu.Core.ExpressionBuilder
/// 문장 빌더 모듈
module StatementBuilder =
    
    /// 기본 문장 생성
    let assign tag expr = Assignment(tag, expr)
    let assignVar name dtype expr = Assignment(DsTag.Create(name, dtype), expr)
    let ifThen condition thenStmts = Conditional(condition, thenStmts, [])
    let ifThenElse condition thenStmts elseStmts = Conditional(condition, thenStmts, elseStmts)
    let whileLoop condition stmts = Loop(condition, stmts)
    let block stmts = Block(stmts)
    let callStmt funcName args = FunctionCall(funcName, args)
    let returnStmt expr = Return(Some expr)
    let returnVoid () = Return(None)
    let breakStmt = Break
    let continueStmt = Continue
    let comment text = Comment(text)
    let empty = Empty
    
    /// 시스템 문장 생성
    let timer name preset enable = TimerCall(name, preset, enable)
    let counter name preset count resetLoad = CounterCall(name, preset, count, resetLoad)
    let syscall name args = SystemCall(name, args)
    
    /// 복합 문장 빌더
    let sequence stmts = Block(stmts)
    
    let ifOnly condition thenStmt = 
        Conditional(condition, [thenStmt], [])
    
    let ifElse condition thenStmt elseStmt =
        Conditional(condition, [thenStmt], [elseStmt])
    
    /// 조건부 빌더
    type IfBuilder() =
        member _.Yield(()) = (boolConst true, [], [])
        
        [<CustomOperation("condition")>]
        member _.Condition((_, thenStmts, elseStmts), cond) = (cond, thenStmts, elseStmts)
        
        [<CustomOperation("then_do")>]
        member _.Then((cond, _, elseStmts), stmts) = (cond, stmts, elseStmts)
        
        [<CustomOperation("else_do")>]
        member _.Else((cond, thenStmts, _), stmts) = (cond, thenStmts, stmts)
        
        member _.Run((cond, thenStmts, elseStmts)) = 
            Conditional(cond, thenStmts, elseStmts)
    
    /// 루프 빌더
    type WhileBuilder() =
        member _.Yield(()) = (boolConst true, [])
        
        [<CustomOperation("condition")>]
        member _.Condition((_, stmts), cond) = (cond, stmts)
        
        [<CustomOperation("do_body")>]
        member _.DoBody((cond, _), stmts) = (cond, stmts)
        
        member _.Run((cond, stmts)) = 
            Loop(cond, stmts)
    
    /// DSL 빌더 인스턴스
    let ifStmt = IfBuilder()
    let whileStmt = WhileBuilder()
