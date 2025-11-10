namespace Ev2.Cpu.Runtime

open System
open System.Collections.Generic

// ═════════════════════════════════════════════════════════════════════
// Relay Lifecycle - State Machines for Work/Call Relays (GAP-009)
// ═════════════════════════════════════════════════════════════════════
// Implements RuntimeSpec.md §3: Execution State Machines
// - Work Relay: Idle → Armed → Latched → Resetting → Idle
// - Call Relay: Waiting → Invoking → AwaitingAck → Waiting/Faulted
// ═════════════════════════════════════════════════════════════════════

/// <summary>Call strategy interface for external API integration</summary>
/// <remarks>
/// Implementers provide:
/// - Begin: Initiate call (e.g., send API request)
/// - Poll: Check completion status
/// - GetProgress: Report progress percentage (0-100)
/// - OnTimeout: Handle timeout with retry/fail-safe strategy
/// - OnError: Handle error conditions
/// </remarks>
type ICallStrategy =
    /// <summary>Begin call execution</summary>
    /// <returns>Success indicator</returns>
    abstract member Begin: unit -> bool

    /// <summary>Poll for call completion</summary>
    /// <returns>True if call completed successfully</returns>
    abstract member Poll: unit -> bool

    /// <summary>Get current progress (0-100)</summary>
    abstract member GetProgress: unit -> int

    /// <summary>Handle timeout</summary>
    /// <param name="retryCount">Number of retries attempted</param>
    /// <returns>True to retry, false to fail</returns>
    abstract member OnTimeout: retryCount:int -> bool

    /// <summary>Handle error</summary>
    /// <param name="error">Error message</param>
    abstract member OnError: error:string -> unit

    /// <summary>Cleanup resources</summary>
    abstract member Dispose: unit -> unit

/// <summary>Relay state transition event</summary>
type RelayStateTransition<'TState> = {
    RelayName: string
    FromState: 'TState
    ToState: 'TState
    Timestamp: DateTime
    ScanIndex: int64  // MEDIUM FIX: int64 to prevent overflow
}

/// <summary>Base state machine for relay lifecycle management</summary>
type RelayStateMachine<'TState when 'TState : equality>(
    relayName: string,
    initialState: 'TState,
    timeProvider: ITimeProvider
) =
    let mutable currentState = initialState
    // MAJOR FIX (DEFECT-016-5): Use GetTimestamp for monotonic clock guarantee
    // UtcNow.Ticks is system clock, vulnerable to NTP/DST/manual adjustments
    // GetTimestamp returns Stopwatch ticks for true monotonic timing
    let mutable stateEntryTimestamp : int64 = timeProvider.GetTimestamp()
    let mutable scanIndex = 0L  // MEDIUM FIX: int64 to prevent overflow
    let transitions = ResizeArray<RelayStateTransition<'TState>>()

    // CRITICAL FIX (DEFECT-CRIT-3): Add capacity limit for transition history
    // Previous code: unbounded ResizeArray growth could cause OOM in long-running systems
    // New behavior: Keep last 500 transitions per relay (FIFO eviction)
    let maxTransitionCapacity = 500

    /// <summary>Trim transition history to capacity (FIFO eviction)</summary>
    let trimTransitions() =
        if transitions.Count > maxTransitionCapacity then
            // Remove oldest transitions (keep last maxTransitionCapacity)
            let excessCount = transitions.Count - maxTransitionCapacity
            transitions.RemoveRange(0, excessCount)

    /// <summary>Current relay state</summary>
    member _.CurrentState = currentState

    /// <summary>Time in current state</summary>
    /// <remarks>
    /// MAJOR FIX (DEFECT-016-5): Use GetTimestamp() for monotonic clock guarantee
    /// UtcNow.Ticks is system clock, vulnerable to NTP/DST/manual adjustments
    /// GetTimestamp() returns Stopwatch ticks for true monotonic timing
    /// </remarks>
    member _.TimeInState : TimeSpan =
        let nowTimestamp = timeProvider.GetTimestamp()
        let elapsedMs = Timebase.elapsedMilliseconds stateEntryTimestamp nowTimestamp
        TimeSpan.FromMilliseconds(float elapsedMs)

    /// <summary>Relay name</summary>
    member _.RelayName = relayName

    /// <summary>Transition to new state</summary>
    member _.TransitionTo(newState: 'TState) =
        if currentState <> newState then
            let transition = {
                RelayName = relayName
                FromState = currentState
                ToState = newState
                Timestamp = timeProvider.UtcNow  // Keep UtcNow for logging/display
                ScanIndex = scanIndex
            }
            transitions.Add(transition)
            trimTransitions()  // CRITICAL FIX: Enforce capacity limit
            currentState <- newState
            // MAJOR FIX (DEFECT-016-5): Use GetTimestamp for state entry time
            stateEntryTimestamp <- timeProvider.GetTimestamp()

    /// <summary>Update scan index (MEDIUM FIX: int64 to prevent overflow)</summary>
    member _.UpdateScanIndex(index: int64) =
        scanIndex <- index

    /// <summary>Get transition history</summary>
    member _.GetTransitionHistory() =
        transitions.ToArray()

    /// <summary>Clear transition history</summary>
    member _.ClearHistory() =
        transitions.Clear()

/// <summary>Work relay state machine</summary>
/// <remarks>
/// State transitions (RuntimeSpec.md §3.1):
/// Idle → Armed: Event detected
/// Armed → Latched: Condition met
/// Latched → Resetting: Reset command
/// Resetting → Idle: Post-reset hook
/// Armed → Idle: Timeout expired
/// </remarks>
type WorkRelayStateMachine(
    relayName: string,
    timeProvider: ITimeProvider,
    timeoutMs: int option
) =
    inherit RelayStateMachine<WorkRelayState>(relayName, WorkRelayState.Idle, timeProvider)

    let timeout = timeoutMs |> Option.defaultValue RuntimeLimits.Current.DefaultWorkRelayTimeoutMs
    let mutable onResetHook : (unit -> unit) option = None

    /// <summary>Try to arm the relay</summary>
    member this.TryArm(eventDetected: bool) : bool =
        if this.CurrentState = WorkRelayState.Idle && eventDetected then
            this.TransitionTo(WorkRelayState.Armed)
            true
        else
            false

    /// <summary>Try to latch the relay</summary>
    member this.TryLatch(conditionMet: bool) : bool =
        if this.CurrentState = WorkRelayState.Armed && conditionMet then
            this.TransitionTo(WorkRelayState.Latched)
            true
        else
            false

    /// <summary>Begin reset sequence</summary>
    member this.BeginReset() =
        if this.CurrentState = WorkRelayState.Latched then
            this.TransitionTo(WorkRelayState.Resetting)

    /// <summary>Complete reset and return to idle</summary>
    member this.CompleteReset() =
        if this.CurrentState = WorkRelayState.Resetting then
            // Run post-reset hook
            onResetHook |> Option.iter (fun hook -> hook())
            this.TransitionTo(WorkRelayState.Idle)

    /// <summary>Check for timeout and revert to idle</summary>
    member this.CheckTimeout() =
        if this.CurrentState = WorkRelayState.Armed then
            let elapsed : float = this.TimeInState.TotalMilliseconds
            if elapsed > float timeout then
                this.TransitionTo(WorkRelayState.Idle)
                true
            else
                false
        else
            false

    /// <summary>Register post-reset hook</summary>
    member _.SetResetHook(hook: unit -> unit) =
        onResetHook <- Some hook

    /// <summary>Is relay active (latched)?</summary>
    member this.IsActive = this.CurrentState = WorkRelayState.Latched

/// <summary>Call relay state machine with timeout and retry</summary>
/// <remarks>
/// State transitions (RuntimeSpec.md §3.2):
/// Waiting → Invoking: Trigger
/// Invoking → AwaitingAck: Handshake
/// AwaitingAck → Waiting: Ack received
/// AwaitingAck → Faulted: Timeout
/// Faulted → Waiting: Recover
/// </remarks>
/// <summary>Fault reason for call relay failures</summary>
type CallFaultReason =
    | CallTimeout
    | CallError

type CallRelayStateMachine(
    relayName: string,
    timeProvider: ITimeProvider,
    strategy: ICallStrategy,
    timeoutMs: int option,
    maxRetries: int option
) =
    inherit RelayStateMachine<CallRelayState>(relayName, CallRelayState.Waiting, timeProvider)

    let timeout = timeoutMs |> Option.defaultValue RuntimeLimits.Current.DefaultCallRelayTimeoutMs
    let maxRetryCount = maxRetries |> Option.defaultValue 3
    let mutable retryCount = 0
    let mutable lastError : string option = None
    // MAJOR FIX: Track fault reason explicitly instead of string matching (DEFECT-014-5)
    let mutable faultReason : CallFaultReason option = None

    /// <summary>Trigger call invocation</summary>
    member this.Trigger() : bool =
        if this.CurrentState = CallRelayState.Waiting then
            this.TransitionTo(CallRelayState.Invoking)
            // Execute ICallStrategy.Begin
            try
                let success = strategy.Begin()
                if success then
                    this.TransitionTo(CallRelayState.AwaitingAck)
                    true
                else
                    this.TransitionTo(CallRelayState.Faulted)
                    lastError <- Some "ICallStrategy.Begin failed"
                    faultReason <- Some CallFaultReason.CallError  // MAJOR FIX: Track fault reason
                    strategy.OnError("Begin failed")
                    false
            with ex ->
                this.TransitionTo(CallRelayState.Faulted)
                lastError <- Some ex.Message
                faultReason <- Some CallFaultReason.CallError  // MAJOR FIX: Track fault reason
                strategy.OnError(ex.Message)
                false
        else
            false

    /// <summary>Poll for completion</summary>
    member this.Poll() : bool =
        if this.CurrentState = CallRelayState.AwaitingAck then
            try
                let completed = strategy.Poll()
                if completed then
                    this.TransitionTo(CallRelayState.Waiting)
                    retryCount <- 0
                    true
                else
                    // Check timeout
                    let elapsed : float = this.TimeInState.TotalMilliseconds
                    if elapsed > float timeout then
                        this.HandleTimeout()
                    false
            with ex ->
                this.TransitionTo(CallRelayState.Faulted)
                lastError <- Some ex.Message
                faultReason <- Some CallFaultReason.CallError  // MAJOR FIX: Track fault reason
                strategy.OnError(ex.Message)
                false
        else
            false

    /// <summary>Handle timeout</summary>
    member private this.HandleTimeout() =
        this.TransitionTo(CallRelayState.Faulted)
        lastError <- Some (sprintf "Timeout after %dms" timeout)
        faultReason <- Some CallFaultReason.CallTimeout  // MAJOR FIX: Track timeout explicitly

        // Ask strategy if we should retry
        let shouldRetry = strategy.OnTimeout(retryCount)
        if shouldRetry && retryCount < maxRetryCount then
            retryCount <- retryCount + 1
            this.TransitionTo(CallRelayState.Waiting)
        else
            // Max retries exceeded or strategy declined retry
            strategy.OnError(sprintf "Call timed out after %d retries" retryCount)

    /// <summary>Recover from faulted state</summary>
    member this.Recover() =
        if this.CurrentState = CallRelayState.Faulted then
            this.TransitionTo(CallRelayState.Waiting)
            retryCount <- 0
            lastError <- None
            faultReason <- None  // MAJOR FIX: Clear fault reason on recovery

    /// <summary>Get current progress (0-100)</summary>
    member _.GetProgress() : int =
        try
            strategy.GetProgress()
        with _ ->
            0

    /// <summary>Get last error message</summary>
    member _.LastError = lastError

    /// <summary>Get retry count</summary>
    member _.RetryCount = retryCount

    /// <summary>Get fault reason (Timeout vs Error) - MAJOR FIX: Use instead of string matching</summary>
    member _.FaultReason = faultReason

    /// <summary>Is call in progress?</summary>
    member this.IsInProgress =
        this.CurrentState = CallRelayState.Invoking ||
        this.CurrentState = CallRelayState.AwaitingAck

    /// <summary>Cleanup resources</summary>
    member _.Dispose() =
        strategy.Dispose()

/// <summary>Default no-op call strategy for testing</summary>
type NoOpCallStrategy() =
    interface ICallStrategy with
        member _.Begin() = true
        member _.Poll() = true
        member _.GetProgress() = 100
        member _.OnTimeout(_retryCount) = false
        member _.OnError(_error) = ()
        member _.Dispose() = ()

/// <summary>Simulated call strategy for testing (time-based)</summary>
type SimulatedCallStrategy(completionDelayMs: int, ?timeProvider: ITimeProvider) =
    let timeProvider = timeProvider |> Option.defaultWith (fun () -> SystemTimeProvider() :> ITimeProvider)
    // MAJOR FIX (DEFECT-016-7): Use GetTimestamp for deterministic time simulation
    // UtcNow breaks custom ITimeProvider implementations (RuntimeSpec.md:32)
    let mutable startTimestamp = 0L
    let mutable completed = false

    interface ICallStrategy with
        member _.Begin() =
            startTimestamp <- timeProvider.GetTimestamp()
            completed <- false
            true

        member _.Poll() =
            if completed then
                true
            else
                let nowTimestamp = timeProvider.GetTimestamp()
                let elapsed = Timebase.elapsedMilliseconds startTimestamp nowTimestamp
                if elapsed >= completionDelayMs then
                    completed <- true
                    true
                else
                    false

        member _.GetProgress() =
            if completed then
                100
            else
                let nowTimestamp = timeProvider.GetTimestamp()
                let elapsed = Timebase.elapsedMilliseconds startTimestamp nowTimestamp
                let progress = int ((float elapsed / float completionDelayMs) * 100.0)
                Math.Min(progress, 99)

        member _.OnTimeout(retryCount) =
            retryCount < 2  // Allow 2 retries

        member _.OnError(error) =
            printfn "[SimulatedCallStrategy] Error: %s" error

        member _.Dispose() = ()
