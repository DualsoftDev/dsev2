# Runtime Deep Dive Specification

This document expands the high-level runtime spec with operational timing, state machines, and observability guarantees. Use it alongside `Ev2.Cpu.Runtime.md` for implementation details.

---

## 1. Scope

- **Audience**: Runtime maintainers, performance engineers, and integration owners that embed the scan engine.
- **Out of scope**: PLC DSL semantics (`specs/core/`) and code generation pipelines (`specs/codegen/`). Those modules feed artefacts into the runtime but are documented separately.
- **Artifacts**: Applies to `Ev2.Cpu.Runtime.dll` and hosted services embedding the scan engine.

---

## 2. Timing Model

### 2.1 Scan Cycle Stages

| Stage | Description | Observability |
|-------|-------------|---------------|
| `PrepareInputs` | Process runtime updates and manage dependencies. Retain data restoration happens once at engine initialization, not during each scan. | `ScanTimeline.PrepareInputsDuration`, `RuntimeEvent.RuntimeUpdateApplied` |
| `ExecuteLogic` | Evaluate statements and expressions for all scheduled tasks and user FB instances. | `ScanTimeline.ExecutionDuration` |
| `FlushOutputs` | Persist retain variables synchronously on the scan thread and clear change flags (selective mode). | `ScanTimeline.FlushDuration`, `RuntimeEvent.RetainPersisted` |
| `Finalize` | Evaluate error log, process relay state transitions via `RelayStateManager`, prune warning history (`RuntimeLimits.Current.WarningCleanupIntervalScans`, default 1 000), and emit scan result events. | `ScanTimeline.FinalizeDuration`, `RuntimeEvent.ScanCompleted` / `RuntimeEvent.ScanFailed` |

The engine records per-stage durations and invokes `StageDeadlineEnforcer` to compare them with configured thresholds. Violations emit `RuntimeEvent.StageDeadlineMissed` events (telemetry-only; no automatic throttling). If the total scan duration exceeds the effective cycle time, a `RuntimeEvent.ScanDeadlineMissed` is published and the optional `WarnIfOverMs` threshold (5 000 ms by default) raises a console warning. When no stage thresholds are supplied, the runtime derives defaults from the cycle time in effect when the `CpuScanEngine` is created via `StageThresholds.defaultForCycleTime` (10 % / 70 % / 10 % / 10 %, each clamped to ≥1 ms). Hosts may supply custom thresholds through `ScanConfig.StageThresholds`; during each scan the engine recalculates derived defaults on-the-fly whenever the configured `Total` no longer matches the effective cycle time, but it preserves the original configuration structure (diagnostic views continue to show the caller-provided values). Helpers such as `StageThresholds.relaxed` therefore remain intact unless the host replaces them, while deadline checks fall back to the recomputed defaults when the stored `Total` diverges. Updating `ExecutionContext.CycleTime` after engine construction is detected on the next scan and causes subsequent deadline checks to use those recomputed defaults unless the caller continues to provide stage thresholds whose `Total` matches the new cycle time.

### 2.2 Scheduling Guarantees

- Scan loop runs on a dedicated thread (`TaskCreationOptions.LongRunning`). Cooperative cancellation is mandatory via `CancellationToken`.
- The scheduler enforces **single-writer** semantics for memory; the backing `Memory` store is single-threaded, so cross-thread readers should first capture a snapshot (for example via `ExecutionContext.CreateSnapshot()`) before accessing data outside the scan thread.
- Timer and counter services use a pluggable `ITimeProvider` abstraction (injected into `ExecutionContext`) for timestamp generation. The default implementation uses a `Stopwatch`-based monotonic clock. (The `TP` helper acquires timestamps through the provider while reusing `Timebase` helpers for elapsed calculations to remain backwards compatible.)
- Retain snapshot creation and persistence run synchronously on the scan thread. The `RetainPersisted` event is emitted immediately before the scan completes.

### 2.3 Runtime Limits & Tunables

The runtime centralises operational guardrails in `RuntimeLimits`:

- `RuntimeLimits.Current` exposes the active limits; `Default`, `Development`, `Minimal`, and `HighPerformance` presets ship with the library.
- Memory bounds: `MaxMemoryVariables`, `MaxHistorySize`, and `TraceCapacity` gate symbol registration, change history, and trace buffering. `Memory.Declare*` and snapshot creation enforce these limits and surface structured errors via `RuntimeExceptions`.
- Time-related knobs: `DefaultWorkRelayTimeoutMs`, `DefaultCallRelayTimeoutMs`, `StopTimeoutMs`, and `WarningCleanupIntervalScans` govern relay handshakes, engine shutdown, and warning log pruning.
- Additional caches such as the string conversion cache honour `StringCacheSize`.
- Override the defaults during host startup only; the module is not designed for hot reconfiguration.

---

## 3. Execution State Machines

### 3.1 Work Relay Lifecycle

Work relays are tracked via `WorkRelayStateMachine` managed by `RelayStateManager`. The state machine implements the following lifecycle:

- **Idle → Armed**: Event detected
- **Armed → Latched**: Condition met
- **Latched → Resetting**: Reset command
- **Resetting → Idle**: Post-reset hook completed
- **Armed → Idle**: Timeout expired

State transitions publish `RuntimeEvent.WorkRelayStateChanged` telemetry through the `IRuntimeEventSink`. The `RelayStateManager` processes all relay transitions during the `Finalize` stage of each scan cycle.

### 3.2 Call Relay Lifecycle

Call relays are managed via `CallRelayStateMachine` with full support for handshake, timeout management, and progress telemetry through the `ICallStrategy` interface. The state machine implements:

- **Waiting → Invoking**: Trigger received
- **Invoking → AwaitingAck**: `ICallStrategy.Begin()` succeeded
- **AwaitingAck → Waiting**: `ICallStrategy.Poll()` returns true (call completed)
- **AwaitingAck → Faulted**: Timeout exceeded
- **Faulted → Waiting**: Recover called or retry logic succeeded

State transitions publish:
- `RuntimeEvent.CallRelayStateChanged` for state changes
- `RuntimeEvent.CallProgress` for progress updates (0-100%)
- `RuntimeEvent.CallTimeout` when timeouts occur

Handshake rules:

- The evaluator only calls `Trigger()` when the relay is idle (`Waiting`). A successful trigger immediately schedules the `Poll()` handshake; the evaluation result returned to the PLC scan is the payload from the strategy once `Poll()` signals completion.
- In-progress relays (`Invoking`/`AwaitingAck`) are polled each scan until completion; while a call is pending the evaluator returns an empty object, enabling ladder logic to wait without blocking.
- Fault recovery invokes `Recover()` followed by a fresh `Trigger()` so that timeout telemetry, retry counters, and progress tracking remain consistent. `FaultReason` distinguishes timeout from other errors and is surfaced alongside `RetryCount`.
- `RelayStateManager` exposes `Register*/Update*` APIs that replace strategies atomically and ensure existing progress is disposed correctly. Dictionaries are concurrency safe (`ConcurrentDictionary`), allowing registration to occur from supervisory threads while scans execute.

The `ICallStrategy` interface provides:
- `Begin()`: Initiate call execution
- `Poll()`: Check completion status
- `GetProgress()`: Report progress percentage
- `OnTimeout(retryCount)`: Handle timeout with retry strategy
- `OnError(error)`: Handle error conditions

---

## 4. Memory & Retain Semantics

- Memory domains: `Input`, `Output`, `Local`, `Internal` (prefixed as I:, O:, L:, V: respectively). Each variable has an `IsRetain` flag; when true, the variable persists across power cycles regardless of domain.
- Retain variables are materialised on startup via `IRetainStorage.Load`. Subsequent scans rely on in-memory state until the next restart.
- Snapshots (`RetainSnapshot`) serialise both retain variables and FB static data. Each snapshot includes:
  - Variable values with data type information
  - FB instance static variables bundled by instance name (each entry stores `Name`, `DataType`, and `ValueJson`; the runtime validates type compatibility during restore)
  - SHA256 checksum for integrity verification
  - Version metadata for compatibility checks
- External tooling must avoid mutating runtime memory directly; use runtime updates or diagnostics APIs. There is no `WithReaderLock` helper in the current implementation.
- PLC logic continues to respect domain writability. The runtime exposes `Memory.SetForced` for diagnostics/runtime updates so that hosts can drive input (`I:`) variables intentionally without relaxing the guard inside the scan interpreter.

### Runtime Updates

The `RuntimeUpdateManager` coordinates hot updates:

- Requests are enqueued via `UpdateRequest` (single updates or `BatchUpdate`). Validation runs before snapshots are taken and honours `UpdateConfig` (`ForceValidation`, `AutoRollback`, `MaxSnapshotHistory`).
- Successful updates apply atomically during the `PrepareInputs` stage. `UpdateUserFB` automatically reconciles existing FB instances (migrating retain state when types are compatible). `UpdateCallRelay`/`UpdateWorkRelay` invocations from hosts can precede these updates to ensure relay definitions exist before execution.
- Failures trigger structured `UpdateResult` responses. When `AutoRollback` is enabled, the manager restores program bodies, user definitions, runtime memory snapshots (`ExecutionContext.CreateSnapshot()`), and error logs so the next scan resumes from a known-good baseline.
- Batch updates execute in dependency order (UserFC → UserFB → FBInstance → Program/Memory). Partial failures surface aggregated error messages; callers can inspect `UpdateStatistics` for aggregate insight.

---

## 5. Diagnostics & Observability

| Area | Implementation | Notes |
|------|---------------|-------|
| Scan timing | `ScanTimeline` emitted through `RuntimeEvent.ScanCompleted` | Per-stage durations are included with successful scans. Failure telemetry (`RuntimeEvent.ScanFailed`) carries the structured error without a timeline payload. Both per-stage and aggregate deadline checks remain active. |
| Deadline miss | `RuntimeEvent.ScanDeadlineMissed` / `StageDeadlineMissed`, console warning (`WarnIfOverMs`) | Per-stage violations detected by `StageDeadlineEnforcer`. No automatic throttling beyond event emission. |
| Errors | `RuntimeErrorLog` entries with severity, FB instance, scan index | Structured errors stored in-context; sinks may publish via `IRuntimeEventSink`. |
| Retain persistence | `RuntimeEvent.RetainPersisted` / `RetainLoaded` | Snapshot creation and save happen on the scan thread; events are emitted before the scan finalises. |
| Relay state | `RuntimeEvent.WorkRelayStateChanged` / `CallRelayStateChanged` | Published during `Finalize` stage via `RelayStateManager`. |
| Call progress | `RuntimeEvent.CallProgress` / `CallTimeout` | Progress updates (0-100%) and timeout notifications for call relays. |

- `IRuntimeEventSink` allows hosts to subscribe to runtime events (console and null sinks ship by default).
- `RuntimeMetricsEventSource` emits ETW telemetry for APM tools (Azure Monitor, Application Insights, PerfView, and other ETW consumers). EventCounter/PollingCounter streams for `dotnet-counters` remain on the backlog. It currently publishes:
  - Scan completion metrics (`ScanCompleted`, `ScanDeadlineMissed`, `ScanFailed`)
  - Per-stage timing (`StageCompleted`, `StageDeadlineMissed`)
  - Retain operations (`RetainPersisted`, `RetainLoaded`)
  - Runtime updates (`RuntimeUpdateApplied`)
  - Error tracking (`FatalError`, `RecoverableError`)
- Diagnostics and hooks should avoid allocations >2 KB per scan to keep GC pressure low.

---

## 6. Safety & Error Handling

- Exceptions raised inside `StmtEvaluator.exec` are captured as `RuntimeError` entries, tagged with `Severity` (Fatal, Recoverable, Warning).
- Policy matrix:
  - `Fatal`: stop the scan loop by transitioning the engine to `ExecutionState.Error`, surface to host, require manual intervention.
  - `Recoverable`: log, apply rollback, continue scan.
  - `Warning`: log once per debounce window.
- Recovery uses `ExecutionContext.Rollback()` snapshots to restore variable memory (and error log state) to the start-of-scan state; timer/counter caches continue from their last values.
- Fatal errors that occur inside the transactional execution path (`StmtEvaluator.exec*` wrapped by `ExecutionContext.WithTransaction`) roll back memory, restore the pre-scan error log snapshot, and then append a new fatal entry before the caller sets `ExecutionState.Error`. Hosts can observe the fatal outcome either through the `ErrorLog` (fatal entries are preserved after rollback), by examining the returned `Result`, or by checking `ExecutionState`.
- Warning retention is pruned every `RuntimeLimits.Current.WarningCleanupIntervalScans`; during pruning the log respects the 10-second debounce window that prevents duplicate warning spam.

---

## 7. Integration Points

| Integration | API | Notes |
|-------------|-----|-------|
| Hosted runtime service | `CpuScanEngine.StartAsync` | Provide an optional cancellation token. Telemetry sinks and retain storage are supplied through the constructor/`ScanConfig`. |
| Code generation pipeline | `Ev2.Cpu.Generation.Make.ProgramGen` (and related builders) | Use the generation helpers (e.g., `ProgramBuilder`, relay helpers in `Ev2.Cpu.Generation.Core`) to align AST nodes with the Work/Call relay contracts. A single `BuildRuntimeAst` entry point is not provided. |
| Diagnostics UI | `IRuntimeEventSink` | Subscribe to relay/state events and structured errors. |
| Retain persistence | `IRetainStorage` implementations | Ensure atomic save/load with checksum verification (backed by retain snapshots with version metadata). |

---

## 8. Related Documents

- `specs/runtime/Ev2.Cpu.Runtime.md` – Module-level architecture & roadmap
- `guides/operations/Retain-Memory-Guide.md` – Operational retain memory playbook
- `guides/quickstarts/PLC-Code-Generation-Guide.md` – Build & deploy workflow
- `guides/manuals/runtime/Ev2.Cpu.Runtime-사용자매뉴얼.md` – User-facing runtime manual
- `reference/Ev2.Cpu-API-Reference.md` – API surface and code samples
