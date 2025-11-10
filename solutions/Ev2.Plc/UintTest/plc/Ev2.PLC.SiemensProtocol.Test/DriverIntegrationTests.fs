namespace Ev2.PLC.SiemensProtocol.Tests

open Xunit
open Xunit.Sdk
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces
open TestEndpoints

module DriverIntegrationTests =

    let private run task = task |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    let ``Driver connects and reads bit`` () =
        let plcId = "SIEMENS-DRIVER"
        let driver = createDriver siemensS1500 plcId
        let ifc = driver :> IPlcDriver
        try
            let connected = run (ifc.ConnectAsync())
            if not connected then
                raise (new XunitException("Driver failed to connect"))

            let tag = createTagConfiguration plcId Addresses.bit PlcTagDataType.Bool
            let result = run (ifc.ReadTagAsync(tag))
            if not result.Quality.IsGood then
                raise (new XunitException($"Read failed: {result.Quality}"))
            Assert.Equal(tag.Id, result.TagId)
        finally
            run (ifc.DisconnectAsync())
