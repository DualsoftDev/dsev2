namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

// ═════════════════════════════════════════════════════════════════════════════
// Loop Execution Engine - FOR/WHILE Loop Runtime Execution
// ═════════════════════════════════════════════════════════════════════════════
// LoopContext를 사용하여 실제 루프 실행 로직 제공:
// - FOR 루프: 초기화 → 조건 검사 → 본문 실행 → 증분 → 반복
// - WHILE 루프: 조건 검사 → 본문 실행 → 반복
// - BREAK 처리: 조기 탈출
// - 중첩 루프 지원
// ═════════════════════════════════════════════════════════════════════════════

/// 루프 실행 엔진
module LoopEngine =

    /// FOR 루프 실행
    /// ctx: 실행 컨텍스트 (state checking for stop-on-fatal policy)
    /// loopCtx: 루프 컨텍스트
    /// loopVar: 루프 변수 태그
    /// startExpr: 시작값 표현식
    /// endExpr: 종료값 표현식
    /// stepExpr: 증분 표현식 (None이면 1)
    /// body: 루프 본문 명령문 리스트
    /// evalExpr: 표현식 평가 함수
    /// evalStmt: 명령문 평가 함수
    /// setVar: 변수 설정 함수 (name, value)
    let executeFor
        (ctx: ExecutionContext)
        (loopCtx: LoopContext)
        (loopVar: DsTag)
        (startExpr: DsExpr)
        (endExpr: DsExpr)
        (stepExpr: DsExpr option)
        (body: DsStmt list)
        (evalExpr: DsExpr -> obj)
        (evalStmt: DsStmt -> unit)
        (setVar: string -> obj -> unit)
        : unit =

        // 1. 시작값, 종료값, 증분값 평가
        // CRITICAL FIX (DEFECT-CRIT-14): Safe float-to-int conversion with range validation
        // Previous code: Unchecked int cast from float causes undefined behavior
        // Problem: float values outside Int32 range (e.g., 1e20) truncate unpredictably
        // Solution: Validate range before conversion, use TypeConverter.toInt for IEC semantics
        let startValue =
            match evalExpr startExpr with
            | :? int as i -> i
            | :? float as f ->
                if f > float Int32.MaxValue || f < float Int32.MinValue then
                    raise (ArgumentException($"FOR loop start value {f} exceeds Int32 range"))
                TypeHelpers.toInt (box f)  // IEC 61131-3 truncation toward zero
            | v -> raise (ArgumentException($"FOR loop start value must be numeric, got {v.GetType().Name}"))

        let endValue =
            match evalExpr endExpr with
            | :? int as i -> i
            | :? float as f ->
                if f > float Int32.MaxValue || f < float Int32.MinValue then
                    raise (ArgumentException($"FOR loop end value {f} exceeds Int32 range"))
                TypeHelpers.toInt (box f)  // IEC 61131-3 truncation toward zero
            | v -> raise (ArgumentException($"FOR loop end value must be numeric, got {v.GetType().Name}"))

        let stepValue =
            match stepExpr with
            | Some expr ->
                match evalExpr expr with
                | :? int as i -> i
                | :? float as f ->
                    if f > float Int32.MaxValue || f < float Int32.MinValue then
                        raise (ArgumentException($"FOR loop step value {f} exceeds Int32 range"))
                    TypeHelpers.toInt (box f)  // IEC 61131-3 truncation toward zero
                | v -> raise (ArgumentException($"FOR loop step value must be numeric, got {v.GetType().Name}"))
            | None -> 1

        // 2. 루프 진입 (LoopContext에 상태 추가)
        loopCtx.EnterForLoop(loopVar.Name, endValue, stepValue)

        try
            // 3. 루프 변수 초기화
            setVar loopVar.Name (box startValue)

            // 4. 루프 실행
            let mutable currentValue = startValue
            let mutable shouldContinue = true

            while shouldContinue do
                // 4.1 종료 조건 검사
                let reachedEnd =
                    if stepValue > 0 then
                        currentValue > endValue
                    elif stepValue < 0 then
                        currentValue < endValue
                    else
                        // stepValue = 0이면 무한 루프 방지
                        raise (ArgumentException("FOR loop step value cannot be zero"))

                if reachedEnd then
                    shouldContinue <- false
                else
                    // 4.2 반복 횟수 증가 및 제한 검사
                    let canContinue = loopCtx.IncrementIteration()
                    if not canContinue then
                        shouldContinue <- false
                    else
                        // 4.3 BREAK 확인
                        if loopCtx.IsBreakRequested() then
                            loopCtx.ClearBreak()
                            shouldContinue <- false
                        else
                            // 4.4 본문 실행
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

                            // 4.5 BREAK 재확인 (본문 실행 후)
                            if loopCtx.IsBreakRequested() then
                                loopCtx.ClearBreak()
                                shouldContinue <- false
                            else
                                // 4.6 루프 변수 증가 (only if next iteration will be valid)
                                // CRITICAL FIX (DEFECT-017-3): Check if next value would exceed bounds before incrementing
                                // IEC 61131-3 requires final value to be last used value, not end + step
                                // Without this check, post-loop code reads wrong value (end + step instead of end)
                                let nextValue = currentValue + stepValue
                                let wouldExceedBounds =
                                    if stepValue > 0 then
                                        nextValue > endValue
                                    else
                                        nextValue < endValue

                                if wouldExceedBounds then
                                    // Next iteration would be out of bounds - stop without incrementing
                                    shouldContinue <- false
                                else
                                    // Next iteration is valid - increment and continue
                                    currentValue <- nextValue
                                    setVar loopVar.Name (box currentValue)

        finally
            // 5. 루프 탈출 (LoopContext에서 제거)
            loopCtx.ExitLoop()

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

    /// FOR 루프 간편 실행 (기본 LoopContext 사용)
    let runFor
        (loopVarName: string)
        (startVal: int)
        (endVal: int)
        (stepVal: int)
        (setVar: string -> obj -> unit)
        (body: unit -> unit)
        : unit =

        let loopCtx = LoopContextManager.getDefault()
        loopCtx.EnterForLoop(loopVarName, endVal, stepVal)

        try
            setVar loopVarName (box startVal)
            let mutable current = startVal

            while (if stepVal > 0 then current <= endVal else current >= endVal) do
                if not (loopCtx.IncrementIteration()) then
                    failwith "Loop iteration limit exceeded"

                if loopCtx.IsBreakRequested() then
                    loopCtx.ClearBreak()
                    current <- if stepVal > 0 then endVal + 1 else endVal - 1
                else
                    body()
                    // HIGH FIX (DEFECT-019-1): Check if next value would exceed bounds before incrementing
                    // Previous code wrote (end + step) after last iteration, breaking post-loop logic
                    // IEC 61131-3 requires final value to be last in-range value (RuntimeSpec.md)
                    let nextValue = current + stepVal
                    let wouldExceedBounds =
                        if stepVal > 0 then nextValue > endVal
                        else nextValue < endVal

                    if not wouldExceedBounds then
                        current <- nextValue
                        setVar loopVarName (box current)

        finally
            loopCtx.ExitLoop()

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

    /// FOR 루프 패턴: 범위 반복 (0부터 count-1까지)
    /// 예: forRange 10 (fun i -> printfn "%d" i)
    let forRange (count: int) (action: int -> unit) : unit =
        if count <= 0 then ()
        else
            let loopCtx = LoopContextManager.getDefault()
            loopCtx.EnterForLoop("_i", count - 1, 1)

            try
                // HIGH FIX (DEFECT-019-2): Use while loop to properly exit on BREAK
                // F# for-loop cannot early-exit, so BREAK was cleared but loop continued
                let mutable i = 0
                let mutable shouldContinue = true
                while shouldContinue && i < count do
                    if not (loopCtx.IncrementIteration()) then
                        raise (InvalidOperationException("Loop iteration limit exceeded"))

                    if loopCtx.IsBreakRequested() then
                        loopCtx.ClearBreak()
                        shouldContinue <- false
                    else
                        action i
                        i <- i + 1

            finally
                loopCtx.ExitLoop()

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

    /// 리스트 반복 처리 (배열/리스트 순회)
    /// 예: forEach [1;2;3] (fun item -> printfn "%d" item)
    let forEach (items: 'T list) (action: 'T -> unit) : unit =
        let loopCtx = LoopContextManager.getDefault()
        let count = items.Length
        loopCtx.EnterForLoop("_foreach", count - 1, 1)

        try
            // HIGH FIX (DEFECT-019-4): Use manual iteration to properly exit on BREAK
            // List.iteri cannot early-exit, so BREAK was cleared but iteration continued
            let mutable i = 0
            let mutable shouldContinue = true
            let mutable remaining = items
            while shouldContinue && not (List.isEmpty remaining) do
                if not (loopCtx.IncrementIteration()) then
                    raise (InvalidOperationException("Loop iteration limit exceeded"))

                if loopCtx.IsBreakRequested() then
                    loopCtx.ClearBreak()
                    shouldContinue <- false
                else
                    action (List.head remaining)
                    remaining <- List.tail remaining
                    i <- i + 1

        finally
            loopCtx.ExitLoop()
