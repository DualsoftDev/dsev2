namespace ProtocolTestHelper

open System
open Xunit
open ProtocolTestHelper.TestLogging
open ProtocolTestHelper.TestExecution

/// Generic protocol client test utilities that can be shared across all protocols
module ClientTestHelpers =
    
    /// Generic interface for protocol errors
    type IProtocolError =
        abstract member IsSuccess: bool
        abstract member IsError: bool  
        abstract member Message: string
    
    /// Generic test result wrapper
    type TestResult<'T, 'TError when 'TError :> IProtocolError> = TestExecution.TestResult<'T, 'TError>
    
    /// Generic configuration builder interface
    type IConfigBuilder<'TConfig> =
        abstract member BuildConfig: unit -> 'TConfig
        abstract member BuildConfigWith: ('TConfig -> 'TConfig) -> 'TConfig
    
    /// Generic client interface for testing
    type ITestClient<'TConfig, 'TError when 'TError :> IProtocolError> =
        abstract member Connect: unit -> 'TError * obj option
        abstract member Disconnect: unit -> unit
        abstract member Dispose: unit -> unit
    
    /// Build log summary from test result
    let buildLogSummary (result: TestResult<_, _>) =
        TestExecution.buildLogSummary result

    /// Dump result logs to test output
    let dumpResultLogs (logger: TestLogger) (result: TestResult<_, _>) =
        let summary = buildLogSummary result
        if not (String.IsNullOrWhiteSpace summary) then
            TestLogging.log logger summary

    /// Fail test with logs from result
    let failWithLogs (dumpLogs: unit -> string) (result: TestResult<_, _>) message =
        TestExecution.failWithLogsWithResult dumpLogs result message

    /// Capture output during action execution
    let captureOutput (mapError: exn -> 'TError) (action: unit -> 'T) =
        TestExecution.captureOutput mapError action
    
    /// Generic function to run action with a protocol client
    let runWithClient<'TClient, 'TConfig, 'TError, 'T when 'TClient :> ITestClient<'TConfig, 'TError> and 'TClient :> IDisposable and 'TError :> IProtocolError>
        (clientFactory: 'TConfig -> TestLogger -> 'TClient)
        (configBuilder: IConfigBuilder<'TConfig>)
        (logger: TestLogger)
        (noErrorValue: 'TError)
        (unknownErrorFactory: string -> 'TError)
        (action: 'TClient -> 'T) =
        
        captureOutput (fun ex -> unknownErrorFactory ex.Message) (fun () ->
            let config = configBuilder.BuildConfig()
            use client = clientFactory config logger
            match client.Connect() with
            | (error, Some _) when error.IsSuccess ->
                try
                    let result = action client
                    client.Disconnect()
                    Ok result
                with ex ->
                    client.Disconnect()
                    Error (unknownErrorFactory ex.Message)
            | (error, _) -> Error error)

    /// Create a tag data type resolver interface
    type ITagDataTypeResolver<'TDataType> =
        abstract member GetDataTypeForTag: string -> 'TDataType

    /// Generic tag reading helper
    let createTagReader<'TClient, 'TDataType, 'TError>
        (readTagFunc: 'TClient -> string -> 'TDataType -> Result<obj[], 'TError>)
        (readTagCountFunc: 'TClient -> string -> 'TDataType -> int -> Result<obj[], 'TError>)
        (resolver: ITagDataTypeResolver<'TDataType>) =
        
        let readTag (client: 'TClient) (tagName: string) =
            let dataType = resolver.GetDataTypeForTag tagName
            readTagFunc client tagName dataType
        
        let readTagCount (client: 'TClient) (tagName: string) (count: int) =
            let dataType = resolver.GetDataTypeForTag tagName
            readTagCountFunc client tagName dataType count
            
        (readTag, readTagCount)

    /// Helper to parse base tag name from complex tag expressions
    let parseBaseTagName (tagName: string) (bitSelectorParser: string -> (string * int * int) option) (indexerParser: string -> (string * int[]) option) =
        match bitSelectorParser tagName with
        | Some (baseTag, _, _) -> baseTag
        | None ->
            match indexerParser tagName with
            | Some (baseTag, _) -> baseTag
            | None -> tagName

    /// Configuration modifier helper
    let buildConfigWith<'TConfig> (baseConfig: 'TConfig) (modifier: 'TConfig -> 'TConfig) =
        modifier baseConfig

    /// Generic error assertion helpers
    module Assertions =
        
        /// Assert that operation succeeded
        let assertSuccess<'T, 'TError when 'TError :> IProtocolError> (result: Result<'T, 'TError>) message =
            match result with
            | Ok _ -> ()
            | Error error -> Assert.True(false, sprintf "%s: %s" message error.Message)
        
        /// Assert that operation failed with specific error
        let assertError<'T, 'TError when 'TError :> IProtocolError> (result: Result<'T, 'TError>) expectedErrorCheck message =
            match result with
            | Ok _ -> Assert.True(false, sprintf "%s: Expected error but got success" message)
            | Error error -> 
                if not (expectedErrorCheck error) then
                    Assert.True(false, sprintf "%s: Got unexpected error: %s" message error.Message)
        
        /// Assert value equals expected
        let assertEqual<'T> (expected: 'T) (actual: 'T) (message: string) =
            Assert.Equal<'T>(expected, actual)
            
        /// Assert condition is true
        let assertTrue condition message =
            Assert.True(condition, message)
            
        /// Assert condition is false
        let assertFalse condition message =
            Assert.False(condition, message)

    /// Protocol-specific test data generators
    module TestData =
        
        /// Generate test values for different data types
        let generateTestValue<'T> (dataType: Type) : 'T =
            let random = Random()
            match dataType with
            | t when t = typeof<bool> -> box (random.Next(2) = 1) :?> 'T
            | t when t = typeof<byte> -> box (byte (random.Next(256))) :?> 'T
            | t when t = typeof<int16> -> box (int16 (random.Next(-32768, 32767))) :?> 'T
            | t when t = typeof<int32> -> box (random.Next()) :?> 'T
            | t when t = typeof<float32> -> box (float32 (random.NextDouble() * 1000.0)) :?> 'T
            | t when t = typeof<string> -> box (sprintf "Test_%d" (random.Next(1000))) :?> 'T
            | _ -> Unchecked.defaultof<'T>
        
        /// Generate array of test values
        let generateTestArray<'T> (dataType: Type) (count: int) : 'T[] =
            Array.init count (fun _ -> generateTestValue<'T> dataType)

    /// Timing and performance test helpers
    module Performance =
        
        /// Measure execution time of an operation
        let measureTime (operation: unit -> 'T) : 'T * TimeSpan =
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            let result = operation()
            stopwatch.Stop()
            (result, stopwatch.Elapsed)
        
        /// Assert operation completes within time limit
        let assertWithinTimeLimit (timeLimit: TimeSpan) (operation: unit -> 'T) message =
            let (result, elapsed) = measureTime operation
            if elapsed > timeLimit then
                Assert.True(false, sprintf "%s: Operation took %A, limit was %A" message elapsed timeLimit)
            result
        
        /// Run performance test with multiple iterations
        let runPerformanceTest (iterations: int) (operation: unit -> 'T) =
            let times = Array.zeroCreate<TimeSpan> iterations
            let mutable lastResult = Unchecked.defaultof<'T>
            
            for i in 0 .. iterations - 1 do
                let (result, elapsed) = measureTime operation
                times.[i] <- elapsed
                lastResult <- result
            
            let totalTime = times |> Array.sumBy (fun t -> t.TotalMilliseconds)
            let averageTime = totalTime / float iterations
            let minTime = times |> Array.minBy (fun t -> t.TotalMilliseconds)
            let maxTime = times |> Array.maxBy (fun t -> t.TotalMilliseconds)
            
            (lastResult, averageTime, minTime, maxTime)