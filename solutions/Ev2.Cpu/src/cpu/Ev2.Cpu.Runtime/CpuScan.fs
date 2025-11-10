namespace Ev2.Cpu.Runtime

open System
open System.Threading
open System.Threading.Tasks
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined
open Ev2.Cpu.Runtime.DependencyAnalyzer

// ─────────────────────────────────────────────
// 스캔 설정 (간단 버전: 주기/오버런 경고만)
// ─────────────────────────────────────────────
type ScanConfig =
    {
        /// 스캔 주기(ms). None이면 ctx.CycleTime 사용
        CycleTimeMs  : int option
        /// 스캔 경고 임계(ms). None이면 경고 비활성
        WarnIfOverMs : int option
        /// 변경 감지 기반 선택적 스캔
        SelectiveMode: bool
        /// Runtime event sink (RuntimeSpec.md:26 - GAP-003 fix)
        EventSink    : IRuntimeEventSink option
        /// Per-stage deadline thresholds (RuntimeSpec.md:21 - stage-aware deadlines)
        StageThresholds : StageThresholds option
    }
    static member Default =
        // MEDIUM FIX: Per-stage deadlines computed from cycle time (RuntimeSpec.md §2.1/§2.2)
        // StageThresholds will be computed in constructor based on actual cycle time
        { CycleTimeMs = None; WarnIfOverMs = Some 5_000; SelectiveMode = false; EventSink = None; StageThresholds = None }

// ─────────────────────────────────────────────
// 스캔 엔진 (Task 기반, 메모리 직접 접근)
// ─────────────────────────────────────────────
type CpuScanEngine
    (
        program : Statement.Program,
        ctx     : Ev2.Cpu.Runtime.ExecutionContext,
        config  : ScanConfig option,
        updateManager : RuntimeUpdateManager option,
        retainStorage : IRetainStorage option
    ) =

    let mutable cfg = defaultArg config ScanConfig.Default
    let mutable loopTask : Task option = None
    let mutable cts      : CancellationTokenSource option = None
    let mutable isFirstScan = true
    let mutable currentProgramBody = program.Body

    // CRITICAL FIX (DEFECT-CRIT-6): Make pendingRetainSave volatile for thread safety
    // Previous code: mutable field with locks but no volatile semantics
    // Problem: Reader threads may see stale null value even after writer updates it
    // Solution: [<VolatileField>] ensures memory barrier for cross-thread visibility
    // Lock protects compound operations; volatile ensures single-read visibility
    [<VolatileField>]
    let mutable pendingRetainSave : Task option = None
    let retainSaveLock = obj()

    do
        // MEDIUM FIX: Compute per-stage deadlines from cycle time if not specified (RuntimeSpec.md:26)
        let cycleTimeMs = defaultArg cfg.CycleTimeMs ctx.CycleTime
        // MEDIUM FIX: Update ctx.CycleTime with custom cycle time from config
        cfg.CycleTimeMs |> Option.iter (fun customCycleTime -> ctx.CycleTime <- customCycleTime)
        cfg <- match cfg.StageThresholds with
               | None -> { cfg with StageThresholds = Some (StageThresholds.defaultForCycleTime cycleTimeMs) }
               | Some _ -> cfg
        let dependencies = DependencyAnalyzer.buildDependencyMap currentProgramBody
        ctx.Memory.SetDependencyMap dependencies
        if cfg.SelectiveMode then
            ctx.Memory.MarkAllChanged()

        // UpdateManager에 초기 Program.Body 설정
        updateManager |> Option.iter (fun mgr -> mgr.SetProgramBody(currentProgramBody))

        // HIGH FIX: Only create RelayStateManager if not provided by host
        // Preserves custom managers with pre-registered relays or custom sinks
        match ctx.RelayStateManager with
        | None -> ctx.RelayStateManager <- Some (new RelayStateManager(ctx.TimeProvider, cfg.EventSink))
        | Some _ -> ()  // Keep host-supplied manager

        // ═════════════════════════════════════════════════════════════════════
        // 리테인 데이터 자동 복원 (엔진 시작 시)
        // ═════════════════════════════════════════════════════════════════════
        retainStorage |> Option.iter (fun storage ->
            match storage.Load() with
            | Ok (Some snapshot) ->
                ctx.Memory.RestoreFromSnapshot(snapshot)
                Context.trace ctx (sprintf "[RETAIN] Data restored: %d variables from %s"
                    snapshot.Variables.Length
                    (snapshot.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")))
                // MEDIUM FIX: Include FB static data in retain telemetry count (RuntimeSpec.md:79-82)
                let varCount = snapshot.Variables.Length
                let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                let totalCount = varCount + fbStaticCount
                RuntimeTelemetry.retainLoaded totalCount
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.RetainLoaded(totalCount, DateTime.UtcNow)))
            | Ok None ->
                Context.trace ctx "[RETAIN] No retain data found (first run or file deleted)"
            | Error err ->
                Context.warning ctx (sprintf "[RETAIN] Load failed: %s" err))

    /// 단일 스캔을 수행하고 실제 소요 ms를 반환 (GAP-003+005: Per-stage timing, monotonic)
    member this.ScanOnce() : int =
        // BLOCKER FIX: ExecutionState.Error must halt scanning immediately
        // Return 0 to prevent execution on corrupted context (RuntimeSpec safety policy)
        // HIGH FIX: Preserve debug states (Paused/Breakpoint) - don't overwrite
        match ctx.State with
        | ExecutionState.Error _ -> 0  // BLOCKER: Return immediately on fatal error
        | ExecutionState.Paused | ExecutionState.Breakpoint _ -> 0  // HIGH FIX: Preserve debug states
        | ExecutionState.Stopped ->
            // MAJOR FIX: Capture prior state and restore after manual scan
            let priorState = ctx.State
            ctx.State <- ExecutionState.Running
            let result = this.executeScan()
            // Restore Stopped state (unless scan resulted in Error or Breakpoint)
            match ctx.State with
            | ExecutionState.Error _ -> ()  // Keep error state
            | ExecutionState.Breakpoint _ -> ()  // HIGH FIX (DEFECT-018-4): Keep breakpoint state for single-step debugging
            | _ -> ctx.State <- priorState
            result
        | ExecutionState.Running ->
            this.executeScan()

    // Helper method to execute the actual scan logic
    member private this.executeScan() : int =

        // MAJOR FIX: Increment scan index BEFORE execution to ensure correct index in events
        // Previously, index was incremented inside StmtEvaluator.execScan, causing off-by-one
        ctx.Memory.IncrementScan()
        let scanIndex = ctx.ScanIndex
        let scanStartTime = DateTime.UtcNow
        let sw = System.Diagnostics.Stopwatch.StartNew()  // Monotonic timing

        // MAJOR FIX: Update relay scan index BEFORE execution (not in Finalize)
        // Relay transitions during execution need current scan index, not previous
        ctx.RelayStateManager |> Option.iter (fun manager ->
            manager.UpdateScanIndex(scanIndex))

        // Emit ScanStarted event
        cfg.EventSink |> Option.iter (fun sink ->
            sink.Publish(RuntimeEvent.ScanStarted(scanIndex, scanStartTime)))

        try
            // ═════════════════════════════════════════════════════════════════════
            // Stage 1: PrepareInputs - Runtime updates, dependency management
            // ═════════════════════════════════════════════════════════════════════
            let swPrepare = System.Diagnostics.Stopwatch.StartNew()

            match updateManager with
            | Some mgr ->
                let updateResults = mgr.ProcessPendingUpdates()

                // Emit RuntimeUpdateApplied events and log
                updateResults |> List.iter (fun result ->
                    match result with
                    | UpdateResult.Success msg ->
                        Context.trace ctx (sprintf "[UPDATE SUCCESS] %s" msg)
                        // MEDIUM FIX: Emit ETW telemetry for runtime updates
                        RuntimeTelemetry.runtimeUpdateApplied msg
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RuntimeUpdateApplied(msg, DateTime.UtcNow)))
                    | UpdateResult.ValidationFailed errors ->
                        let errorMsgs = errors |> List.map (fun e -> e.Format()) |> String.concat "; "
                        Context.warning ctx (sprintf "[UPDATE VALIDATION FAILED] %s" errorMsgs)
                    | UpdateResult.ApplyFailed error ->
                        Context.warning ctx (sprintf "[UPDATE APPLY FAILED] %s" error)
                    | UpdateResult.RolledBack (reason, originalError) ->
                        Context.warning ctx (sprintf "[UPDATE ROLLED BACK] %s (Original: %s)" reason originalError)
                    | UpdateResult.PartialSuccess (succeeded, failed, errors) ->
                        Context.trace ctx (sprintf "[UPDATE PARTIAL] %d succeeded, %d failed" succeeded failed)
                        let msg = $"Partial: {succeeded} succeeded, {failed} failed"
                        // MEDIUM FIX: Emit ETW telemetry for runtime updates
                        RuntimeTelemetry.runtimeUpdateApplied msg
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RuntimeUpdateApplied(msg, DateTime.UtcNow))))

                // Program.Body 업데이트 확인 및 적용
                match mgr.GetProgramBody() with
                | Some newBody ->
                    currentProgramBody <- newBody
                    let newDependencies = DependencyAnalyzer.buildDependencyMap currentProgramBody
                    ctx.Memory.SetDependencyMap newDependencies

                    if cfg.SelectiveMode then
                        ctx.Memory.MarkAllChanged()

                    Context.trace ctx "[UPDATE] Program.Body updated successfully"
                    // MEDIUM FIX: Emit ETW telemetry for runtime updates
                    RuntimeTelemetry.runtimeUpdateApplied "Program.Body"
                    cfg.EventSink |> Option.iter (fun sink ->
                        sink.Publish(RuntimeEvent.RuntimeUpdateApplied("Program.Body", DateTime.UtcNow)))
                | None -> ()
            | None -> ()

            swPrepare.Stop()
            let prepareInputsDuration = swPrepare.Elapsed

            // ═════════════════════════════════════════════════════════════════════
            // Stage 2: Execution - Program body execution
            // ═════════════════════════════════════════════════════════════════════
            let swExec = System.Diagnostics.Stopwatch.StartNew()
            let useSelective = cfg.SelectiveMode && not isFirstScan

            if useSelective then
                StmtEvaluator.execScanSelective ctx currentProgramBody
            else
                StmtEvaluator.execScan ctx currentProgramBody

            if isFirstScan then
                isFirstScan <- false

            swExec.Stop()
            let executionDuration = swExec.Elapsed

            // ═════════════════════════════════════════════════════════════════════
            // Stage 3: FlushOutputs - Persist retain, clear change flags
            // ═════════════════════════════════════════════════════════════════════
            let swFlush = System.Diagnostics.Stopwatch.StartNew()

            // CRITICAL FIX (DEFECT-015-4): Revert to synchronous retain save - Memory is single-threaded
            // CreateRetainSnapshot accesses ConcurrentDictionary but modifying entries during iteration
            // violates single-threaded contract (Memory.fs:273-284). Async snapshot risks race conditions.
            // Accept FlushOutputs blocking (10-100ms) to maintain correctness and stage telemetry order.
            // MAJOR FIX (DEFECT-015-9): Wait for previous save before starting new one
            lock retainSaveLock (fun () ->
                match pendingRetainSave with
                | Some task ->
                    try
                        if not task.IsCompleted then
                            task.Wait(100) |> ignore  // Wait up to 100ms for previous save
                    with _ -> ()
                    pendingRetainSave <- None
                | None -> ()
            )

            // CRITICAL FIX (DEFECT-020-10): Check for fatal errors BEFORE saving retain data
            // Previous code saved retain in Stage 3 even when Stage 2 raised fatal error
            // This persisted corrupt/partial data before Stage 4 detected the fatal
            // RuntimeSpec.md §6: Fatal errors must stop execution immediately - no persist
            if not ctx.ErrorLog.HasFatalErrors then
                match retainStorage with
                | Some storage ->
                    let snapshot = ctx.Memory.CreateRetainSnapshot()
                    match storage.Save(snapshot) with
                    | Ok () ->
                        // Include FB static data in retain telemetry count (RuntimeSpec.md:79-82)
                        let varCount = snapshot.Variables.Length
                        let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                        let totalCount = varCount + fbStaticCount
                        RuntimeTelemetry.retainPersisted totalCount
                        cfg.EventSink |> Option.iter (fun sink ->
                            sink.Publish(RuntimeEvent.RetainPersisted(totalCount, DateTime.UtcNow)))
                    | Error err ->
                        ctx.LogWarning($"Retain save failed: {err}")
                | None -> ()
            else
                // Fatal error detected - skip retain save to prevent persisting corrupt data
                ctx.LogWarning("Skipping retain save due to fatal error in scan execution")

            if cfg.SelectiveMode then
                ctx.Memory.ClearChangeFlags()

            swFlush.Stop()
            let flushDuration = swFlush.Elapsed

            // ═════════════════════════════════════════════════════════════════════
            // Stage 4: Finalize - Check errors, emit telemetry, process relay states
            // ═════════════════════════════════════════════════════════════════════
            let swFinalize = System.Diagnostics.Stopwatch.StartNew()

            // Process relay state transitions and emit telemetry (RuntimeSpec.md:41,56 - GAP-009)
            // MAJOR FIX: UpdateScanIndex moved to start of ScanOnce() (before execution)
            ctx.RelayStateManager |> Option.iter (fun manager ->
                manager.ProcessStateChanges())

            // Cleanup old warnings periodically (NEW-DEFECT-004 fix, NEW-DEFECT-002 fix: configurable)
            // Prevents unbounded memory growth in long-running systems
            // MEDIUM FIX: Cast to int64 for modulo operation
            if scanIndex % (int64 RuntimeLimits.Current.WarningCleanupIntervalScans) = 0L then
                ctx.ErrorLog.ClearOldWarnings()

            // Check for fatal errors BEFORE emitting ScanCompleted (DEFECT-009 fix: also emit to EventSource)
            // BLOCKER FIX: Stop execution on fatal errors (RuntimeSpec.md §6 policy)
            if ctx.ErrorLog.HasFatalErrors then
                // Emit ScanFailed instead of ScanCompleted
                let fatalErrors = ctx.ErrorLog.GetErrorsBySeverity(RuntimeErrorSeverity.Fatal)
                let firstFatal = fatalErrors |> List.tryHead
                match firstFatal with
                | Some error ->
                    RuntimeTelemetry.scanFailed scanIndex error.Message
                    cfg.EventSink |> Option.iter (fun sink ->
                        sink.Publish(RuntimeEvent.ScanFailed(scanIndex, error, DateTime.UtcNow)))
                    // HIGH FIX: Set ExecutionState.Error instead of Stopped (preserves error message)
                    ctx.State <- ExecutionState.Error error.Message
                    Context.trace ctx (sprintf "[FATAL] Engine stopped due to fatal error: %s" error.Message)
                | None -> ()

            // Emit per-stage metrics to EventSource
            RuntimeTelemetry.stageCompleted "PrepareInputs" (int prepareInputsDuration.TotalMilliseconds) scanIndex
            RuntimeTelemetry.stageCompleted "Execution" (int executionDuration.TotalMilliseconds) scanIndex
            RuntimeTelemetry.stageCompleted "FlushOutputs" (int flushDuration.TotalMilliseconds) scanIndex

            // MEDIUM FIX: Stop finalize timer AFTER all post-finalize work (RuntimeSpec.md:24)
            swFinalize.Stop()
            let finalizeDuration = swFinalize.Elapsed

            RuntimeTelemetry.stageCompleted "Finalize" (int finalizeDuration.TotalMilliseconds) scanIndex

            // MEDIUM FIX: Use actual elapsed time for total duration, not sum of stages (RuntimeSpec.md:26)
            sw.Stop()
            let totalDuration = sw.Elapsed
            let totalMs = int totalDuration.TotalMilliseconds

            // Create timeline with actual total duration
            let timeline = {
                PrepareInputsDuration = prepareInputsDuration
                ExecutionDuration = executionDuration
                FlushDuration = flushDuration
                FinalizeDuration = finalizeDuration
                TotalDuration = totalDuration  // Use actual elapsed, not sum
                ScanIndex = scanIndex
            }

            // MEDIUM FIX: Deadline violations only emit telemetry, no runtime behavior change (RuntimeSpec.md:93)
            // HIGH FIX: Recompute stage thresholds based on current cycle time, not constructor value
            // HIGH FIX: Respect ctx.CycleTime updates (RuntimeSpec §2.2), fall back to cfg only if not set
            let currentCycleTimeMs = match cfg.CycleTimeMs with
                                     | Some configMs when configMs = ctx.CycleTime -> ctx.CycleTime  // Config matches current
                                     | Some _ -> ctx.CycleTime  // Use context (may have been updated)
                                     | None -> ctx.CycleTime  // No config, use context default
            let currentThresholds = match cfg.StageThresholds with
                                    | Some thresholds ->
                                        // CRITICAL FIX (DEFECT-021-6): Scale custom proportions, don't replace with defaults
                                        // Previous code discarded caller's stage proportions when cycle time changed
                                        // RuntimeSpec §2.1: preserve original configuration structure
                                        match thresholds.Total with
                                        | Some total when total <> currentCycleTimeMs ->
                                            // Cycle time changed - scale each stage proportionally
                                            let scaleFactor = float currentCycleTimeMs / float total
                                            let scaleThreshold (optMs: int option) =
                                                optMs |> Option.map (fun ms -> max 1 (int (float ms * scaleFactor)))
                                            Some {
                                                PrepareInputs = scaleThreshold thresholds.PrepareInputs
                                                Execution = scaleThreshold thresholds.Execution
                                                FlushOutputs = scaleThreshold thresholds.FlushOutputs
                                                Finalize = scaleThreshold thresholds.Finalize
                                                Total = Some currentCycleTimeMs
                                            }
                                        | None ->
                                            // Total=None means no aggregate check, keep as-is
                                            Some thresholds
                                        | Some _ ->
                                            // Total matches current cycle time, keep existing thresholds
                                            Some thresholds
                                    | None ->
                                        // No thresholds configured, use defaults
                                        Some (StageThresholds.defaultForCycleTime currentCycleTimeMs)

            currentThresholds |> Option.iter (fun thresholds ->
                let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds cfg.EventSink
                // All violations: Telemetry only (no warning/error logging)
                // Violations are already emitted via StageDeadlineEnforcer
                ())

            // HIGH FIX: Use ctx.CycleTime directly (not defaultArg cfg.CycleTimeMs)
            // Ensures deadline checks reflect live cycle time updates (RuntimeSpec §2.2)
            let cycleTimeMs = ctx.CycleTime

            // Emit ScanCompleted only if no fatal errors (DEFECT-009 fix: also emit to EventSource)
            if not ctx.ErrorLog.HasFatalErrors then
                RuntimeTelemetry.scanCompleted scanIndex totalMs cycleTimeMs
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.ScanCompleted(scanIndex, timeline, DateTime.UtcNow)))

            // Check deadline - use CycleTime for ScanDeadlineMissed event (DEFECT-009 fix: also emit to EventSource)
            if totalMs > cycleTimeMs then
                let deadline = TimeSpan.FromMilliseconds(float cycleTimeMs)
                RuntimeTelemetry.scanDeadlineMissed scanIndex totalMs cycleTimeMs
                cfg.EventSink |> Option.iter (fun sink ->
                    sink.Publish(RuntimeEvent.ScanDeadlineMissed(scanIndex, totalDuration, deadline, DateTime.UtcNow)))

            // Check WarnIfOverMs for console warning (separate from deadline event)
            match cfg.WarnIfOverMs with
            | Some warnThresholdMs when totalMs > warnThresholdMs ->
                Context.warning ctx $"Scan overrun: {totalMs}ms > {warnThresholdMs}ms (warn threshold)"
            | _ -> ()

            // ENHANCEMENT: Integrated PerformanceProfiler for scan time tracking
            // Uses global profiler instance for performance metrics collection
            PerformanceProfiler.recordScanTime (int64 totalMs) cycleTimeMs

            totalMs

        with ex ->
            // Emit ScanFailed event on exception
            let error = RuntimeError.fatal $"Scan failed: {ex.Message}"
                        |> RuntimeError.withScanIndex scanIndex
                        |> RuntimeError.withException ex
            ctx.ErrorLog.Log(error)
            cfg.EventSink |> Option.iter (fun sink ->
                sink.Publish(RuntimeEvent.ScanFailed(scanIndex, error, DateTime.UtcNow)))
            // HIGH FIX: Set ExecutionState.Error before reraise (prevents Stopped downgrade)
            ctx.State <- ExecutionState.Error error.Message
            reraise()

    member private this.RunLoopAsync(token: CancellationToken) : Task =
        // GAP-004 fix: Use LongRunning for dedicated thread (RuntimeSpec.md:30)
        Task.Factory.StartNew(
            (fun () ->
                try
                    ctx.State <- ExecutionState.Running
                    // CRITICAL FIX: Use fixed-point timing instead of delta timing
                    // This prevents timing drift from Task.Delay overhead and clock changes
                    let frequency = System.Diagnostics.Stopwatch.Frequency
                    let ticksPerMs = frequency / 1000L
                    let mutable nextScanTime = ctx.TimeProvider.GetTimestamp()

                    // CRITICAL FIX: Support Pause/Resume/Breakpoint - keep loop alive for all non-terminal states
                    // CRITICAL FIX (DEFECT-021-5): Include Breakpoint state to allow debugging resume
                    // Previous code only checked Running/Paused, causing loop to exit on breakpoint hit
                    let isLoopAlive () =
                        match ctx.State with
                        | ExecutionState.Running | ExecutionState.Paused | ExecutionState.Breakpoint _ -> true
                        | ExecutionState.Stopped | ExecutionState.Error _ -> false

                    while not token.IsCancellationRequested && isLoopAlive() do
                        // CRITICAL FIX: Skip scan execution if Paused or at Breakpoint, just check for resume
                        match ctx.State with
                        | ExecutionState.Paused | ExecutionState.Breakpoint _ ->
                            // MAJOR FIX: Yield CPU instead of busy-waiting during pause/breakpoint
                            Task.Delay(10, token).Wait() |> ignore
                        | ExecutionState.Running ->
                            let scanStartTime = ctx.TimeProvider.GetTimestamp()
                            let _ = this.ScanOnce()

                            // HIGH FIX: Re-evaluate cycle time each iteration (RuntimeSpec §2.2)
                            let cycleTimeMs = match cfg.CycleTimeMs with
                                              | Some configMs when configMs = ctx.CycleTime -> ctx.CycleTime
                                              | Some _ -> ctx.CycleTime  // Context may have been updated
                                              | None -> ctx.CycleTime
                            // CRITICAL FIX: Avoid tick conversion drift - use multiplication before division
                            // Old: ticksPerMs = frequency / 1000; cycleTimeTicks = cycleTimeMs * ticksPerMs
                            // New: cycleTimeTicks = (cycleTimeMs * frequency) / 1000 (higher precision)
                            // MAJOR FIX (DEFECT-015-10): Protect against division by zero when cycleTime is 0
                            let cycleTimeTicks =
                                if cycleTimeMs <= 0 then
                                    eprintfn "[RUNTIME] Warning: CycleTime is %d, using minimum 1ms to prevent division by zero" cycleTimeMs
                                    frequency / 1000L  // 1ms minimum
                                else
                                    (int64 cycleTimeMs * frequency) / 1000L

                            // Calculate next scan time using fixed-point scheduling
                            nextScanTime <- nextScanTime + cycleTimeTicks
                            let now = ctx.TimeProvider.GetTimestamp()
                            let waitTicks = nextScanTime - now

                            // HIGH FIX: Use waitTicks > 0 instead of waitMs > 0 to avoid sub-millisecond overrun
                            if waitTicks > 0L then
                                let waitMs = int (waitTicks / ticksPerMs)
                                if waitMs > 0 then
                                    // GAP-006 fix: Respect cancellation token (RuntimeSpec.md:30)
                                    try
                                        Task.Delay(waitMs, token).Wait()
                                    with
                                    | :? System.AggregateException as ae ->
                                        match ae.InnerException with
                                        | :? System.Threading.Tasks.TaskCanceledException -> ()
                                        | _ -> reraise()
                                    | :? System.OperationCanceledException -> ()
                                else
                                    // MAJOR FIX: Sub-millisecond slack - yield CPU instead of busy spin
                                    // Prevents 100% CPU burn on tight cycles (<1ms wait)
                                    Task.Delay(0, token).Wait() |> ignore
                            else
                                // Overrun: skip to next time slot
                                // MAJOR FIX (DEFECT-015-10): Protect against division by zero
                                if cycleTimeTicks > 0L then
                                    let missedSlots = (-waitTicks) / cycleTimeTicks + 1L
                                    nextScanTime <- nextScanTime + (missedSlots * cycleTimeTicks)
                                else
                                    // CycleTime is effectively zero - just advance by 1ms
                                    nextScanTime <- nextScanTime + (frequency / 1000L)
                        | _ -> () // Stopped/Error states handled by isLoopAlive() - shouldn't reach here
                finally
                    // CRITICAL FIX: Preserve Error and Paused states - don't overwrite with Stopped
                    match ctx.State with
                    | ExecutionState.Error _ -> ()  // Keep error state
                    | ExecutionState.Paused -> ()   // Keep paused state for resume
                    | ExecutionState.Breakpoint _ -> ()  // Keep breakpoint state
                    | _ -> ctx.State <- ExecutionState.Stopped),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)

    /// 연속 실행 시작(외부 토큰과 링크 가능)
    member this.StartAsync(?externalToken: CancellationToken) : Task =
        match loopTask with
        | Some t when not t.IsCompleted -> Task.CompletedTask
        | _ ->
            let linkedCts =
                match externalToken with
                | Some t -> CancellationTokenSource.CreateLinkedTokenSource t
                | None   -> new CancellationTokenSource()
            cts <- Some linkedCts

            let t = this.RunLoopAsync(linkedCts.Token)
            loopTask <- Some t
            Task.CompletedTask

    /// 안전한 중지(취소+대기)
    member this.StopAsync(?timeoutMs:int) : Task =
        task {
            // MEDIUM FIX: Preserve Error state during shutdown (host needs diagnostic info)
            match ctx.State with
            | ExecutionState.Error _ -> ()  // Keep error state for diagnostics
            | _ -> ctx.State <- ExecutionState.Stopped
            cts |> Option.iter (fun s -> s.Cancel())

            // 루프 종료 대기
            match loopTask with
            | Some t when not t.IsCompleted ->
                let timeout = defaultArg timeoutMs RuntimeLimits.Current.StopTimeoutMs  // NEW-DEFECT-002 fix: configurable
                let! completed = Task.WhenAny(t, Task.Delay(timeout))
                // MAJOR FIX: Don't wait indefinitely if timeout expired
                if obj.ReferenceEquals(completed, t) then
                    // Task completed within timeout - safe to await
                    try do! t with _ -> ()
                else
                    // Timeout expired - log warning and continue shutdown
                    Context.warning ctx $"StopAsync timeout ({timeout}ms) expired, forcing shutdown"
            | _ -> ()

            // ═════════════════════════════════════════════════════════════════════
            // 리테인 데이터 자동 저장 (엔진 정상 종료 시)
            // ═════════════════════════════════════════════════════════════════════

            // CRITICAL FIX (DEFECT-021-4): Skip retain save if fatal errors occurred
            // Previous code always saved on shutdown, persisting corrupt data after crashes
            // RuntimeSpec §6: Fatal errors must not persist state (matches DEFECT-020-10 in ScanOnce)
            if not ctx.ErrorLog.HasFatalErrors then
                // HIGH FIX: No async retain save task to wait for (now synchronous)
                retainStorage |> Option.iter (fun storage ->
                    let snapshot = ctx.Memory.CreateRetainSnapshot()
                    // HIGH FIX: Check both Variables and FBStaticData to avoid skipping FB-static-only deployments
                    let varCount = snapshot.Variables.Length
                    let fbStaticCount = snapshot.FBStaticData |> List.sumBy (fun fb -> fb.Variables.Length)
                    let totalCount = varCount + fbStaticCount
                    if totalCount > 0 then
                        match storage.Save(snapshot) with
                        | Ok () ->
                            Context.trace ctx (sprintf "[RETAIN] Data saved: %d variables, %d FB static" varCount fbStaticCount)
                        | Error err ->
                            Context.warning ctx (sprintf "[RETAIN] Save failed: %s" err)
                    else
                        Context.trace ctx "[RETAIN] No retain data to save")
            else
                Context.warning ctx "[RETAIN] Skipping save on shutdown due to fatal errors"

            // NOTE: EventSource disposal (RuntimeTelemetry.shutdown()) should be called
            // by the hosting application at process exit, not at individual engine stop.
            // The EventSource singleton is shared across all engine instances.

            // CRITICAL FIX: Wait for pending retain save to complete before exiting (DEFECT-014-6)
            lock retainSaveLock (fun () ->
                match pendingRetainSave with
                | Some task ->
                    try
                        // Wait up to 5 seconds for async retain save to complete
                        if not (task.Wait(5000)) then
                            eprintfn "[RETAIN] Warning: Async save did not complete within 5 seconds"
                    with ex ->
                        eprintfn "[RETAIN] Error waiting for async save: %s" ex.Message
                    pendingRetainSave <- None
                | None -> ()
            )

            // 정리
            cts |> Option.iter (fun s -> s.Dispose())
            cts      <- None
            loopTask <- None
        }

// ─────────────────────────────────────────────
// 간단 래퍼 (모듈 함수들)
// ─────────────────────────────────────────────
module CpuScan =

    /// Engine 생성 (ctx 없으면 새 컨텍스트 사용)
    let create (program: Statement.Program,
                ctxOpt   : Ev2.Cpu.Runtime.ExecutionContext option,
                cfgOpt   : ScanConfig option,
                updateMgrOpt : RuntimeUpdateManager option,
                retainStorageOpt : IRetainStorage option) =
        new CpuScanEngine(program,
                          defaultArg ctxOpt (Context.create()),
                          cfgOpt,
                          updateMgrOpt,
                          retainStorageOpt)

    /// 기본 생성기
    let createDefault (program: Statement.Program) =
        create (program, None, None, None, None)

    /// 단발 스캔
    let scanOnce (engine: CpuScanEngine) = engine.ScanOnce()

    /// 연속 실행 시작(외부 취소 토큰 있으면 링크)
    let start (engine: CpuScanEngine, tokenOpt: CancellationToken option) =
        match tokenOpt with
        | Some t -> engine.StartAsync(externalToken = t)
        | None   -> engine.StartAsync()

    /// 토큰 없이 시작
    let startNoToken (engine: CpuScanEngine) =
        engine.StartAsync()

    /// 중지
    let stopAsync (engine: CpuScanEngine) =
        engine.StopAsync()
