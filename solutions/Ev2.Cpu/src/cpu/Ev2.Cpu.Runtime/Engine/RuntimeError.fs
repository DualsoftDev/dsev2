namespace Ev2.Cpu.Runtime

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Runtime Error Types - Structured Error Handling (GAP-001 Fix)
// ═════════════════════════════════════════════════════════════════════════════
// Implements RuntimeSpec.md:89 requirement for structured errors with severity
// ═════════════════════════════════════════════════════════════════════════════

/// Error severity classification
type RuntimeErrorSeverity =
    /// Fatal error - scan loop must stop, requires manual intervention
    | Fatal
    /// Recoverable error - log, apply rollback if available, continue scan
    | Recoverable
    /// Warning - log once per debounce window, continue execution
    | Warning

/// Structured runtime error record
type RuntimeError = {
    /// Error severity classification
    Severity: RuntimeErrorSeverity

    /// Human-readable error message
    Message: string

    /// Function block instance that caused the error (if applicable)
    FBInstance: string option

    /// Scan cycle index when error occurred (MEDIUM FIX: int64 to prevent overflow)
    ScanIndex: int64

    /// AST statement node that caused the error (if applicable)
    AstNode: DsStmt option

    /// Timestamp when error occurred
    Timestamp: DateTime

    /// Stack trace for debugging
    StackTrace: string option

    /// Exception that caused the error (if applicable)
    Exception: exn option
}

/// Runtime error builder for fluent API
module RuntimeError =

    /// Create a fatal error
    let fatal (message: string) : RuntimeError =
        {
            Severity = Fatal
            Message = message
            FBInstance = None
            ScanIndex = 0
            AstNode = None
            Timestamp = DateTime.UtcNow
            StackTrace = None
            Exception = None
        }

    /// Create a recoverable error
    let recoverable (message: string) : RuntimeError =
        {
            Severity = Recoverable
            Message = message
            FBInstance = None
            ScanIndex = 0
            AstNode = None
            Timestamp = DateTime.UtcNow
            StackTrace = None
            Exception = None
        }

    /// Create a warning
    let warning (message: string) : RuntimeError =
        {
            Severity = Warning
            Message = message
            FBInstance = None
            ScanIndex = 0
            AstNode = None
            Timestamp = DateTime.UtcNow
            StackTrace = None
            Exception = None
        }

    /// Add FB instance context
    let withFBInstance (instance: string) (error: RuntimeError) : RuntimeError =
        { error with FBInstance = Some instance }

    /// Add scan index (MEDIUM FIX: int64 to prevent overflow)
    let withScanIndex (index: int64) (error: RuntimeError) : RuntimeError =
        { error with ScanIndex = index }

    /// Add AST node context
    let withAstNode (node: DsStmt) (error: RuntimeError) : RuntimeError =
        { error with AstNode = Some node }

    /// Add stack trace
    let withStackTrace (trace: string) (error: RuntimeError) : RuntimeError =
        { error with StackTrace = Some trace }

    /// Add exception context
    let withException (ex: exn) (error: RuntimeError) : RuntimeError =
        { error with
            Exception = Some ex
            StackTrace = Some ex.StackTrace
        }

    /// Format error for logging
    let format (error: RuntimeError) : string =
        let severityStr =
            match error.Severity with
            | Fatal -> "FATAL"
            | Recoverable -> "RECOVERABLE"
            | Warning -> "WARNING"

        let fbContext =
            error.FBInstance
            |> Option.map (fun fb -> $" [FB: {fb}]")
            |> Option.defaultValue ""

        let astContext =
            error.AstNode
            |> Option.map (fun node -> $" [AST: {node.GetType().Name}]")
            |> Option.defaultValue ""

        let stackContext =
            error.StackTrace
            |> Option.map (fun trace -> $"\nStack Trace:\n{trace}")
            |> Option.defaultValue ""

        $"[{severityStr}] Scan #{error.ScanIndex}{fbContext}{astContext}: {error.Message}{stackContext}"

/// Runtime error collection with debouncing
type RuntimeErrorLog() =
    let mutable errors : RuntimeError list = []
    let mutable warningCache : Map<string, DateTime> = Map.empty
    let warningDebounceWindow = TimeSpan.FromSeconds(10.0)

    // CRITICAL FIX (DEFECT-CRIT-2): Add configurable capacity limit for error log
    // Previous code: unbounded list growth could cause OOM in long-running systems
    // New behavior: FIFO eviction when capacity exceeded, preserving most recent errors
    let maxErrorCapacity = 1000  // Default: keep last 1000 errors
    let maxWarningCapacity = 500  // Keep last 500 warnings separately

    /// Trim error log to capacity (FIFO eviction for old entries)
    let trimErrors() =
        if errors.Length > maxErrorCapacity then
            // Keep most recent errors (front of list), drop oldest
            errors <- errors |> List.take maxErrorCapacity

    /// Add an error to the log
    /// MINOR FIX: Telemetry emission moved to CpuScan.fs to avoid compilation order issues
    member this.Log(error: RuntimeError) =
        match error.Severity with
        | Warning ->
            // Debounce warnings - only log if not seen recently
            let key = error.Message
            match warningCache.TryFind(key) with
            | Some lastSeen when DateTime.UtcNow - lastSeen < warningDebounceWindow ->
                // Skip duplicate warning within debounce window
                ()
            | _ ->
                warningCache <- warningCache.Add(key, DateTime.UtcNow)
                errors <- error :: errors
                trimErrors()  // CRITICAL FIX: Enforce capacity limit
                printfn "%s" (RuntimeError.format error)
        | _ ->
            errors <- error :: errors
            trimErrors()  // CRITICAL FIX: Enforce capacity limit
            printfn "%s" (RuntimeError.format error)

    /// Get all logged errors
    member _.GetErrors() : RuntimeError list =
        List.rev errors

    /// Get errors by severity
    member _.GetErrorsBySeverity(severity: RuntimeErrorSeverity) : RuntimeError list =
        errors
        |> List.filter (fun e -> e.Severity = severity)
        |> List.rev

    /// Get error count
    member _.ErrorCount : int =
        errors.Length

    /// Get fatal error count
    member _.FatalCount : int =
        errors |> List.filter (fun e -> e.Severity = Fatal) |> List.length

    /// Get recoverable error count
    member _.RecoverableCount : int =
        errors |> List.filter (fun e -> e.Severity = Recoverable) |> List.length

    /// Get warning count
    member _.WarningCount : int =
        errors |> List.filter (fun e -> e.Severity = Warning) |> List.length

    /// Check if any fatal errors occurred
    member _.HasFatalErrors : bool =
        errors |> List.exists (fun e -> e.Severity = Fatal)

    /// Clear all errors
    member this.Clear() =
        errors <- []
        warningCache <- Map.empty

    /// Create snapshot of error log state (DEFECT-005 fix: for transaction rollback)
    member this.CreateSnapshot() : RuntimeError list * Map<string, DateTime> =
        (errors, warningCache)

    /// Restore error log from snapshot (DEFECT-005 fix: for transaction rollback)
    member this.RestoreSnapshot(snapshot: RuntimeError list * Map<string, DateTime>) =
        let (snapshotErrors, snapshotCache) = snapshot
        errors <- snapshotErrors
        warningCache <- snapshotCache

    /// Clear old warnings (for memory management)
    member this.ClearOldWarnings() =
        let cutoff = DateTime.UtcNow - warningDebounceWindow
        // MAJOR FIX: Prune warningCache (LRU)
        warningCache <-
            warningCache
            |> Map.filter (fun _ lastSeen -> lastSeen > cutoff)
        // MAJOR FIX: Prune old warnings from errors list (prevents memory leak)
        errors <-
            errors
            |> List.filter (fun error ->
                match error.Severity with
                | Warning -> error.Timestamp > cutoff  // Remove old warnings
                | _ -> true  // Keep Fatal and Recoverable errors
            )
