namespace Ev2.S7Protocol.Tests

open ProtocolTestHelper

module TagDefinitions =
    module Env = ProtocolTestHelper.TestEnvironment

    /// Merker area addresses used in integration scenarios.
    let merkerBit = Env.getString [ "S7_TEST_MERKER_BIT" ] "M0.0"
    let merkerByte = Env.getString [ "S7_TEST_MERKER_BYTE" ] "MB1"
    let merkerWord = Env.getString [ "S7_TEST_MERKER_WORD" ] "MW2"
    let merkerDWord = Env.getString [ "S7_TEST_MERKER_DWORD" ] "MD4"
    let merkerReal = Env.getString [ "S7_TEST_MERKER_REAL" ] "MD8"
    let merkerBulkStart = Env.getInt [ "S7_TEST_MERKER_BULK_START" ] 0
    let merkerBulkLength = Env.getInt [ "S7_TEST_MERKER_BULK_LENGTH" ] 8

    /// Optional DB block configuration for extended tests.
    let dbNumber = Env.getInt [ "S7_TEST_DB_NUMBER" ] 1
    let dbStartByte = Env.getInt [ "S7_TEST_DB_START" ] 0
    let dbLengthBytes = Env.getInt [ "S7_TEST_DB_LENGTH" ] 4
