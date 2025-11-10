namespace Ev2.S7Protocol.Tests

open Xunit
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Protocol
open Ev2.S7Protocol.Tests.TestHelpers

module S7ProtocolTests =

    [<Fact>]
    let ``buildCotpConnect encodes TSAP parameters`` () =
        log "Validating COTP connect frame structure."
        let localTsap = 0x0123
        let remoteTsap = 0x4567
        let frame = S7Protocol.buildCotpConnect localTsap remoteTsap
        log $"COTP frame length: {frame.Length}"
        // TPKT header indicates total frame length 22 bytes (0x16).
        assertEqual 22 frame.Length
        assertEqual 0x03uy frame.[0]
        assertEqual 0x00uy frame.[1]
        assertEqual 0x00uy frame.[2]
        assertEqual 0x16uy frame.[3]

        // Local TSAP parameter (0xC1) and remote TSAP parameter (0xC2).
        let localIndex = Array.findIndex (fun b -> b = 0xC1uy) frame
        assertTrue (localIndex > 0) "Local TSAP parameter not found."
        assertEqual 0x02uy frame.[localIndex + 1]
        assertEqual (byte (localTsap >>> 8)) frame.[localIndex + 2]
        assertEqual (byte localTsap) frame.[localIndex + 3]

        let remoteIndex = Array.findIndex (fun b -> b = 0xC2uy) frame
        assertTrue (remoteIndex > 0) "Remote TSAP parameter not found."
        assertEqual 0x02uy frame.[remoteIndex + 1]
        assertEqual (byte (remoteTsap >>> 8)) frame.[remoteIndex + 2]
        assertEqual (byte remoteTsap) frame.[remoteIndex + 3]
    [<Fact>]
    let ``buildReadBytesRequest encodes overflow for large DB addresses`` () =
        log "Checking read request overflow encoding for large DB address."
        let startByte = 65_535
        let request = S7Protocol.buildReadBytesRequest DataArea.DataBlock 1 startByte 4

        // Parameter length should account for the function header plus variable specification.
        assertEqual 0x00uy request.[6]
        assertEqual 0x0Euy request.[7]
        assertEqual 0x00uy request.[8]
        assertEqual 0x00uy request.[9]

        // Variable specification begins at offset 12.
        assertEqual 0x12uy request.[12]
        assertEqual 0x0Auy request.[13]
        assertEqual 0x10uy request.[14]
        assertEqual 0x02uy request.[15]

        // Ensure overflow byte follows the 24-bit addressing rules.
        assertEqual 0x84uy request.[20] // Area code for DB
        assertEqual 0x07uy request.[21] // Expected overflow
        assertEqual 0xFFuy request.[22]
        assertEqual 0xF8uy request.[23]
        log (
            request.[12..23]
            |> Array.map (fun b -> $"0x{b:X2}")
            |> String.concat " "
            |> sprintf "Variable spec bytes: %s")

    [<Fact>]
    let ``buildWriteBytesRequest emits correct data header`` () =
        log "Validating write request data header metadata."
        let payload = [| 0x01uy; 0x02uy; 0x03uy |]
        let request = S7Protocol.buildWriteBytesRequest DataArea.Merker 0 100 payload

        // Parameter length is still 14 bytes and data length equals payload + metadata.
        assertEqual 0x00uy request.[6]
        assertEqual 0x0Euy request.[7]
        assertEqual 0x00uy request.[8]
        assertEqual 0x07uy request.[9]

        // Variable specification encodes the start address correctly.
        assertEqual 0x83uy request.[20]
        assertEqual 0x00uy request.[21]
        assertEqual 0x03uy request.[22]
        assertEqual 0x20uy request.[23]

        // Data section starts at offset 24 and encodes bit-length metadata.
        assertEqual 0x00uy request.[24]
        assertEqual 0x04uy request.[25]
        assertEqual 0x00uy request.[26]
        assertEqual 0x18uy request.[27]
        assertEqual payload request.[28..30]
    [<Fact>]
    let ``buildWriteBitRequest encodes bit position`` () =
        log "Validating bit-write request addressing."
        let request = S7Protocol.buildWriteBitRequest DataArea.DataBlock 5 12 3 true

        // Parameter length 0x0E and data length 0x05.
        assertEqual 0x00uy request.[6]
        assertEqual 0x0Euy request.[7]
        assertEqual 0x00uy request.[8]
        assertEqual 0x05uy request.[9]

        // Variable spec: DB number 0x0005 and bit address (12 bytes + bit 3).
        assertEqual 0x00uy request.[18]
        assertEqual 0x05uy request.[19]
        assertEqual 0x84uy request.[20]
        assertEqual 0x00uy request.[21]
        assertEqual 0x00uy request.[22]
        assertEqual 0x63uy request.[23]

        // Data section is 4 bytes with a single bit value.
        assertEqual 0x00uy request.[24]
        assertEqual 0x03uy request.[25]
        assertEqual 0x00uy request.[26]
        assertEqual 0x01uy request.[27]
        assertEqual 0x01uy request.[28]
    [<Fact>]
    let ``buildS7Setup uses consistent job header`` () =
        log "Validating S7 setup frame header."
        let frame = S7Protocol.buildS7Setup()

        // Total length equals TPKT length (0x19 == 25 bytes).
        assertEqual 25 frame.Length

        // TPKT header prefix.
        assertEqual 0x03uy frame.[0]
        assertEqual 0x00uy frame.[1]

        // After TPKT/COTP (7 bytes), expect S7 job header.
        assertEqual 0x32uy frame.[7]
        assertEqual 0x01uy frame.[8]

        // Parameter length = 8, data length = 0.
        assertEqual 0x00uy frame.[13]
        assertEqual 0x08uy frame.[14]
        assertEqual 0x00uy frame.[15]
        assertEqual 0x00uy frame.[16]
