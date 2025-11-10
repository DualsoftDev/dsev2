namespace Ev2.Cpu.Generation.Make

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// 프로그램 코드 생성
module ProgramGen =

    /// 프로그램 빌더
    type ProgramBuilder(name: string) =
        let mutable inputs = []
        let mutable outputs = []
        let mutable locals = []
        let mutable body = []

        member _.AddInput(name: string, dataType: Type) =
            inputs <- (name, dataType) :: inputs

        member _.AddOutput(name: string, dataType: Type) =
            outputs <- (name, dataType) :: outputs

        member _.AddLocal(name: string, dataType: Type) =
            locals <- (name, dataType) :: locals

        member _.AddStatement(stmt: DsStmt) =
            body <- stmt :: body

        member _.AddStatements(stmts: DsStmt list) =
            body <- (List.rev stmts) @ body

        member _.AddRelay(relay: Relay) =
            body <- (GenerationUtils.relayToStmt relay) :: body

        member _.AddRelays(relays: Relay list) =
            let stmts = relays |> List.map GenerationUtils.relayToStmt
            body <- (List.rev stmts) @ body

        member _.Build() : Program =
            { Name = name
              Inputs = List.rev inputs
              Outputs = List.rev outputs
              Locals = List.rev locals
              Body = List.rev body }

    /// 기본 프로그램 템플릿
    let createEmptyProgram name = 
        { Name = name
          Inputs = []
          Outputs = []
          Locals = []
          Body = [] }

    /// 간단한 릴레이 프로그램
    let createRelayProgram name relays =
        let statements = relays |> List.map GenerationUtils.relayToStmt
        { Name = name
          Inputs = []
          Outputs = []
          Locals = []
          Body = statements }

    /// 상태 기계 프로그램 생성
    let createStateMachineProgram name states =
        let builder = ProgramBuilder(name)
        
        // 상태 변수 추가
        builder.AddLocal("State", typeof<int>)
        builder.AddLocal("NextState", typeof<int>)
        
        // 상태별 로직 추가
        states |> List.iteri (fun i (stateName, logic) ->
            builder.AddLocal($"{stateName}_Active", typeof<bool>)
            let stateCondition = eq (Terminal(DsTag.Int("State"))) (intExpr i)
            builder.AddStatement(assignAuto $"{stateName}_Active" typeof<bool> stateCondition)
            builder.AddStatements(logic)
        )
        
        // 상태 전환 로직
        builder.AddStatement(assignAuto "State" typeof<int> (Terminal(DsTag.Int("NextState"))))
        
        builder.Build()

    /// 타이머 기반 시퀀스 프로그램
    let createTimerSequenceProgram name steps =
        let builder = ProgramBuilder(name)
        
        builder.AddInput("Start", typeof<bool>)
        builder.AddInput("Reset", typeof<bool>)
        builder.AddOutput("Running", typeof<bool>)
        builder.AddOutput("Complete", typeof<bool>)
        builder.AddLocal("Step", typeof<int>)
        
        steps |> List.iteri (fun i (stepName, duration, actions) ->
            let stepCondition = eq (Terminal(DsTag.Int("Step"))) (intExpr i)
            let timerName = sprintf "Step%d_Timer" i
            let timerDone = Function("TON", [stepCondition; stringExpr timerName; intExpr duration])
            
            // 스텝 활성화 출력
            builder.AddOutput($"{stepName}_Active", typeof<bool>)
            builder.AddStatement(assignAuto $"{stepName}_Active" typeof<bool> stepCondition)
            
            // 스텝별 액션
            actions |> List.iter builder.AddStatement
            
            // 다음 스텝으로 전환
            let nextStep = if i < List.length steps - 1 then i + 1 else 0
            builder.AddStatement(whenAt 0 timerDone (mov (intExpr nextStep) (DsTag.Int("Step"))))
        )
        
        // 전역 제어 로직
        let running = not' (eq (Terminal(DsTag.Int("Step"))) (intExpr 0))
        builder.AddStatement(assignAuto "Running" typeof<bool> running)
        
        let complete = and' (not' (Terminal(DsTag.Bool("Start")))) 
                           (eq (Terminal(DsTag.Int("Step"))) (intExpr 0))
        builder.AddStatement(assignAuto "Complete" typeof<bool> complete)
        
        // 리셋 로직
        let resetCondition = Terminal(DsTag.Bool("Reset"))
        builder.AddStatement(when' resetCondition (mov (intExpr 0) (DsTag.Int("Step"))))
        
        builder.Build()

    /// 인터록 프로그램 생성
    let createInterlockProgram name conditions outputs =
        let builder = ProgramBuilder(name)
        
        conditions |> List.iter (fun (inputName, dataType) ->
            builder.AddInput(inputName, dataType))
        
        outputs |> List.iter (fun (outputName, conditionExpr) ->
            builder.AddOutput(outputName, typeof<bool>)
            builder.AddStatement(assignAuto outputName typeof<bool> conditionExpr))
        
        builder.Build()