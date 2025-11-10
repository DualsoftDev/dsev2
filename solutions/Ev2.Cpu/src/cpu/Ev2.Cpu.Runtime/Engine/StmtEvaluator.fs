namespace Ev2.Cpu.Runtime
open System
open Ev2.Cpu.Core
open Ev2.Cpu.Runtime.DependencyAnalyzer

/// <summary>
/// 문장 실행기 (Statement Evaluator)
/// </summary>
/// <remarks>
/// PLC 문장(DsStmt)을 런타임에 실행하는 핵심 모듈입니다.
/// - Assign: 변수 할당 실행
/// - Command: 조건부 액션 실행
/// - 브레이크포인트 및 워치리스트 지원
/// - 선택적 스캔 최적화 (변경된 변수만 재평가)
/// </remarks>
module StmtEvaluator =

    // ─────────────────────────────────────────────────────────────────────
    // 공통 유틸
    // ─────────────────────────────────────────────────────────────────────

    // 안전한 위치 문자열(브레이크포인트용)
    let private toLocation (stmt: DsStmt) =
        try stmt.ToText() with _ -> stmt.ToString()

    // 조건식 평가 → bool
    let private evalCond (ctx: ExecutionContext) (cond: DsExpr) : bool =
        ExprEvaluator.eval ctx cond |> TypeConverter.toBool

    // 감시 변수에 기록 + 트레이스
    let private setVar (ctx: ExecutionContext) (dsTag: DsTag) (value: obj) =
        ctx.Memory.Set(dsTag.Name, value)
        if ctx.Watchlist.ContainsKey(dsTag.Name) then
            Context.trace ctx $"Watch: {dsTag.Name} = {TypeConverter.toString value}"

    // Action 본체 처리 (MOV/Function/Terminal/임의식)
    let private runAction (ctx: ExecutionContext) (actionExpr: DsExpr) : unit =
        match actionExpr with
        // MOV(source, Terminal target) 최적 경로
        | Function ("MOV", [ source; Terminal (target) ]) ->
            let v = ExprEvaluator.eval ctx source
            setVar ctx target v

        // 임의 함수 호출(부작용/로그 유발)
        | Function (_name, _args) ->
            let _ = ExprEvaluator.eval ctx actionExpr
            ()

        // Terminal만 온 경우: 현재 값을 재기록(히스토리/트레이스 용도)
        | Terminal (target) ->
            let v = ctx.Memory.Get target.Name
            setVar ctx target v

        // 그 외 임의 표현식: 평가만 수행
        | _ ->
            let _ = ExprEvaluator.eval ctx actionExpr
            ()

    // CRITICAL FIX (DEFECT-015-1): Extract all variables from statement list recursively
    // Required for checking loop body dependencies in selective scan mode
    let rec private getStatementVariables (stmts: DsStmt list) : string list =
        stmts
        |> List.collect (fun stmt ->
            match stmt with
            | Assign (_, _, expr) -> getExpressionVariables expr
            | Command (_, cond, action) ->
                let condVars = getExpressionVariables cond
                let actionVars = getExpressionVariables action
                condVars @ actionVars
            | For (_, _, startExpr, endExpr, stepExpr, body) ->
                let startVars = getExpressionVariables startExpr
                let endVars = getExpressionVariables endExpr
                let stepVars = stepExpr |> Option.map getExpressionVariables |> Option.defaultValue []
                let bodyVars = getStatementVariables body
                startVars @ endVars @ stepVars @ bodyVars
            | While (_, cond, body, _) ->
                let condVars = getExpressionVariables cond
                let bodyVars = getStatementVariables body
                condVars @ bodyVars
            | Break _ -> []
        )
        |> List.distinct

    // 문장 실행 필요성 판단 (선택적 스캔)
    let private shouldExecuteStatement (ctx: ExecutionContext) (stmt: DsStmt) : bool =
        let hasAnyChanged names =
            match names |> List.distinct with
            | [] -> true  // No dependencies = always execute (constants, unconditional commands)
            | vars -> vars |> List.exists ctx.Memory.HasChanged

        match stmt with
        | Assign (_, target, expr) ->
            // CRITICAL FIX (DEFECT-018-1): Exclude target from dependency check for self-referential assignments
            // Counter := Counter + 1 would stall after first scan because Counter is both target and dependency
            // Only check if OTHER variables in the expression have changed (not the assignment target itself)
            expr
            |> getExpressionVariables
            |> List.filter (fun v -> v <> target.Name)  // Exclude self-reference
            |> hasAnyChanged

        | Command (_, condition, actionExpr) ->
            let conditionVars = getExpressionVariables condition
            let actionVars =
                match actionExpr with
                | Function ("MOV", [_; Terminal target]) ->
                    getExpressionVariables actionExpr
                    |> List.filter (fun name -> name <> target.Name)
                | _ ->
                    getExpressionVariables actionExpr

            (conditionVars @ actionVars) |> hasAnyChanged

        | For (_, _, startExpr, endExpr, stepExpr, body) ->
            // CRITICAL FIX: FOR loop should execute if start/end/step OR body variables changed (DEFECT-015-1)
            // Body dependencies must be checked to avoid skipping loops when only internal variables change
            let startVars = getExpressionVariables startExpr
            let endVars = getExpressionVariables endExpr
            let stepVars = stepExpr |> Option.map getExpressionVariables |> Option.defaultValue []
            let bodyVars = getStatementVariables body  // Check body variables recursively
            (startVars @ endVars @ stepVars @ bodyVars) |> hasAnyChanged

        | While (_, condition, body, _) ->
            // CRITICAL FIX: WHILE loop should execute if condition OR body variables changed (DEFECT-015-1)
            // Body dependencies must be checked to avoid skipping loops when only internal variables change
            let condVars = getExpressionVariables condition
            let bodyVars = getStatementVariables body  // Check body variables recursively
            (condVars @ bodyVars) |> hasAnyChanged

        | Break (_) ->
            // BREAK always executes if reached
            true

    // ─────────────────────────────────────────────────────────────────────
    // 단일 문장 실행
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>단일 문장 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmt">실행할 문장</param>
    /// <remarks>
    /// 브레이크포인트를 확인하고 문장을 실행합니다.
    /// - Assign: 표현식을 평가하여 대상 변수에 할당
    /// - Command: 조건이 참이면 액션 실행
    /// 예외 발생 시 Context.error로 기록됩니다.
    /// </remarks>
    let rec exec (ctx: ExecutionContext) (stmt: DsStmt) : unit =
        // 브레이크포인트 즉시 중단
        let location = toLocation stmt
        if Context.checkBreakpoint ctx location then
            ()
        else
            // CRITICAL FIX: Wrap statement execution with exception classification
            try
                match stmt with
                // 변수 대입: target := expr
                | Assign (_, target, expr) ->
                    let value = ExprEvaluator.eval ctx expr
                    setVar ctx target value

                // 조건부 액션: if condition then action
                | Command (_, condition, actionExpr) ->
                    if evalCond ctx condition then
                        runAction ctx actionExpr

                // FOR 루프: FOR loopVar := startExpr TO endExpr STEP stepExpr DO body END_FOR
                | For (_, loopVar, startExpr, endExpr, stepExpr, body) ->
                    // MEDIUM FIX: Use per-context LoopContext instead of global singleton
                    // CRITICAL FIX (DEFECT-016-1): Pass ctx for state checking in loop body
                    LoopEngine.executeFor
                        ctx
                        ctx.LoopContext
                        loopVar
                        startExpr
                        endExpr
                        stepExpr
                        body
                        (ExprEvaluator.eval ctx)
                        (exec ctx)
                        (fun name value -> ctx.Memory.Set(name, value))

                // WHILE 루프: WHILE condition DO body END_WHILE
                | While (_, condition, body, maxIterations) ->
                    // MEDIUM FIX: Use per-context LoopContext instead of global singleton
                    // CRITICAL FIX (DEFECT-016-1): Pass ctx for state checking in loop body
                    LoopEngine.executeWhile
                        ctx
                        ctx.LoopContext
                        condition
                        body
                        maxIterations
                        (ExprEvaluator.eval ctx)
                        (exec ctx)

                // BREAK: 루프 탈출
                | Break (_) ->
                    // MEDIUM FIX: Use per-context LoopContext instead of global singleton
                    LoopEngine.executeBreak ctx.LoopContext
            with ex ->
                // Classify exception and log appropriately
                let severity = ctx.ClassifyException(ex)
                match severity with
                | RuntimeErrorSeverity.Fatal ->
                    ctx.LogFatal($"Statement execution failed: {ex.Message}", astNode = stmt, ex = ex)
                    // CRITICAL FIX: Set ExecutionState.Error to halt scan immediately (RuntimeSpec §6)
                    // execList checks ctx.State and stops iterating when Error is detected
                    ctx.State <- ExecutionState.Error ex.Message
                | RuntimeErrorSeverity.Recoverable ->
                    ctx.LogRecoverable($"Statement execution failed (recoverable): {ex.Message}", astNode = stmt, ex = ex)
                | RuntimeErrorSeverity.Warning ->
                    ctx.LogWarning($"Statement execution warning: {ex.Message}", astNode = stmt)

    // ─────────────────────────────────────────────────────────────────────
    // 문장 리스트 실행
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>문장 리스트 순차 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <remarks>
    /// 컨텍스트 상태가 Running인 동안만 문장을 실행합니다.
    /// Paused, Stopped, Error, Breakpoint 상태에서는 실행을 중단합니다.
    /// </remarks>
    let execList (ctx: ExecutionContext) (stmts: DsStmt list) : unit =
        for s in stmts do
            match ctx.State with
            | ExecutionState.Running       -> exec ctx s
            | ExecutionState.Paused
            | ExecutionState.Stopped
            | ExecutionState.Error _
            | ExecutionState.Breakpoint _  -> ()  // 현재 상태에서는 실행 안 함

    /// <summary>선택적 문장 리스트 실행 (최적화)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <remarks>
    /// 변경된 변수에 의존하는 문장만 선택적으로 실행합니다.
    /// - shouldExecuteStatement로 실행 필요성 판단
    /// - 변경되지 않은 변수만 사용하는 문장은 스킵
    /// - 스캔 시간 최적화에 유용
    /// </remarks>
    let execListSelective (ctx: ExecutionContext) (stmts: DsStmt list) : unit =
        for s in stmts do
            match ctx.State with
            | ExecutionState.Running ->
                if shouldExecuteStatement ctx s then
                    exec ctx s
            | ExecutionState.Paused
            | ExecutionState.Stopped
            | ExecutionState.Error _
            | ExecutionState.Breakpoint _  -> ()

    /// <summary>Result 기반 문장 리스트 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <returns>성공 시 Ok (), 실패 시 Error with 오류 메시지</returns>
    /// <remarks>
    /// execList의 Result 래퍼 버전입니다.
    /// - 예외를 Result 타입으로 변환
    /// - 함수형 에러 처리 파이프라인에서 사용
    /// - 오류는 RuntimeError.Fatal로 기록됨
    /// </remarks>
    let execListResult (ctx: ExecutionContext) (stmts: DsStmt list) : Result<unit,string> =
        try
            execList ctx stmts
            Ok ()
        with ex ->
            ctx.LogFatal($"Execution failed: {ex.Message}", ex = ex)
            Error ex.Message

    // ─────────────────────────────────────────────────────────────────────
    // 단일 스캔 실행 (사이클 단위)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>단일 스캔 사이클 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <remarks>
    /// PLC 스캔 사이클을 시뮬레이션합니다.
    /// - 스캔 카운터 증가
    /// - 실행 시간 측정 및 오버런 경고
    /// - CycleTime 초과 시 warning 생성
    /// - LastCycle 시간 업데이트
    /// </remarks>
    let execScan (ctx: ExecutionContext) (stmts: DsStmt list) : unit =
        // MAJOR FIX: Scan index now incremented by CpuScanEngine.ScanOnce() BEFORE execution
        // to prevent off-by-one error in telemetry events
        // MAJOR FIX: Use monotonic clock (TimeProvider) instead of DateTime.UtcNow
        // to prevent incorrect timing when system clock is adjusted
        let t0 = ctx.TimeProvider.GetTimestamp()
        // CRITICAL FIX: Use WithTransaction for exception classification and rollback
        match ctx.WithTransaction(fun () -> execList ctx stmts) with
        | Ok () ->
            let t1 = ctx.TimeProvider.GetTimestamp()
            let elapsed = Timebase.elapsedMilliseconds t0 t1
            if elapsed > ctx.CycleTime then
                Context.warning ctx $"Scan overrun: {elapsed}ms > {ctx.CycleTime}ms"
            ctx.LastCycle <- DateTime.UtcNow  // UI display timestamp
            ctx.LastCycleTicks <- t1
        | Error error ->
            // CRITICAL FIX: Only fatal errors stop scan loop - recoverable errors continue
            // Transaction already rolled back and logged by WithTransaction
            match error.Severity with
            | RuntimeErrorSeverity.Fatal ->
                ctx.State <- ExecutionState.Error error.Message
                // LogFatal already called in WithTransaction, no need to call again
            | RuntimeErrorSeverity.Recoverable ->
                // Recoverable error - already logged and rolled back, continue execution
                ()
            | RuntimeErrorSeverity.Warning ->
                // Warning - already logged, continue execution
                ()

    /// <summary>선택적 스캔 사이클 실행 (최적화)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <remarks>
    /// 변경 기반 최적화를 적용한 스캔 실행입니다.
    /// - execListSelective 사용하여 필요한 문장만 실행
    /// - 실행 시간 단축 효과
    /// - 스캔 오버런 경고 포함
    /// - 대규모 프로그램에 유용
    /// </remarks>
    let execScanSelective (ctx: ExecutionContext) (stmts: DsStmt list) : unit =
        // HIGH FIX: Scan index now incremented by CpuScanEngine.ScanOnce() BEFORE execution
        // to prevent double-increment in selective mode (was incrementing twice per cycle)
        // HIGH FIX: Use monotonic clock (TimeProvider) instead of DateTime.UtcNow
        let t0 = ctx.TimeProvider.GetTimestamp()
        // CRITICAL FIX: Use WithTransaction for exception classification and rollback
        match ctx.WithTransaction(fun () -> execListSelective ctx stmts) with
        | Ok () ->
            let t1 = ctx.TimeProvider.GetTimestamp()
            let elapsed = Timebase.elapsedMilliseconds t0 t1
            if elapsed > ctx.CycleTime then
                Context.warning ctx $"Scan overrun: {elapsed}ms > {ctx.CycleTime}ms"
            ctx.LastCycle <- DateTime.UtcNow  // UI display timestamp
            ctx.LastCycleTicks <- t1
        | Error error ->
            // CRITICAL FIX: Only fatal errors stop scan loop - recoverable errors continue
            // Transaction already rolled back and logged by WithTransaction
            match error.Severity with
            | RuntimeErrorSeverity.Fatal ->
                ctx.State <- ExecutionState.Error error.Message
                // LogFatal already called in WithTransaction, no need to call again
            | RuntimeErrorSeverity.Recoverable ->
                // Recoverable error - already logged and rolled back, continue execution
                ()
            | RuntimeErrorSeverity.Warning ->
                // Warning - already logged, continue execution
                ()

    // ─────────────────────────────────────────────────────────────────────
    // 연속 실행 (주기적 스캔 루프)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>연속 스캔 루프 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmts">실행할 문장 목록</param>
    /// <param name="token">취소 토큰</param>
    /// <remarks>
    /// PLC 런타임을 시뮬레이션하는 연속 실행 루프입니다.
    /// - CycleTime에 맞춰 주기적으로 스캔 실행
    /// - CancellationToken으로 외부 중단 지원
    /// - 실행 시간을 고려한 대기 시간 조정
    /// - 종료 시 자동으로 Stopped 상태로 전환
    /// </remarks>
    let execContinuous (ctx: ExecutionContext) (stmts: DsStmt list) (token: System.Threading.CancellationToken) =
        // MAJOR FIX (DEFECT-017-5): Only transition Stopped → Running, preserve Paused/Breakpoint
        // Host may have set Paused or Breakpoint for diagnostics - don't overwrite
        if ctx.State = ExecutionState.Stopped then
            ctx.State <- ExecutionState.Running

        while not token.IsCancellationRequested && ctx.State = ExecutionState.Running do
            execScan ctx stmts
            let elapsed = int (DateTime.UtcNow - ctx.LastCycle).TotalMilliseconds
            let waitMs  = Math.Max(0, ctx.CycleTime - elapsed)
            if waitMs > 0 then
                token.WaitHandle.WaitOne(waitMs) |> ignore

        // MAJOR FIX (DEFECT-017-5): Only transition Running → Stopped, preserve Error/Breakpoint
        // If execution was halted due to error or breakpoint, preserve that state
        if ctx.State = ExecutionState.Running then
            ctx.State <- ExecutionState.Stopped

    // ─────────────────────────────────────────────────────────────────────
    // 단계 실행 (디버깅)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>단일 문장 스텝 실행 (디버깅용)</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmt">실행할 문장</param>
    /// <remarks>
    /// 디버거에서 사용하는 단계별 실행 함수입니다.
    /// - 실행 전 트레이스 로그 기록
    /// - 문장 위치 정보 포함
    /// - exec 호출하여 실제 실행
    /// </remarks>
    let step (ctx: ExecutionContext) (stmt: DsStmt) : unit =
        Context.trace ctx $"Step: {toLocation stmt}"
        exec ctx stmt

    // ─────────────────────────────────────────────────────────────────────
    // 문장 검증(실행 없이, 구조적 오류만 간단 점검) — Result 기반(예외 X)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>문장 구조 검증 (실행 없이)</summary>
    /// <param name="stmt">검증할 문장</param>
    /// <returns>성공 시 Ok (), 실패 시 Error with 오류 메시지</returns>
    /// <remarks>
    /// 실행 전 문장의 구조적 유효성을 검사합니다.
    /// - 빈 변수명/함수명 체크
    /// - 표현식 트리 재귀 검증
    /// - 예외를 사용하지 않는 Result 기반 검증
    /// - 컴파일 타임 검증에 유용
    /// </remarks>
    let validate (stmt: DsStmt) : Result<unit, string> =

        // 헬퍼
        let ok : Result<unit,string> = Ok ()
        let fail (msg: string) : Result<unit,string> = Error msg
        let combine (r1: Result<unit,string>) (r2: Result<unit,string>) =
            match r1 with
            | Ok ()     -> r2
            | Error _   -> r1

        // 표현식 점검
        let rec checkExpr (e: DsExpr) : Result<unit, string> =
            match e with
            | Const    (_v, _t) -> ok
            | Terminal (dsTag) ->
                if String.IsNullOrWhiteSpace dsTag.Name then fail "Empty terminal name" else ok
            | Unary    (_op, e1) -> checkExpr e1
            | Binary   (_op, l, r) -> checkExpr l |> combine (checkExpr r)
            | Function (name, args) ->
                if String.IsNullOrWhiteSpace name then
                    fail "Empty function name"
                else
                    args |> List.fold (fun acc a -> acc |> combine (checkExpr a)) ok

        // 문장 점검 (프로젝트 정의된 DsStmt 케이스가 더 있다면 여기서 추가)
        let rec checkStmt (s: DsStmt) : Result<unit, string> =
            match s with
            | Assign (_, target, expr) ->
                if String.IsNullOrWhiteSpace target.Name
                then fail "Empty assignment target"
                else checkExpr expr
            | Command (_, cond, act) ->
                checkExpr cond |> combine (checkExpr act)
            | Break _ | For _ | While _ ->
                ok

        try checkStmt stmt with ex -> fail ex.Message



/// <summary>디버거 유틸리티 모듈</summary>
/// <remarks>
/// 브레이크포인트, 워치리스트, 트레이스, 스냅샷 등 디버깅 기능을 제공합니다.
/// - 브레이크포인트: 특정 문장에서 실행 중단
/// - 워치리스트: 특정 변수 변경 모니터링
/// - 트레이스: 실행 로그 수집
/// - 스냅샷: 메모리 상태 캡처
/// - 실행 제어: Pause/Resume/Stop
/// </remarks>
module Debugger =

    // ── 브레이크포인트 ───────────────────────────────────────────────────

    /// <summary>브레이크포인트 추가</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="location">중단할 문장 위치 (문장의 텍스트 표현)</param>
    /// <remarks>해당 위치에서 실행이 중단됩니다.</remarks>
    let addBreakpoint    (ctx: ExecutionContext) (location: string) =
        ctx.Breakpoints.TryAdd(location, 0uy) |> ignore
        Context.trace ctx $"Breakpoint added: {location}"

    /// <summary>브레이크포인트 제거</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="location">제거할 브레이크포인트 위치</param>
    let removeBreakpoint (ctx: ExecutionContext) (location: string) =
        ctx.Breakpoints.TryRemove(location) |> ignore
        Context.trace ctx $"Breakpoint removed: {location}"

    /// <summary>모든 브레이크포인트 제거</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    let clearBreakpoints (ctx: ExecutionContext) =
        ctx.Breakpoints.Clear()
        Context.trace ctx "Breakpoints cleared"

    /// <summary>설정된 모든 브레이크포인트 목록 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <returns>브레이크포인트 위치 문자열 리스트</returns>
    let listBreakpoints  (ctx: ExecutionContext) : string list =
        ctx.Breakpoints.Keys |> Seq.toList

    // ── 워치 리스트 ──────────────────────────────────────────────────────

    /// <summary>워치 변수 추가</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">모니터링할 변수 이름</param>
    /// <remarks>변수 값이 변경될 때마다 트레이스에 기록됩니다.</remarks>
    let addWatch    (ctx: ExecutionContext) (name: string) =
        ctx.Watchlist.TryAdd(name, 0uy) |> ignore
        Context.trace ctx $"Watch added: {name}"

    /// <summary>워치 변수 제거</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">제거할 워치 변수 이름</param>
    let removeWatch (ctx: ExecutionContext) (name: string) =
        ctx.Watchlist.TryRemove(name) |> ignore
        Context.trace ctx $"Watch removed: {name}"

    /// <summary>모든 워치 변수 제거</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    let clearWatch  (ctx: ExecutionContext) =
        ctx.Watchlist.Clear()
        Context.trace ctx "Watchlist cleared"

    /// <summary>설정된 모든 워치 변수 목록 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <returns>워치 변수 이름 리스트</returns>
    let listWatch   (ctx: ExecutionContext) : string list =
        ctx.Watchlist.Keys |> Seq.toList

    // ── 트레이스/스냅샷 ─────────────────────────────────────────────────

    /// <summary>트레이스 로그 전체 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <returns>트레이스 메시지 리스트 (시간순)</returns>
    let getTrace (ctx: ExecutionContext) : string list =
        ctx.Trace.ToArray() |> Array.toList

    /// <summary>트레이스 로그 삭제</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    let clearTrace (ctx: ExecutionContext) =
        ctx.Trace.Clear()

    /// <summary>현재 메모리 상태 스냅샷 생성</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <returns>모든 변수와 현재 값의 맵</returns>
    /// <remarks>특정 시점의 전체 메모리 상태를 캡처합니다.</remarks>
    let snapshot (ctx: ExecutionContext) : Map<string,obj> =
        ctx.Memory.Snapshot()

    /// <summary>변수 값 히스토리 조회</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="name">조회할 변수 이름</param>
    /// <param name="count">가져올 히스토리 개수</param>
    /// <returns>변수의 과거 값 리스트 (최신순)</returns>
    let varHistory (ctx: ExecutionContext) (name: string) (count: int) =
        ctx.Memory.GetHistory(name, count = count)

    // ── 실행 제어 ────────────────────────────────────────────────────────

    /// <summary>실행 일시 정지</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <remarks>실행 중인 프로그램을 일시 정지합니다.</remarks>
    let pause   (ctx: ExecutionContext) = ctx.State <- ExecutionState.Paused

    /// <summary>실행 재개</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <remarks>일시 정지된 프로그램을 다시 실행합니다.</remarks>
    let resume  (ctx: ExecutionContext) = ctx.State <- ExecutionState.Running

    /// <summary>실행 중단</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <remarks>프로그램 실행을 완전히 중단합니다.</remarks>
    let stop    (ctx: ExecutionContext) = ctx.State <- ExecutionState.Stopped

    /// <summary>단일 문장 스텝 실행</summary>
    /// <param name="ctx">실행 컨텍스트</param>
    /// <param name="stmt">실행할 문장</param>
    /// <remarks>StmtEvaluator.step을 호출하여 한 문장씩 실행합니다.</remarks>
    let step (ctx: ExecutionContext) (stmt: DsStmt) =
        StmtEvaluator.step ctx stmt

    // ── 도우미 출력 ──────────────────────────────────────────────────────

    /// <summary>스냅샷을 사람이 읽을 수 있는 텍스트로 포맷팅</summary>
    /// <param name="snap">메모리 스냅샷 (변수명 -> 값 맵)</param>
    /// <returns>각 변수를 "변수명 = 값" 형식으로 표시한 멀티라인 문자열</returns>
    /// <remarks>null 값은 "&lt;null&gt;"로 표시됩니다.</remarks>
    let formatSnapshot (snap: Map<string,obj>) =
        snap
        |> Seq.map (fun (KeyValue(k, v)) ->
            let s = if isNull v then "<null>" else v.ToString()
            $"{k} = {s}")
        |> String.concat Environment.NewLine
