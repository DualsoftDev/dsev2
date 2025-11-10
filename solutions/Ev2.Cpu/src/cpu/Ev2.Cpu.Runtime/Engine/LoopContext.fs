namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Core.Common

// ═════════════════════════════════════════════════════════════════════════════
// Loop Execution Context - Runtime State Tracking for FOR/WHILE Loops
// ═════════════════════════════════════════════════════════════════════════════
// 반복문 실행 시 필요한 상태 추적 및 안전성 메커니즘 제공:
// - 중첩 루프 스택 관리
// - 반복 횟수 제한 및 감시
// - BREAK 플래그 처리
// - 루프 변수 상태 추적
// ═════════════════════════════════════════════════════════════════════════════

/// 루프 타입 분류
[<StructuralEquality; NoComparison>]
type LoopType =
    /// FOR 루프 (카운터 기반)
    | ForLoop
    /// WHILE 루프 (조건 기반)
    | WhileLoop

/// 단일 루프의 실행 상태
[<StructuralEquality; NoComparison>]
type LoopState = {
    /// 루프 타입
    LoopType: LoopType

    /// 루프 변수 이름 (FOR 루프만 해당, WHILE은 None)
    LoopVariable: string option

    /// 현재 반복 횟수 (0부터 시작)
    CurrentIteration: int

    /// 최대 반복 횟수 제한 (None이면 무제한, 안전성 검사용)
    MaxIterations: int option

    /// FOR 루프의 종료 값 (FOR만 해당)
    EndValue: int option

    /// FOR 루프의 증분 값 (FOR만 해당, 기본값 1)
    StepValue: int option

    /// 루프 시작 시각 (성능 모니터링/타임아웃용)
    /// MAJOR FIX (DEFECT-016-6): Use Stopwatch timestamp instead of DateTime
    StartTimestamp: int64

    /// BREAK 플래그 - true면 즉시 탈출
    BreakRequested: bool
}

/// 루프 실행 컨텍스트 (중첩 루프 지원)
type LoopContext(timeProvider: ITimeProvider) =

    /// 루프 스택 (중첩 루프 추적용) - 가장 안쪽 루프가 Top
    let mutable loopStack: LoopState list = []

    /// 전역 최대 스택 깊이 (중첩 제한)
    let mutable maxStackDepth = 10

    /// 전역 최대 반복 횟수 (기본값 - 루프별로 오버라이드 가능)
    let mutable defaultMaxIterations = 10000

    /// 전역 타임아웃 (밀리초, 0이면 비활성화)
    let mutable globalTimeout = 5000

    // ─────────────────────────────────────────────────────────────────
    // 설정 프로퍼티
    // ─────────────────────────────────────────────────────────────────

    /// 최대 중첩 깊이 설정/조회
    member _.MaxStackDepth
        with get() = maxStackDepth
        and set(value) =
            if value <= 0 then
                raise (ArgumentException("Max stack depth must be positive"))
            maxStackDepth <- value

    /// 기본 최대 반복 횟수 설정/조회
    member _.DefaultMaxIterations
        with get() = defaultMaxIterations
        and set(value) =
            if value <= 0 then
                raise (ArgumentException("Default max iterations must be positive"))
            defaultMaxIterations <- value

    /// 전역 타임아웃 설정/조회 (밀리초)
    member _.GlobalTimeout
        with get() = globalTimeout
        and set(value) =
            if value < 0 then
                raise (ArgumentException("Global timeout must be non-negative"))
            globalTimeout <- value

    // ─────────────────────────────────────────────────────────────────
    // 스택 상태 조회
    // ─────────────────────────────────────────────────────────────────

    /// 현재 중첩 깊이 (0 = 루프 밖, 1 = 최외곽 루프, ...)
    member _.CurrentDepth = loopStack.Length

    /// 루프 내부에 있는지 여부
    member _.IsInLoop = not loopStack.IsEmpty

    /// 현재 루프 상태 조회 (가장 안쪽)
    member _.CurrentLoop : LoopState option =
        match loopStack with
        | [] -> None
        | top :: _ -> Some top

    /// 전체 루프 스택 조회 (디버깅용)
    member _.LoopStack = loopStack

    // ─────────────────────────────────────────────────────────────────
    // 루프 진입/탈출
    // ─────────────────────────────────────────────────────────────────

    /// FOR 루프 진입
    member this.EnterForLoop(loopVarName: string, endValue: int, stepValue: int, ?maxIter: int) =
        // 스택 깊이 체크
        if loopStack.Length >= maxStackDepth then
            RuntimeExceptions.raiseLoopStackOverflow loopStack.Length maxStackDepth

        let maxIterations =
            match maxIter with
            | Some m when m > 0 -> Some m
            | _ -> Some defaultMaxIterations

        let newState = {
            LoopType = ForLoop
            LoopVariable = Some loopVarName
            CurrentIteration = 0
            MaxIterations = maxIterations
            EndValue = Some endValue
            StepValue = Some stepValue
            // MAJOR FIX (DEFECT-016-6): Use GetTimestamp() for monotonic clock guarantee
            // UtcNow is system clock, vulnerable to NTP/DST adjustments
            StartTimestamp = timeProvider.GetTimestamp()
            BreakRequested = false
        }

        loopStack <- newState :: loopStack

    /// WHILE 루프 진입
    member this.EnterWhileLoop(?maxIter: int) =
        // 스택 깊이 체크
        if loopStack.Length >= maxStackDepth then
            RuntimeExceptions.raiseLoopStackOverflow loopStack.Length maxStackDepth

        let maxIterations =
            match maxIter with
            | Some m when m > 0 -> Some m
            | _ -> Some defaultMaxIterations

        let newState = {
            LoopType = WhileLoop
            LoopVariable = None
            CurrentIteration = 0
            MaxIterations = maxIterations
            EndValue = None
            StepValue = None
            // MAJOR FIX (DEFECT-016-6): Use GetTimestamp() for monotonic clock guarantee
            // UtcNow is system clock, vulnerable to NTP/DST adjustments
            StartTimestamp = timeProvider.GetTimestamp()
            BreakRequested = false
        }

        loopStack <- newState :: loopStack

    /// 루프 탈출 (정상 종료 또는 BREAK)
    member this.ExitLoop() =
        match loopStack with
        | [] ->
            raise (InvalidOperationException("Cannot exit loop: not in any loop"))
        | _ :: rest ->
            loopStack <- rest

    // ─────────────────────────────────────────────────────────────────
    // 반복 관리
    // ─────────────────────────────────────────────────────────────────

    /// 반복 증가 (루프 본문 실행 전 호출)
    /// 반환값: true = 계속 실행, false = 제한 초과로 중단
    member this.IncrementIteration() : bool =
        match loopStack with
        | [] ->
            raise (InvalidOperationException("Cannot increment iteration: not in any loop"))
        | current :: rest ->
            let newIteration = current.CurrentIteration + 1

            // 최대 반복 횟수 체크
            // CRITICAL FIX: Use > instead of >= to allow max-th iteration (off-by-one fix)
            // Example: maxIterations=1 should allow 1 iteration (newIteration goes from 0→1)
            let shouldContinue =
                match current.MaxIterations with
                | Some max when newIteration > max -> false
                | _ -> true

            // MAJOR FIX (DEFECT-016-6): Use GetTimestamp() for timeout check (monotonic clock guarantee)
            // UtcNow is system clock, vulnerable to NTP/DST/manual adjustments
            // GetTimestamp() returns Stopwatch ticks for true monotonic timeout enforcement
            let nowTimestamp = timeProvider.GetTimestamp()
            let elapsedMs = Timebase.elapsedMilliseconds current.StartTimestamp nowTimestamp
            let timedOut = globalTimeout > 0 && elapsedMs > globalTimeout

            // CRITICAL FIX: Return false instead of throwing exceptions (graceful exit)
            // LoopEngine.executeFor/While expect false return for limit/timeout violations
            // Throwing tears down the scan as fatal exception instead of controlled exit
            let finalShouldContinue = shouldContinue && not timedOut

            // 상태 업데이트
            let updated = { current with CurrentIteration = newIteration }
            loopStack <- updated :: rest

            finalShouldContinue

    /// BREAK 요청 (가장 안쪽 루프에 BREAK 플래그 설정)
    member this.RequestBreak() =
        match loopStack with
        | [] ->
            raise (InvalidOperationException("Cannot break: not in any loop"))
        | current :: rest ->
            let updated = { current with BreakRequested = true }
            loopStack <- updated :: rest

    /// BREAK 플래그 확인
    member this.IsBreakRequested() : bool =
        match loopStack with
        | [] -> false
        | current :: _ -> current.BreakRequested

    /// BREAK 플래그 클리어
    member this.ClearBreak() =
        match loopStack with
        | [] -> ()
        | current :: rest ->
            let updated = { current with BreakRequested = false }
            loopStack <- updated :: rest

    // ─────────────────────────────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────────────────────────────

    /// 모든 루프 스택 초기화 (에러 복구용)
    member this.Reset() =
        loopStack <- []

    /// 현재 루프 정보를 문자열로 변환 (디버깅용)
    member this.ToDebugString() =
        if loopStack.IsEmpty then
            "Not in any loop"
        else
            loopStack
            |> List.rev
            |> List.mapi (fun i state ->
                let loopType =
                    match state.LoopType with
                    | ForLoop -> sprintf "FOR (%s)" (state.LoopVariable |> Option.defaultValue "?")
                    | WhileLoop -> "WHILE"
                let iter = sprintf "iter=%d" state.CurrentIteration
                let maxIter =
                    match state.MaxIterations with
                    | Some m -> sprintf "max=%d" m
                    | None -> "max=∞"
                let breakFlag = if state.BreakRequested then " [BREAK]" else ""
                sprintf "  [%d] %s %s/%s%s" i loopType iter maxIter breakFlag)
            |> String.concat "\n"

/// 루프 컨텍스트 싱글톤 (스레드별 인스턴스 권장)
module LoopContextManager =

    /// CRITICAL FIX: Use ThreadLocal instead of global mutable to prevent concurrent corruption (DEFECT-014-8)
    /// Per-thread loop context for safe concurrent test execution
    /// Previously: global mutable caused parallel tests to trample each other's loop stacks
    let private threadLocalContext = new System.Threading.ThreadLocal<LoopContext option>(fun () -> None)

    /// 기본 컨텍스트 조회 (없으면 생성) - CRITICAL FIX: Thread-safe via ThreadLocal
    let getDefault() =
        match threadLocalContext.Value with
        | Some ctx -> ctx
        | None ->
            // MAJOR FIX: Use SystemTimeProvider as default for backward compatibility
            let ctx = LoopContext(SystemTimeProvider())
            threadLocalContext.Value <- Some ctx
            ctx

    /// 기본 컨텍스트 리셋 - CRITICAL FIX: Thread-safe via ThreadLocal
    let resetDefault() =
        match threadLocalContext.Value with
        | Some ctx -> ctx.Reset()
        | None -> ()

    /// 새 컨텍스트 생성 (멀티스레드 환경용)
    let create(timeProvider: ITimeProvider) = LoopContext(timeProvider)
