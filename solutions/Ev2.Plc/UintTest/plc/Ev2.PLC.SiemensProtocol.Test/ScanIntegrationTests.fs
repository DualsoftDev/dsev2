namespace Ev2.PLC.SiemensProtocol.Tests

open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.SiemensProtocol
open TestEndpoints
open System

module ScanIntegrationTests =

    [<Fact>]
    let ``Scan refreshes value`` () =
        let settings = createConnectionSettings siemensS1500
        let scan = new SiemensPlcScan(siemensS1500.Ip, settings, 100, false)
        try
            try
                let tagInfos = [ createTagInfo Addresses.bit PlcTagDataType.Bool false false ]
                let tagMap = scan.PrepareTags(tagInfos)
                try
                    scan.ReadHighSpeedAreaAsync 0 |> Async.RunSynchronously
                with
                | :? InvalidOperationException as ex -> raise (new XunitException($"Siemens scan read failed ({ex.Message})"))
                let tag = tagMap.[Addresses.bit]
                Assert.NotNull(tag)
                Assert.NotNull(tag.Value)
            with
            | :? InvalidOperationException as ex -> raise (new XunitException($"Siemens scan setup failed ({ex.Message})"))
        finally
            scan.ConnectionClose()
