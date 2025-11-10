namespace Ev2.PLC.ABProtocol.Tests

open System
open System.Threading
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol
open TestEndpoints

module ScanManagerTests =

    [<Fact>]
    let ``Scan manager populates tag map`` () =
        let settings = createConnectionSettings ()
        let manager = new AbScanManager(settings, 100, false)
        let tags = [ createTagInfo "bit1" PlcTagDataType.Bool false false ]
        try
            let tagMap = manager.StartScan(plcIp, tags)
            Thread.Sleep 500
            Assert.True(tagMap.ContainsKey "bit1", "Scan manager did not register the expected tag")
        with
        | :? InvalidOperationException as ex -> Assert.True(false, $"Allen-Bradley scan manager failed ({ex.Message})")
