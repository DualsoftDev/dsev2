namespace ProtocolTestHelper

open System
open System.Collections.Generic
open Xunit
open ProtocolTestHelper.TestLogging

/// Common assertion helpers that can be shared across all protocols
module AssertionHelpers =
    
    /// Assert that a value is within a specified range
    let assertInRange<'T when 'T : comparison> (min: 'T) (max: 'T) (actual: 'T) (message: string option) =
        if actual < min || actual > max then
            let msg = defaultArg message $"Expected {actual} to be between {min} and {max}"
            Assert.True(false, msg)
    
    /// Assert that two floating point values are approximately equal within tolerance
    let assertApproxEqual (tolerance: float) (expected: float) (actual: float) (message: string option) =
        let diff = abs(expected - actual)
        if diff > tolerance then
            let msg = defaultArg message $"Expected {expected} +/- {tolerance}, got {actual} (diff: {diff})"
            Assert.True(false, msg)
    
    /// Assert that a condition is true with custom message
    let assertTrue (condition: bool) (message: string) =
        if not condition then
            Assert.True(false, message)
    
    /// Assert that a condition is false with custom message  
    let assertFalse (condition: bool) (message: string) =
        if condition then
            Assert.False(true, message)
    
    /// Assert that an object is not null
    let assertNotNull (value: obj) (message: string option) =
        let msg = defaultArg message "Expected value to not be null"
        if obj.ReferenceEquals(value, null) then
            Assert.True(false, msg)
    
    /// Assert that a string contains expected substring
    let assertContains (expected: string) (actual: string) (message: string option) =
        let msg = defaultArg message $"Expected '{actual}' to contain '{expected}'"
        if actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0 then
            Assert.True(false, msg)
    
    /// Assert that a collection is empty
    let assertEmpty<'T> (items: 'T seq) (message: string option) =
        let msg = defaultArg message "Expected collection to be empty"
        if not (Seq.isEmpty items) then
            Assert.True(false, msg)
    
    /// Assert that a collection is not empty
    let assertNotEmpty<'T> (items: 'T seq) (message: string option) =
        let msg = defaultArg message "Expected collection to not be empty"
        if Seq.isEmpty items then
            Assert.True(false, msg)
    
    /// Assert that two values are equal
    let assertEqual<'T> (expected: 'T) (actual: 'T) (message: string) =
        try 
            Assert.Equal<'T>(expected, actual)
        with ex ->
            Assert.True(false, message + ": " + ex.Message)
    
    /// Assert that two sequences are equal
    let assertSequenceEqual<'T> (expected: 'T seq) (actual: 'T seq) (message: string option) =
        try 
            Assert.Equal<'T>(expected, actual)
        with ex ->
            let msg = defaultArg message ex.Message
            Assert.True(false, msg)
    
    /// Assert that an exception is thrown
    let assertThrows<'TException when 'TException :> exn> (action: unit -> unit) (message: string option) =
        try
            action()
            let msg = defaultArg message $"Expected exception of type {typeof<'TException>.Name} to be thrown"
            Assert.True(false, msg)
        with
        | :? 'TException -> () // Expected exception type
        | ex ->
            let msg = defaultArg message $"Expected {typeof<'TException>.Name} but got {ex.GetType().Name}: {ex.Message}"
            Assert.True(false, msg)
    
    /// Assert that no exception is thrown
    let assertNoThrow (action: unit -> unit) (message: string option) =
        try
            action()
        with ex ->
            let msg = defaultArg message $"Expected no exception but got {ex.GetType().Name}: {ex.Message}"
            Assert.True(false, msg)
    
    /// Assert with custom failure function that includes logs
    let assertWithLogs (condition: bool) (dumpLogs: unit -> string) (message: string) =
        if not condition then
            let logs = dumpLogs()
            let fullMessage = 
                if String.IsNullOrWhiteSpace logs then message
                else message + Environment.NewLine + Environment.NewLine + logs
            Assert.True(false, fullMessage)
    
    /// Skip test conditionally with reason
    let skipIf (condition: bool) (reason: string) =
        if condition then
            // In xunit, we can't dynamically skip tests, so just return true to indicate skip
            true
        else
            false
    
    /// Skip test if integration tests are disabled
    let skipIfIntegrationDisabled (isIntegrationTest: bool) (skipIntegration: bool) (testName: string) =
        if isIntegrationTest && skipIntegration then
            let reason = sprintf "Integration test '%s' skipped (integration tests disabled)" testName
            // In xunit, we can't dynamically skip tests, so just return true to indicate skip
            true
        else
            false