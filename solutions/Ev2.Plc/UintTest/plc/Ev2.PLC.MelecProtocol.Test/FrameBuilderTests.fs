namespace Ev2.PLC.MelecProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol

module FrameBuilderTests =

    [<Fact>]
    let ``DeviceAccessCommand values`` () =
        Assert.Equal<uint16>(0x0401us, uint16 DeviceAccessCommand.BatchRead)
        Assert.Equal<uint16>(0x1401us, uint16 DeviceAccessCommand.BatchWrite)
        Assert.Equal<uint16>(0x0403us, uint16 DeviceAccessCommand.RandomRead)
        Assert.Equal<uint16>(0x1402us, uint16 DeviceAccessCommand.RandomWrite)

    [<Fact>]
    let ``MxDevice IsHexa detects hexadecimal devices`` () =
        Assert.True(MxDevice.IsHexa MxDevice.X)
        Assert.True(MxDevice.IsHexa MxDevice.Y)
        Assert.False(MxDevice.IsHexa MxDevice.D)
