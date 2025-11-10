namespace ProtocolTestHelper

open System.Diagnostics

/// Timing helpers for wrapping test actions.
[<AutoOpen>]
module TestTiming =

    /// Measures execution time of an action and returns the result plus elapsed TimeSpan.
    let measure (action: unit -> 'T) =
        let stopwatch = Stopwatch.StartNew()
        let result = action()
        stopwatch.Stop()
        result, stopwatch.Elapsed

    /// Measures execution time of an action and returns the result plus elapsed milliseconds.
    let measureMilliseconds (action: unit -> 'T) =
        let stopwatch = Stopwatch.StartNew()
        let result = action()
        stopwatch.Stop()
        result, stopwatch.ElapsedMilliseconds
