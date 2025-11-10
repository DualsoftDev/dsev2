namespace Ev2.Cpu.Runtime

open System

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Event Types - Observability (GAP-003 Fix)
// ═════════════════════════════════════════════════════════════════════════════
// Implements RuntimeSpec.md:26 requirement for runtime event emissions
// Implements RuntimeSpec.md:21 requirement for per-stage scan timeline
// ═════════════════════════════════════════════════════════════════════════════

/// Per-stage scan timing breakdown (RuntimeSpec.md:21)
type ScanTimeline = {
    /// Time spent preparing inputs (loading retain, processing updates)
    PrepareInputsDuration: TimeSpan

    /// Time spent executing program body (StmtEvaluator.execScan)
    ExecutionDuration: TimeSpan

    /// Time spent flushing outputs (persisting retain, updating I/O)
    FlushDuration: TimeSpan

    /// Time spent finalizing (cleanup, telemetry)
    FinalizeDuration: TimeSpan

    /// Total scan duration (should equal sum of above)
    TotalDuration: TimeSpan

    /// Scan cycle index (MEDIUM FIX: int64 to prevent overflow)
    ScanIndex: int64
}

/// Runtime event types for observability
type RuntimeEvent =
    /// Scan started event (MEDIUM FIX: int64 to prevent overflow)
    | ScanStarted of scanIndex:int64 * timestamp:DateTime

    /// Scan completed successfully (MEDIUM FIX: int64 to prevent overflow)
    | ScanCompleted of scanIndex:int64 * timeline:ScanTimeline * timestamp:DateTime

    /// Scan deadline was missed (overrun) (MEDIUM FIX: int64 to prevent overflow)
    | ScanDeadlineMissed of scanIndex:int64 * elapsed:TimeSpan * deadline:TimeSpan * timestamp:DateTime

    /// Stage deadline was missed (per-stage overrun) - DEFECT-007 fix (MEDIUM FIX: int64 to prevent overflow)
    | StageDeadlineMissed of stageName:string * actualMs:int * thresholdMs:int * scanIndex:int64 * timestamp:DateTime

    /// Fatal error occurred during scan (MEDIUM FIX: int64 to prevent overflow)
    | ScanFailed of scanIndex:int64 * error:RuntimeError * timestamp:DateTime

    /// Runtime update applied
    | RuntimeUpdateApplied of updateType:string * timestamp:DateTime

    /// Retain data persisted
    | RetainPersisted of variableCount:int * timestamp:DateTime

    /// Retain data loaded
    | RetainLoaded of variableCount:int * timestamp:DateTime

    /// Work relay state changed (GAP-009) (MEDIUM FIX: int64 to prevent overflow)
    | WorkRelayStateChanged of relayName:string * fromState:WorkRelayState * toState:WorkRelayState * scanIndex:int64 * timestamp:DateTime

    /// Call relay state changed (GAP-009) (MEDIUM FIX: int64 to prevent overflow)
    | CallRelayStateChanged of relayName:string * fromState:CallRelayState * toState:CallRelayState * scanIndex:int64 * timestamp:DateTime

    /// Call progress update (GAP-009)
    | CallProgress of relayName:string * progress:int * timestamp:DateTime

    /// Call timeout occurred (GAP-009)
    | CallTimeout of relayName:string * retryCount:int * timestamp:DateTime

/// Interface for runtime event sinks (observers)
type IRuntimeEventSink =
    /// Publish a runtime event to the sink
    abstract member Publish: RuntimeEvent -> unit

/// Default console event sink (for development/debugging)
type ConsoleEventSink() =
    interface IRuntimeEventSink with
        member _.Publish(event: RuntimeEvent) =
            match event with
            | ScanStarted(idx, ts) ->
                printfn "[EVENT %s] Scan #%s started" (ts.ToString("HH:mm:ss.fff")) (idx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
            | ScanCompleted(idx, timeline, ts) ->
                printfn "[EVENT %s] Scan #%s completed in %dms (Prepare: %dms, Exec: %dms, Flush: %dms, Finalize: %dms)"
                    (ts.ToString("HH:mm:ss.fff"))
                    (idx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
                    (int timeline.TotalDuration.TotalMilliseconds)
                    (int timeline.PrepareInputsDuration.TotalMilliseconds)
                    (int timeline.ExecutionDuration.TotalMilliseconds)
                    (int timeline.FlushDuration.TotalMilliseconds)
                    (int timeline.FinalizeDuration.TotalMilliseconds)
            | ScanDeadlineMissed(idx, elapsed, deadline, ts) ->
                printfn "[WARN %s] Scan #%s DEADLINE MISSED: %dms > %dms (overrun: +%dms)"
                    (ts.ToString("HH:mm:ss.fff"))
                    (idx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
                    (int elapsed.TotalMilliseconds)
                    (int deadline.TotalMilliseconds)
                    (int (elapsed - deadline).TotalMilliseconds)
            | StageDeadlineMissed(stageName, actualMs, thresholdMs, scanIdx, ts) ->
                printfn "[WARN %s] Stage '%s' DEADLINE MISSED (scan #%s): %dms > %dms (overrun: +%dms)"
                    (ts.ToString("HH:mm:ss.fff"))
                    stageName
                    (scanIdx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
                    actualMs
                    thresholdMs
                    (actualMs - thresholdMs)
            | ScanFailed(idx, error, ts) ->
                printfn "[ERROR %s] Scan #%s FAILED: %s"
                    (ts.ToString("HH:mm:ss.fff"))
                    (idx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
                    (RuntimeError.format error)
            | RuntimeUpdateApplied(updateType, ts) ->
                printfn "[EVENT %s] Runtime update applied: %s"
                    (ts.ToString("HH:mm:ss.fff"))
                    updateType
            | RetainPersisted(count, ts) ->
                printfn "[EVENT %s] Retain data persisted (%d variables)"
                    (ts.ToString("HH:mm:ss.fff"))
                    count
            | RetainLoaded(count, ts) ->
                printfn "[EVENT %s] Retain data loaded (%d variables)"
                    (ts.ToString("HH:mm:ss.fff"))
                    count
            | WorkRelayStateChanged(name, fromState, toState, scanIdx, ts) ->
                printfn "[EVENT %s] Work relay '%s' state changed: %A -> %A (scan #%s)"
                    (ts.ToString("HH:mm:ss.fff"))
                    name
                    fromState
                    toState
                    (scanIdx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
            | CallRelayStateChanged(name, fromState, toState, scanIdx, ts) ->
                printfn "[EVENT %s] Call relay '%s' state changed: %A -> %A (scan #%s)"
                    (ts.ToString("HH:mm:ss.fff"))
                    name
                    fromState
                    toState
                    (scanIdx.ToString())  // MEDIUM FIX: Format int64 as string to avoid overflow
            | CallProgress(name, progress, ts) ->
                printfn "[EVENT %s] Call relay '%s' progress: %d%%"
                    (ts.ToString("HH:mm:ss.fff"))
                    name
                    progress
            | CallTimeout(name, retryCount, ts) ->
                printfn "[WARN %s] Call relay '%s' timed out (retry %d)"
                    (ts.ToString("HH:mm:ss.fff"))
                    name
                    retryCount

/// Silent event sink (no output)
type NullEventSink() =
    interface IRuntimeEventSink with
        member _.Publish(_: RuntimeEvent) = ()

/// Helper module for creating ScanTimeline
module ScanTimeline =
    /// Create a ScanTimeline from individual durations (CRITICAL FIX: int64 scanIndex)
    let create (prepareInputs: TimeSpan) (execution: TimeSpan) (flush: TimeSpan) (finalize: TimeSpan) (scanIndex: int64) : ScanTimeline =
        {
            PrepareInputsDuration = prepareInputs
            ExecutionDuration = execution
            FlushDuration = flush
            FinalizeDuration = finalize
            TotalDuration = prepareInputs + execution + flush + finalize
            ScanIndex = scanIndex
        }

    /// Create an empty timeline (for error cases) (CRITICAL FIX: int64 scanIndex)
    let empty (scanIndex: int64) : ScanTimeline =
        {
            PrepareInputsDuration = TimeSpan.Zero
            ExecutionDuration = TimeSpan.Zero
            FlushDuration = TimeSpan.Zero
            FinalizeDuration = TimeSpan.Zero
            TotalDuration = TimeSpan.Zero
            ScanIndex = scanIndex
        }
