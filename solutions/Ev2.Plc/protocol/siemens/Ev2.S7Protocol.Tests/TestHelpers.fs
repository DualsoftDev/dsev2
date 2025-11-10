namespace Ev2.S7Protocol.Tests

open System
open Xunit
open ProtocolTestHelper
open ProtocolTestHelper.PacketLogger
open ProtocolTestHelper.TestLogging
open ProtocolTestHelper.TestExecution
open ProtocolTestHelper.AssertionHelpers
open ProtocolTestHelper.RetryHelpers
open Ev2.S7Protocol.Core

module TestHelpers =
    module Env = ProtocolTestHelper.TestEnvironment

    let random = Random()

    // Environment-driven configuration
    let s7Name = Env.getString [ "S7_TEST_NAME" ] "S7 Test PLC" 
    let s7Ip = Env.getString [ "S7_TEST_IP" ] "192.168.9.97"
    let s7Rack = Env.getInt [ "S7_TEST_RACK" ] 0
    let s7Slot = Env.getInt [ "S7_TEST_SLOT" ] 2
    let s7Port = Env.getInt [ "S7_TEST_PORT" ] 102
    let s7LocalTsap = Env.getInt [ "S7_TEST_LOCAL_TSAP" ] 0x0100
    let s7RemoteTsap = Env.getInt [ "S7_TEST_REMOTE_TSAP" ] 0x0100
    let s7TimeoutMs = Env.getInt [ "S7_TEST_TIMEOUT_MS" ] 5_000
    let s7MaxPdu = Env.getInt [ "S7_TEST_MAX_PDU" ] 480
    let skipIntegration = false // Force integration tests to run

    let s7CpuType =
        match Env.getString [ "S7_TEST_CPU_TYPE" ] "S7300" with
        | value when value.Equals("S7200", StringComparison.OrdinalIgnoreCase) -> CpuType.S7200
        | value when value.Equals("S7400", StringComparison.OrdinalIgnoreCase) -> CpuType.S7400
        | value when value.Equals("S71200", StringComparison.OrdinalIgnoreCase) -> CpuType.S71200
        | value when value.Equals("S71500", StringComparison.OrdinalIgnoreCase) -> CpuType.S71500
        | _ -> CpuType.S7300

    let s7Password =
        match Environment.GetEnvironmentVariable("S7_TEST_PASSWORD") with
        | null
        | "" -> None
        | value -> Some value

    let private sharedLogger = TestLogger(500)

    let log message =
        TestLogging.log sharedLogger message

    let logf format (args: obj[]) =
        TestLogging.logf sharedLogger format args

    let logPacket direction (bytes: byte[]) length =
        TestLogging.logPacket sharedLogger direction bytes length

    let createPacketLogger (config: S7Config) =
        fun direction (bytes: byte[]) length ->
            logPacket direction bytes length
            TestLogging.forwardPacket "S7" config.IpAddress config.Port direction bytes length

    let dumpLogs () = TestLogging.dump sharedLogger "S7 protocol log"

    type TestResult<'T> = TestExecution.TestResult<'T, S7ProtocolError>

    let failWithLogs message =
        TestExecution.failWithLogs dumpLogs message

    let failWithLogsWithResult (result: TestResult<_>) message =
        TestExecution.failWithLogsWithResult dumpLogs result message

    let assertEqual<'T> (expected: 'T) (actual: 'T) =
        AssertionHelpers.assertEqual expected actual "Values should be equal"

    let assertTrue condition message =
        AssertionHelpers.assertTrue condition message
