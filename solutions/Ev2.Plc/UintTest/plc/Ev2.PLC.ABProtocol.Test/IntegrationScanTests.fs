namespace Ev2.PLC.ABProtocol.Tests

open System
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol
open TestEndpoints

module IntegrationScanTests =

    [<Fact>]
    let ``Scan refreshes tag value`` () =
        let settings = createConnectionSettings ()
        let scan = new AbPlcScan(plcIp, settings, 100, false)
        try
            let tagInfos = [ createTagInfo "bit1" PlcTagDataType.Bool false false ]
            let tagMap = scan.PrepareTags(tagInfos)
            scan.ReadHighSpeedAreaAsync 0 |> Async.RunSynchronously
            let tag = tagMap.["bit1"]
            Assert.NotNull(tag)
            Assert.NotNull(tag.Value)
        with
        | :? InvalidOperationException as ex -> Assert.True(false, $"Allen-Bradley scan failed ({ex.Message})")
