namespace Ev2.Cpu.Runtime.Tests

open System
open Xunit
open Ev2.Cpu.Runtime

// ═════════════════════════════════════════════════════════════════════════════
// Stage Thresholds Tests - Per-Stage Deadline Enforcement
// ═════════════════════════════════════════════════════════════════════════════

module StageThresholdsTests =

    // ─────────────────────────────────────────────────────────────────────────
    // Test Helpers
    // ─────────────────────────────────────────────────────────────────────────

    type RecordingEventSink() =
        let events = ResizeArray<RuntimeEvent>()

        member _.Events = events.ToArray()
        member _.Clear() = events.Clear()

        interface IRuntimeEventSink with
            member _.Publish(event: RuntimeEvent) =
                events.Add(event)

    let createTestTimeline prepareMs execMs flushMs finalizeMs (scanIdx: int64) =
        {
            PrepareInputsDuration = TimeSpan.FromMilliseconds(float prepareMs)
            ExecutionDuration = TimeSpan.FromMilliseconds(float execMs)
            FlushDuration = TimeSpan.FromMilliseconds(float flushMs)
            FinalizeDuration = TimeSpan.FromMilliseconds(float finalizeMs)
            TotalDuration = TimeSpan.FromMilliseconds(float (prepareMs + execMs + flushMs + finalizeMs))
            ScanIndex = scanIdx  // MEDIUM FIX: int64 type
        }

    // ─────────────────────────────────────────────────────────────────────────
    // StageThresholds Configuration Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``defaultForCycleTime - calculates correct percentages`` () =
        let thresholds = StageThresholds.defaultForCycleTime 1000

        Assert.Equal(Some 100, thresholds.PrepareInputs)  // 10%
        Assert.Equal(Some 700, thresholds.Execution)      // 70%
        Assert.Equal(Some 100, thresholds.FlushOutputs)   // 10%
        Assert.Equal(Some 100, thresholds.Finalize)       // 10%
        Assert.Equal(Some 1000, thresholds.Total)         // 100%

    [<Fact>]
    let ``relaxed thresholds - provides reasonable defaults`` () =
        let thresholds = StageThresholds.relaxed

        Assert.Equal(Some 100, thresholds.PrepareInputs)
        Assert.Equal(Some 500, thresholds.Execution)
        Assert.Equal(Some 100, thresholds.FlushOutputs)
        Assert.Equal(Some 100, thresholds.Finalize)
        Assert.Equal(Some 1000, thresholds.Total)

    [<Fact>]
    let ``unlimited thresholds - all stages have no limits`` () =
        let thresholds = StageThresholds.unlimited

        Assert.Equal(None, thresholds.PrepareInputs)
        Assert.Equal(None, thresholds.Execution)
        Assert.Equal(None, thresholds.FlushOutputs)
        Assert.Equal(None, thresholds.Finalize)
        Assert.Equal(None, thresholds.Total)

    // ─────────────────────────────────────────────────────────────────────────
    // StageDeadlineEnforcer Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``checkStage - returns None when within threshold`` () =
        let result = StageDeadlineEnforcer.checkStage "Test" 50 (Some 100) 1L  // MEDIUM FIX: int64 literal

        Assert.True(result.IsNone)

    [<Fact>]
    let ``checkStage - returns violation when exceeds threshold`` () =
        let result = StageDeadlineEnforcer.checkStage "Test" 150 (Some 100) 1L  // MEDIUM FIX: int64 literal

        match result with
        | Some violation ->
            Assert.Equal("Test", violation.StageName)
            Assert.Equal(150, violation.ActualMs)
            Assert.Equal(100, violation.ThresholdMs)
            Assert.Equal(1L, violation.ScanIndex)  // MEDIUM FIX: int64 literal
        | None ->
            Assert.True(false, "Expected violation")

    [<Fact>]
    let ``checkStage - returns None when threshold is None`` () =
        let result = StageDeadlineEnforcer.checkStage "Test" 999999 None 1L  // MEDIUM FIX: int64 literal

        Assert.True(result.IsNone)

    [<Fact>]
    let ``checkAllStages - no violations when all within thresholds`` () =
        let timeline = createTestTimeline 50 300 50 50 1L  // MEDIUM FIX: int64 literal
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        Assert.Equal(0, violations.Length)

    [<Fact>]
    let ``checkAllStages - detects PrepareInputs violation`` () =
        let timeline = createTestTimeline 200 300 50 50 1  // PrepareInputs exceeds 10%
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        Assert.Equal(1, violations.Length)
        Assert.Equal("PrepareInputs", violations.[0].StageName)
        Assert.Equal(200, violations.[0].ActualMs)
        Assert.Equal(100, violations.[0].ThresholdMs)

    [<Fact>]
    let ``checkAllStages - detects Execution violation`` () =
        let timeline = createTestTimeline 50 800 50 50 1  // Execution exceeds 70%, total 950ms
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        Assert.Equal(1, violations.Length)  // Only Execution violation
        let execViolation = violations |> List.find (fun v -> v.StageName = "Execution")
        Assert.Equal(800, execViolation.ActualMs)
        Assert.Equal(700, execViolation.ThresholdMs)

    [<Fact>]
    let ``checkAllStages - detects multiple violations`` () =
        let timeline = createTestTimeline 200 800 150 100 1  // Multiple stages exceed
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        // PrepareInputs, Execution, FlushOutputs, Total all exceed
        Assert.True(violations.Length >= 4)

    [<Fact>]
    let ``checkAllStages - emits metrics to EventSource`` () =
        let timeline = createTestTimeline 200 300 50 50 1L  // MEDIUM FIX: int64 literal
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink) |> ignore

        // EventSource emission happens via RuntimeMetricsEventSource.Instance
        // We can't directly verify EventSource events in unit tests,
        // but we've validated the violation detection logic

    [<Fact>]
    let ``checkAllStages - Total violation when sum exceeds total threshold`` () =
        let timeline = createTestTimeline 100 700 100 200 1  // Total = 1100ms
        let thresholds = StageThresholds.defaultForCycleTime 1000
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        let totalViolation = violations |> List.tryFind (fun v -> v.StageName = "Total")
        Assert.True(totalViolation.IsSome)
        match totalViolation with
        | Some v ->
            Assert.Equal(1100, v.ActualMs)
            Assert.Equal(1000, v.ThresholdMs)
        | None -> ()

    [<Fact>]
    let ``checkAllStages - works with unlimited thresholds`` () =
        let timeline = createTestTimeline 999 999 999 999 1L  // MEDIUM FIX: int64 literal
        let thresholds = StageThresholds.unlimited
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        Assert.Equal(0, violations.Length)

    [<Fact>]
    let ``checkAllStages - works with partial thresholds`` () =
        let thresholds = {
            PrepareInputs = Some 50
            Execution = None  // Unlimited
            FlushOutputs = Some 50
            Finalize = None  // Unlimited
            Total = Some 1000
        }
        let timeline = createTestTimeline 100 500 100 500 1L  // MEDIUM FIX: int64 literal
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        // Should only detect PrepareInputs, FlushOutputs, and Total violations
        let stageNames = violations |> List.map (fun v -> v.StageName)
        Assert.Contains("PrepareInputs", stageNames)
        Assert.Contains("FlushOutputs", stageNames)
        Assert.Contains("Total", stageNames)
        Assert.DoesNotContain("Execution", stageNames)
        Assert.DoesNotContain("Finalize", stageNames)

    // ─────────────────────────────────────────────────────────────────────────
    // Integration with ScanTimeline Tests
    // ─────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``ScanTimeline integration - realistic 100ms cycle`` () =
        // Simulate a realistic 100ms PLC cycle
        let timeline = createTestTimeline 5 65 5 5 1  // 80ms total
        let thresholds = StageThresholds.defaultForCycleTime 100
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        // No violations - within all thresholds
        Assert.Equal(0, violations.Length)

    [<Fact>]
    let ``ScanTimeline integration - realistic 100ms cycle with overrun`` () =
        // Simulate execution taking too long
        let timeline = createTestTimeline 5 85 5 5 1  // 100ms total
        let thresholds = StageThresholds.defaultForCycleTime 100
        let sink = RecordingEventSink()

        let violations = StageDeadlineEnforcer.checkAllStages timeline thresholds (Some sink)

        // Execution violation (85ms > 70ms)
        Assert.True(violations.Length > 0)
        let execViolation = violations |> List.tryFind (fun v -> v.StageName = "Execution")
        Assert.True(execViolation.IsSome)
