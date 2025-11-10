namespace Ev2.PLC.ABProtocol.Tests

open System
open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.ABProtocol

module FrameBuilderTests =

    [<Fact>]
    let ``normalizeToScalar converts bool objects`` () =
        let value = AbConversions.normalizeToScalar PlcTagDataType.Bool (box true)
        match value with
        | ScalarValue.BoolValue flag -> Assert.True(flag)
        | _ -> Assert.True(false, "Bool conversion failed")

    [<Fact>]
    let ``normalizeToScalar converts int16 array`` () =
        let source : obj = [| int16 10; int16 11 |] :> obj
        let value = AbConversions.normalizeToScalar (PlcTagDataType.Array(PlcTagDataType.Int16, 2)) source
        match value with
        | ScalarValue.ArrayValue items ->
            Assert.Equal(2, items.Length)
            Assert.Contains(items, fun scalar -> scalar = ScalarValue.Int16Value 10s)
        | _ -> Assert.True(false, "Array conversion failed")

    [<Fact>]
    let ``normalizeToScalar rejects invalid byte buffers`` () =
        let source : obj = [| 1uy; 2uy; 3uy |] :> obj
        Assert.Throws<ArgumentException>(fun () -> AbConversions.normalizeToScalar (PlcTagDataType.Bytes 4) source |> ignore)
        |> ignore
