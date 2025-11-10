namespace Ev2.PLC.SiemensProtocol.Tests

open System.Threading
open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.SiemensProtocol
open TestEndpoints
open System

module ScanManagerTests =

    [<Fact>]
    let ``Scan manager prepares tags`` () =
        let settings = createConnectionSettings siemensS1500
        let manager = new SiemensScanManager(settings, 100, false)
        let tags = [ createTagInfo Addresses.bit PlcTagDataType.Bool false false ]
        try
            try
                let tagMap = manager.StartScan(siemensS1500.Ip, tags)
                Thread.Sleep 500
                if not (tagMap.ContainsKey Addresses.bit) then
                    raise (new XunitException("Siemens scan manager did not observe expected tag"))
            with
            | :? InvalidOperationException as ex -> raise (new XunitException($"Siemens scan manager failed ({ex.Message})"))
        finally
            manager.StopScan(siemensS1500.Ip)
