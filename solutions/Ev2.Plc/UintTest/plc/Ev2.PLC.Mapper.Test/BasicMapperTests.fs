namespace Ev2.PLC.Mapper.Test

open Xunit
open FsUnit.Xunit
open System
open Microsoft.Extensions.Logging.Abstractions
open DSPLCServer.Core
open DSPLCServer.Common
open Ev2.PLC.Common.Types

/// Basic tests for the new PLC mapper implementation
module BasicMapperTests =

    [<Fact>]
    let ``PlcMapperFactory should create mapper for different vendors`` () =
        let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
        
        let siemensFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Siemens)
        let mitsubishiFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
        let allenBradleyFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.AllenBradley)
        let lsElectricFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.LSElectric)
        
        siemensFormat.Vendor |> should equal PlcVendor.Siemens
        mitsubishiFormat.Vendor |> should equal PlcVendor.Mitsubishi
        allenBradleyFormat.Vendor |> should equal PlcVendor.AllenBradley
        lsElectricFormat.Vendor |> should equal PlcVendor.LSElectric

    [<Fact>]
    let ``PlcMapper should create successfully`` () =
        let mapperFactory = PlcMapperFactory(NullLoggerFactory.Instance)
        let addressFormat = mapperFactory.CreateDefaultAddressFormat(PlcVendor.Mitsubishi)
        
        let config = {
            PlcId = "PLC001"
            Vendor = PlcVendor.Mitsubishi
            TagMappings = []
            AddressMappings = []
            AddressFormat = addressFormat
            EnableAutoMapping = true
            DefaultDataType = PlcDataType.Int16
        }
        
        match mapperFactory.CreateMapper(config) with
        | Result.Ok mapper ->
            // Basic test - just verify mapper was created
            mapper |> should not' (be null)
        | Result.Error error -> failwith $"Mapper creation failed: {error}"

    [<Fact>]
    let ``Basic test should always pass`` () =
        // Simple test to ensure basic infrastructure works
        1 + 1 |> should equal 2