namespace ProtocolTestHelper

open System
open Xunit

/// Shared helpers for capturing console output during protocol tests.
module TestExecution =

    /// Result wrapper that includes captured stdout/stderr alongside a computation result.
    type TestResult<'T,'TError> =
        { Result: Result<'T,'TError>
          StdOut: string
          StdErr: string }

        /// Builds a short text summary of captured stdout/stderr for inclusion in assertion failures.
    let buildLogSummary (result: TestResult<_,_>) =
        ConsoleCapture.summarize
            { Result = Ok ()
              StdOut = result.StdOut
              StdErr = result.StdErr }

    /// Combine message with captured stdout/stderr summary.
    let private appendSection (message: string) (section: string) =
        if String.IsNullOrWhiteSpace section then message
        else message + Environment.NewLine + Environment.NewLine + section

    /// Formats a failure message by including dumped protocol logs.
    let formatFailure dumpLogs message =
        let logs = dumpLogs()
        appendSection message logs

    /// Asserts with a message that includes captured protocol logs.
    let failWithLogs dumpLogs message =
        Assert.True(false, formatFailure dumpLogs message)

    /// Asserts with a message plus captured stdout/stderr and protocol logs.
    let failWithLogsWithResult dumpLogs (result: TestResult<_,_>) message =
        message
        |> appendSection (buildLogSummary result)
        |> failWithLogs dumpLogs

    /// Executes <paramref name="action"/> while capturing stdout/stderr.
    /// Exceptions are converted to an error value using <paramref name="mapError"/>.
    let captureOutput (mapError: exn -> 'TError) (action: unit -> 'T) =
        let captured = ConsoleCapture.capture action
        match captured.Result with
        | Ok value ->
            { Result = Ok value
              StdOut = captured.StdOut
              StdErr = captured.StdErr }
        | Error ex ->
            { Result = Error (mapError ex)
              StdOut = captured.StdOut
              StdErr = captured.StdErr }
