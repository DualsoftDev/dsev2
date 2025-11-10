namespace ProtocolTestHelper

open System
open System.Threading
open ProtocolTestHelper.TestLogging

/// Common retry and utility helpers that can be shared across all protocols
module RetryHelpers =
    
    /// Retry configuration options
    type RetryConfig = {
        MaxAttempts: int
        DelayMs: int
        BackoffMultiplier: float
        MaxDelayMs: int
        OnRetry: (int -> exn option -> unit) option
    }
    
    /// Default retry configuration
    let defaultRetryConfig = {
        MaxAttempts = 3
        DelayMs = 100
        BackoffMultiplier = 1.5
        MaxDelayMs = 5000
        OnRetry = None
    }
    
    /// Retry with exponential backoff
    let retryWithBackoff (config: RetryConfig) (operation: unit -> Result<'T, 'TError>) =
        let rec loop attempt currentDelay =
            match operation() with
            | Ok result -> Ok result
            | Error error when attempt < config.MaxAttempts ->
                // Call retry callback if provided
                config.OnRetry |> Option.iter (fun callback -> callback attempt None)
                
                // Wait with current delay
                Thread.Sleep(currentDelay: int)
                
                // Calculate next delay with backoff
                let nextDelay = min config.MaxDelayMs (int (float currentDelay * config.BackoffMultiplier))
                loop (attempt + 1) nextDelay
            | Error error -> Error error
        
        loop 1 config.DelayMs
    
    /// Simple retry with fixed delay
    let retry (maxAttempts: int) (delayMs: int) (operation: unit -> Result<'T, 'TError>) =
        let config = { defaultRetryConfig with MaxAttempts = maxAttempts; DelayMs = delayMs }
        retryWithBackoff config operation
    
    /// Retry with logging
    let retryWithLogging (logger: TestLogger) (maxAttempts: int) (delayMs: int) (operation: unit -> Result<'T, 'TError>) =
        let onRetryCallback attempt exn =
            TestLogging.log logger $"Retry attempt {attempt}/{maxAttempts} after {delayMs}ms delay"
        
        let config = { 
            defaultRetryConfig with 
                MaxAttempts = maxAttempts
                DelayMs = delayMs
                OnRetry = Some onRetryCallback 
        }
        retryWithBackoff config operation
    
    /// Retry for exception-based operations
    let retryOnException (maxAttempts: int) (delayMs: int) (operation: unit -> 'T) =
        let wrappedOperation () =
            try
                Ok (operation())
            with ex ->
                Error ex.Message
        
        retry maxAttempts delayMs wrappedOperation
    
    /// Retry with predicate - only retry if predicate returns true for the error
    let retryWhen (predicate: 'TError -> bool) (maxAttempts: int) (delayMs: int) (operation: unit -> Result<'T, 'TError>) =
        let rec loop attempt =
            match operation() with
            | Ok result -> Ok result
            | Error error when attempt < maxAttempts && predicate error ->
                Thread.Sleep(delayMs)
                loop (attempt + 1)
            | Error error -> Error error
        
        loop 1
    
    /// Timeout helpers
    module Timeout =
        
        /// Execute operation with timeout
        let withTimeout (timeoutMs: int) (operation: unit -> 'T) =
            use cts = new CancellationTokenSource(timeoutMs)
            let task = Threading.Tasks.Task.Run(operation, cts.Token)
            
            try
                if task.Wait(timeoutMs) then
                    Ok task.Result
                else
                    Error "Operation timed out"
            with
            | :? OperationCanceledException -> Error "Operation timed out"
            | ex -> Error ex.Message
        
        /// Execute async operation with timeout
        let withTimeoutAsync (timeoutMs: int) (operation: Async<'T>) =
            async {
                try
                    let! child = Async.StartChild(operation, timeoutMs)
                    let! result = child
                    return Ok result
                with
                | :? TimeoutException -> return Error "Operation timed out"
                | ex -> return Error ex.Message
            }
    
    /// Connection retry helpers specifically for protocol clients
    module Connection =
        
        /// Retry connection establishment
        let retryConnect (maxAttempts: int) (delayMs: int) (connectFn: unit -> Result<'TConnection, 'TError>) =
            retry maxAttempts delayMs connectFn
        
        /// Retry connection with exponential backoff
        let retryConnectWithBackoff (connectFn: unit -> Result<'TConnection, 'TError>) =
            let config = { 
                defaultRetryConfig with 
                    MaxAttempts = 5
                    DelayMs = 500
                    BackoffMultiplier = 2.0
                    MaxDelayMs = 10000
            }
            retryWithBackoff config connectFn
        
        /// Retry connection with logging
        let retryConnectWithLogging (logger: TestLogger) (maxAttempts: int) (connectFn: unit -> Result<'TConnection, 'TError>) =
            let onRetryCallback attempt exn =
                TestLogging.log logger $"Connection attempt {attempt}/{maxAttempts} failed, retrying..."
            
            let config = { 
                defaultRetryConfig with 
                    MaxAttempts = maxAttempts
                    DelayMs = 1000
                    OnRetry = Some onRetryCallback 
            }
            retryWithBackoff config connectFn
    
    /// Performance measurement helpers
    module Performance =
        
        /// Measure execution time of operation
        let measureTime (operation: unit -> 'T) =
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            let result = operation()
            stopwatch.Stop()
            (result, stopwatch.Elapsed)
        
        /// Measure execution time with retry
        let measureTimeWithRetry (maxAttempts: int) (delayMs: int) (operation: unit -> Result<'T, 'TError>) =
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            let result = retry maxAttempts delayMs operation
            stopwatch.Stop()
            (result, stopwatch.Elapsed)
        
        /// Run operation multiple times and collect statistics
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
            
            {|
                Result = lastResult
                AverageMs = averageTime
                MinTime = minTime
                MaxTime = maxTime
                TotalTime = TimeSpan.FromMilliseconds(totalTime)
                Iterations = iterations
            |}
    
    /// Configuration validation helpers
    module Validation =
        
        /// Validate timeout values
        let validateTimeout (timeoutMs: int) (paramName: string) =
            if timeoutMs <= 0 then
                Error $"{paramName} must be positive, got {timeoutMs}"
            elif timeoutMs > 300000 then  // 5 minutes max
                Error $"{paramName} too large, got {timeoutMs}ms (max 300000ms)"
            else
                Ok timeoutMs
        
        /// Validate retry configuration
        let validateRetryConfig (config: RetryConfig) =
            [
                if config.MaxAttempts <= 0 then yield "MaxAttempts must be positive"
                if config.DelayMs < 0 then yield "DelayMs must be non-negative"
                if config.BackoffMultiplier <= 0.0 then yield "BackoffMultiplier must be positive"
                if config.MaxDelayMs <= 0 then yield "MaxDelayMs must be positive"
                if config.MaxDelayMs < config.DelayMs then yield "MaxDelayMs must be >= DelayMs"
            ]
            |> function
            | [] -> Ok config
            | errors -> Error (String.concat "; " errors)