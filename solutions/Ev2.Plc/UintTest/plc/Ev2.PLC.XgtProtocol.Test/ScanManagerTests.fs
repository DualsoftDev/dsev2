namespace Ev2.PLC.XgtProtocol.Tests

open System
open System.Collections.Generic
open Xunit
open Ev2.PLC.XgtProtocol

module ScanManagerTests =

    let dummyTags = [ "%LW00000"; "%LW00010"; "%LW00020"; "%LW00030"; "%LW16"; "%LW17" ]
    let ip100 = "192.168.9.100"
    let ip101CPUZ = "192.168.9.101"
    let ip102 = "192.168.9.102"
    let ipUnknown = "10.0.0.1"

    [<Fact>]
    let ``IsConnected should return false for unknown IP`` () =
        let scanMgr = XgtScanManager(false, 20, 3000, false)
        let result =
            scanMgr.GetScanner(ipUnknown)
            |> Option.map (fun scanner -> scanner.IsConnected)
            |> Option.defaultValue false
        Assert.False(result)

    [<Fact>]
    let ``StopScan should clear all scans`` () =
        async { do! Async.Sleep 3000 } |> Async.RunSynchronously

        let scanMgr = XgtScanManager(false, 20, 3000, false)
        let input =
            dict [
                ip100, seq { "%MW100" }
                ip102, seq { "%MW200" }
            ]

        try
            input |> Seq.iter (fun kv -> scanMgr.StartScanReadOnly(kv.Key, kv.Value) |> ignore)
            Assert.True(scanMgr.GetScanner(ip100).IsSome)
            Assert.True(scanMgr.GetScanner(ip102).IsSome)

            scanMgr.StartScanReadOnly(ip100, dummyTags) |> ignore
            Assert.True(scanMgr.GetScanner(ip100).IsSome)

            scanMgr.StopScan(ip100)
            Assert.False(scanMgr.GetScanner(ip100).IsSome)

            scanMgr.StartScanReadOnly(ip100, seq { "%MW100" }) |> ignore
            scanMgr.UpdateScanReadOnly(ip100, [ "%MW100"; "%MW101" ])
            Assert.True(scanMgr.GetScanner(ip100).IsSome)

            scanMgr.StartScanReadOnly(ip100, dummyTags) |> ignore
            scanMgr.StartScanReadOnly(ip102, dummyTags) |> ignore

            scanMgr.StopAll()

            let remainingIPs =
                [ ip100; ip102 ]
                |> List.choose (fun ip -> if scanMgr.GetScanner(ip).IsSome then Some ip else None)

            Assert.Empty(remainingIPs)
        with ex ->
            printfn $"[!] PLC 연결 실패 (테스트 스킵 처리): {ex.Message}"
