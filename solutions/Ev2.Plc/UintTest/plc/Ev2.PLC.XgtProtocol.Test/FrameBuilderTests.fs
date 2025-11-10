namespace Ev2.PLC.XgtProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.XgtProtocol
open Ev2.PLC.XgtProtocol.XgtFrameBuilder
open Ev2.PLC.XgtProtocol.XgtResponse

module FrameBuilderTests =

    [<Fact>]
    let ``createMultiReadFrame builds frame`` () =
        let frameId = [| 0x01uy; 0x02uy |]
        let addresses = [| "%MX0"; "%MX1" |]
        let dataTypes = [| PlcTagDataType.Bool; PlcTagDataType.Bool |]
        let frame = createMultiReadFrame frameId addresses dataTypes
        Assert.True(frame.Length > HeaderSize)

    [<Fact>]
    let ``Creates multi-write frame`` () =
        let frameId = [| 0x01uy; 0x02uy |]
        let blocks =
            [|
                getWriteBlock "%MW0" PlcTagDataType.Int16 (ScalarValue.Int16Value 42s)
                getWriteBlock "%MW1" PlcTagDataType.Int16 (ScalarValue.Int16Value 99s)
            |]
        let frame = createMultiWriteFrame frameId blocks
        Assert.True(frame.Length > HeaderSize)
