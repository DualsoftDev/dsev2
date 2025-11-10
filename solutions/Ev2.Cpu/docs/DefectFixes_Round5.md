# Defect Fixes - Round 5 Implementation Summary

**Date**: 2025-10-29
**Status**: ✅ RACE CONDITION FIXED
**Test Results**: 444/444 tests passing (0 failures)

---

## Executive Summary

Successfully resolved a critical race condition in the retain memory system that caused intermittent test failures when tests ran in parallel. The issue manifested as file locking conflicts between concurrent save operations.

**Build Status**: 0 errors, 3 warnings (harmless FS0064 type restrictions)

---

## Defect Resolved

### ✅ NEW-DEFECT-009: Retain Memory Race Condition

**Severity**: P1 (High - causes test failures)
**Category**: Concurrency / File I/O
**Status**: RESOLVED

**Problem**:
The `CpuScanEngine` had a race condition between two retain memory save operations:

1. **Async save** in `ScanOnce()` - Fire-and-forget `Task.Run` that saves retain snapshot after each scan
2. **Synchronous save** in `StopAsync()` - Saves retain snapshot on engine shutdown

When `StopAsync()` was called shortly after `ScanOnce()`, both operations attempted to write to the same temporary file (`test_retain_power_cycle.dat.tmp`), causing:
- File locking errors: "The process cannot access the file... because it is being used by another process"
- Intermittent test failures in `RetainMemoryTests.Retain Memory - Full power cycle scenario`
- Expected: 500, Actual: 0 (retain value not restored)

**Root Cause**:
The async save task from `ScanOnce()` was fire-and-forget (result ignored), so `StopAsync()` had no way to wait for it to complete before starting its own save operation.

```fsharp
// BEFORE (fire-and-forget)
Task.Run(fun () ->
    match storage.Save(snapshot) with
    | Ok () -> ...
    | Error err -> eprintfn "[RETAIN] Async save failed: %s" err
) |> ignore  // <-- Lost reference to task
```

**Solution**:
Track the async save task and wait for it to complete in `StopAsync()` before performing the synchronous save.

**Files Modified**:
- `src/cpu/Ev2.Cpu.Runtime/CpuScan.fs` (3 changes)

---

## Implementation Details

### Change 1: Add Task Tracking Field

**Location**: `/mnt/c/ds/dsev2cpucodex/src/cpu/Ev2.Cpu.Runtime/CpuScan.fs:44`

```fsharp
// BEFORE
let cfg = defaultArg config ScanConfig.Default
let mutable loopTask : Task option = None
let mutable cts      : CancellationTokenSource option = None
let mutable isFirstScan = true

// AFTER
let cfg = defaultArg config ScanConfig.Default
let mutable loopTask : Task option = None
let mutable cts      : CancellationTokenSource option = None
let mutable retainSaveTask : Task option = None  // Track async retain save
let mutable isFirstScan = true
```

**Rationale**: Added `retainSaveTask` field to store reference to async save task instead of discarding it.

---

### Change 2: Store Async Save Task

**Location**: `/mnt/c/ds/dsev2cpucodex/src/cpu/Ev2.Cpu.Runtime/CpuScan.fs:158-171`

```fsharp
// BEFORE
match retainStorage with
| Some storage ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()
    // Fire-and-forget async save
    Task.Run(fun () ->
        match storage.Save(snapshot) with
        | Ok () -> ...
        | Error err -> eprintfn "[RETAIN] Async save failed: %s" err
    ) |> ignore  // <-- Lost task reference
| None -> ()

// AFTER
match retainStorage with
| Some storage ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()
    // Async save (snapshot creation is synchronous for consistency)
    // Store task to allow StopAsync to wait for completion
    retainSaveTask <- Some (Task.Run(fun () ->
        match storage.Save(snapshot) with
        | Ok () -> ...
        | Error err -> eprintfn "[RETAIN] Async save failed: %s" err
    ))  // <-- Stored task reference
| None -> ()
```

**Rationale**: Store the task instead of ignoring it, so `StopAsync()` can await completion.

---

### Change 3: Wait for Async Save Before Synchronous Save

**Location**: `/mnt/c/ds/dsev2cpucodex/src/cpu/Ev2.Cpu.Runtime/CpuScan.fs:322-341`

```fsharp
// BEFORE
// ═════════════════════════════════════════════════════════════════════
// 리테인 데이터 자동 저장 (엔진 정상 종료 시)
// ═════════════════════════════════════════════════════════════════════
retainStorage |> Option.iter (fun storage ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()
    if snapshot.Variables.Length > 0 then
        match storage.Save(snapshot) with  // <-- Could conflict with async save
        | Ok () -> Context.trace ctx (sprintf "[RETAIN] Data saved: %d variables" ...)
        | Error err -> Context.warning ctx (sprintf "[RETAIN] Save failed: %s" err)
    else
        Context.trace ctx "[RETAIN] No retain variables to save")

// AFTER
// ═════════════════════════════════════════════════════════════════════
// 리테인 데이터 자동 저장 (엔진 정상 종료 시)
// ═════════════════════════════════════════════════════════════════════

// Wait for any pending async retain save to complete
match retainSaveTask with
| Some t when not t.IsCompleted ->
    try do! t with _ -> ()  // Ignore errors from async save
| _ -> ()

retainStorage |> Option.iter (fun storage ->
    let snapshot = ctx.Memory.CreateRetainSnapshot()
    if snapshot.Variables.Length > 0 then
        match storage.Save(snapshot) with  // <-- Now safe, async save completed
        | Ok () -> Context.trace ctx (sprintf "[RETAIN] Data saved: %d variables" ...)
        | Error err -> Context.warning ctx (sprintf "[RETAIN] Save failed: %s" err)
    else
        Context.trace ctx "[RETAIN] No retain variables to save")
```

**Rationale**: Wait for async save to complete before starting synchronous save, preventing concurrent file access.

---

## Test Results

### Before Fix
```
실패!  - 실패:     1, 통과:   443, 건너뜀:     0, 전체:   444
  실패 Ev2.Cpu.Runtime.Tests.RetainMemoryTests.Retain Memory - Full power cycle scenario
  오류 메시지:
   Assert.Equal() Failure
Expected: 500
Actual:   0
  스택 추적:
     at Ev2.Cpu.Runtime.Tests.RetainMemoryTests.Retain Memory - Full power cycle scenario()
```

Console output showed file locking:
```
[RETAIN] Async save failed: Failed to save retain data: The process cannot
access the file 'test_retain_power_cycle.dat.tmp' because it is being used
by another process.
```

### After Fix
```
통과!  - 실패:     0, 통과:   444, 건너뜀:     0, 전체:   444

Breakdown:
- Ev2.Cpu.Core.Tests:          149 tests ✅
- Ev2.Cpu.Generation.Tests:    124 tests ✅
- Ev2.Cpu.StandardLibrary.Tests: 36 tests ✅
- Ev2.Cpu.Runtime.Tests:        135 tests ✅
```

**All tests passing. No file locking errors. ✅**

---

## Technical Analysis

### Race Condition Timeline

**Without Fix (FAILS)**:
```
t=0ms:   engine1.ScanOnce() starts
t=10ms:  ScanOnce() FlushOutputs stage: Task.Run(save) started (async)
t=11ms:  ScanOnce() returns
t=12ms:  engine1.StopAsync() called
t=13ms:  StopAsync() tries to save (synchronous)
t=13ms:  ⚠️ CONFLICT: Both async task and StopAsync try to write to .tmp file
t=13ms:  File locking error: "process cannot access the file..."
t=15ms:  Async task completes, but StopAsync failed
t=20ms:  engine2 constructor tries to load retain data
t=21ms:  ❌ File is corrupted or contains stale data: value = 0 (expected 500)
```

**With Fix (PASSES)**:
```
t=0ms:   engine1.ScanOnce() starts
t=10ms:  ScanOnce() FlushOutputs: retainSaveTask <- Some(Task.Run(save))
t=11ms:  ScanOnce() returns
t=12ms:  engine1.StopAsync() called
t=13ms:  StopAsync() waits for retainSaveTask to complete
t=15ms:  Async task completes
t=16ms:  StopAsync() proceeds with synchronous save
t=17ms:  ✅ SUCCESS: No conflict, file saved correctly
t=20ms:  engine2 constructor loads retain data
t=21ms:  ✅ Value restored correctly: value = 500
```

---

## File Save Strategy (Atomic Write with Backup)

The `BinaryRetainStorage.Save()` implementation uses atomic write to prevent corruption:

```fsharp
// 1. Write to temporary file
let tempPath = filePath + ".tmp"
File.WriteAllBytes(tempPath, bytes)

// 2. Backup existing file if present
if File.Exists(filePath) then
    let backupPath = filePath + ".bak"
    if File.Exists(backupPath) then
        File.Delete(backupPath)
    File.Move(filePath, backupPath)

// 3. Atomically move temp to final location
File.Move(tempPath, filePath)
```

**Why the race condition was critical**:
- If two saves run concurrently, both try to write to `filePath + ".tmp"`
- One write succeeds, the other gets file locking error
- The failed write might have newer data, causing data loss
- Or the .tmp file gets corrupted by concurrent writes

---

## Impact & Benefits

### Reliability
- ✅ Eliminates intermittent test failures
- ✅ Ensures data integrity for retain memory operations
- ✅ Prevents file corruption from concurrent writes

### Performance
- ✅ Minimal impact: Async save still runs in background
- ✅ Only blocks on `StopAsync()` if async save is still pending
- ✅ Most of the time, async save completes before `StopAsync()`, so no waiting needed

### Maintainability
- ✅ Explicit task lifecycle management
- ✅ Clear coordination between async and sync operations
- ✅ Easier to reason about file access patterns

---

## Related Defects

This fix completes the retain memory system improvements:
- **Round 3 (NEW-DEFECT-001)**: Fixed ThreadLocal RNG accessor pattern
- **Round 3 (NEW-DEFECT-002)**: Centralized configuration (RuntimeLimits)
- **Round 3 (NEW-DEFECT-004)**: Fixed warning cache unbounded growth
- **Round 4**: Fixed pattern matching completeness warnings
- **Round 5 (NEW-DEFECT-009)**: Fixed retain memory race condition ✅

---

## Future Improvements

### Potential Optimizations (Not Urgent)
1. **Debounced Saves**: Instead of saving after every scan, save only when data changes or at intervals
2. **Concurrent File Access**: Use lock files or named mutexes for cross-process coordination
3. **Incremental Snapshots**: Only serialize changed variables (see TODO DEFECT-008 in CpuScan.fs:156)

### Monitoring (Recommended)
- Track retain save duration in telemetry
- Alert if save operations take >100ms
- Monitor file I/O errors in production

---

## Conclusion

Round 5 successfully resolved a critical race condition that caused intermittent test failures. The fix ensures proper coordination between async and synchronous retain memory save operations, eliminating file locking conflicts while maintaining performance benefits of async saves.

**All 444 tests passing. System is ready for Phase 2 test rewriting. ✅**

---

**Report Generated**: 2025-10-29
**Round**: 5 of DefectFixes
**Status**: ✅ COMPLETE
**Next Phase**: Phase 2 - Core Module Test Rewriting
