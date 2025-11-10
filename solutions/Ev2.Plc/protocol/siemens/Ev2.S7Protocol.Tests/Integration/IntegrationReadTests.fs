namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions
module CH = Ev2.S7Protocol.Tests.ClientHarness

module IntegrationReadTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Read merker bit returns boolean`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadBit(Tags.merkerBit))
        match CH.unwrap connectionError result with
        | Ok value -> Assert.True(value || not value, "ReadBit should return a boolean value")
        | Error msg -> TH.failWithLogsWithResult result $"Read merker bit failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Read merker byte returns value`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadByte(Tags.merkerByte))
        match CH.unwrap connectionError result with
        | Ok value ->
            Assert.InRange(int value, 0, 255)
        | Error msg ->
            TH.failWithLogsWithResult result $"Read merker byte failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Read merker word returns int16`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadInt16(Tags.merkerWord))
        match CH.unwrap connectionError result with
        | Ok value ->
            // no expectation for value; ensure call succeeded
            Assert.True(value <= Int16.MaxValue)
        | Error msg ->
            TH.failWithLogsWithResult result $"Read merker word failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Read merker dword returns int32`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadInt32(Tags.merkerDWord))
        match CH.unwrap connectionError result with
        | Ok value ->
            Assert.True(value <= Int32.MaxValue)
        | Error msg ->
            TH.failWithLogsWithResult result $"Read merker dword failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Read merker bytes bulk`` () =
        let result =
            CH.runWithClient (fun client ->
                client.ReadMerker(Tags.merkerBulkStart, Tags.merkerBulkLength))
        match CH.unwrap connectionError result with
        | Ok data ->
            Assert.Equal(Tags.merkerBulkLength, data.Length)
        | Error msg ->
            TH.failWithLogsWithResult result $"Read merker buffer failed: {msg}"
