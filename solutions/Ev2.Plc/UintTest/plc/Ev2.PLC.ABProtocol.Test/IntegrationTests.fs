namespace Ev2.PLC.ABProtocol.Tests

open System
open System.Threading.Tasks
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Common.Interfaces
open TestEndpoints

module IntegrationTests =

    let private runTask task = task |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    let ``Driver connects and reads bit tag`` () =
        let plcId = "AB-INTEGRATION"
        let driver = createDriver plcId
        let driverIfc = driver :> IPlcDriver
        try
            let connected = driverIfc.ConnectAsync() |> runTask
            Assert.True(connected, "Driver failed to connect to Allen-Bradley PLC")

            let tagConfig = createTagConfiguration plcId "bit1" PlcTagDataType.Bool
            let result = driverIfc.ReadTagAsync(tagConfig) |> runTask
            Assert.True(result.Quality.IsGood, $"Tag read failed with quality {result.Quality}")
            Assert.Equal(tagConfig.Id, result.TagId)
        finally
            driverIfc.DisconnectAsync() |> runTask |> ignore
