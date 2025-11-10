namespace Ev2.Cpu.Runtime

open System
open System.Collections.Generic
open System.Collections.Concurrent

// ═════════════════════════════════════════════════════════════════════
// Relay State Manager - Manages all Work/Call relay lifecycles (GAP-009)
// ═════════════════════════════════════════════════════════════════════

/// <summary>Relay state manager for tracking all relay lifecycles</summary>
/// <remarks>
/// Manages Work and Call relay state machines in the execution context.
/// Publishes state change events to IRuntimeEventSink.
/// Thread-safe: Uses ConcurrentDictionary (DEFECT-002 fix).
/// </remarks>
type RelayStateManager(timeProvider: ITimeProvider, eventSink: IRuntimeEventSink option) =
    let workRelays = ConcurrentDictionary<string, WorkRelayStateMachine>()
    let callRelays = ConcurrentDictionary<string, CallRelayStateMachine>()
    let mutable currentScanIndex = 0L  // MEDIUM FIX: int64 to prevent overflow

    /// <summary>Register a work relay (thread-safe)</summary>
    /// <returns>True if added, false if already exists (use UpdateWorkRelay to reconfigure)</returns>
    member _.RegisterWorkRelay(name: string, timeoutMs: int option) =
        let relay = WorkRelayStateMachine(name, timeProvider, timeoutMs)
        // MODERATE FIX: Seed with current scan index to prevent telemetry chronology break
        // Without this, first state transitions publish with scanIndex=0 instead of actual scan number
        relay.UpdateScanIndex(currentScanIndex)
        workRelays.TryAdd(name, relay)

    /// <summary>Update or register work relay (thread-safe reconfiguration)</summary>
    /// <remarks>
    /// MAJOR FIX (DEFECT-016-8): Support relay reconfiguration
    /// TryAdd silently fails on duplicates, leaving hosts without reconfiguration path
    /// This method disposes old relay and replaces with new configuration
    /// </remarks>
    member _.UpdateWorkRelay(name: string, timeoutMs: int option) =
        // Dispose old relay if exists
        match workRelays.TryGetValue(name) with
        | true, oldRelay -> ()  // WorkRelay has no Dispose, just replace
        | false, _ -> ()

        // Create and register new relay
        let newRelay = WorkRelayStateMachine(name, timeProvider, timeoutMs)
        newRelay.UpdateScanIndex(currentScanIndex)
        workRelays.[name] <- newRelay

    /// <summary>Register a call relay (thread-safe)</summary>
    /// <returns>True if added, false if already exists (use UpdateCallRelay to reconfigure)</returns>
    member _.RegisterCallRelay(name: string, strategy: ICallStrategy, timeoutMs: int option, maxRetries: int option) =
        let relay = CallRelayStateMachine(name, timeProvider, strategy, timeoutMs, maxRetries)
        // MODERATE FIX: Seed with current scan index to prevent telemetry chronology break
        relay.UpdateScanIndex(currentScanIndex)
        callRelays.TryAdd(name, relay)

    /// <summary>Update or register call relay (thread-safe reconfiguration)</summary>
    /// <remarks>
    /// MAJOR FIX (DEFECT-016-8): Support relay reconfiguration
    /// TryAdd silently fails on duplicates, leaving hosts without reconfiguration path
    /// This method disposes old relay (including strategy cleanup) and replaces with new configuration
    /// </remarks>
    member _.UpdateCallRelay(name: string, strategy: ICallStrategy, timeoutMs: int option, maxRetries: int option) =
        // Dispose old relay if exists
        match callRelays.TryGetValue(name) with
        | true, oldRelay -> oldRelay.Dispose()
        | false, _ -> ()

        // Create and register new relay
        let newRelay = CallRelayStateMachine(name, timeProvider, strategy, timeoutMs, maxRetries)
        newRelay.UpdateScanIndex(currentScanIndex)
        callRelays.[name] <- newRelay

    /// <summary>Get work relay by name</summary>
    member _.TryGetWorkRelay(name: string) =
        match workRelays.TryGetValue(name) with
        | true, relay -> Some relay
        | false, _ -> None

    /// <summary>Get call relay by name</summary>
    member _.TryGetCallRelay(name: string) =
        match callRelays.TryGetValue(name) with
        | true, relay -> Some relay
        | false, _ -> None

    /// <summary>Update scan index for all relays (thread-safe snapshot) (MEDIUM FIX: int64 to prevent overflow)</summary>
    member _.UpdateScanIndex(scanIndex: int64) =
        currentScanIndex <- scanIndex
        // Create snapshot to avoid modification during enumeration (DEFECT-002 fix)
        for relay in workRelays.Values |> Seq.toArray do
            relay.UpdateScanIndex(scanIndex)
        for relay in callRelays.Values |> Seq.toArray do
            relay.UpdateScanIndex(scanIndex)

    /// <summary>Process all relays and publish state changes (thread-safe snapshot)</summary>
    member this.ProcessStateChanges() =
        // Process work relays - snapshot to avoid concurrent modification (DEFECT-002 fix)
        for KeyValue(name, relay) in workRelays |> Seq.toArray do
            // MAJOR FIX: Check for timeout and revert to Idle (RuntimeSpec.md:47)
            relay.CheckTimeout() |> ignore
            let transitions = relay.GetTransitionHistory()
            for transition in transitions do
                this.PublishWorkRelayTransition(transition)
            relay.ClearHistory()

        // Process call relays - snapshot to avoid concurrent modification
        for KeyValue(name, relay) in callRelays |> Seq.toArray do
            // MEDIUM FIX: Publish call progress for in-progress calls (RuntimeSpec.md:62-64)
            if relay.CurrentState = CallRelayState.AwaitingAck then
                this.PublishCallProgress(name)

            let transitions = relay.GetTransitionHistory()
            for transition in transitions do
                this.PublishCallRelayTransition(transition)
            relay.ClearHistory()

    /// <summary>Publish work relay state transition</summary>
    member private _.PublishWorkRelayTransition(transition: RelayStateTransition<WorkRelayState>) =
        match eventSink with
        | Some (sink: IRuntimeEventSink) ->
            let event = RuntimeEvent.WorkRelayStateChanged(
                transition.RelayName,
                transition.FromState,
                transition.ToState,
                transition.ScanIndex,
                transition.Timestamp
            )
            sink.Publish(event)
        | None -> ()

    /// <summary>Publish call relay state transition</summary>
    member private _.PublishCallRelayTransition(transition: RelayStateTransition<CallRelayState>) =
        match eventSink with
        | Some (sink: IRuntimeEventSink) ->
            let event = RuntimeEvent.CallRelayStateChanged(
                transition.RelayName,
                transition.FromState,
                transition.ToState,
                transition.ScanIndex,
                transition.Timestamp
            )
            sink.Publish(event)

            // MAJOR FIX: Only publish CallTimeout event for actual timeouts, not errors (RuntimeSpec.md:62-64)
            // MAJOR FIX: Use structured FaultReason instead of string matching (DEFECT-014-5)
            if transition.ToState = CallRelayState.Faulted then
                match callRelays.TryGetValue(transition.RelayName) with
                | true, relay ->
                    // Check if faulted due to timeout (not error) using structured state
                    match relay.FaultReason with
                    | Some CallFaultReason.CallTimeout ->
                        let timeoutEvent = RuntimeEvent.CallTimeout(
                            transition.RelayName,
                            relay.RetryCount,
                            transition.Timestamp
                        )
                        sink.Publish(timeoutEvent)
                    | Some CallFaultReason.CallError -> ()  // Not a timeout - skip CallTimeout event
                    | None -> ()  // No fault reason recorded
                | false, _ -> ()
        | None -> ()

    /// <summary>Publish call progress</summary>
    member _.PublishCallProgress(relayName: string) =
        match eventSink with
        | Some (sink: IRuntimeEventSink) ->
            match callRelays.TryGetValue(relayName) with
            | true, relay ->
                let rawProgress = relay.GetProgress()
                // LOW FIX: Clamp progress to 0-100% range (RuntimeSpec.md:63-64)
                let progress = max 0 (min 100 rawProgress)
                let event = RuntimeEvent.CallProgress(
                    relayName,
                    progress,
                    timeProvider.UtcNow
                )
                sink.Publish(event)
            | false, _ -> ()
        | None -> ()

    /// <summary>Get all work relay names</summary>
    member _.GetWorkRelayNames() =
        workRelays.Keys |> Seq.toList

    /// <summary>Get all call relay names</summary>
    member _.GetCallRelayNames() =
        callRelays.Keys |> Seq.toList

    /// <summary>Clear all relays</summary>
    member _.Clear() =
        // CRITICAL FIX (DEFECT-CRIT-12): Snapshot keys before disposal iteration
        // Previous code: Disposed during ConcurrentDictionary.Values enumeration
        // Problem: Dispose() may trigger callbacks that modify dictionary, breaking iteration
        // Solution: Snapshot all relays to array before disposal loop
        let callRelaysSnapshot = callRelays.Values |> Seq.toArray
        for relay in callRelaysSnapshot do
            try
                relay.Dispose()
            with ex ->
                // Log disposal errors but continue cleanup (best-effort)
                eprintfn "[RelayStateManager] Error disposing call relay: %s" ex.Message
        workRelays.Clear()
        callRelays.Clear()

    /// <summary>Dispose all resources</summary>
    interface IDisposable with
        member this.Dispose() =
            this.Clear()
