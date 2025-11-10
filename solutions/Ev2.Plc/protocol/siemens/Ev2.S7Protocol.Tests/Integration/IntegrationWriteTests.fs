namespace Ev2.S7Protocol.Tests.Integration

open System
open Xunit
open Ev2.S7Protocol.Tests.TestAttributes
module TH = Ev2.S7Protocol.Tests.TestHelpers
module Tags = Ev2.S7Protocol.Tests.TagDefinitions
module CH = Ev2.S7Protocol.Tests.ClientHarness

module IntegrationWriteTests =
    
    let private connectionError (error: Ev2.S7Protocol.Core.S7ProtocolError) = $"Connection failed: {error.Message}"
    
    [<RequiresS7PLC>]
    let ``Write merker bit toggles value`` () =
        let result =
            CH.runWithClient (fun client ->
                match client.ReadBit(Tags.merkerBit) with
                | Error msg -> Error $"Initial read failed: {msg}"
                | Ok original ->
                    let desired = not original
                    match client.WriteBit(Tags.merkerBit, desired) with
                    | Error msg -> Error $"Write failed: {msg}"
                    | Ok () ->
                        match client.ReadBit(Tags.merkerBit) with
                        | Ok verify ->
                            ignore (client.WriteBit(Tags.merkerBit, original))
                            Ok (original, desired, verify)
                        | Error msg ->
                            ignore (client.WriteBit(Tags.merkerBit, original))
                            Error $"Verification read failed: {msg}")
        match CH.unwrap connectionError result with
        | Ok (original, desired, verify) ->
            Assert.NotEqual(original, desired)
            Assert.Equal(desired, verify)
        | Error msg ->
            TH.failWithLogsWithResult result $"Merker bit roundtrip failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Write merker byte restores original`` () =
        let result =
            CH.runWithClient (fun client ->
                match client.ReadByte(Tags.merkerByte) with
                | Error msg -> Error $"Initial read failed: {msg}"
                | Ok original ->
                    let desired = byte ((int original + 1) % 255)
                    match client.WriteByte(Tags.merkerByte, desired) with
                    | Error msg -> Error $"Write failed: {msg}"
                    | Ok () ->
                        System.Threading.Thread.Sleep(50) // Small delay to ensure write completes
                        match client.ReadByte(Tags.merkerByte) with
                        | Ok verify ->
                            ignore (client.WriteByte(Tags.merkerByte, original))
                            Ok (original, desired, verify)
                        | Error msg ->
                            ignore (client.WriteByte(Tags.merkerByte, original))
                            Error $"Verification read failed: {msg}")
        match CH.unwrap connectionError result with
        | Ok (original, desired, verify) ->
            Assert.Equal(desired, verify)
            Assert.NotEqual(original, desired)
        | Error msg ->
            TH.failWithLogsWithResult result $"Merker byte write failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Write merker word roundtrip`` () =
        let result =
            CH.runWithClient (fun client ->
                match client.ReadInt16(Tags.merkerWord) with
                | Error msg -> Error $"Initial read failed: {msg}"
                | Ok original ->
                    let desired = if original = Int16.MaxValue then Int16.MinValue else original + 1s
                    match client.WriteInt16(Tags.merkerWord, desired) with
                    | Error msg -> Error $"Write failed: {msg}"
                    | Ok () ->
                        System.Threading.Thread.Sleep(50) // Small delay to ensure write completes
                        match client.ReadInt16(Tags.merkerWord) with
                        | Ok verify ->
                            ignore (client.WriteInt16(Tags.merkerWord, original))
                            Ok (original, desired, verify)
                        | Error msg ->
                            ignore (client.WriteInt16(Tags.merkerWord, original))
                            Error $"Verification read failed: {msg}")
        match CH.unwrap connectionError result with
        | Ok (original, desired, verify) ->
            Assert.Equal(desired, verify)
        | Error msg ->
            TH.failWithLogsWithResult result $"Merker word write failed: {msg}"
    
    [<RequiresS7PLC>]
    let ``Write DB block`` () =
        let payload =
            [| 0xAAuy; 0xBBuy; 0xCCuy; 0xDDuy |]
        let result =
            CH.runWithClient (fun client ->
                match client.WriteDB(Tags.dbNumber, Tags.dbStartByte, payload) with
                | Ok () ->
                    match client.ReadDB(Tags.dbNumber, Tags.dbStartByte, payload.Length) with
                    | Ok verify -> Ok (payload, verify)
                    | Error msg -> Error $"Read-back failed: {msg}"
                | Error msg -> Error $"DB write failed: {msg}")
        match CH.unwrap connectionError result with
        | Ok (expected, actual) ->
            Assert.Equal<byte[]>(expected, actual)
        | Error msg ->
            TH.failWithLogsWithResult result $"DB block write failed: {msg}"
