namespace Ev2.Cpu.Runtime

open System

// ═════════════════════════════════════════════════════════════════════
// Stage-Aware Deadline Configuration
// ═════════════════════════════════════════════════════════════════════
// Implements per-stage deadline enforcement and metrics emission
// ═════════════════════════════════════════════════════════════════════

/// <summary>Per-stage deadline thresholds (milliseconds)</summary>
/// <remarks>
/// Configurable thresholds for each scan cycle stage.
/// If a stage exceeds its threshold, a warning metric is emitted.
/// </remarks>
type StageThresholds = {
    /// PrepareInputs stage threshold (ms). None = no limit
    PrepareInputs: int option

    /// Execution stage threshold (ms). None = no limit
    Execution: int option

    /// FlushOutputs stage threshold (ms). None = no limit
    FlushOutputs: int option

    /// Finalize stage threshold (ms). None = no limit
    Finalize: int option

    /// Total scan threshold (ms). None = no limit
    Total: int option
}

module StageThresholds =
    /// <summary>Default thresholds for production use</summary>
    /// <remarks>
    /// Based on RuntimeSpec.md timing requirements:
    /// - PrepareInputs: <10% of cycle
    /// - Execution: <70% of cycle
    /// - FlushOutputs: <10% of cycle
    /// - Finalize: <10% of cycle
    /// </remarks>
    let defaultForCycleTime (cycleTimeMs: int) : StageThresholds =
        // DEFECT-006 fix: Use float calculation to avoid integer division precision loss
        // MAJOR FIX: Clamp thresholds to minimum 1ms to prevent immediate violations
        let cycleTime = float cycleTimeMs
        let clampMin1 value = max 1 (int value)
        let prepareInputs = clampMin1 (cycleTime * 0.10)   // 10%, min 1ms
        let execution = clampMin1 (cycleTime * 0.70)       // 70%, min 1ms
        let flushOutputs = clampMin1 (cycleTime * 0.10)    // 10%, min 1ms
        let finalize = clampMin1 (cycleTime * 0.10)        // 10%, min 1ms
        // MAJOR FIX: Ensure Total is at least sum of stage budgets (prevents false violations)
        // For small cycle times (<4ms), clamping stages to 1ms each can exceed original total
        let stageSum = prepareInputs + execution + flushOutputs + finalize
        let total = max cycleTimeMs stageSum  // Use larger of cycle time or sum of stages
        {
            PrepareInputs = Some prepareInputs
            Execution = Some execution
            FlushOutputs = Some flushOutputs
            Finalize = Some finalize
            Total = Some (max 1 total)
        }

    /// <summary>Relaxed thresholds for development/testing</summary>
    let relaxed : StageThresholds =
        {
            PrepareInputs = Some 100
            Execution = Some 500
            FlushOutputs = Some 100
            Finalize = Some 100
            Total = Some 1000
        }

    /// <summary>No thresholds (all stages unlimited)</summary>
    let unlimited : StageThresholds =
        {
            PrepareInputs = None
            Execution = None
            FlushOutputs = None
            Finalize = None
            Total = None
        }

/// <summary>Stage deadline violation event</summary>
type StageDeadlineViolation = {
    StageName: string
    ActualMs: int
    ThresholdMs: int
    ScanIndex: int64  // MEDIUM FIX: int64 to prevent overflow
    Timestamp: DateTime
}

/// <summary>Stage deadline enforcement</summary>
module StageDeadlineEnforcer =

    /// <summary>Check if stage exceeded threshold (MEDIUM FIX: int64 to prevent overflow)</summary>
    let checkStage (stageName: string) (actualMs: int) (thresholdOpt: int option) (scanIndex: int64) : StageDeadlineViolation option =
        match thresholdOpt with
        | Some threshold when actualMs > threshold ->
            Some {
                StageName = stageName
                ActualMs = actualMs
                ThresholdMs = threshold
                ScanIndex = scanIndex
                Timestamp = DateTime.UtcNow
            }
        | _ -> None

    /// <summary>Check all stages and emit violations</summary>
    let checkAllStages
        (timeline: ScanTimeline)
        (thresholds: StageThresholds)
        (eventSink: IRuntimeEventSink option)
        : StageDeadlineViolation list =

        let checks = [
            // MINOR FIX: Use ceiling instead of truncation for accurate sub-millisecond detection (RuntimeSpec.md:26)
            checkStage "PrepareInputs" (int (System.Math.Ceiling(timeline.PrepareInputsDuration.TotalMilliseconds))) thresholds.PrepareInputs timeline.ScanIndex;
            checkStage "Execution" (int (System.Math.Ceiling(timeline.ExecutionDuration.TotalMilliseconds))) thresholds.Execution timeline.ScanIndex;
            checkStage "FlushOutputs" (int (System.Math.Ceiling(timeline.FlushDuration.TotalMilliseconds))) thresholds.FlushOutputs timeline.ScanIndex;
            checkStage "Finalize" (int (System.Math.Ceiling(timeline.FinalizeDuration.TotalMilliseconds))) thresholds.Finalize timeline.ScanIndex;
            checkStage "Total" (int (System.Math.Ceiling(timeline.TotalDuration.TotalMilliseconds))) thresholds.Total timeline.ScanIndex;
        ]

        let violations = checks |> List.choose id

        // Emit violations to EventSource and IRuntimeEventSink (DEFECT-007 fix)
        violations |> List.iter (fun violation ->
            // EventSource for APM (Azure Monitor, dotnet-counters, etc.)
            RuntimeMetricsEventSource.Instance.StageDeadlineMissed(
                violation.StageName,
                violation.ActualMs,
                violation.ThresholdMs,
                violation.ScanIndex
            )

            // IRuntimeEventSink for external observers (DEFECT-007 fix)
            eventSink |> Option.iter (fun sink ->
                let event = RuntimeEvent.StageDeadlineMissed(
                    violation.StageName,
                    violation.ActualMs,
                    violation.ThresholdMs,
                    violation.ScanIndex,
                    violation.Timestamp
                )
                sink.Publish(event))
        )

        violations
