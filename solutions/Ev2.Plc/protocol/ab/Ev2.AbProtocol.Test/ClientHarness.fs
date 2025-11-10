namespace Ev2.AbProtocol.Test

open System
open Ev2.AbProtocol.Core
open Ev2.AbProtocol.Client
open ProtocolTestHelper
open ProtocolTestHelper.IntegrationTestRunner
open TagFixtures

module ClientHarness =
    
    type TestResult<'T> = ProtocolTestHelper.TestExecution.TestResult<'T, AbProtocolError>
    
    let private buildConfig () =
        { IpAddress = TestHelpers.abIp
          Port = TestHelpers.abPort
          PlcType =
              match TestHelpers.abPlcType with
              | "ControlLogix" -> PlcType.ControlLogix
              | "MicroLogix" -> PlcType.MicroLogix
              | _ -> PlcType.CompactLogix
          Slot = TestHelpers.abSlot
          Timeout = TimeSpan.FromMilliseconds(float TestHelpers.abTimeoutMs)
          MaxRetries = TestHelpers.abRetries
          RetryDelay = TimeSpan.FromMilliseconds 100.0
          ConnectionPath = None
          UseConnectedMessaging = false
          MaxConcurrentRequests = 1 }
    
    let private createClient () =
        let config = buildConfig()
        let logger = TestHelpers.createPacketLogger config
        new ABClient(config, packetLogger = logger)
    
    let private augmentError (error: AbProtocolError) (logs: string) =
        if String.IsNullOrWhiteSpace logs then error
        else
            let combinedMessage =
                if String.IsNullOrWhiteSpace error.Message then logs
                else error.Message + Environment.NewLine + Environment.NewLine + logs
            AbProtocolError.UnknownError combinedMessage
    
    let private lifecycle : ClientLifecycle<ABClient, AbProtocolError, uint32 option> =
        { CreateClient = createClient
          Connect = fun client ->
              let (status, sessionInfo) = client.Connect()
              if status.IsSuccess then Ok sessionInfo else Error status
          Disconnect = fun client -> client.Disconnect() |> ignore
          Dispose = fun client -> (client :> IDisposable).Dispose()
          MapException = fun ex -> AbProtocolError.UnknownError ex.Message
          DumpLogs = TestHelpers.dumpLogs
          AugmentError = augmentError }
    
    let runWithClient (action: ABClient -> 'T) : TestResult<'T> =
        IntegrationTestRunner.runWithClient lifecycle action
    
    let unwrap (messageBuilder: AbProtocolError -> string) (result: TestResult<'T>) : 'T =
        IntegrationTestRunner.unwrapOrFail TestHelpers.failWithLogsResult messageBuilder result
    
    let readTag (client: ABClient) (tag: TagDescriptor) =
        let count = max tag.ElementCount 1
        client.ReadTag(tag.Name, tag.DataType, count)
    
    let readTagCount (client: ABClient) (tag: TagDescriptor) count =
        client.ReadTag(tag.Name, tag.DataType, count)
    
    let readBit (client: ABClient) (baseTag: TagDescriptor) elementIndex bitIndex =
        let tagName =
            if elementIndex >= 0 then
                sprintf "%s[%d].%d" baseTag.Name elementIndex bitIndex
            else
                sprintf "%s.%d" baseTag.Name bitIndex
        client.ReadTag(tagName, DataType.BOOL, 1)
    
    let writeScalar (client: ABClient) (tag: TagDescriptor) (bytes: byte[]) =
        client.WriteTag(tag.Name, tag.DataType, bytes)
    
    let writeBool (client: ABClient) (tagName: string) (value: bool) =
        let payload = if value then [| 0xFFuy |] else [| 0uy |]
        client.WriteTag(tagName, DataType.BOOL, payload)
