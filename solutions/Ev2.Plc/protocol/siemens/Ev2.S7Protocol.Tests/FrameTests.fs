namespace Ev2.S7Protocol.Tests

open Xunit
open Ev2.S7Protocol.Protocol
open Ev2.S7Protocol.Core
open Ev2.S7Protocol.Tests.TestHelpers

module FrameTests =

    [<Fact>]
    let ``buildReadBitRequest creates S7ANY header`` () =
        log "Building read bit request."
        let request = S7Protocol.buildReadBitRequest DataArea.Merker 0 0
        log $"Request frame length: {request.Length} bytes"
        // S7 job header (10) + Function+Count (2) + packReadWriteSpec에서 transport size는 인덱스 3
        // 전체에서는 인덱스 12 + 3 = 15
        // buildReadBitRequest는 buildReadBytesRequest를 호출하므로 BYTE(0x02)를 사용
        if request.Length > 15 then
            assertEqual 0x02uy request.[15]  // BYTE transport size
        else
            failwith $"Frame too short: {request.Length} bytes, expected > 15"
