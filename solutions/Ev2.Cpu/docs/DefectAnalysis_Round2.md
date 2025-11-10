# Runtime Implementation Defect Analysis - Round 2

**Date**: 2025-10-28
**Scope**: Complete solution review after GAP-009 and enhancements
**Status**: 🔴 **10 Critical/Major Defects Identified**

---

## Executive Summary

After implementing all 10 RuntimeSpec gaps and enhancements (444 tests passing), a comprehensive code review revealed **10 significant defects** that affect correctness, thread safety, and performance.

**Severity Distribution**:
- 🔴 **P0 (Critical)**: 3 defects - Correctness/thread safety issues
- 🟡 **P1 (Major)**: 4 defects - Design flaws affecting functionality
- 🟢 **P2 (Minor)**: 3 defects - Performance and code quality issues

---

## P0 Defects (Critical - Blocking Production)

### 🔴 **DEFECT-001: Call Relay Handshake Not Implemented in Function Execution**

**Severity**: P0 - Critical
**Category**: Missing Implementation

**Description**:
User requirement: "Wrap call execution with handshake/timeout logic" was marked complete but **never actually implemented**. Call relay state machines exist but are not integrated with function evaluation.

**Evidence**:
```fsharp
// ExprEvaluator.fs - Function evaluation (NO relay integration)
| Function (name, args) ->
    match BuiltinFunctionRegistry.tryFind name with
    | Some descriptor ->
        let argValues = args |> List.map (eval ctx)
        descriptor.Execute(ctx, argValues)  // Direct execution, no relay handshake!
    | None ->
        failwith $"Unknown function: {name}"
```

**Expected Behavior**:
```fsharp
// Should wrap with call relay
| Function (name, args) when isExternalCall name ->
    match ctx.RelayStateManager with
    | Some manager ->
        match manager.TryGetCallRelay(name) with
        | Some relay ->
            if relay.Trigger() then
                // Execute with timeout monitoring
                let result = executeWithTimeout relay (fun () ->
                    let argValues = args |> List.map (eval ctx)
                    descriptor.Execute(ctx, argValues))
                relay.Poll() |> ignore
                result
            else
                failwith $"Call relay '{name}' in invalid state"
        | None -> // fallback to direct execution
    | None -> // fallback
```

**Impact**:
- ❌ External API calls have NO timeout protection
- ❌ No progress monitoring for long-running calls
- ❌ Call relay state machines exist but are unused
- ❌ CallTimeout events never emitted
- ❌ Retry mechanism never triggered

**Root Cause**:
Implementation focused on infrastructure (state machines, telemetry) but forgot the critical integration point: **wrapping actual function calls**.

**Fix Priority**: 🔴 **IMMEDIATE** - Core functionality missing

---

### 🔴 **DEFECT-002: RelayStateManager Thread Safety Violation**

**Severity**: P0 - Critical
**Category**: Concurrency Bug

**Description**:
`RelayStateManager` uses non-thread-safe collections (`Dictionary`, `ResizeArray`) but is accessed from multiple contexts without synchronization.

**Evidence**:
```fsharp
// Engine/RelayStateManager.fs:13-15
type RelayStateManager(timeProvider: ITimeProvider, eventSink: IRuntimeEventSink option) =
    let workRelays = Dictionary<string, WorkRelayStateMachine>()  // NOT THREAD-SAFE
    let callRelays = Dictionary<string, CallRelayStateMachine>()
    let stateTransitions = ResizeArray<StateTransition>()  // NOT THREAD-SAFE

    member _.RegisterWorkRelay(name: string, timeoutMs: int option) =
        let relay = WorkRelayStateMachine(name, timeProvider, timeoutMs)
        workRelays.[name] <- relay  // RACE CONDITION if called concurrently
```

**Attack Scenario**:
1. Scan thread calls `ProcessStateChanges()` → iterates `workRelays`
2. Update thread calls `RegisterWorkRelay()` → modifies `workRelays`
3. **Dictionary throws `InvalidOperationException: Collection was modified`**

**Evidence from Framework**:
```
System.Collections.Generic.Dictionary<TKey,TValue> is NOT thread-safe.
Multiple readers are safe, but any writer requires exclusive access.
```

**Impact**:
- ❌ Runtime crashes with `InvalidOperationException`
- ❌ Relay registration during scan causes race conditions
- ❌ State transition list corruption
- ❌ Lost relay events

**Fix Required**:
```fsharp
type RelayStateManager(timeProvider: ITimeProvider, eventSink: IRuntimeEventSink option) =
    let workRelays = ConcurrentDictionary<string, WorkRelayStateMachine>()
    let callRelays = ConcurrentDictionary<string, CallRelayStateMachine>()
    let stateTransitionsLock = obj()
    let stateTransitions = ResizeArray<StateTransition>()

    member _.ProcessStateChanges() =
        lock stateTransitionsLock (fun () ->
            // ... process transitions
            stateTransitions.Clear())
```

**Fix Priority**: 🔴 **IMMEDIATE** - Production crash risk

---

### 🔴 **DEFECT-003: ITimeProvider Not Propagated to Standard Library Timers**

**Severity**: P0 - Critical
**Category**: Incomplete Integration

**Description**:
Despite implementing `ITimeProvider` abstraction and threading it through relay state machines, **standard library timers (TON, TOF, TP, TONR) still use system time directly**, breaking testability.

**Evidence**:
```fsharp
// Ev2.Cpu.StandardLibrary/Timers/TON.fs:47
type TON() =
    // ...
    member this.Execute(ctx: IExecutionContext, args: obj list) =
        let currentTime = System.DateTime.UtcNow  // DIRECT SYSTEM TIME - NOT TESTABLE!
        let elapsedMs = (currentTime - startTimeOpt.Value).TotalMilliseconds
```

**Contradiction**:
```fsharp
// Tests use TestTimeProvider for relays
let timeProvider = TestTimeProvider(DateTime.UtcNow)
timeProvider.AdvanceMs(500)  // Works for relays

// But TON timer IGNORES injected time!
TON.Execute(ctx, [PT 1000])  // Uses DateTime.UtcNow, not timeProvider
```

**Impact**:
- ❌ Timer tests cannot use simulated time
- ❌ Must use `Thread.Sleep` → slow tests
- ❌ Cannot test timer edge cases deterministically
- ❌ `ITimeProvider` benefit lost for 50% of time-dependent code

**Required Fix**:
```fsharp
// ExecutionContext needs ITimeProvider field
type ExecutionContext = {
    // ...
    TimeProvider: ITimeProvider  // ADD THIS
}

// TON uses injected provider
member this.Execute(ctx: IExecutionContext, args: obj list) =
    let currentTime = ctx.TimeProvider.UtcNow  // Use injected time
```

**Files Affected**:
- `StandardLibrary/Timers/TON.fs`
- `StandardLibrary/Timers/TOF.fs`
- `StandardLibrary/Timers/TP.fs`
- `StandardLibrary/Timers/TONR.fs`
- `StandardLibrary/Counters/CTU.fs` (elapsed time tracking)

**Fix Priority**: 🔴 **HIGH** - Breaks testability promise

---

## P1 Defects (Major - Functional Impact)

### 🟡 **DEFECT-004: RelayStateManager Not Auto-Initialized**

**Severity**: P1 - Major
**Category**: Usability / API Design Flaw

**Description**:
`ExecutionContext.RelayStateManager` defaults to `None` and is never automatically created. Users must manually instantiate it, but this is **not documented** and tests pass because they explicitly create it.

**Evidence**:
```fsharp
// Engine/Context.fs:42
let create () : ExecutionContext =
    { // ...
      RelayStateManager = None  // Always None - user must set manually!
    }

// CpuScan.fs:180 - Silently does nothing if None
ctx.RelayStateManager |> Option.iter (fun manager ->
    manager.ProcessStateChanges())  // Skipped if None
```

**User Impact**:
```fsharp
// User creates context and engine
let ctx = Context.create()
let engine = CpuScanEngine(program, ctx, None, None, None)

// Registers relays via ??? (NO API EXISTS!)
// Expected: ctx.RegisterWorkRelay("Relay1", Some 1000)
// Reality: Must manually create manager
ctx.RelayStateManager <- Some (RelayStateManager(ctx.TimeProvider, config.EventSink))
```

**Expected API**:
```fsharp
// Option 1: Auto-create on first use
let create (timeProvider: ITimeProvider) (eventSink: IRuntimeEventSink option) =
    { // ...
      RelayStateManager = Some (RelayStateManager(timeProvider, eventSink))
    }

// Option 2: Lazy initialization
member ctx.GetOrCreateRelayManager() =
    match ctx.RelayStateManager with
    | Some m -> m
    | None ->
        let m = RelayStateManager(ctx.TimeProvider, None)
        ctx.RelayStateManager <- Some m
        m
```

**Impact**:
- ❌ Relay features unusable without undocumented manual setup
- ❌ Silent failure (no errors, just no relay processing)
- ❌ Poor developer experience

**Fix Priority**: 🟡 **HIGH** - Usability blocker

---

### 🟡 **DEFECT-005: Stage Threshold Integer Division Precision Loss**

**Severity**: P1 - Major
**Category**: Numerical Accuracy

**Description**:
`StageThresholds.defaultForCycleTime` uses integer division, causing precision loss for small cycle times.

**Evidence**:
```fsharp
// Engine/ScanThresholds.fs:42-49
let defaultForCycleTime (cycleTimeMs: int) : StageThresholds =
    {
        PrepareInputs = Some (cycleTimeMs / 10)      // 10% - INTEGER DIVISION!
        Execution = Some (cycleTimeMs * 7 / 10)      // 70%
        FlushOutputs = Some (cycleTimeMs / 10)       // 10%
        Finalize = Some (cycleTimeMs / 10)           // 10%
        Total = Some cycleTimeMs
    }
```

**Problem Cases**:
```fsharp
// Case 1: 105ms cycle
defaultForCycleTime 105
// PrepareInputs = 105 / 10 = 10ms (should be 10.5ms)
// Loss: 0.5ms per stage = 2ms total (2% error)

// Case 2: 50ms cycle (fast PLC)
defaultForCycleTime 50
// PrepareInputs = 50 / 10 = 5ms (correct)
// Execution = 50 * 7 / 10 = 350 / 10 = 35ms (should be 35ms, correct)

// Case 3: 33ms cycle (30Hz)
defaultForCycleTime 33
// PrepareInputs = 33 / 10 = 3ms (should be 3.3ms)
// Execution = 33 * 7 / 10 = 231 / 10 = 23ms (should be 23.1ms)
// Total allocated: 3 + 23 + 3 + 3 = 32ms < 33ms (1ms unaccounted)
```

**Impact**:
- ❌ Thresholds don't sum to 100% for many cycle times
- ❌ Stages have "extra" headroom, making violations less sensitive
- ❌ Inconsistent behavior across different cycle times

**Fix**:
```fsharp
let defaultForCycleTime (cycleTimeMs: int) : StageThresholds =
    let cycleTime = float cycleTimeMs
    {
        PrepareInputs = Some (int (cycleTime * 0.10))   // Round down
        Execution = Some (int (cycleTime * 0.70))
        FlushOutputs = Some (int (cycleTime * 0.10))
        Finalize = Some (int (cycleTime * 0.10))
        Total = Some cycleTimeMs
    }
```

**Fix Priority**: 🟡 **MEDIUM** - Affects monitoring accuracy

---

### 🟡 **DEFECT-006: Relay Timeout Checks Delayed Until Finalize Stage**

**Severity**: P1 - Major
**Category**: Timing/Responsiveness

**Description**:
Call relay timeout detection happens in **Finalize stage** (end of scan), not during **Execution stage** when the timeout actually occurs. This delays timeout detection by up to one full scan cycle.

**Evidence**:
```fsharp
// CpuScan.fs:130-145 - Execution stage
let swExec = System.Diagnostics.Stopwatch.StartNew()
if useSelective then
    StmtEvaluator.execScanSelective ctx currentProgramBody
else
    StmtEvaluator.execScan ctx currentProgramBody  // Call may timeout HERE
swExec.Stop()

// ... FlushOutputs stage ...

// CpuScan.fs:180-183 - Finalize stage
ctx.RelayStateManager |> Option.iter (fun manager ->
    manager.ProcessStateChanges())  // Timeout detected LATER!
```

**Scenario**:
```
T=0ms:   Scan 1 starts
T=10ms:  External API call starts (timeout = 50ms)
T=60ms:  API call times out (still waiting)
T=100ms: Scan 1 ends, Finalize detects timeout ← 40ms delay!
T=100ms: CallTimeout event emitted
```

**Impact**:
- ❌ Timeout detection delayed by full scan duration
- ❌ Slow response to hung external calls
- ❌ Timeout metric inaccuracy (reports wrong scan index)
- ❌ Call relay may block multiple scans before detection

**Better Design**:
```fsharp
// Option 1: Poll during execution
let executeWithTimeoutCheck relay action =
    let result = action()
    relay.Poll() |> ignore  // Check immediately
    result

// Option 2: Background timeout monitor
let timeoutWatcher = Task.Run(fun () ->
    while running do
        ctx.RelayStateManager |> Option.iter _.CheckTimeouts()
        Task.Delay(10).Wait())
```

**Fix Priority**: 🟡 **MEDIUM** - Affects real-time responsiveness

---

### 🟡 **DEFECT-007: Duplicate EventSource Emissions in Stage Metrics**

**Severity**: P1 - Major
**Category**: Correctness / Double-Counting

**Description**:
Stage metrics are emitted **twice**: once in `CpuScan.fs` via `RuntimeTelemetry`, and again in `StageDeadlineEnforcer.checkAllStages` (for violations only). This creates confusion in monitoring dashboards.

**Evidence**:
```fsharp
// CpuScan.fs:199-203 - First emission (ALWAYS)
RuntimeTelemetry.stageCompleted "PrepareInputs" (int prepareInputsDuration.TotalMilliseconds) scanIndex
RuntimeTelemetry.stageCompleted "Execution" (int executionDuration.TotalMilliseconds) scanIndex
RuntimeTelemetry.stageCompleted "FlushOutputs" (int flushDuration.TotalMilliseconds) scanIndex
RuntimeTelemetry.stageCompleted "Finalize" (int finalizeDuration.TotalMilliseconds) scanIndex

// CpuScan.fs:206-207 - Second emission (violations)
cfg.StageThresholds |> Option.iter (fun thresholds ->
    let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds cfg.EventSink)

// ScanThresholds.fs:114-121 - Inside checkAllStages (DUPLICATE for violations)
violations |> List.iter (fun violation ->
    RuntimeMetricsEventSource.Instance.StageDeadlineMissed(...)  // Event ID 10
)
```

**Impact**:
- ❌ APM dashboards show duplicate `StageCompleted` events? (NO - different event IDs)
- ✅ Actually OK: StageCompleted (ID 9) vs StageDeadlineMissed (ID 10) are different
- ❌ **BUT**: `checkAllStages` also calls EventSource INSIDE, while caller may emit to IRuntimeEventSink
- ❌ Confusion: Who is responsible for emitting?

**Actual Issue**:
```fsharp
// checkAllStages emits to EventSource directly
RuntimeMetricsEventSource.Instance.StageDeadlineMissed(...)

// But also receives IRuntimeEventSink option (unused for violations!)
let checkAllStages (timeline: ScanTimeline) (thresholds: StageThresholds)
                   (eventSink: IRuntimeEventSink option) =
    // eventSink is IGNORED for StageDeadlineMissed!
```

**Design Inconsistency**:
- `ScanCompleted`, `ScanFailed` → emitted to `IRuntimeEventSink`
- `StageCompleted` → emitted to `EventSource` only
- `StageDeadlineMissed` → emitted to `EventSource` only

This breaks the pattern where `IRuntimeEventSink` is the primary abstraction.

**Fix Priority**: 🟡 **LOW** - Functional but inconsistent design

---

## P2 Defects (Minor - Code Quality / Performance)

### 🟢 **DEFECT-008: RetainMemory Snapshot Creation Blocks Scan Cycle**

**Severity**: P2 - Minor
**Category**: Performance

**Description**:
`CreateRetainSnapshot()` is synchronous and creates deep copies of all retain variables during the FlushOutputs stage, potentially blocking the scan for milliseconds.

**Evidence**:
```fsharp
// CpuScan.fs:152-165 - FlushOutputs stage
let swFlush = System.Diagnostics.Stopwatch.StartNew()

match retainStorage with
| Some storage ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()  // BLOCKING CALL!
    Task.Run(fun () ->  // Only the SAVE is async
        match storage.Save(snapshot) with ...
    ) |> ignore
| None -> ()

swFlush.Stop()
```

```fsharp
// Memory.fs:643-691 - CreateRetainSnapshot
member this.CreateRetainSnapshot() : RetainSnapshot =
    let retainVars = retainMemory.Keys |> Seq.map (fun name ->
        let value = retainMemory.[name]
        { Name = name; Value = value; Type = value.GetType().FullName }
    ) |> Seq.toArray  // SYNCHRONOUS iteration and array creation

    let fbStaticData = ... // More synchronous work
    let checksum = computeChecksum retainVars fbStaticData  // SHA256 computation!

    { Variables = retainVars; ... }  // Return
```

**Performance Impact**:
```
Typical PLC: 1000 retain variables
- Array allocation: ~10μs
- Type name strings: ~100μs (1000 × GetType())
- SHA256 checksum: ~500μs for 50KB data
Total: ~600μs per scan (acceptable for 100ms cycle)

Large PLC: 10,000 retain variables
- Array allocation: ~100μs
- Type names: ~1ms
- SHA256: ~5ms for 500KB
Total: ~6ms per scan (6% of 100ms cycle!) ← PROBLEM
```

**Impact**:
- ❌ Large retain datasets cause FlushOutputs stage violations
- ❌ Contributes to scan overruns
- ❌ Checksum computation is CPU-intensive

**Fix**:
```fsharp
// Option 1: Incremental snapshots (only changed variables)
member this.CreateIncrementalSnapshot(lastSnapshot: RetainSnapshot option) =
    let changedVars = getChangedRetainVariables lastSnapshot
    { Variables = changedVars; IsIncremental = true }

// Option 2: Move to background task
let snapshotTask = Task.Run(fun () ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()  // Off scan thread
    storage.Save(snapshot))
```

**Fix Priority**: 🟢 **LOW** - Only affects large deployments

---

### 🟢 **DEFECT-009: EventSource Singleton Never Disposed**

**Severity**: P2 - Minor
**Category**: Resource Leak

**Description**:
`RuntimeMetricsEventSource` is a singleton that implements `IDisposable` (inherited from `EventSource`) but is never disposed, causing resource leaks on application shutdown.

**Evidence**:
```fsharp
// Engine/RuntimeMetrics.fs:26-31
[<EventSource(Name = "Ev2-Cpu-Runtime")>]
type RuntimeMetricsEventSource() =
    inherit EventSource()  // IDisposable

    static let instance = new RuntimeMetricsEventSource()  // NEVER DISPOSED
    static member Instance = instance
```

**Framework Requirement**:
```
EventSource.Dispose() must be called to:
1. Flush pending events
2. Release ETW session handles
3. Unregister from EventListener
4. Free native resources
```

**Impact**:
- ❌ ETW sessions may leak
- ❌ EventListeners not notified of shutdown
- ❌ Pending events may be lost
- ❌ Native memory leak (~1KB per instance)

**Fix**:
```fsharp
// Add disposal on app shutdown
type RuntimeMetrics =
    static let instance = new RuntimeMetricsEventSource()

    static member Instance = instance

    static member Shutdown() =
        instance.Dispose()

// CpuScanEngine
member this.StopAsync(?timeoutMs:int) : Task =
    task {
        // ... existing shutdown ...

        // Dispose EventSource
        RuntimeMetrics.Shutdown()
    }
```

**Fix Priority**: 🟢 **LOW** - Cosmetic (minimal impact)

---

### 🟢 **DEFECT-010: ScanConfig.Default Doesn't Enable Stage Monitoring**

**Severity**: P2 - Minor
**Category**: Usability

**Description**:
`ScanConfig.Default` sets `StageThresholds = None`, disabling stage monitoring by default. Users must explicitly enable it, but may not know the feature exists.

**Evidence**:
```fsharp
// CpuScan.fs:26-27
static member Default =
    { CycleTimeMs = None; WarnIfOverMs = Some 5_000; SelectiveMode = false;
      EventSink = None; StageThresholds = None }  // Disabled by default!
```

**User Experience**:
```fsharp
// User creates engine with defaults
let engine = CpuScanEngine(program, ctx, None, None, None)  // Uses ScanConfig.Default
engine.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

// Stage metrics: SILENT - no violations detected (feature disabled)
```

**Comparison**:
- ✅ `WarnIfOverMs = Some 5_000` (enabled by default - good!)
- ❌ `StageThresholds = None` (disabled by default - inconsistent)

**Expected Behavior**:
```fsharp
static member Default =
    { CycleTimeMs = None
      WarnIfOverMs = Some 5_000
      SelectiveMode = false
      EventSink = None
      StageThresholds = None  // OK - requires explicit cycle time
    }

// But provide a helper
static member DefaultWithStageMonitoring(cycleTimeMs: int) =
    { ScanConfig.Default with
        CycleTimeMs = Some cycleTimeMs
        StageThresholds = Some (StageThresholds.defaultForCycleTime cycleTimeMs)
    }
```

**Impact**:
- ❌ Feature discoverability problem
- ❌ Users may not realize stage monitoring exists
- ❌ Inconsistent with other monitoring features

**Fix Priority**: 🟢 **LOW** - Documentation issue

---

## Summary Table

| ID | Severity | Category | Issue | Impact | Fix Priority |
|----|----------|----------|-------|--------|--------------|
| 001 | P0 | Missing Implementation | Call relay handshake not in ExprEvaluator | No timeout protection for function calls | 🔴 IMMEDIATE |
| 002 | P0 | Concurrency | RelayStateManager not thread-safe | Runtime crashes | 🔴 IMMEDIATE |
| 003 | P0 | Incomplete Integration | ITimeProvider not in TON/TOF/TP timers | Breaks testability | 🔴 HIGH |
| 004 | P1 | Usability | RelayStateManager not auto-initialized | Relay features unusable | 🟡 HIGH |
| 005 | P1 | Numerical | Stage threshold integer division | Precision loss for small cycles | 🟡 MEDIUM |
| 006 | P1 | Timing | Relay timeout checks delayed | Slow timeout detection | 🟡 MEDIUM |
| 007 | P1 | Correctness | Inconsistent EventSource usage | Design confusion | 🟡 LOW |
| 008 | P2 | Performance | Retain snapshot blocks scan | Large dataset overhead | 🟢 LOW |
| 009 | P2 | Resource Leak | EventSource never disposed | Memory leak on shutdown | 🟢 LOW |
| 010 | P2 | Usability | Stage monitoring disabled by default | Feature discoverability | 🟢 LOW |

---

## Recommended Fix Order

### Phase 1 (Immediate - P0)
1. **DEFECT-001**: Implement call relay handshake in ExprEvaluator
2. **DEFECT-002**: Add thread safety to RelayStateManager (ConcurrentDictionary)
3. **DEFECT-003**: Thread ITimeProvider through ExecutionContext to timers

### Phase 2 (High Priority - P1)
4. **DEFECT-004**: Auto-initialize RelayStateManager in Context.create()
5. **DEFECT-005**: Fix stage threshold calculation to use float
6. **DEFECT-006**: Move relay timeout checks to Execution stage

### Phase 3 (Quality - P2)
7. **DEFECT-007**: Standardize EventSource/IRuntimeEventSink emission
8. **DEFECT-008**: Optimize retain snapshot creation
9. **DEFECT-009**: Add EventSource disposal
10. **DEFECT-010**: Update ScanConfig.Default documentation

---

## Test Impact

**Current Status**: 444/444 tests passing
**After Defect Fixes**: Estimated 460+ tests (16 new tests for fixes)

**New Test Requirements**:
- DEFECT-001: Call relay integration tests (5 tests)
- DEFECT-002: Concurrent relay registration tests (3 tests)
- DEFECT-003: Timer with ITimeProvider tests (4 tests)
- DEFECT-005: Threshold precision tests (2 tests)
- DEFECT-006: Timeout detection timing tests (2 tests)

---

**Report Generated**: 2025-10-28
**Next Action**: Prioritize P0 fixes for production readiness
