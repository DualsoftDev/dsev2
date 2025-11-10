namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestAttributes
module CH = Ev2.S7Protocol.Tests.ClientHarness
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions

module ComprehensiveTests =

    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"

    [<RequiresS7PLC>]
    let ``full workflow`` () =
        let result =
            CH.runWithClient (fun client ->
                // 1. Snapshot initial values
                let initialBit = client.ReadBit(Tags.merkerBit)
                let initialByte = client.ReadByte(Tags.merkerByte)
                let initialWord = client.ReadInt16(Tags.merkerWord)
                // 2. Toggle bit
                let bitStep =
                    match initialBit with
                    | Ok value ->
                        let desired = not value
                        match client.WriteBit(Tags.merkerBit, desired) with
                        | Ok () ->
                            match client.ReadBit(Tags.merkerBit) with
                            | Ok verify ->
                                ignore (client.WriteBit(Tags.merkerBit, value))
                                Ok verify
                            | Error msg ->
                                ignore (client.WriteBit(Tags.merkerBit, value))
                                Error msg
                        | Error msg -> Error msg
                    | Error msg -> Error msg
                // 3. Increment byte
                let byteStep =
                    match initialByte with
                    | Ok value ->
                        let desired = byte ((int value + 5) % 255)
                        match client.WriteByte(Tags.merkerByte, desired) with
                        | Ok () ->
                            match client.ReadByte(Tags.merkerByte) with
                            | Ok verify ->
                                ignore (client.WriteByte(Tags.merkerByte, value))
                                Ok verify
                            | Error msg ->
                                ignore (client.WriteByte(Tags.merkerByte, value))
                                Error msg
                        | Error msg -> Error msg
                    | Error msg -> Error msg
                // 4. Bulk read/write loop
                let bulkPayload = [| 0x10uy; 0x20uy; 0x30uy; 0x40uy |]
                let bulkStep =
                    match client.WriteBytes(DataArea.Merker, 0, Tags.merkerBulkStart, bulkPayload) with
                    | Ok () -> client.ReadMerker(Tags.merkerBulkStart, bulkPayload.Length)
                    | Error msg -> Error msg
                // 5. Capture final statistics
                let stats = client.GetStatistics()
                initialBit, initialByte, initialWord, bitStep, byteStep, bulkStep, stats)
        let (initialBit, initialByte, initialWord, bitStep, byteStep, bulkStep, stats) =
            CH.unwrap connectionError result

        match initialBit, bitStep with
        | Ok original, Ok verify -> Assert.NotEqual(original, verify)
        | Ok _, Error msg -> TH.failWithLogsWithResult result $"Bit verification failed: {msg}"
        | Error msg, _ -> TH.failWithLogsWithResult result $"Initial bit read failed: {msg}"

        match initialByte, byteStep with
        | Ok original, Ok verify -> Assert.InRange(int verify, 0, 255); Assert.NotEqual<byte>(original, verify)
        | Ok _, Error msg -> TH.failWithLogsWithResult result $"Byte verification failed: {msg}"
        | Error msg, _ -> TH.failWithLogsWithResult result $"Initial byte read failed: {msg}"

        match initialWord with
        | Ok _ -> ()
        | Error msg -> TH.failWithLogsWithResult result $"Initial word read failed: {msg}"

        match bulkStep with
        | Ok data -> Assert.Equal<byte[]>([| 0x10uy; 0x20uy; 0x30uy; 0x40uy |], data)
        | Error msg -> TH.failWithLogsWithResult result $"Bulk verification failed: {msg}"

        Assert.True(stats.SuccessRate >= 0.0)
