namespace Ev2.PLC.Mapper.Test

open System
open System.IO
open Xunit
open FsUnit.Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces
open Ev2.PLC.Mapper
open TestHelpers

module MapperFactoryTests =

    let createFactory() =
        let logger = createLogger<MapperFactory>()
        MapperFactory(logger)

    [<Fact>]
    let ``MapperFactory should create Allen-Bradley mapper`` () =
        let factory = createFactory()

        let mapper = factory.CreateMapper(PlcVendor.AllenBradley)

        mapper |> should not' (be null)
        mapper.Vendor |> should equal PlcVendor.AllenBradley

    [<Fact>]
    let ``MapperFactory should create LS Electric mapper`` () =
        let factory = createFactory()

        let mapper = factory.CreateMapper(PlcVendor.LSElectric)

        mapper |> should not' (be null)
        mapper.Vendor |> should equal PlcVendor.LSElectric

    [<Fact>]
    let ``MapperFactory should create Mitsubishi mapper`` () =
        let factory = createFactory()

        let mapper = factory.CreateMapper(PlcVendor.Mitsubishi)

        mapper |> should not' (be null)
        mapper.Vendor |> should equal PlcVendor.Mitsubishi

    [<Fact>]
    let ``MapperFactory should create Siemens mapper`` () =
        let factory = createFactory()

        let mapper = factory.CreateMapper(PlcVendor.Siemens)

        mapper |> should not' (be null)
        mapper.Vendor |> should equal PlcVendor.Siemens

    [<Fact>]
    let ``MapperFactory should detect vendor from file extension`` () =
        let factory = createFactory()

        factory.DetectVendorFromFile("project.L5K") |> should equal (Some PlcVendor.AllenBradley)
        factory.DetectVendorFromFile("project.xml") |> should equal (Some PlcVendor.LSElectric)
        factory.DetectVendorFromFile("project.csv") |> should equal (Some PlcVendor.Mitsubishi)
        factory.DetectVendorFromFile("project.awl") |> should equal (Some PlcVendor.Siemens)
        factory.DetectVendorFromFile("project.txt") |> should equal None

    [<Fact>]
    let ``MapperFactory should create mapper from file`` () =
        let factory = createFactory()
        let tempFile = createTempFile ".L5K" TestData.sampleL5K

        try
            let mapper = factory.CreateMapperFromFile(tempFile)

            mapper |> Option.isSome |> should be True
            mapper.Value.Vendor |> should equal PlcVendor.AllenBradley
        finally
            cleanupTempFile tempFile

    [<Fact>]
    let ``MapperFactory should list available mappers`` () =
        let factory = createFactory()

        let mappers = factory.GetAvailableMappers()

        mappers |> should contain PlcVendor.AllenBradley
        mappers |> should contain PlcVendor.LSElectric
        mappers |> should contain PlcVendor.Mitsubishi
        mappers |> should contain PlcVendor.Siemens
        mappers |> List.length |> should equal 4

    [<Fact>]
    let ``MapperFactory should create mapper with configuration`` () =
        let factory = createFactory()
        let config = TestData.createTestMappingConfig(PlcVendor.AllenBradley)

        let mapper = factory.CreateMapperWithConfig(config)

        mapper.Vendor |> should equal PlcVendor.AllenBradley
        mapper.Configuration.Rules.VariableNaming.Prefix |> should equal (Some "PLC_")

    [<Fact>]
    let ``MapperFactory should support mapper registration`` () =
        let factory = createFactory()

        // Custom mapper implementation
        let customMapper = {
            new IPlcMapper with
                member _.Vendor = PlcVendor.AllenBradley
                member _.Configuration = TestData.createTestMappingConfig(PlcVendor.AllenBradley)
                member _.MapProject(project) = async { return project }
                member _.MapSystem(system) = async { return system }
                member _.MapVariables(vars) = async { return vars }
                member _.MapLogic(logic) = async { return logic }
        }

        factory.RegisterMapper(PlcVendor.AllenBradley, fun _ -> customMapper)
        let mapper = factory.CreateMapper(PlcVendor.AllenBradley)

        mapper |> should be (identical customMapper)

    [<Fact>]
    let ``MapperFactory should validate mapper compatibility`` () =
        let factory = createFactory()

        let abFile = "project.L5K"
        let xmlFile = "project.xml"

        factory.IsCompatibleMapper(PlcVendor.AllenBradley, abFile) |> should be True
        factory.IsCompatibleMapper(PlcVendor.AllenBradley, xmlFile) |> should be False
        factory.IsCompatibleMapper(PlcVendor.LSElectric, xmlFile) |> should be True

    [<Fact>]
    let ``MapperFactory should get mapper capabilities`` () =
        let factory = createFactory()

        let capabilities = factory.GetMapperCapabilities(PlcVendor.AllenBradley)

        capabilities.SupportedFileTypes |> should contain ".L5K"
        capabilities.SupportedFileTypes |> should contain ".L5X"
        capabilities.SupportsVariableMapping |> should be True
        capabilities.SupportsLogicMapping |> should be True

    [<Fact>]
    let ``MapperFactory should cache mapper instances`` () =
        let factory = createFactory()

        let mapper1 = factory.CreateMapper(PlcVendor.AllenBradley)
        let mapper2 = factory.CreateMapper(PlcVendor.AllenBradley)

        mapper1 |> should be (identical mapper2)  // Same instance from cache

    [<Fact>]
    let ``MapperFactory should clear mapper cache`` () =
        let factory = createFactory()

        let mapper1 = factory.CreateMapper(PlcVendor.AllenBradley)
        factory.ClearCache()
        let mapper2 = factory.CreateMapper(PlcVendor.AllenBradley)

        mapper1 |> should not' (be (identical mapper2))  // Different instances after cache clear

    [<Fact>]
    let ``MapperFactory should create batch mapper`` () =
        let factory = createFactory()
        let files = [
            ("project1.L5K", PlcVendor.AllenBradley)
            ("project2.xml", PlcVendor.LSElectric)
            ("project3.csv", PlcVendor.Mitsubishi)
        ]

        let batchMapper = factory.CreateBatchMapper(files)

        batchMapper.MapperCount |> should equal 3
        batchMapper.SupportedVendors |> should contain PlcVendor.AllenBradley
        batchMapper.SupportedVendors |> should contain PlcVendor.LSElectric
        batchMapper.SupportedVendors |> should contain PlcVendor.Mitsubishi

    [<Fact>]
    let ``MapperFactory should generate factory report`` () =
        let factory = createFactory()

        let report = factory.GenerateFactoryReport()

        report |> should haveSubstring "Mapper Factory Report"
        report |> should haveSubstring "Available Mappers:"
        report |> should haveSubstring "AllenBradley"
        report |> should haveSubstring "LSElectric"
        report |> should haveSubstring "Mitsubishi"
        report |> should haveSubstring "Siemens"