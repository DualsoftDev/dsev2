namespace Ev2.PLC.MelecProtocol.Tests

open System
open System.Net.Sockets
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol
open TestEndpoints

module ScanManagerTests =

    let ipLocal = TestEndpoints.ipMitsubishiLocalEthernet
    let defaultDelay = 20
    let defaultTimeout = 3000
    let defaultPort = uint16 TestEndpoints.defaultPort

    [<Fact>]
    let ``IsConnected should return false for unknown IP`` () =
        let scanMgr = MxScanManager(defaultDelay, defaultTimeout, false, defaultPort, false)
        let result = scanMgr.GetScanner("10.0.0.1") |> Option.map (fun s -> s.IsConnected) |> Option.defaultValue false
        Assert.False(result)

    [<Fact>]
    let ``StopScan clears scan entries`` () =
        try
            let scanMgr = MxScanManager(defaultDelay, defaultTimeout, false, defaultPort, false)
            let inputs =
                dict [
                    ipLocal, seq { "%D100" }
                    TestEndpoints.ipMitsubishiEthernet, seq { "%D110" }
                ]

            inputs |> Seq.iter (fun kv -> scanMgr.StartScanReadOnly(kv.Key, kv.Value) |> ignore)

            Assert.True(scanMgr.GetScanner(ipLocal).IsSome)
            Assert.True(scanMgr.GetScanner(TestEndpoints.ipMitsubishiEthernet).IsSome)

            scanMgr.StopScan(ipLocal)
            Assert.False(scanMgr.GetScanner(ipLocal).IsSome)

            scanMgr.StartScanReadOnly(ipLocal, seq { "%D120" }) |> ignore
            scanMgr.UpdateScanReadOnly(ipLocal, [ "%D120"; "%D121" ])
            Assert.True(scanMgr.GetScanner(ipLocal).IsSome)

            scanMgr.StopAll()
            Assert.False(scanMgr.GetScanner(ipLocal).IsSome)
            Assert.False(scanMgr.GetScanner(TestEndpoints.ipMitsubishiEthernet).IsSome)
        with
        | :? SocketException as ex -> Assert.True(false, sprintf "MELSEC scan manager failed (%s)" ex.Message)
