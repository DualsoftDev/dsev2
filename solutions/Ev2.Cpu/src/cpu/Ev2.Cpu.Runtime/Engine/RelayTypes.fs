namespace Ev2.Cpu.Runtime

// ═════════════════════════════════════════════════════════════════════
// Relay State Types - Enums for Work/Call Relay States (GAP-009)
// ═════════════════════════════════════════════════════════════════════

/// <summary>Work relay states (RuntimeSpec.md §3.1)</summary>
[<RequireQualifiedAccess>]
type WorkRelayState =
    /// Relay dormant, timers paused
    | Idle = 0
    /// Preconditions tracked, timers run, outputs not asserted
    | Armed = 1
    /// Active relay, outputs asserted, retain snapshots capture state
    | Latched = 2
    /// Cleanup hooks, detach observers
    | Resetting = 3

/// <summary>Call relay states (RuntimeSpec.md §3.2)</summary>
[<RequireQualifiedAccess>]
type CallRelayState =
    /// Waiting for trigger
    | Waiting = 0
    /// Executing ICallStrategy.Begin
    | Invoking = 1
    /// Polling ICallStrategy.End, surfacing progress
    | AwaitingAck = 2
    /// Timeout or error occurred
    | Faulted = 3
