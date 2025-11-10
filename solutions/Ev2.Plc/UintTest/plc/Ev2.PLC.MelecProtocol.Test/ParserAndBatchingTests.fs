namespace Ev2.PLC.MelecProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol
open Ev2.PLC.MelecProtocol.MelecBatching

module ParserAndBatchingTests =

    [<Fact>]
    let ``MxDeviceInfo parses decimal word address`` () =
        match MxDeviceInfo.Create "D100" with
        | Some info ->
            Assert.Equal(MxDevice.D, info.Device)
            Assert.Equal(MxDeviceType.MxWord, info.DataTypeSize)
            Assert.Equal(1600, info.BitOffset)
        | None -> Assert.True(false, "Expected parsing success for D100")

    [<Fact>]
    let ``MxDeviceInfo parses hexadecimal bit address`` () =
        match MxDeviceInfo.Create "X10" with
        | Some info ->
            Assert.Equal(MxDevice.X, info.Device)
            Assert.Equal(MxDeviceType.MxBit, info.DataTypeSize)
            Assert.Equal(0x10, info.BitOffset)
        | None -> Assert.True(false, "Expected parsing success for X10")

    [<Fact>]
    let ``MxTagParser creates tag from TagInfo`` () =
        let tagInfo =
            { Name = "MxTag"
              Address = "D10"
              Comment = ""
              DataType = None
              IsLowSpeedArea = false
              IsOutput = false }
        match MxTagParser.TryParseToMxTag tagInfo with
        | Some tag ->
            Assert.Equal("D10", tag.Address)
            Assert.Equal(160, tag.BitOffset)
        | None -> Assert.True(false, "Expected tag parsing success")

    [<Fact>]
    let ``prepareReadBatches groups tags by DWord`` () =
        let tags =
            [| for i in 0..5 ->
                let address = $"D{i}"
                let info = MxDeviceInfo.Create(address) |> Option.get
                MelsecTag($"Tag{i}", info) |]

        let batches = prepareReadBatches tags
        Assert.Equal(1, batches.Length)
        Assert.Equal(6, batches.[0].Tags.Length)

    [<Fact>]
    let ``ParseFromSegment recreates address`` () =
        let address = MxTagParser.ParseFromSegment("D", 160, 16)
        Assert.Equal("D10", address)

    [<Fact>]
    let ``Invalid address returns None`` () =
        Assert.True(MxDeviceInfo.Create "QXYZ123" |> Option.isNone)
