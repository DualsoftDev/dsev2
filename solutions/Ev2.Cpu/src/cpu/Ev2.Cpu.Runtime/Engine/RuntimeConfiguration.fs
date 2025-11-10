namespace Ev2.Cpu.Runtime

// ─────────────────────────────────────────────────────────────────────
// Runtime Configuration
// ─────────────────────────────────────────────────────────────────────
// Centralized configuration for runtime limits and timeouts (NEW-DEFECT-002 fix)
// Previously scattered as magic numbers across multiple files
// ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Runtime configuration limits for memory, timeouts, and caching
/// </summary>
/// <remarks>
/// Addresses NEW-DEFECT-002: Centralizes hard-coded magic numbers into
/// a single configurable module, improving maintainability and testability.
///
/// Usage:
/// <code>
/// // Use default values
/// let config = RuntimeLimits.Default
///
/// // Override for production
/// RuntimeLimits.Current &lt;- { RuntimeLimits.Default with
///     MaxMemoryVariables = 5000
///     StopTimeoutMs = 10000 }
///
/// // Override for testing
/// RuntimeLimits.Current &lt;- { RuntimeLimits.Default with
///     MaxHistorySize = 100
///     StringCacheSize = 10 }
/// </code>
/// </remarks>
type RuntimeLimits = {
    /// Maximum number of variables in memory (Memory.fs:15)
    /// Default: 2000
    MaxMemoryVariables: int

    /// Maximum history size for variable changes (Memory.fs:19)
    /// Default: 10000
    MaxHistorySize: int

    /// Trace message capacity (Context.fs:131)
    /// Default: 1000
    TraceCapacity: int

    /// String cache size for performance optimization (FunctionCommon.fs:40)
    /// Default: 1000
    StringCacheSize: int

    /// Default work relay timeout in milliseconds (RelayLifecycle.fs:118)
    /// Default: 30000ms (30 seconds)
    DefaultWorkRelayTimeoutMs: int

    /// Default call relay timeout in milliseconds (RelayLifecycle.fs:186)
    /// Default: 5000ms (5 seconds)
    DefaultCallRelayTimeoutMs: int

    /// Engine stop timeout in milliseconds (CpuScan.fs:308)
    /// Default: 5000ms (5 seconds)
    StopTimeoutMs: int

    /// Warning cache cleanup interval in scans (CpuScan.fs:189)
    /// Default: 1000 scans (~100 seconds at 10Hz)
    WarningCleanupIntervalScans: int
}

/// Runtime configuration module
module RuntimeLimits =

    /// <summary>Default runtime limits (production-ready values)</summary>
    let Default = {
        MaxMemoryVariables = 2000
        MaxHistorySize = 10000
        TraceCapacity = 1000
        StringCacheSize = 1000
        DefaultWorkRelayTimeoutMs = 30000  // 30 seconds
        DefaultCallRelayTimeoutMs = 5000   // 5 seconds
        StopTimeoutMs = 5000               // 5 seconds
        WarningCleanupIntervalScans = 1000 // ~100s at 10Hz
    }

    /// <summary>Relaxed limits for development/testing</summary>
    let Development = {
        MaxMemoryVariables = 500
        MaxHistorySize = 1000
        TraceCapacity = 100
        StringCacheSize = 100
        DefaultWorkRelayTimeoutMs = 10000  // 10 seconds
        DefaultCallRelayTimeoutMs = 2000   // 2 seconds
        StopTimeoutMs = 2000               // 2 seconds
        WarningCleanupIntervalScans = 100  // ~10s at 10Hz
    }

    /// <summary>Strict limits for resource-constrained environments</summary>
    let Minimal = {
        MaxMemoryVariables = 100
        MaxHistorySize = 100
        TraceCapacity = 50
        StringCacheSize = 50
        DefaultWorkRelayTimeoutMs = 5000   // 5 seconds
        DefaultCallRelayTimeoutMs = 1000   // 1 second
        StopTimeoutMs = 1000               // 1 second
        WarningCleanupIntervalScans = 50   // ~5s at 10Hz
    }

    /// <summary>High-performance limits for large deployments</summary>
    let HighPerformance = {
        MaxMemoryVariables = 10000
        MaxHistorySize = 50000
        TraceCapacity = 5000
        StringCacheSize = 5000
        DefaultWorkRelayTimeoutMs = 60000  // 60 seconds
        DefaultCallRelayTimeoutMs = 10000  // 10 seconds
        StopTimeoutMs = 10000              // 10 seconds
        WarningCleanupIntervalScans = 5000 // ~500s at 10Hz
    }

    /// <summary>Current active runtime limits (mutable, thread-safe read)</summary>
    /// <remarks>
    /// Can be modified at application startup to override defaults.
    /// Should not be changed during runtime operation.
    ///
    /// Thread safety: Reads are safe. Modifications should only occur
    /// during initialization before any engines are started.
    /// </remarks>
    let mutable Current = Default

    /// <summary>Validate runtime limits for consistency</summary>
    /// <param name="limits">Limits to validate</param>
    /// <returns>Ok if valid, Error message if invalid</returns>
    let validate (limits: RuntimeLimits) : Result<unit, string> =
        if limits.MaxMemoryVariables < 1 then
            Error "MaxMemoryVariables must be at least 1"
        elif limits.MaxHistorySize < 0 then
            Error "MaxHistorySize must be non-negative"
        elif limits.TraceCapacity < 1 then
            Error "TraceCapacity must be at least 1"
        elif limits.StringCacheSize < 1 then
            Error "StringCacheSize must be at least 1"
        elif limits.DefaultWorkRelayTimeoutMs < 100 then
            Error "DefaultWorkRelayTimeoutMs must be at least 100ms"
        elif limits.DefaultCallRelayTimeoutMs < 100 then
            Error "DefaultCallRelayTimeoutMs must be at least 100ms"
        elif limits.StopTimeoutMs < 100 then
            Error "StopTimeoutMs must be at least 100ms"
        elif limits.WarningCleanupIntervalScans < 1 then
            Error "WarningCleanupIntervalScans must be at least 1"
        else
            Ok ()

    /// <summary>Set current limits with validation</summary>
    /// <param name="limits">New limits to apply</param>
    /// <returns>Ok if applied, Error message if invalid</returns>
    let trySet (limits: RuntimeLimits) : Result<unit, string> =
        match validate limits with
        | Ok () ->
            Current <- limits
            Ok ()
        | Error msg ->
            Error msg
