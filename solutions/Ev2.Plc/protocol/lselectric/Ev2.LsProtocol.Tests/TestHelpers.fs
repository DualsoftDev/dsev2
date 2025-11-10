namespace Ev2.LsProtocol.Tests

open System
open Xunit
open Ev2.LsProtocol
open Ev2.LsProtocol.Core
open ProtocolTestHelper
open ProtocolTestHelper.PacketLogger
open ProtocolTestHelper.TestExecution
open ProtocolTestHelper.TestLogging
open ProtocolTestHelper.AssertionHelpers
open ProtocolTestHelper.ValueGenerators
open ProtocolTestHelper.RetryHelpers

[<AutoOpen>]
module TestHelpers =
    
    // ========================================
    // Environment Configuration for LS Electric XGT
    // ========================================
    
    module Env = ProtocolTestHelper.TestEnvironment

    let xgtIp = Env.getString ["XGT_TEST_IP"] "192.168.9.103"
    let xgtPort = Env.getInt ["XGT_TEST_PORT"] 2004
    let xgtTimeoutMs = Env.getInt ["XGT_TEST_TIMEOUT_MS"] 5000
    let xgtRetries = Env.getInt ["XGT_TEST_RETRIES"] 3

    let xgtCpuType = 
        Env.getString ["XGT_TEST_CPU_TYPE"] "XGK"
        |> fun s ->
            match s.ToUpperInvariant() with
            | "XGK" -> "XGK"
            | "XGI" -> "XGI"
            | _ -> "XGK"

    let skipIntegrationTests =
        Env.getBool ["XGT_SKIP_INTEGRATION"] false

    // ========================================
    // Logging helpers
    // ========================================

    let private sharedLogger = TestLogger(500)

    let log message = TestLogging.log sharedLogger message

    let logPacket direction (bytes: byte[]) length =
        TestLogging.logPacket sharedLogger direction bytes length

    let createPacketLogger host port =
        fun direction (bytes: byte[]) length ->
            logPacket direction bytes length
            TestLogging.forwardPacket "LS" host port direction bytes length

    let dumpLogs () = TestLogging.dump sharedLogger "XGT protocol log"

    type TestResult<'T> = TestExecution.TestResult<'T, LsProtocolError>

    let private appendSummary summary message =
        if String.IsNullOrWhiteSpace summary then message
        else message + Environment.NewLine + Environment.NewLine + summary

    let private formatFailure message =
        let logs = dumpLogs()
        if String.IsNullOrWhiteSpace logs then message
        else message + Environment.NewLine + Environment.NewLine + logs

    let failWithLogs message =
        TestExecution.failWithLogs dumpLogs message

    let failWithLogsResult (result: TestResult<_>) message =
        TestExecution.failWithLogsWithResult dumpLogs result message

    let failWithLogsWithResult = failWithLogsResult

    // ========================================
    // Test Data Generators
    // ========================================
    
    /// Generate test frame ID
    let generateFrameId() = [| 0xAAuy; 0xBBuy |]
    
    /// Generate test addresses for XGK CPU
    let xgkTestAddresses = [
        "P10", PlcTagDataType.Bool
        "M100", PlcTagDataType.UInt16
        "D1000", PlcTagDataType.UInt32
        "K50", PlcTagDataType.UInt16
        "T10", PlcTagDataType.UInt16
    ]
    
    /// Generate test addresses for XGI CPU
    let xgiTestAddresses = [
        "I10", PlcTagDataType.Bool
        "Q20", PlcTagDataType.Bool
        "M100", PlcTagDataType.UInt16
        "L1000", PlcTagDataType.UInt32
        "F50", PlcTagDataType.UInt16
    ]

    // ========================================
    // Assertions using common helpers
    // ========================================
    
    let assertInRange min max actual =
        AssertionHelpers.assertInRange min max actual None
    
    let assertApproxEqual tolerance expected actual =
        AssertionHelpers.assertApproxEqual tolerance expected actual None

    let assertValidXgtFrame (frame: byte[]) =
        AssertionHelpers.assertTrue (frame.Length >= XgtUtil.HeaderSize) "Frame too short"
        
        // Check company ID
        let companyIdBytes = frame.[0..11]
        let expectedCompanyId = CompanyHeader.IdBytes
        BufferAssert.equal expectedCompanyId companyIdBytes
        
        // Check frame source
        Assert.Equal(byte FrameSource.ClientToServer, frame.[13])

    let assertValidXgtResponse (response: byte[]) =
        AssertionHelpers.assertTrue (response.Length >= XgtUtil.HeaderSize) "Response too short"
        
        // Check company ID
        let companyIdBytes = response.[0..11]
        let expectedCompanyId = CompanyHeader.IdBytes
        BufferAssert.equal expectedCompanyId companyIdBytes
        
        // Check frame source
        Assert.Equal(byte FrameSource.ServerToClient, response.[13])

    // ========================================
    // Test Skip Helpers using common helpers
    // ========================================
    
    let skipIfIntegrationDisabled testName =
        AssertionHelpers.skipIfIntegrationDisabled true skipIntegrationTests testName |> ignore

    // ========================================
    // Common Test Values using common generators
    // ========================================
    
    let commonBoolValues = CommonTestValues.boolValues
    let commonByteValues = CommonTestValues.byteValues  
    let commonWordValues = CommonTestValues.wordValues
    let commonDWordValues = CommonTestValues.dwordValues

    let createClient (ip: string, port: int, timeoutMs: int, isLocal: bool) =
        let logger = createPacketLogger ip port
        new LsClient(ip, port, timeoutMs, isLocal, packetLogger = logger)
