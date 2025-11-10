namespace Ev2.Cpu.Tests.Infrastructure

open System
open System.Collections.Generic
open Xunit

// ═══════════════════════════════════════════════════════════════════════
// Assertion Helpers Module - 상세한 에러 메시지를 제공하는 어서션
// ═══════════════════════════════════════════════════════════════════════
// Phase 1: 기반 인프라
// XUnit Assert를 보완하는 도메인 특화 어서션 메서드
// ═══════════════════════════════════════════════════════════════════════

module AssertionHelpers =

    // ───────────────────────────────────────────────────────────────────
    // Core Equality Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert equality with detailed failure message</summary>
    let assertEqual (expected: 'T) (actual: 'T) (context: string) =
        if not (expected = actual) then
            failwithf "Assertion failed: %s\nExpected: %A\nActual:   %A" context expected actual

    /// <summary>Assert inequality with detailed failure message</summary>
    let assertNotEqual (expected: 'T) (actual: 'T) (context: string) =
        if expected = actual then
            failwithf "Assertion failed: %s\nExpected values to be different, but both were: %A" context expected

    /// <summary>Assert approximate equality for floating point numbers</summary>
    let assertApproximatelyEqual (epsilon: float) (expected: float) (actual: float) (context: string) =
        let diff = abs (expected - actual)
        if diff >= epsilon then
            failwithf "Assertion failed: %s\nExpected: %f (±%f)\nActual:   %f\nDifference: %f"
                context expected epsilon actual diff

    /// <summary>Assert reference equality</summary>
    let assertSame (expected: obj) (actual: obj) (context: string) =
        if not (Object.ReferenceEquals(expected, actual)) then
            failwithf "Assertion failed: %s\nExpected objects to be the same reference\nExpected: %A\nActual:   %A"
                context expected actual

    /// <summary>Assert reference inequality</summary>
    let assertNotSame (expected: obj) (actual: obj) (context: string) =
        if Object.ReferenceEquals(expected, actual) then
            failwithf "Assertion failed: %s\nExpected objects to have different references, but they were the same" context

    // ───────────────────────────────────────────────────────────────────
    // Boolean Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert condition is true</summary>
    let assertTrue (condition: bool) (context: string) =
        if not condition then
            failwithf "Assertion failed: %s\nExpected: true\nActual:   false" context

    /// <summary>Assert condition is false</summary>
    let assertFalse (condition: bool) (context: string) =
        if condition then
            failwithf "Assertion failed: %s\nExpected: false\nActual:   true" context

    // ───────────────────────────────────────────────────────────────────
    // Null Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert value is null</summary>
    let assertNull (value: obj) (context: string) =
        if not (isNull value) then
            failwithf "Assertion failed: %s\nExpected: null\nActual:   %A" context value

    /// <summary>Assert value is not null</summary>
    let assertNotNull (value: obj) (context: string) =
        if isNull value then
            failwithf "Assertion failed: %s\nExpected: non-null value\nActual:   null" context

    // ───────────────────────────────────────────────────────────────────
    // Collection Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert collection is empty</summary>
    let assertEmpty (collection: 'T seq) (context: string) =
        let items = collection |> Seq.toList
        if not (List.isEmpty items) then
            failwithf "Assertion failed: %s\nExpected: empty collection\nActual:   %d items: %A"
                context (List.length items) items

    /// <summary>Assert collection is not empty</summary>
    let assertNotEmpty (collection: 'T seq) (context: string) =
        if Seq.isEmpty collection then
            failwithf "Assertion failed: %s\nExpected: non-empty collection\nActual:   empty collection" context

    /// <summary>Assert collection has specific count</summary>
    let assertCount (expectedCount: int) (collection: 'T seq) (context: string) =
        let actualCount = Seq.length collection
        if actualCount <> expectedCount then
            failwithf "Assertion failed: %s\nExpected count: %d\nActual count:   %d\nItems: %A"
                context expectedCount actualCount (collection |> Seq.toList)

    /// <summary>Assert collection contains item</summary>
    let assertContains (item: 'T) (collection: 'T seq) (context: string) =
        if not (Seq.contains item collection) then
            failwithf "Assertion failed: %s\nExpected collection to contain: %A\nActual collection: %A"
                context item (collection |> Seq.toList)

    /// <summary>Assert collection does not contain item</summary>
    let assertNotContains (item: 'T) (collection: 'T seq) (context: string) =
        if Seq.contains item collection then
            failwithf "Assertion failed: %s\nExpected collection NOT to contain: %A\nActual collection: %A"
                context item (collection |> Seq.toList)

    /// <summary>Assert all items in collection satisfy predicate</summary>
    let assertAll (predicate: 'T -> bool) (collection: 'T seq) (context: string) =
        let failures = collection |> Seq.filter (predicate >> not) |> Seq.toList
        if not (List.isEmpty failures) then
            failwithf "Assertion failed: %s\nExpected all items to satisfy condition\nFailing items: %A\nAll items: %A"
                context failures (collection |> Seq.toList)

    /// <summary>Assert any item in collection satisfies predicate</summary>
    let assertAny (predicate: 'T -> bool) (collection: 'T seq) (context: string) =
        if not (Seq.exists predicate collection) then
            failwithf "Assertion failed: %s\nExpected at least one item to satisfy condition\nActual items: %A"
                context (collection |> Seq.toList)

    /// <summary>Assert collections are equal (order matters)</summary>
    let assertSequenceEqual (expected: 'T seq) (actual: 'T seq) (context: string) =
        let expList = expected |> Seq.toList
        let actList = actual |> Seq.toList
        if expList <> actList then
            let expCount = List.length expList
            let actCount = List.length actList
            if expCount <> actCount then
                failwithf "Assertion failed: %s\nExpected count: %d\nActual count:   %d\nExpected: %A\nActual:   %A"
                    context expCount actCount expList actList
            else
                // Find first difference
                let firstDiff =
                    List.zip expList actList
                    |> List.tryFindIndex (fun (e, a) -> e <> a)
                match firstDiff with
                | Some idx ->
                    failwithf "Assertion failed: %s\nSequences differ at index %d\nExpected[%d]: %A\nActual[%d]:   %A\nExpected: %A\nActual:   %A"
                        context idx idx expList.[idx] idx actList.[idx] expList actList
                | None ->
                    failwithf "Assertion failed: %s\nExpected: %A\nActual:   %A" context expList actList

    /// <summary>Assert collections are equivalent (order doesn't matter)</summary>
    let assertEquivalent (expected: 'T seq) (actual: 'T seq) (context: string) =
        let expSorted = expected |> Seq.sort |> Seq.toList
        let actSorted = actual |> Seq.sort |> Seq.toList
        if expSorted <> actSorted then
            let expCount = List.length expSorted
            let actCount = List.length actSorted
            failwithf "Assertion failed: %s\nExpected count: %d\nActual count:   %d\nExpected items: %A\nActual items:   %A"
                context expCount actCount expSorted actSorted

    /// <summary>Assert collection is subset of another</summary>
    let assertSubset (subset: 'T seq) (superset: 'T seq) (context: string) =
        let supersetSet = Set.ofSeq superset
        let missing = subset |> Seq.filter (fun x -> not (Set.contains x supersetSet)) |> Seq.toList
        if not (List.isEmpty missing) then
            failwithf "Assertion failed: %s\nExpected subset to be contained in superset\nMissing items: %A\nSubset:   %A\nSuperset: %A"
                context missing (subset |> Seq.toList) (superset |> Seq.toList)

    // ───────────────────────────────────────────────────────────────────
    // String Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert string contains substring</summary>
    let assertStringContains (substring: string) (actual: string) (context: string) =
        if isNull actual then
            failwithf "Assertion failed: %s\nExpected string to contain: '%s'\nActual: null" context substring
        if not (actual.Contains(substring)) then
            failwithf "Assertion failed: %s\nExpected string to contain: '%s'\nActual string: '%s'"
                context substring actual

    /// <summary>Assert string does not contain substring</summary>
    let assertStringNotContains (substring: string) (actual: string) (context: string) =
        if not (isNull actual) && actual.Contains(substring) then
            failwithf "Assertion failed: %s\nExpected string NOT to contain: '%s'\nActual string: '%s'"
                context substring actual

    /// <summary>Assert string starts with prefix</summary>
    let assertStringStartsWith (prefix: string) (actual: string) (context: string) =
        if isNull actual then
            failwithf "Assertion failed: %s\nExpected string to start with: '%s'\nActual: null" context prefix
        if not (actual.StartsWith(prefix)) then
            failwithf "Assertion failed: %s\nExpected string to start with: '%s'\nActual string: '%s'"
                context prefix actual

    /// <summary>Assert string ends with suffix</summary>
    let assertStringEndsWith (suffix: string) (actual: string) (context: string) =
        if isNull actual then
            failwithf "Assertion failed: %s\nExpected string to end with: '%s'\nActual: null" context suffix
        if not (actual.EndsWith(suffix)) then
            failwithf "Assertion failed: %s\nExpected string to end with: '%s'\nActual string: '%s'"
                context suffix actual

    /// <summary>Assert string matches regex pattern</summary>
    let assertStringMatches (pattern: string) (actual: string) (context: string) =
        if isNull actual then
            failwithf "Assertion failed: %s\nExpected string to match pattern: '%s'\nActual: null" context pattern
        let regex = Text.RegularExpressions.Regex(pattern)
        if not (regex.IsMatch(actual)) then
            failwithf "Assertion failed: %s\nExpected string to match pattern: '%s'\nActual string: '%s'"
                context pattern actual

    /// <summary>Assert string is null or empty</summary>
    let assertStringNullOrEmpty (actual: string) (context: string) =
        if not (String.IsNullOrEmpty(actual)) then
            failwithf "Assertion failed: %s\nExpected: null or empty string\nActual: '%s'" context actual

    /// <summary>Assert string is not null or empty</summary>
    let assertStringNotNullOrEmpty (actual: string) (context: string) =
        if String.IsNullOrEmpty(actual) then
            failwithf "Assertion failed: %s\nExpected: non-empty string\nActual: '%s'"
                context (if isNull actual then "null" else "empty")

    // ───────────────────────────────────────────────────────────────────
    // Numeric Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert value is in range (inclusive)</summary>
    let assertInRange (min: 'T) (max: 'T) (actual: 'T) (context: string) =
        if actual < min || actual > max then
            failwithf "Assertion failed: %s\nExpected value in range [%A, %A]\nActual: %A"
                context min max actual

    /// <summary>Assert value is greater than</summary>
    let assertGreaterThan (threshold: 'T) (actual: 'T) (context: string) =
        if actual <= threshold then
            failwithf "Assertion failed: %s\nExpected: > %A\nActual:   %A" context threshold actual

    /// <summary>Assert value is greater than or equal</summary>
    let assertGreaterThanOrEqual (threshold: 'T) (actual: 'T) (context: string) =
        if actual < threshold then
            failwithf "Assertion failed: %s\nExpected: >= %A\nActual:   %A" context threshold actual

    /// <summary>Assert value is less than</summary>
    let assertLessThan (threshold: 'T) (actual: 'T) (context: string) =
        if actual >= threshold then
            failwithf "Assertion failed: %s\nExpected: < %A\nActual:   %A" context threshold actual

    /// <summary>Assert value is less than or equal</summary>
    let assertLessThanOrEqual (threshold: 'T) (actual: 'T) (context: string) =
        if actual > threshold then
            failwithf "Assertion failed: %s\nExpected: <= %A\nActual:   %A" context threshold actual

    /// <summary>Assert value is positive</summary>
    let assertPositive (actual: int) (context: string) =
        if actual <= 0 then
            failwithf "Assertion failed: %s\nExpected: positive value\nActual: %d" context actual

    /// <summary>Assert value is negative</summary>
    let assertNegative (actual: int) (context: string) =
        if actual >= 0 then
            failwithf "Assertion failed: %s\nExpected: negative value\nActual: %d" context actual

    /// <summary>Assert value is zero</summary>
    let assertZero (actual: int) (context: string) =
        if actual <> 0 then
            failwithf "Assertion failed: %s\nExpected: 0\nActual: %d" context actual

    /// <summary>Assert double is NaN</summary>
    let assertNaN (actual: float) (context: string) =
        if not (Double.IsNaN(actual)) then
            failwithf "Assertion failed: %s\nExpected: NaN\nActual: %f" context actual

    /// <summary>Assert double is not NaN</summary>
    let assertNotNaN (actual: float) (context: string) =
        if Double.IsNaN(actual) then
            failwithf "Assertion failed: %s\nExpected: non-NaN value\nActual: NaN" context

    /// <summary>Assert double is infinite</summary>
    let assertInfinite (actual: float) (context: string) =
        if not (Double.IsInfinity(actual)) then
            failwithf "Assertion failed: %s\nExpected: ±Infinity\nActual: %f" context actual

    /// <summary>Assert double is finite</summary>
    let assertFinite (actual: float) (context: string) =
        if not (Double.IsFinite(actual)) then
            let value = if Double.IsNaN(actual) then "NaN" else "Infinity"
            failwithf "Assertion failed: %s\nExpected: finite value\nActual: %s" context value

    // ───────────────────────────────────────────────────────────────────
    // Exception Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert action throws specific exception type</summary>
    let assertThrows<'TException when 'TException :> exn> (action: unit -> obj) (context: string) : 'TException =
        try
            action() |> ignore
            failwithf "Assertion failed: %s\nExpected exception: %s\nActual: No exception was thrown"
                context typeof<'TException>.Name
        with
        | :? 'TException as ex -> ex
        | ex ->
            failwithf "Assertion failed: %s\nExpected exception: %s\nActual exception: %s - %s"
                context typeof<'TException>.Name (ex.GetType().Name) ex.Message

    /// <summary>Assert action throws exception with specific message</summary>
    let assertThrowsWithMessage<'TException when 'TException :> exn>
        (expectedMessage: string) (action: unit -> obj) (context: string) : 'TException =
        try
            action() |> ignore
            failwithf "Assertion failed: %s\nExpected exception: %s with message '%s'\nActual: No exception was thrown"
                context typeof<'TException>.Name expectedMessage
        with
        | :? 'TException as ex ->
            if not (ex.Message.Contains(expectedMessage)) then
                failwithf "Assertion failed: %s\nExpected message to contain: '%s'\nActual message: '%s'"
                    context expectedMessage ex.Message
            ex
        | ex ->
            failwithf "Assertion failed: %s\nExpected exception: %s\nActual exception: %s - %s"
                context typeof<'TException>.Name (ex.GetType().Name) ex.Message

    /// <summary>Assert action does not throw any exception</summary>
    let assertNoThrow (action: unit -> 'T) (context: string) : 'T =
        try
            action()
        with ex ->
            failwithf "Assertion failed: %s\nExpected: No exception\nActual exception: %s - %s\nStack trace:\n%s"
                context (ex.GetType().Name) ex.Message ex.StackTrace

    // ───────────────────────────────────────────────────────────────────
    // Type Assertions
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert value is of specific type</summary>
    let assertIsType<'T> (value: obj) (context: string) =
        if isNull value then
            failwithf "Assertion failed: %s\nExpected type: %s\nActual: null"
                context typeof<'T>.Name
        let actualType = value.GetType()
        if actualType <> typeof<'T> then
            failwithf "Assertion failed: %s\nExpected type: %s\nActual type: %s\nValue: %A"
                context typeof<'T>.Name actualType.Name value

    /// <summary>Assert value is assignable to type</summary>
    let assertIsAssignableTo<'T> (value: obj) (context: string) =
        if not (isNull value) then
            let actualType = value.GetType()
            if not (typeof<'T>.IsAssignableFrom(actualType)) then
                failwithf "Assertion failed: %s\nExpected type assignable to: %s\nActual type: %s\nValue: %A"
                    context typeof<'T>.Name actualType.Name value

    // ───────────────────────────────────────────────────────────────────
    // Result/Option Assertions (F# specific)
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Assert Result is Ok with specific value</summary>
    let assertOk (expected: 'T) (result: Result<'T, 'E>) (context: string) =
        match result with
        | Ok actual ->
            if actual <> expected then
                failwithf "Assertion failed: %s\nExpected Ok: %A\nActual Ok: %A"
                    context expected actual
        | Error err ->
            failwithf "Assertion failed: %s\nExpected: Ok %A\nActual: Error %A"
                context expected err

    /// <summary>Assert Result is Ok (any value)</summary>
    let assertIsOk (result: Result<'T, 'E>) (context: string) : 'T =
        match result with
        | Ok value -> value
        | Error err ->
            failwithf "Assertion failed: %s\nExpected: Ok\nActual: Error %A" context err

    /// <summary>Assert Result is Error with specific error</summary>
    let assertError (expected: 'E) (result: Result<'T, 'E>) (context: string) =
        match result with
        | Ok value ->
            failwithf "Assertion failed: %s\nExpected: Error %A\nActual: Ok %A"
                context expected value
        | Error actual ->
            if actual <> expected then
                failwithf "Assertion failed: %s\nExpected Error: %A\nActual Error: %A"
                    context expected actual

    /// <summary>Assert Result is Error (any error)</summary>
    let assertIsError (result: Result<'T, 'E>) (context: string) : 'E =
        match result with
        | Ok value ->
            failwithf "Assertion failed: %s\nExpected: Error\nActual: Ok %A" context value
        | Error err -> err

    /// <summary>Assert Option is Some with specific value</summary>
    let assertSome (expected: 'T) (option: 'T option) (context: string) =
        match option with
        | Some actual ->
            if actual <> expected then
                failwithf "Assertion failed: %s\nExpected Some: %A\nActual Some: %A"
                    context expected actual
        | None ->
            failwithf "Assertion failed: %s\nExpected: Some %A\nActual: None" context expected

    /// <summary>Assert Option is Some (any value)</summary>
    let assertIsSome (option: 'T option) (context: string) : 'T =
        match option with
        | Some value -> value
        | None ->
            failwithf "Assertion failed: %s\nExpected: Some\nActual: None" context

    /// <summary>Assert Option is None</summary>
    let assertNone (option: 'T option) (context: string) =
        match option with
        | Some value ->
            failwithf "Assertion failed: %s\nExpected: None\nActual: Some %A" context value
        | None -> ()

    // ───────────────────────────────────────────────────────────────────
    // Combinators
    // ───────────────────────────────────────────────────────────────────

    /// <summary>Execute multiple assertions, collecting all failures</summary>
    let assertMultiple (assertions: (unit -> unit) list) (context: string) =
        let failures =
            assertions
            |> List.mapi (fun i assertion ->
                try
                    assertion()
                    None
                with ex ->
                    Some (i, ex.Message))
            |> List.choose id

        if not (List.isEmpty failures) then
            let failureMessages =
                failures
                |> List.map (fun (i, msg) -> sprintf "  [%d] %s" i msg)
                |> String.concat "\n"
            failwithf "Assertion failed: %s\n%d of %d assertions failed:\n%s"
                context (List.length failures) (List.length assertions) failureMessages
