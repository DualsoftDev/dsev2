namespace Ev2.PLC.ABProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol

module ParserAndBatchingTests =

    [<Fact>]
    let ``binding for bool produces single bit element`` () =
        let binding = AbConversions.bindingFor PlcTagDataType.Bool
        Assert.Equal(PlcTagDataType.Bool, binding.ElementDataType)
        Assert.Equal(TagDataType.Bit, binding.TagDataType)
        Assert.False(binding.FlattenAsBytes)
        Assert.Equal(1, binding.ElementCount)

    [<Fact>]
    let ``binding for byte buffer flattens into bytes`` () =
        let binding = AbConversions.bindingFor (PlcTagDataType.Bytes 4)
        Assert.Equal(PlcTagDataType.UInt8, binding.ElementDataType)
        Assert.Equal(TagDataType.UInt8, binding.TagDataType)
        Assert.True(binding.FlattenAsBytes)
        Assert.Equal(4, binding.ElementCount)

    [<Fact>]
    let ``binding for int16 array preserves element type`` () =
        let binding = AbConversions.bindingFor (PlcTagDataType.Array(PlcTagDataType.Int16, 3))
        Assert.Equal(PlcTagDataType.Int16, binding.ElementDataType)
        Assert.Equal(TagDataType.Int16, binding.TagDataType)
        Assert.False(binding.FlattenAsBytes)
        Assert.Equal(3, binding.ElementCount)
