namespace Ev2.Cpu.Generation.Core

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement

/// 간결한 표현식 헬퍼
[<AutoOpen>]
module ExprHelpers =
    // 타입별 변수
    let tag name typ = Terminal(DsTag.Create(name, typ))
    let boolTag name = Terminal(DsTag.Bool(name))
    let intTag name = Terminal(DsTag.Int(name))
    let doubleTag name = Terminal(DsTag.Double(name))
    let strTag name = Terminal(DsTag.String(name))
    
    // 타입별 상수
    let boolConst v = Const(box v, DsDataType.TBool)
    let intConst v = Const(box v, DsDataType.TInt) 
    let doubleConst v = Const(box v, DsDataType.TDouble)
    let strConst v = Const(box v, DsDataType.TString)
    
    // Set/Reset 패턴
    let setWhen cond tag = Relay.Create(tag, cond, boolConst false)
    let resetWhen cond tag = Relay.Create(tag, boolConst false, cond)
    let setResetWhen set' reset tag = Relay.Create(tag, set', reset)
    let latchWhen set' reset tag = 
        let self = Terminal(tag)
        Relay.Create(tag, set' ||. self, reset)
    
    // 연산 축약
    let (<|) expr1 expr2 = expr1, expr2
    let (<|>) opt1 opt2 = match opt1 with Some x -> Some x | None -> opt2
    let (~~) expr = Unary(DsOp.Not, expr)
    
    // 함수형 연산
    let fn fname args = Function(fname, args)
    let add' = List.reduce (fun a b -> Binary(DsOp.Add, a, b))
    let mul' = List.reduce (fun a b -> Binary(DsOp.Mul, a, b))
    let sub left right = Binary(DsOp.Sub, left, right)
    let div left right = Binary(DsOp.Div, left, right)
    let pow left right = Binary(DsOp.Pow, left, right)
    let mod' left right = Binary(DsOp.Mod, left, right)

[<AutoOpen>]
module StmtInline =
    /// 기본 Assign 생성 (step=0)
    let inline assign tag expr = Assign(0, tag, expr)

    /// 지정된 step을 가진 Assign 생성
    let inline assignAt step tag expr = Assign(step, tag, expr)

    /// 형식별 Assign 헬퍼
    let inline assignBool name expr = assign (DsTag.Bool name) expr
    let inline assignInt name expr = assign (DsTag.Int name) expr
    let inline assignDouble name expr = assign (DsTag.Double name) expr
    let inline assignString name expr = assign (DsTag.String name) expr

    /// 상수를 바로 할당하는 헬퍼
    let inline setBoolConst name value = assignBool name (boolConst value)
    let inline setIntConst name value = assignInt name (intConst value)
    let inline setDoubleConst name value = assignDouble name (doubleConst value)
    let inline setStringConst name value = assignString name (strConst value)

    /// 기본 Command 생성 (step=0)
    let inline command cond action = Command(0, cond, action)

    /// 지정된 step의 Command 생성
    let inline commandAt step cond action = Command(step, cond, action)

    /// 조건부 동작을 직관적으로 표현
    let inline whenDo cond action = command cond action
    let inline whenAt step cond action = commandAt step cond action

    /// 기존 문장의 step 재설정
    let inline withStep step stmt = Statement.withStep step stmt

    /// 여러 문장 묶음 생성 (읽기 쉬운 DSL 지원)
    let inline block stmts = stmts

/// 코드 생성 유틸리티
module GenerationUtils =

    /// Relay를 Statement로 변환
    let relayToStmt (relay: Relay) : DsStmt =
        assignWithStep 0 relay.Tag (relay.ToExpr())
    
    /// 여러 Relay를 Statement 리스트로 변환
    let relaysToStmts (relays: Relay list) : DsStmt list =
        relays |> List.map relayToStmt
    
    /// 조건부 Relay 생성
    let conditionalRelay (condition: DsExpr) (tag: DsTag) : Relay =
        Relay.Create(tag, condition, !!. condition)
    
    /// 래치 Relay 생성 (자기유지)
    let latchRelay (setExpr: DsExpr) (resetExpr: DsExpr) (tag: DsTag) : Relay =
        let tagVar = Terminal(tag)
        Relay.Create(tag, setExpr ||. tagVar, resetExpr)
    
    /// 펄스 Relay 생성
    let pulseRelay (trigger: DsExpr) (tag: DsTag) : Relay =
        Relay.Create(tag, Unary(DsOp.Rising, trigger), boolConst false)
    
    /// 타이머 기반 Relay 생성
    let timerRelay (startExpr: DsExpr) (timeMs: int) (tag: DsTag) : Relay =
        let timerName = sprintf "%s_Timer" tag.Name
        let timerExpr = fn "TON" [startExpr; strConst timerName; intConst timeMs]
        Relay.Create(tag, timerExpr, !!. startExpr)
    
    let term (name:string) = Terminal(DsTag.Create(name, DsDataType.TBool))
    let termInt (name:string) = Terminal(DsTag.Create(name, DsDataType.TInt))
    let termDouble (name:string) = Terminal(DsTag.Create(name, DsDataType.TDouble))
    let termString (name:string) = Terminal(DsTag.Create(name, DsDataType.TString))

/// 코드 생성 패턴
module Generation =
    let toStmt (relay: Relay) =
        assignWithStep 0 relay.Tag (relay.ToExpr())
    
    let toLatch (relay: Relay) = 
        assignWithStep 0 relay.Tag (relay.ToLatch())
    
    let toStmts = List.map toStmt
    let toLatches = List.map toLatch
    
    let pulse trigger tag =
        Relay.CreateWithMode(tag, rising trigger, boolConst false, RelayMode.Pulse)
    
    // CRITICAL FIX (DEFECT-021-8): TON requires 3-arg form [enable; name; preset]
    // Previous 2-arg form rejected by runtime (deprecated since Round 20)
    let timer start ms (tag: DsTag) =
        let timerName = sprintf "%s_timer" tag.Name
        Relay.Create(tag, fn "TON" [start; strConst timerName; intConst ms], ~~start)

    // CRITICAL FIX (DEFECT-021-9): CTU requires 4-arg form [name; countUp; reset; preset]
    // Previous 1-arg form only passed tag name, missing required parameters
    let counterUp up reset (tag: DsTag) =
        let counterName = sprintf "%s_counter" tag.Name
        // IEC CTU(CU, R, PV) - use 100 as default preset
        Relay.Create(tag, up &&. fn "CTU" [strConst counterName; up; reset; intConst 100], reset)

    // CRITICAL FIX (DEFECT-021-9): CTD requires 4-arg form [name; down; load; preset]
    // Previous 1-arg form only passed tag name, missing required parameters
    let counterDown down reset (tag: DsTag) =
        let counterName = sprintf "%s_counter" tag.Name
        // IEC CTD(CD, LD, PV) - use 100 as default preset, false for load
        Relay.Create(tag, down &&. fn "CTD" [strConst counterName; down; boolConst false; intConst 100], reset)
    
    let state current target cond =
        Relay.Create(target,
            (Terminal(current) ==. Terminal(target)) &&. cond,
            Terminal(current) <>. Terminal(target))
    
    let interlock conditions tag =
        Relay.Create(tag, all conditions, any (List.map (~~) conditions))
