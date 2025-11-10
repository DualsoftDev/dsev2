namespace Ev2.Cpu.Tests.Infrastructure

open System
open System.Collections.Generic
open Xunit

// ═══════════════════════════════════════════════════════════════════════
// Test Helpers Module - 공통 테스트 유틸리티
// ═══════════════════════════════════════════════════════════════════════
// Phase 1: 기반 인프라
// 모든 테스트에서 사용할 수 있는 공통 헬퍼 함수들
// ═══════════════════════════════════════════════════════════════════════

module TestHelpers =

    // ───────────────────────────────────────────────────────────────────
    // Test Execution Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Execute action and ensure it throws specific exception</summary>
    /// <param name="action">Action that should throw</param>
    /// <returns>The thrown exception</returns>
    let shouldThrow<'TException when 'TException :> exn> (action: unit -> obj) : 'TException =
        try
            action() |> ignore
            failwithf "Expected exception %s but nothing was thrown" typeof<'TException>.Name
        with
        | :? 'TException as ex -> ex
        | ex -> failwithf "Expected exception %s but got %s: %s"
                    typeof<'TException>.Name (ex.GetType().Name) ex.Message

    /// <summary>Execute action and ensure it does NOT throw</summary>
    /// <param name="action">Action that should succeed</param>
    /// <returns>Result of action</returns>
    let shouldNotThrow (action: unit -> 'a) : 'a =
        try
            action()
        with
        | ex -> failwithf "Expected no exception but got %s: %s" (ex.GetType().Name) ex.Message

    /// <summary>Execute action multiple times</summary>
    /// <param name="count">Number of times to execute</param>
    /// <param name="action">Action to execute</param>
    let repeat count (action: int -> unit) =
        for i = 0 to count - 1 do
            action i

    /// <summary>Execute action and measure time</summary>
    /// <param name="action">Action to measure</param>
    /// <returns>Tuple of (result, elapsed milliseconds)</returns>
    let measureTime (action: unit -> 'a) : 'a * float =
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let result = action()
        sw.Stop()
        (result, sw.Elapsed.TotalMilliseconds)

    // ───────────────────────────────────────────────────────────────────
    // Collection Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Check if two sequences are equal (order matters)</summary>
    let seqEqual (expected: 'a seq) (actual: 'a seq) =
        Seq.compareWith Unchecked.compare expected actual = 0

    /// <summary>Check if two sequences contain same elements (order doesn't matter)</summary>
    let seqEquivalent (expected: 'a seq) (actual: 'a seq) =
        let exp = expected |> Seq.sort |> Seq.toList
        let act = actual |> Seq.sort |> Seq.toList
        exp = act

    /// <summary>Generate all pairs from a sequence</summary>
    let allPairs (items: 'a seq) =
        seq {
            let arr = items |> Seq.toArray
            for i = 0 to arr.Length - 1 do
                for j = i + 1 to arr.Length - 1 do
                    yield (arr.[i], arr.[j])
        }

    /// <summary>Generate cartesian product of two sequences</summary>
    let cartesian (seq1: 'a seq) (seq2: 'b seq) =
        seq {
            for a in seq1 do
                for b in seq2 do
                    yield (a, b)
        }

    // ───────────────────────────────────────────────────────────────────
    // String Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Check if string contains substring (case insensitive)</summary>
    let containsIgnoreCase (substring: string) (str: string) =
        not (isNull str) && str.Contains(substring, StringComparison.OrdinalIgnoreCase)

    /// <summary>Generate random string of given length</summary>
    let randomString (length: int) =
        let chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
        let random = Random()
        String(Array.init length (fun _ -> chars.[random.Next(chars.Length)]))

    /// <summary>Generate very long string (for stress testing)</summary>
    let veryLongString (sizeInMB: float) =
        let bytesPerChar = 2 // UTF-16
        let totalChars = int (sizeInMB * 1024.0 * 1024.0 / float bytesPerChar)
        String('x', totalChars)

    // ───────────────────────────────────────────────────────────────────
    // Numeric Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Check if two floats are approximately equal</summary>
    /// <param name="epsilon">Maximum allowed difference</param>
    let approximatelyEqual (epsilon: float) (expected: float) (actual: float) =
        abs (expected - actual) < epsilon

    /// <summary>Check if value is within range (inclusive)</summary>
    let inRange (min: 'a) (max: 'a) (value: 'a) =
        value >= min && value <= max

    /// <summary>Generate random int in range</summary>
    let randomInt min max =
        let random = Random()
        random.Next(min, max + 1)

    /// <summary>Generate random double in range</summary>
    let randomDouble min max =
        let random = Random()
        min + (random.NextDouble() * (max - min))

    // ───────────────────────────────────────────────────────────────────
    // File System Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Create temporary directory for test</summary>
    /// <returns>Path to temporary directory</returns>
    let createTempDir() =
        let path = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString())
        IO.Directory.CreateDirectory(path) |> ignore
        path

    /// <summary>Delete directory and all contents</summary>
    let deleteDirRecursive path =
        if IO.Directory.Exists(path) then
            IO.Directory.Delete(path, true)

    /// <summary>Execute action with temporary directory that is cleaned up after</summary>
    /// <param name="action">Action to execute with temp dir path</param>
    let withTempDir (action: string -> 'a) =
        let tempDir = createTempDir()
        try
            action tempDir
        finally
            try deleteDirRecursive tempDir with _ -> ()

    /// <summary>Create temporary file for test</summary>
    /// <returns>Path to temporary file</returns>
    let createTempFile() =
        IO.Path.GetTempFileName()

    /// <summary>Execute action with temporary file that is cleaned up after</summary>
    /// <param name="action">Action to execute with temp file path</param>
    let withTempFile (action: string -> 'a) =
        let tempFile = createTempFile()
        try
            action tempFile
        finally
            try IO.File.Delete(tempFile) with _ -> ()

    // ───────────────────────────────────────────────────────────────────
    // Concurrency Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Execute action on multiple threads concurrently</summary>
    /// <param name="threadCount">Number of threads to spawn</param>
    /// <param name="action">Action to execute (receives thread index)</param>
    let runConcurrently (threadCount: int) (action: int -> unit) =
        let threads = Array.init threadCount (fun i ->
            let thread = System.Threading.Thread(fun () -> action i)
            thread)

        // Start all threads
        threads |> Array.iter (fun t -> t.Start())

        // Wait for all to complete
        threads |> Array.iter (fun t -> t.Join())

    /// <summary>Execute action repeatedly on multiple threads</summary>
    /// <param name="threadCount">Number of threads</param>
    /// <param name="iterationsPerThread">Iterations per thread</param>
    /// <param name="action">Action to execute</param>
    let stressTest (threadCount: int) (iterationsPerThread: int) (action: int -> int -> unit) =
        runConcurrently threadCount (fun threadId ->
            for iteration = 0 to iterationsPerThread - 1 do
                action threadId iteration)

    /// <summary>Check for race conditions by running action many times</summary>
    /// <param name="action">Action to test</param>
    /// <returns>True if no exceptions were thrown</returns>
    let detectRaceCondition (action: unit -> unit) =
        let mutable failed = false
        let exceptions = System.Collections.Concurrent.ConcurrentBag<exn>()

        stressTest 10 1000 (fun _ _ ->
            try
                action()
            with ex ->
                failed <- true
                exceptions.Add(ex))

        if failed then
            let allExceptions = exceptions |> Seq.toList
            failwithf "Race condition detected. Exceptions: %A" allExceptions

    // ───────────────────────────────────────────────────────────────────
    // Retry Helpers (for flaky tests)
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Retry action until it succeeds or max attempts reached</summary>
    /// <param name="maxAttempts">Maximum number of attempts</param>
    /// <param name="delayMs">Delay between attempts in milliseconds</param>
    /// <param name="action">Action to retry</param>
    let retry (maxAttempts: int) (delayMs: int) (action: unit -> 'a) =
        let rec tryAction attempt =
            try
                action()
            with ex when attempt < maxAttempts ->
                System.Threading.Thread.Sleep(delayMs)
                tryAction (attempt + 1)
        tryAction 1

    // ───────────────────────────────────────────────────────────────────
    // Debugging Helpers
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Print value to console (for debugging)</summary>
    let debug value =
        printfn "DEBUG: %A" value
        value

    /// <summary>Print value with label to console</summary>
    let debugWith label value =
        printfn "DEBUG [%s]: %A" label value
        value

    /// <summary>Execute action only if condition is true</summary>
    let when' condition action =
        if condition then action()

    /// <summary>Dump object properties to string</summary>
    let dump (obj: 'a) =
        sprintf "%A" obj
