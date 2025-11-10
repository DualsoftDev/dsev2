namespace Ev2.Cpu.Generation.Make

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Expression
open Ev2.Cpu.Core.Statement
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen

/// 구문 코드 생성
module StatementGen =

    /// 기본 구문 생성자들
    let assign step tag expr = Assign(step, tag, expr)
    let command step condition action = Command(step, condition, action)

    /// 단계 번호가 있는 할당
    let assignAt step name dataType expr = 
        assign step (DsTag.Create(name, dataType)) expr

    /// 자동 단계 번호 할당
    let assignAuto name dataType expr = 
        assign 0 (DsTag.Create(name, dataType)) expr

    /// 조건부 명령
    let when' condition action = command 0 condition action
    let whenAt step condition action = command step condition action

    /// MOV 명령
    let mov source target = Function("MOV", [source; Terminal(target)])
    let movTo step condition source targetName dataType =
        whenAt step condition (mov source (DsTag.Create(targetName, dataType)))

    /// SET/RESET 명령
    let set step condition targetName = 
        whenAt step condition (mov (boolExpr true) (DsTag.Bool(targetName)))
    let reset step condition targetName = 
        whenAt step condition (mov (boolExpr false) (DsTag.Bool(targetName)))

    /// 조건부 SET/RESET
    let setWhen condition targetName = set 0 condition targetName
    let resetWhen condition targetName = reset 0 condition targetName

    /// 래치 (자기유지)
    let latch setCondition resetCondition targetName =
        let target = DsTag.Bool(targetName)
        let current = Terminal(target)
        assign 0 target (or' setCondition current &&. not' resetCondition)

    /// 카운터 조작 (CTU/CTD: [name, enable, preset] 형식)
    /// CTU/CTD는 값을 반환하므로 Assign으로 결과를 저장
    let countUp step condition counterName preset =
        Assign(step, DsTag.Int(counterName),
               Function("CTU", [stringExpr counterName; condition; intExpr preset]))
    let countDown step condition counterName preset =
        Assign(step, DsTag.Int(counterName),
               Function("CTD", [stringExpr counterName; condition; boolExpr false; intExpr preset]))
    let countReset step condition counterName =
        whenAt step condition (mov (intExpr 0) (DsTag.Int(counterName)))

    /// 타이머 조작 (TON/TOF: [enable, name, preset] 형식)
    /// TON/TOF는 Bool 값을 반환하므로 Assign으로 결과를 저장
    let startTimer step condition timerName preset =
        Assign(step, DsTag.Bool(timerName), Function("TON", [condition; stringExpr timerName; intExpr preset]))
    let stopTimer step condition timerName preset =
        Assign(step, DsTag.Bool(timerName), Function("TOF", [condition; stringExpr timerName; intExpr preset]))

    /// 수학 연산 (조건부 Assign 문장으로 결과 저장)
    let add' step condition source1 source2 targetName =
        let result = Function("ADD", [source1; source2])
        whenAt step condition (mov result (DsTag.Int(targetName)))
    let sub' step condition source1 source2 targetName =
        let result = Function("SUB", [source1; source2])
        whenAt step condition (mov result (DsTag.Int(targetName)))
    let mul' step condition source1 source2 targetName =
        let result = Function("MUL", [source1; source2])
        whenAt step condition (mov result (DsTag.Int(targetName)))
    let div' step condition source1 source2 targetName =
        let result = Function("DIV", [source1; source2])
        whenAt step condition (mov result (DsTag.Int(targetName)))

    /// 비교 연산 결과를 변수에 저장
    let compare step condition source1 source2 compareOp targetName =
        let compareExpr = Binary(compareOp, source1, source2)
        assignAt step targetName typeof<bool> compareExpr

    /// 조건부 점프 (상태 기계용)
    let jumpTo step condition targetState stateName =
        whenAt step condition (mov (intExpr targetState) (DsTag.Int(stateName)))

    /// 시퀀스 스텝 생성
    let sequenceStep stepNumber condition nextStep stateName actions =
        let guard = eq (Terminal(DsTag.Int(stateName))) (intExpr stepNumber)
        let advanceCondition = and' guard condition
        let advanceAction = mov (intExpr nextStep) (DsTag.Int(stateName))
        actions @ [whenAt 0 advanceCondition advanceAction]