namespace Ev2.AbProtocol.Test

open System
open ProtocolTestHelper
open ProtocolTestHelper.TestLogging
open ProtocolTestHelper.TestExecution
open ProtocolTestHelper.PacketLogger

[<AutoOpen>]
module TestHelpers =
    module Env = ProtocolTestHelper.TestEnvironment

    let abIp = Env.getString [ "AB_TEST_IP" ] "192.168.9.110"
    let abPort = Env.getInt [ "AB_TEST_PORT" ] 44818
    let abSlot = Env.getByte [ "AB_TEST_SLOT" ] 0uy
    let abTimeoutMs = Env.getInt [ "AB_TEST_TIMEOUT_MS" ] 5000
    let abRetries = Env.getInt [ "AB_TEST_RETRIES" ] 3
    let abPlcType =
        Env.getString [ "AB_TEST_PLC_TYPE" ] "CompactLogix"
        |> fun value ->
            match value.Trim().ToLowerInvariant() with
            | "controllogix" -> "ControlLogix"
            | "micrologix" -> "MicroLogix"
            | _ -> "CompactLogix"

    let skipIntegrationTests = Env.getBool [ "AB_SKIP_INTEGRATION" ] false

    let private sharedLogger = TestLogger(1024)

    let log message = TestLogging.log sharedLogger message

    let createPacketLogger (config: Ev2.AbProtocol.Core.ConnectionConfig) =
        fun direction (bytes: byte[]) length ->
            TestLogging.logPacket sharedLogger direction bytes length
            TestLogging.forwardPacket
                "AB"
                config.IpAddress
                config.Port
                direction
                bytes
                length

    let dumpLogs () = TestLogging.dump sharedLogger "AB protocol log"

    let failWithLogs message = TestExecution.failWithLogs dumpLogs message

    let failWithLogsResult (result: TestExecution.TestResult<_, _>) message =
        TestExecution.failWithLogsWithResult dumpLogs result message
