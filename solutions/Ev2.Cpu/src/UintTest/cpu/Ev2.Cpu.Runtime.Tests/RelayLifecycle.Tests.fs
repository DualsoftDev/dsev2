namespace Ev2.Cpu.Runtime.Tests

open System
open System.Threading
open Xunit
open Ev2.Cpu.Runtime

// ═════════════════════════════════════════════════════════════════════════════
// Relay Lifecycle Tests - Work/Call Relay State Machines (GAP-009)
// ═════════════════════════════════════════════════════════════════════════════

module RelayLifecycleTests =

    // ─────────────────────────────────────────────────────────────────────────
    // Test Helpers
    // ─────────────────────────────────────────────────────────────────────────

    type TestTimeProvider(initialTime: DateTime) =
        let mutable currentTime = initialTime
        let mutable ticks = 0L

        member _.AdvanceMs(ms: int) =
            currentTime <- currentTime.AddMilliseconds(float ms)
            ticks <- ticks + int64 ms * 10000L  // 10000 ticks per ms

        interface ITimeProvider with
            member _.GetTimestamp() = ticks
            member _.UtcNow = currentTime

    type RecordingEventSink() =
        let events = ResizeArray<RuntimeEvent>()

        member _.Events = events.ToArray()
        member _.Clear() = events.Clear()

        interface IRuntimeEventSink with
            member _.Publish(event: RuntimeEvent) =
                events.Add(event)

    // ─────────────────────────────────────────────────────────────────────────
    // Work Relay Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``Work relay - Idle to Armed transition`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let relay = WorkRelayStateMachine("TestWork", timeProvider, None)

        Assert.Equal(WorkRelayState.Idle, relay.CurrentState)

        // Arm the relay
        let armed = relay.TryArm(true)
        Assert.True(armed)
        Assert.Equal(WorkRelayState.Armed, relay.CurrentState)

    [<Fact>]
    let ``Work relay - Armed to Latched transition`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let relay = WorkRelayStateMachine("TestWork", timeProvider, None)

        relay.TryArm(true) |> ignore
        Assert.Equal(WorkRelayState.Armed, relay.CurrentState)

        // Latch the relay
        let latched = relay.TryLatch(true)
        Assert.True(latched)
        Assert.Equal(WorkRelayState.Latched, relay.CurrentState)
        Assert.True(relay.IsActive)

    [<Fact>]
    let ``Work relay - Latched to Resetting to Idle transition`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let relay = WorkRelayStateMachine("TestWork", timeProvider, None)

        // Arm and latch
        relay.TryArm(true) |> ignore
        relay.TryLatch(true) |> ignore
        Assert.Equal(WorkRelayState.Latched, relay.CurrentState)

        // Begin reset
        relay.BeginReset()
        Assert.Equal(WorkRelayState.Resetting, relay.CurrentState)

        // Complete reset
        relay.CompleteReset()
        Assert.Equal(WorkRelayState.Idle, relay.CurrentState)
        Assert.False(relay.IsActive)

    [<Fact>]
    let ``Work relay - timeout reverts Armed to Idle`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let relay = WorkRelayStateMachine("TestWork", timeProvider, Some 1000)

        // Arm the relay
        relay.TryArm(true) |> ignore
        Assert.Equal(WorkRelayState.Armed, relay.CurrentState)

        // Check timeout before expiry
        let timedOut1 = relay.CheckTimeout()
        Assert.False(timedOut1)
        Assert.Equal(WorkRelayState.Armed, relay.CurrentState)

        // Advance time beyond timeout
        testTime.AdvanceMs(1100)

        // Check timeout after expiry
        let timedOut2 = relay.CheckTimeout()
        Assert.True(timedOut2)
        Assert.Equal(WorkRelayState.Idle, relay.CurrentState)

    [<Fact>]
    let ``Work relay - reset hook is invoked`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let relay = WorkRelayStateMachine("TestWork", timeProvider, None)
        let mutable hookCalled = false

        relay.SetResetHook(fun () -> hookCalled <- true)

        // Arm, latch, reset
        relay.TryArm(true) |> ignore
        relay.TryLatch(true) |> ignore
        relay.BeginReset()

        Assert.False(hookCalled)

        relay.CompleteReset()

        Assert.True(hookCalled)

    // ─────────────────────────────────────────────────────────────────────────
    // Call Relay Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``Call relay - Waiting to Invoking to AwaitingAck`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let strategy = NoOpCallStrategy()
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, None, None)

        Assert.Equal(CallRelayState.Waiting, relay.CurrentState)

        // Trigger
        let triggered = relay.Trigger()
        Assert.True(triggered)
        Assert.Equal(CallRelayState.AwaitingAck, relay.CurrentState)
        Assert.True(relay.IsInProgress)

    [<Fact>]
    let ``Call relay - Poll completes and returns to Waiting`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let strategy = NoOpCallStrategy()
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, None, None)

        // Trigger
        relay.Trigger() |> ignore
        Assert.Equal(CallRelayState.AwaitingAck, relay.CurrentState)

        // Poll (NoOp strategy always returns true)
        let completed = relay.Poll()
        Assert.True(completed)
        Assert.Equal(CallRelayState.Waiting, relay.CurrentState)
        Assert.False(relay.IsInProgress)

    [<Fact>]
    let ``Call relay - timeout triggers Faulted state`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let strategy = SimulatedCallStrategy(10000, timeProvider)  // 10 seconds to complete
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, Some 1000, Some 0)  // No retries

        // Trigger
        relay.Trigger() |> ignore
        Assert.Equal(CallRelayState.AwaitingAck, relay.CurrentState)

        // Poll before timeout
        let completed1 = relay.Poll()
        Assert.False(completed1)
        Assert.Equal(CallRelayState.AwaitingAck, relay.CurrentState)

        // Advance time beyond timeout
        testTime.AdvanceMs(1100)

        // Poll after timeout
        let completed2 = relay.Poll()
        Assert.False(completed2)
        Assert.Equal(CallRelayState.Faulted, relay.CurrentState)
        Assert.True(relay.LastError.IsSome)

    [<Fact>]
    let ``Call relay - retry mechanism`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let strategy = SimulatedCallStrategy(10000, timeProvider)  // 10 seconds to complete
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, Some 500, Some 2)

        // Trigger
        relay.Trigger() |> ignore

        // First timeout
        testTime.AdvanceMs(600)
        relay.Poll() |> ignore
        Assert.Equal(CallRelayState.Waiting, relay.CurrentState)  // Retried
        Assert.Equal(1, relay.RetryCount)

        // Trigger again
        relay.Trigger() |> ignore

        // Second timeout
        testTime.AdvanceMs(600)
        relay.Poll() |> ignore
        Assert.Equal(CallRelayState.Waiting, relay.CurrentState)  // Retried again
        Assert.Equal(2, relay.RetryCount)

        // Trigger again
        relay.Trigger() |> ignore

        // Third timeout - max retries exceeded
        testTime.AdvanceMs(600)
        relay.Poll() |> ignore
        Assert.Equal(CallRelayState.Faulted, relay.CurrentState)  // No more retries

    [<Fact>]
    let ``Call relay - recover from faulted state`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let strategy = SimulatedCallStrategy(10000, timeProvider)
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, Some 500, Some 0)  // No retries

        // Trigger and timeout
        relay.Trigger() |> ignore
        testTime.AdvanceMs(600)
        relay.Poll() |> ignore
        Assert.Equal(CallRelayState.Faulted, relay.CurrentState)

        // Recover
        relay.Recover()
        Assert.Equal(CallRelayState.Waiting, relay.CurrentState)
        Assert.Equal(0, relay.RetryCount)
        Assert.True(relay.LastError.IsNone)

    [<Fact>]
    let ``Call relay - progress reporting`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let strategy = SimulatedCallStrategy(1000, timeProvider)  // 1 second to complete
        let relay = CallRelayStateMachine("TestCall", timeProvider, strategy, Some 5000, None)

        // Trigger
        relay.Trigger() |> ignore

        // Progress at 0ms
        let progress0 = relay.GetProgress()
        Assert.True(progress0 < 10)

        // Advance 500ms (50%)
        testTime.AdvanceMs(500)
        let progress50 = relay.GetProgress()
        Assert.True(progress50 >= 45 && progress50 <= 55)

        // Advance another 500ms (100%)
        testTime.AdvanceMs(500)
        relay.Poll() |> ignore  // Should complete
        let progress100 = relay.GetProgress()
        Assert.Equal(100, progress100)

    // ─────────────────────────────────────────────────────────────────────────
    // RelayStateManager Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``RelayStateManager - register and retrieve work relay`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let manager = new RelayStateManager(timeProvider, None)

        manager.RegisterWorkRelay("Work1", Some 1000)

        match manager.TryGetWorkRelay("Work1") with
        | Some relay ->
            Assert.Equal("Work1", relay.RelayName)
            Assert.Equal(WorkRelayState.Idle, relay.CurrentState)
        | None ->
            Assert.True(false, "Work relay not found")

    [<Fact>]
    let ``RelayStateManager - register and retrieve call relay`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let strategy = NoOpCallStrategy()
        let manager = new RelayStateManager(timeProvider, None)

        manager.RegisterCallRelay("Call1", strategy, Some 1000, Some 3)

        match manager.TryGetCallRelay("Call1") with
        | Some relay ->
            Assert.Equal("Call1", relay.RelayName)
            Assert.Equal(CallRelayState.Waiting, relay.CurrentState)
        | None ->
            Assert.True(false, "Call relay not found")

    [<Fact>]
    let ``RelayStateManager - publishes work relay state changes`` () =
        let timeProvider = TestTimeProvider(DateTime.UtcNow) :> ITimeProvider
        let sink = RecordingEventSink()
        let manager = new RelayStateManager(timeProvider, Some sink)

        manager.RegisterWorkRelay("Work1", None)
        manager.UpdateScanIndex(1)

        // Transition Idle -> Armed
        match manager.TryGetWorkRelay("Work1") with
        | Some relay -> relay.TryArm(true) |> ignore
        | None -> ()

        manager.ProcessStateChanges()

        let events = sink.Events
        Assert.Equal(1, events.Length)

        match events.[0] with
        | RuntimeEvent.WorkRelayStateChanged(name, fromState, toState, scanIndex, _ts) ->
            Assert.Equal("Work1", name)
            Assert.Equal(WorkRelayState.Idle, fromState)
            Assert.Equal(WorkRelayState.Armed, toState)
            Assert.Equal(1L, scanIndex)  // MEDIUM FIX: int64 literal
        | _ -> Assert.True(false, "Expected WorkRelayStateChanged event")

    [<Fact>]
    let ``RelayStateManager - publishes call relay state changes and timeout`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let sink = RecordingEventSink()
        let strategy = SimulatedCallStrategy(10000, timeProvider)
        let manager = new RelayStateManager(timeProvider, Some sink)

        manager.RegisterCallRelay("Call1", strategy, Some 500, Some 1)
        manager.UpdateScanIndex(1)

        // Trigger
        match manager.TryGetCallRelay("Call1") with
        | Some relay -> relay.Trigger() |> ignore
        | None -> ()

        manager.ProcessStateChanges()

        // Should have Waiting->Invoking and Invoking->AwaitingAck
        Assert.True(sink.Events.Length >= 2)
        sink.Clear()

        // Timeout
        testTime.AdvanceMs(600)

        match manager.TryGetCallRelay("Call1") with
        | Some relay -> relay.Poll() |> ignore
        | None -> ()

        manager.ProcessStateChanges()

        let events = sink.Events
        // Should have AwaitingAck->Faulted and CallTimeout
        Assert.True(events.Length >= 2)

        let hasStateChange = events |> Array.exists (fun e ->
            match e with
            | RuntimeEvent.CallRelayStateChanged(_, _, toState, _, _) -> toState = CallRelayState.Faulted
            | _ -> false)

        let hasTimeout = events |> Array.exists (fun e ->
            match e with
            | RuntimeEvent.CallTimeout _ -> true
            | _ -> false)

        Assert.True(hasStateChange, "Expected CallRelayStateChanged to Faulted")
        Assert.True(hasTimeout, "Expected CallTimeout event")

    [<Fact>]
    let ``RelayStateManager - publishes call progress`` () =
        let testTime = TestTimeProvider(DateTime.UtcNow)
        let timeProvider = testTime :> ITimeProvider
        let sink = RecordingEventSink()
        let strategy = SimulatedCallStrategy(1000, timeProvider)
        let manager = new RelayStateManager(timeProvider, Some sink)

        manager.RegisterCallRelay("Call1", strategy, Some 5000, None)

        // Trigger
        match manager.TryGetCallRelay("Call1") with
        | Some relay -> relay.Trigger() |> ignore
        | None -> ()

        testTime.AdvanceMs(500)

        // Publish progress
        manager.PublishCallProgress("Call1")

        let events = sink.Events
        let progressEvents = events |> Array.filter (fun e ->
            match e with
            | RuntimeEvent.CallProgress _ -> true
            | _ -> false)

        Assert.True(progressEvents.Length > 0, "Expected CallProgress event")

        match progressEvents.[progressEvents.Length - 1] with
        | RuntimeEvent.CallProgress(name, progress, _ts) ->
            Assert.Equal("Call1", name)
            Assert.True(progress >= 40 && progress <= 60)
        | _ -> ()
