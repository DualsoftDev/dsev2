namespace Ev2.PLC.SiemensProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.SiemensProtocol

module ConversionTests =

    [<Fact>]
    let ``binding for bool returns scalar metadata`` () =
        let binding = SiemensConversions.bindingFor PlcTagDataType.Bool
        Assert.False(binding.UsesByteBuffer)
        Assert.Equal(1, binding.ElementCount)

    [<Fact>]
    let ``binding for bytes indicates buffer`` () =
        let binding = SiemensConversions.bindingFor (PlcTagDataType.Bytes 8)
        Assert.True(binding.UsesByteBuffer)
        Assert.Equal(8, binding.ElementCount)

    [<Fact>]
    let ``bool object converts to scalar`` () =
        let binding = SiemensConversions.bindingFor PlcTagDataType.Bool
        let scalar = SiemensConversions.toScalar binding (box true)
        Assert.Equal(ScalarValue.BoolValue true, scalar)

    [<Fact>]
    let ``string conversion trims null`` () =
        let binding = SiemensConversions.bindingFor (PlcTagDataType.String 10)
        let scalar = SiemensConversions.toScalar binding (box "TEST")
        Assert.Equal(ScalarValue.StringValue "TEST", scalar)
