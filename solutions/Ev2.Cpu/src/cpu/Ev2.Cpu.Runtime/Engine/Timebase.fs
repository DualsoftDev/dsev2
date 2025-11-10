namespace Ev2.Cpu.Runtime

open System
open System.Diagnostics

// ═════════════════════════════════════════════════════════════════════
// Time Provider Abstraction (GAP-007: ITimeProvider)
// ═════════════════════════════════════════════════════════════════════

/// <summary>Time provider abstraction for testability and simulation</summary>
/// <remarks>
/// Enables:
/// - Mock time for deterministic testing
/// - Time scaling for simulation (fast-forward, slow-motion)
/// - Monotonic clock guarantee
/// GAP-007 fix: No more direct DateTime.UtcNow or Stopwatch.GetTimestamp() calls
/// </remarks>
type ITimeProvider =
    /// <summary>현재 단조 증가 타임스탬프 (Stopwatch ticks)</summary>
    /// <returns>Monotonic timestamp in Stopwatch ticks</returns>
    abstract member GetTimestamp: unit -> int64

    /// <summary>현재 UTC 시각</summary>
    /// <returns>Current UTC DateTime</returns>
    abstract member UtcNow: DateTime

/// <summary>System time provider using Stopwatch (default)</summary>
type SystemTimeProvider() =
    interface ITimeProvider with
        member _.GetTimestamp() = Stopwatch.GetTimestamp()
        member _.UtcNow = System.DateTime.UtcNow

// ═════════════════════════════════════════════════════════════════════
// Legacy Timebase Module (kept for backward compatibility)
// ═════════════════════════════════════════════════════════════════════

/// 고해상도 단조 증가 시계 유틸리티
module Timebase =
    let private frequency = float Stopwatch.Frequency
    let private ticksPerMillisecond = frequency / 1000.0

    /// 현재 단조 증가 타임스탬프 (Stopwatch ticks)
    /// DEPRECATED: Use ctx.TimeProvider.GetTimestamp() instead (GAP-007)
    let nowTicks() = Stopwatch.GetTimestamp()

    /// ms 값을 ticks로 변환
    let millisecondsToTicks (ms: int) : int64 =
        if ms <= 0 then 0L
        else int64 (float ms * ticksPerMillisecond)

    /// ticks 차이를 ms로 변환 (Int32 범위로 포화)
    let elapsedMilliseconds (startTicks: int64) (endTicks: int64) =
        let delta = endTicks - startTicks
        if delta <= 0L then 0
        else
            let ms = float delta * 1000.0 / frequency
            if ms >= float System.Int32.MaxValue then System.Int32.MaxValue
            else int (ms + 0.5)

    /// ticks에 ms를 더한 새로운 ticks 반환
    let addMilliseconds (ticks: int64) (ms: int) : int64 =
        ticks + millisecondsToTicks ms
