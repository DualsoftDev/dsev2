namespace Ev2.PLC.MelecProtocol.Tests

open System
open System.IO
open System.Net.Sockets
open System.Threading
open Xunit
open Xunit.Sdk
open Ev2.PLC.Driver.Base
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol
open TestEndpoints

module IntegrationScanTests =

    let random = Random()
    let defaultPort = uint16 TestEndpoints.defaultPort

    let generateTagValue (address: string) : obj =
        match MxDeviceInfo.Create address with
        | Some info ->
            match info.DataTypeSize with
            | MxDeviceType.MxBit
            | MxDeviceType.MxDotBit -> box (random.Next(0, 2) = 1)
            | MxDeviceType.MxWord -> box (random.Next(0, 0xFFFF))
        | None -> box 0

    [<Fact>]
    let ``Integration - Random Write & Read for 3 Seconds`` () =
        let scanMgr = MxScanManager(20, 3000, false, defaultPort, false)
        let ip = TestEndpoints.ipMitsubishiEthernet
        let tags =
            [ "%D300"; "%D301"; "%D302"; "%D303"; "%M100"; "%M101"; "%X20"; "%Y20" ]

        try
            let result = scanMgr.StartScanReadOnly(ip, tags)

            match scanMgr.GetScanner(ip) with
            | Some scanner ->
                scanner.TagValueChanged.Add(fun evt ->
                    match evt.Tag with
                    | :? MelsecTag as tag -> 
                        let tag = evt.Tag
                        printfn $"[Read] {tag.Address} → {tag.Value}"
                    | _ -> ())
            | None -> ()

            let startTime = DateTime.Now
            let duration = TimeSpan.FromSeconds(3.0)
            printfn "\n[✓] MELSEC 랜덤 Write 테스트 시작: %O\n" startTime

            while DateTime.Now - startTime < duration do
                for KeyValue(_, tag) in result do
                    let value = generateTagValue tag.Address
                    tag.SetWriteValue(value)
                Thread.Sleep(10)

            scanMgr.StopScan(ip)
            printfn "\n[✓] MELSEC 랜덤 Write 테스트 완료: %O\n" DateTime.Now
        with
        | :? SocketException as ex -> Assert.True(false, sprintf "MELSEC scan integration failed (%s)" ex.Message)
        | :? IOException as ex -> Assert.True(false, sprintf "MELSEC scan integration IO failure (%s)" ex.Message)
