[<AutoOpen>]
module Ev2.MxProtocol.Tests.TestHelpers

open System
open System.Globalization
open Xunit
open Ev2.MxProtocol.Core
open Ev2.MxProtocol.Protocol
open ProtocolTestHelper
open ProtocolTestHelper.PacketLogger
open ProtocolTestHelper.TestLogging
open ProtocolTestHelper.TestExecution
open ProtocolTestHelper.AssertionHelpers
open ProtocolTestHelper.RetryHelpers

module Env = ProtocolTestHelper.TestEnvironment

let private tryParseByte (value: string) =
    let trimmed = value.Trim()
    if trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) then
        Byte.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
    else
        Byte.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture)

let private tryParseUInt16 (value: string) =
    let trimmed = value.Trim()
    if trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) then
        UInt16.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
    else
        UInt16.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture)

let private getByteEnv (names: string list) (defaultValue: byte) =
    names
    |> List.tryPick (fun name ->
        match Environment.GetEnvironmentVariable(name) with
        | null | "" -> None
        | value ->
            match tryParseByte value with
            | true, parsed -> Some parsed
            | _ -> None)
    |> Option.defaultValue defaultValue

let private getUInt16Env (names: string list) (defaultValue: uint16) =
    names
    |> List.tryPick (fun name ->
        match Environment.GetEnvironmentVariable(name) with
        | null | "" -> None
        | value ->
            match tryParseUInt16 value with
            | true, parsed -> Some parsed
            | _ -> None)
    |> Option.defaultValue defaultValue

let createTestConfig name host port =
    let networkNumber = getByteEnv ["MELSEC_ACCESS_NETWORK"; "MELSEC_ACCESS_NETWORK_NUMBER"] 0uy
    let stationNumber = getByteEnv ["MELSEC_ACCESS_STATION"; "MELSEC_ACCESS_STATION_NUMBER"] 0xFFuy

    let ioNumber = getUInt16Env ["MELSEC_ACCESS_IO"; "MELSEC_ACCESS_IO_NUMBER"] 0x03FFus
    let relayType = getByteEnv ["MELSEC_ACCESS_RELAY"; "MELSEC_ACCESS_RELAY_TYPE"] 0uy
    {
        Name = name
        Host = host
        Port = port
        Timeout = TimeSpan.FromMilliseconds(float (Env.getInt ["MELSEC_TEST_TIMEOUT_MS"] 5_000))
        FrameType = FrameType.QnA_3E_Binary
        AccessRoute = {
            NetworkNumber = networkNumber
            StationNumber = stationNumber
            IoNumber = ioNumber
            RelayType = relayType
        }
        MonitoringTimer = 0x0010us
    }

// Mitsubishi PLC configurations based on actual hardware
let private plc1Host = Env.getString ["MELSEC_TEST_PLC1_HOST"; "MELSEC_TEST_HOST"] "192.168.9.120"  // Mitsubishi LocalEthernet
let private plc1Port = Env.getInt ["MELSEC_TEST_PLC1_PORT"] 7777
let private plc2Host = Env.getString ["MELSEC_TEST_PLC2_HOST"] "192.168.9.121"  // Mitsubishi Ethernet TCP
let private plc2Port = Env.getInt ["MELSEC_TEST_PLC2_PORT"] 5002

// PLC configurations - can be selected via environment variable
let plc1Config = createTestConfig "Mitsubishi LocalEthernet" plc1Host plc1Port
let plc2Config = createTestConfig "Mitsubishi Ethernet TCP" plc2Host plc2Port

let defaultTestConfig =
    match Env.getString ["MELSEC_TEST_PLC"] "PLC1" with
    | "PLC1" | "1" -> plc1Config
    | "PLC2" | "2" -> plc2Config
    | _ -> plc1Config // Default to PLC1

// ========================================
// Logging & assertions
// ========================================

let private sharedLogger = TestLogger(500)

let log message =
    TestLogging.log sharedLogger message

let logPacket direction (bytes: byte[]) length =
    TestLogging.logPacket sharedLogger direction bytes length

let createPacketLogger (config: MelsecConfig) =
    fun direction (bytes: byte[]) length ->
        logPacket direction bytes length
        TestLogging.forwardPacket "MELSEC" config.Host config.Port direction bytes length

let logf format (args: obj[]) =
    TestLogging.logf sharedLogger format args

let dumpLogs () = TestLogging.dump sharedLogger "Mitsubishi protocol log"

type TestResult<'T> = TestExecution.TestResult<'T, MxProtocolError>

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

// Use common assertion helpers
let assertEqual<'T> (expected: 'T) (actual: 'T) =
    AssertionHelpers.assertEqual expected actual "Values should be equal"

let assertTrue condition message =
    AssertionHelpers.assertTrue condition message

let assertSequenceEqual<'T> (expected: 'T seq) (actual: 'T seq) =
    AssertionHelpers.assertSequenceEqual expected actual None

let assertFalse condition message =
    AssertionHelpers.assertFalse condition message

let assertNotNull (value: obj) message =
    AssertionHelpers.assertNotNull value (Some message)

let assertContains (expected: string) (actual: string) =
    AssertionHelpers.assertContains expected actual None

let assertEmpty<'T> (items: 'T seq) message =
    AssertionHelpers.assertEmpty items (Some message)

// Use common value generators
let createRandomBools count (random: Random) =
    ValueGenerators.Random.createRandomBools count

let createRandomWords count (random: Random) =
    ValueGenerators.Random.createRandomWords count

let createRandomBytes count (random: Random) =
    ValueGenerators.Random.createRandomBytes count

// Use common retry helper
let retry maxAttempts delay action =
    RetryHelpers.retry maxAttempts delay action
