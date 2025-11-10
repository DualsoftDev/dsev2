namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

// ═════════════════════════════════════════════════════════════════════════════
// Loop Execution Engine - WHILE Loop Runtime Execution
// ═════════════════════════════════════════════════════════════════════════════
// LoopContext를 사용하여 실제 루프 실행 로직 제공:
// - WHILE 루프: 조건 검사 → 본문 실행 → 반복
// - BREAK 처리: 조기 탈출
// - 중첩 루프 지원
// ═════════════════════════════════════════════════════════════════════════════

/// 루프 실행 엔진
module LoopEngine =

    /// WHILE 루프 실행
    /// ctx: 실행 컨텍스트 (state checking for stop-on-fatal policy)
    /// loopCtx: 루프 컨텍스트
    /// condition: 루프 조건 표현식
    /// body: 루프 본문 명령문 리스트
    /// maxIterations: 최대 반복 횟수 (None이면 기본값 사용)
    /// evalExpr: 표현식 평가 함수
    /// evalStmt: 명령문 평가 함수
    let executeWhile
        (ctx: ExecutionContext)
        (loopCtx: LoopContext)
        (condition: DsExpr)
        (body: DsStmt list)
        (maxIterations: int option)
        (evalExpr: DsExpr -> obj)
        (evalStmt: DsStmt -> unit)
        : unit =

        // 1. 루프 진입 (LoopContext에 상태 추가)
        match maxIterations with
        | Some max -> loopCtx.EnterWhileLoop(max)
        | None -> loopCtx.EnterWhileLoop()

        try
            // 2. 루프 실행
            let mutable shouldContinue = true

            while shouldContinue do
                // 2.1 조건 평가
                let conditionResult =
                    match evalExpr condition with
                    | :? bool as b -> b
                    | :? int as i -> i <> 0
                    | :? float as f -> f <> 0.0
                    | v -> raise (ArgumentException($"WHILE loop condition must be boolean or numeric, got {v.GetType().Name}"))

                if not conditionResult then
                    shouldContinue <- false
                else
                    // 2.2 반복 횟수 증가 및 제한 검사
                    let canContinue = loopCtx.IncrementIteration()
                    if not canContinue then
                        shouldContinue <- false
                    else
                        // 2.3 BREAK 확인
                        if loopCtx.IsBreakRequested() then
                            loopCtx.ClearBreak()
                            shouldContinue <- false
                        else
                            // 2.4 본문 실행
                            // CRITICAL FIX (DEFECT-016-1): Check ctx.State after each statement
                            // List.iter doesn't stop on Error/Breakpoint, violating stop-on-fatal policy
                            // Execute statements one-by-one with state check (RuntimeSpec.md:114)
                            let mutable bodyIdx = 0
                            let mutable continueBody = true
                            while continueBody && bodyIdx < body.Length do
                                evalStmt body.[bodyIdx]
                                bodyIdx <- bodyIdx + 1
                                // Check if execution should stop (Error, Breakpoint only)
                                // Continue for Running/Stopped/Paused (tests may not set Running initially)
                                match ctx.State with
                                | ExecutionState.Error _ | ExecutionState.Breakpoint _ ->
                                    continueBody <- false  // Stop on fatal error or breakpoint
                                | _ -> ()  // Continue for Running/Stopped/Paused

                            // 2.5 BREAK 재확인 (본문 실행 후)
                            if loopCtx.IsBreakRequested() then
                                loopCtx.ClearBreak()
                                shouldContinue <- false

        finally
            // 3. 루프 탈출 (LoopContext에서 제거)
            loopCtx.ExitLoop()

    /// BREAK 실행 (가장 안쪽 루프에 BREAK 플래그 설정)
    /// loopCtx: 루프 컨텍스트
    let executeBreak (loopCtx: LoopContext) : unit =
        if not loopCtx.IsInLoop then
            raise (InvalidOperationException("BREAK statement outside of loop"))
        loopCtx.RequestBreak()

/// 루프 실행 헬퍼 (편의 함수)
module LoopExecutionHelpers =

    /// WHILE 루프 간편 실행 (기본 LoopContext 사용)
    let runWhile
        (condition: unit -> bool)
        (body: unit -> unit)
        (maxIter: int option)
        : unit =

        let loopCtx = LoopContextManager.getDefault()

        match maxIter with
        | Some max -> loopCtx.EnterWhileLoop(max)
        | None -> loopCtx.EnterWhileLoop()

        try
            let mutable shouldContinue = true
            while shouldContinue && condition() do
                if not (loopCtx.IncrementIteration()) then
                    failwith "Loop iteration limit exceeded"

                if loopCtx.IsBreakRequested() then
                    loopCtx.ClearBreak()
                    shouldContinue <- false
                else
                    body()

                    // CRITICAL FIX (DEFECT-017-10): Check BREAK after body() executes
                    // Previous code only checked before body(), causing infinite loop if body() requests break
                    // Must check again after body() to honor BREAK statements (RuntimeSpec.md:98-100)
                    if loopCtx.IsBreakRequested() then
                        loopCtx.ClearBreak()
                        shouldContinue <- false

        finally
            loopCtx.ExitLoop()

/// 루프 패턴 유틸리티
module LoopPatterns =

    /// WHILE 패턴: 조건 반복 (최대 횟수 제한 있음)
    /// 예: whileWithLimit 100 (fun () -> someCondition()) (fun () -> doSomething())
    let whileWithLimit (maxIter: int) (condition: unit -> bool) (action: unit -> unit) : unit =
        let loopCtx = LoopContextManager.getDefault()
        loopCtx.EnterWhileLoop(maxIter)

        try
            // HIGH FIX (DEFECT-019-3): Use shouldContinue flag to properly exit on BREAK
            // Previous code cleared BREAK but continued the while loop, ignoring BREAK requests
            let mutable shouldContinue = true
            while shouldContinue && condition() do
                if not (loopCtx.IncrementIteration()) then
                    raise (InvalidOperationException($"Loop iteration limit {maxIter} exceeded"))

                if loopCtx.IsBreakRequested() then
                    loopCtx.ClearBreak()
                    shouldContinue <- false
                else
                    action()

        finally
            loopCtx.ExitLoop()
