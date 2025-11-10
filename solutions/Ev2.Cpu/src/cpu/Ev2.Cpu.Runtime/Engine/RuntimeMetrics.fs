namespace Ev2.Cpu.Runtime

open System.Diagnostics.Tracing

// ═════════════════════════════════════════════════════════════════════
// EventSource for APM Integration (GAP-010)
// ═════════════════════════════════════════════════════════════════════
// Provides performance counters and telemetry for Application Performance Monitoring
// Compatible with: Azure Monitor, Application Insights, Perfview, dotnet-counters, etc.
// ═════════════════════════════════════════════════════════════════════

/// <summary>PLC Runtime Performance Counters (EventSource for APM)</summary>
/// <remarks>
/// Emits performance metrics for monitoring tools:
/// - Scan cycle time (avg, min, max)
/// - Scan overruns (deadline misses)
/// - Error counts (fatal, recoverable, warnings)
/// - Memory statistics
///
/// Usage:
/// - dotnet-counters monitor --counters Ev2-Cpu-Runtime
/// - Application Insights auto-collection
/// - Azure Monitor integration
/// </remarks>
[<EventSource(Name = "Ev2-Cpu-Runtime")>]
type RuntimeMetricsEventSource() =
    inherit EventSource()

    // Singleton instance
    static let instance = new RuntimeMetricsEventSource()
    static member Instance = instance

    // ─────────────────────────────────────────────
    // Scan Cycle Metrics
    // ─────────────────────────────────────────────

    /// <summary>Record scan cycle completion</summary>
    /// <param name="scanIndex">Scan cycle index</param>
    /// <param name="durationMs">Total scan duration in milliseconds</param>
    /// <param name="cycleTimeMs">Target cycle time in milliseconds</param>
    [<Event(1, Level = EventLevel.Informational, Message = "Scan #{0} completed in {1}ms (target: {2}ms)")>]
    member this.ScanCompleted(scanIndex: int64, durationMs: int, cycleTimeMs: int) =
        this.WriteEvent(1, scanIndex, durationMs, cycleTimeMs)

    /// <summary>Record scan deadline miss (overrun)</summary>
    /// <param name="scanIndex">Scan cycle index</param>
    /// <param name="durationMs">Actual scan duration in milliseconds</param>
    /// <param name="deadlineMs">Deadline in milliseconds</param>
    [<Event(2, Level = EventLevel.Warning, Message = "Scan #{0} missed deadline: {1}ms > {2}ms")>]
    member this.ScanDeadlineMissed(scanIndex: int64, durationMs: int, deadlineMs: int) =
        this.WriteEvent(2, scanIndex, durationMs, deadlineMs)

    /// <summary>Record scan failure</summary>
    /// <param name="scanIndex">Scan cycle index</param>
    /// <param name="errorMessage">Error description</param>
    [<Event(3, Level = EventLevel.Error, Message = "Scan #{0} failed: {1}")>]
    member this.ScanFailed(scanIndex: int64, errorMessage: string) =
        this.WriteEvent(3, scanIndex, errorMessage)

    // ─────────────────────────────────────────────
    // Error Tracking
    // ─────────────────────────────────────────────

    /// <summary>Record fatal error</summary>
    /// <param name="message">Error message</param>
    [<Event(4, Level = EventLevel.Critical, Message = "Fatal error: {0}")>]
    member this.FatalError(message: string) =
        this.WriteEvent(4, message)

    /// <summary>Record recoverable error</summary>
    /// <param name="message">Error message</param>
    [<Event(5, Level = EventLevel.Error, Message = "Recoverable error: {0}")>]
    member this.RecoverableError(message: string) =
        this.WriteEvent(5, message)

    // ─────────────────────────────────────────────
    // Runtime Updates
    // ─────────────────────────────────────────────

    /// <summary>Record runtime update application</summary>
    /// <param name="updateType">Type of update (e.g., Program.Body, Variable)</param>
    [<Event(6, Level = EventLevel.Informational, Message = "Runtime update applied: {0}")>]
    member this.RuntimeUpdateApplied(updateType: string) =
        this.WriteEvent(6, updateType)

    // ─────────────────────────────────────────────
    // Retain Persistence
    // ─────────────────────────────────────────────

    /// <summary>Record retain data persistence</summary>
    /// <param name="variableCount">Number of variables persisted</param>
    [<Event(7, Level = EventLevel.Informational, Message = "Retain data persisted: {0} variables")>]
    member this.RetainPersisted(variableCount: int) =
        this.WriteEvent(7, variableCount)

    /// <summary>Record retain data load</summary>
    /// <param name="variableCount">Number of variables loaded</param>
    [<Event(8, Level = EventLevel.Informational, Message = "Retain data loaded: {0} variables")>]
    member this.RetainLoaded(variableCount: int) =
        this.WriteEvent(8, variableCount)

    // ─────────────────────────────────────────────
    // Per-Stage Metrics (Stage-Aware Deadlines)
    // ─────────────────────────────────────────────

    /// <summary>Record stage completion</summary>
    /// <param name="stageName">Stage name (PrepareInputs, Execution, FlushOutputs, Finalize)</param>
    /// <param name="durationMs">Stage duration in milliseconds</param>
    /// <param name="scanIndex">Scan cycle index</param>
    [<Event(9, Level = EventLevel.Verbose, Message = "Stage '{0}' completed in {1}ms (scan #{2})")>]
    member this.StageCompleted(stageName: string, durationMs: int, scanIndex: int64) =
        this.WriteEvent(9, stageName, durationMs, scanIndex)

    /// <summary>Record stage deadline miss</summary>
    /// <param name="stageName">Stage name</param>
    /// <param name="actualMs">Actual duration in milliseconds</param>
    /// <param name="thresholdMs">Threshold in milliseconds</param>
    /// <param name="scanIndex">Scan cycle index</param>
    [<Event(10, Level = EventLevel.Warning, Message = "Stage '{0}' missed deadline: {1}ms > {2}ms (scan #{3})")>]
    member this.StageDeadlineMissed(stageName: string, actualMs: int, thresholdMs: int, scanIndex: int64) =
        this.WriteEvent(10, stageName, actualMs, thresholdMs, scanIndex)

    // ─────────────────────────────────────────────
    // Performance Counters (for dotnet-counters)
    // ─────────────────────────────────────────────
    // NOTE: EventCounter metrics require non-singleton EventSource pattern
    // Current singleton pattern prevents safe EventCounter initialization
    // Future enhancement: Create separate non-singleton performance counter class
    //
    // Desired metrics:
    // - AverageScanTime (EventCounter)
    // - ScanOverrunRate (EventCounter)
    // - ErrorRate (EventCounter)
    // - MemoryUsage (PollingCounter)
    //
    // Alternative: Use PerformanceProfiler.globalProfiler for detailed metrics

/// <summary>Telemetry helper for runtime events</summary>
module RuntimeTelemetry =
    /// <summary>Emit scan completed telemetry (MEDIUM FIX: int64 to prevent overflow)</summary>
    let scanCompleted (scanIndex: int64) (durationMs: int) (cycleTimeMs: int) =
        RuntimeMetricsEventSource.Instance.ScanCompleted(scanIndex, durationMs, cycleTimeMs)

    /// <summary>Emit scan deadline missed telemetry (MEDIUM FIX: int64 to prevent overflow)</summary>
    let scanDeadlineMissed (scanIndex: int64) (durationMs: int) (deadlineMs: int) =
        RuntimeMetricsEventSource.Instance.ScanDeadlineMissed(scanIndex, durationMs, deadlineMs)

    /// <summary>Emit scan failed telemetry (MEDIUM FIX: int64 to prevent overflow)</summary>
    let scanFailed (scanIndex: int64) (errorMessage: string) =
        RuntimeMetricsEventSource.Instance.ScanFailed(scanIndex, errorMessage)

    /// <summary>Emit fatal error telemetry</summary>
    let fatalError (message: string) =
        RuntimeMetricsEventSource.Instance.FatalError(message)

    /// <summary>Emit recoverable error telemetry</summary>
    let recoverableError (message: string) =
        RuntimeMetricsEventSource.Instance.RecoverableError(message)

    /// <summary>Emit runtime update telemetry</summary>
    let runtimeUpdateApplied (updateType: string) =
        RuntimeMetricsEventSource.Instance.RuntimeUpdateApplied(updateType)

    /// <summary>Emit retain persisted telemetry</summary>
    let retainPersisted (variableCount: int) =
        RuntimeMetricsEventSource.Instance.RetainPersisted(variableCount)

    /// <summary>Emit retain loaded telemetry</summary>
    let retainLoaded (variableCount: int) =
        RuntimeMetricsEventSource.Instance.RetainLoaded(variableCount)

    /// <summary>Emit stage completed telemetry (MEDIUM FIX: int64 to prevent overflow)</summary>
    let stageCompleted (stageName: string) (durationMs: int) (scanIndex: int64) =
        RuntimeMetricsEventSource.Instance.StageCompleted(stageName, durationMs, scanIndex)

    /// <summary>Emit stage deadline missed telemetry (MEDIUM FIX: int64 to prevent overflow)</summary>
    let stageDeadlineMissed (stageName: string) (actualMs: int) (thresholdMs: int) (scanIndex: int64) =
        RuntimeMetricsEventSource.Instance.StageDeadlineMissed(stageName, actualMs, thresholdMs, scanIndex)

    /// <summary>Shutdown EventSource and flush pending events (DEFECT-010 fix)</summary>
    /// <remarks>
    /// IMPORTANT: Call this ONLY at application/process exit, NOT when individual
    /// CpuScanEngine instances stop. The EventSource is a singleton shared across
    /// all engine instances.
    ///
    /// Ensures:
    /// - All pending ETW events are flushed
    /// - EventSource resources are released
    /// - Monitoring tools receive final telemetry
    ///
    /// Usage:
    /// - Console apps: Call in Main() after engine.StopAsync()
    /// - ASP.NET: Call in IHostApplicationLifetime.ApplicationStopping
    /// - Windows Services: Call in OnStop() after all engines stopped
    /// </remarks>
    let shutdown () =
        RuntimeMetricsEventSource.Instance.Dispose()
