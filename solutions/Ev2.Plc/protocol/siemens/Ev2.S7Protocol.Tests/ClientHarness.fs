namespace Ev2.S7Protocol.Tests

open System
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Client
open ProtocolTestHelper
open ProtocolTestHelper.IntegrationTestRunner

module ClientHarness =
    
    type TestResult<'T> = ProtocolTestHelper.TestExecution.TestResult<'T, S7ProtocolError>
    
    let private appendLogs (message: string) (logs: string) =
        if String.IsNullOrWhiteSpace logs then message
        elif String.IsNullOrWhiteSpace message then logs
        else message + Environment.NewLine + Environment.NewLine + logs
    
    let private augmentError (error: S7ProtocolError) (logs: string) =
        if String.IsNullOrWhiteSpace logs then error
        else
            let addLogs msg ctor = ctor (appendLogs msg logs)
            match error with
            | S7ProtocolError.ConnectionError msg -> addLogs msg S7ProtocolError.ConnectionError
            | S7ProtocolError.SessionError msg -> addLogs msg S7ProtocolError.SessionError
            | S7ProtocolError.IsoError (code, msg) -> S7ProtocolError.IsoError (code, appendLogs msg logs)
            | S7ProtocolError.S7Error (code, msg) -> S7ProtocolError.S7Error (code, appendLogs msg logs)
            | S7ProtocolError.DataError msg -> addLogs msg S7ProtocolError.DataError
            | S7ProtocolError.UnknownError msg -> addLogs msg S7ProtocolError.UnknownError
            | _ -> S7ProtocolError.UnknownError (appendLogs error.Message logs)
    
    let private lifecycle : ClientLifecycle<S7Client, S7ProtocolError, unit> =
        { CreateClient = ClientHelpers.createClient
          Connect = fun client ->
              match client.Connect() with
              | Ok info -> Ok info
              | Error msg -> Error (S7ProtocolError.UnknownError msg)
          Disconnect = fun client -> client.Disconnect()
          Dispose = fun client -> (client :> IDisposable).Dispose()
          MapException = fun ex -> S7ProtocolError.UnknownError ex.Message
          DumpLogs = TestHelpers.dumpLogs
          AugmentError = augmentError }

    let runWithClient action =
        IntegrationTestRunner.runWithClient lifecycle action

    let unwrap messageBuilder result =
        IntegrationTestRunner.unwrapOrFail TestHelpers.failWithLogsWithResult messageBuilder result
