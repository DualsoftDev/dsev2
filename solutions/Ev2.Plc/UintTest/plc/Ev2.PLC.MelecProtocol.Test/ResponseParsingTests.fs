namespace Ev2.PLC.MelecProtocol.Tests

open Xunit
open Ev2.PLC.Common.Types
open Ev2.PLC.MelecProtocol

module ResponseParsingTests =

    [<Fact>]
    let ``MxDeviceInfo Create returns None for invalid address`` () =
        Assert.True(MxDeviceInfo.Create "Invalid" |> Option.isNone)

    [<Fact>]
    let ``MxTagParser Parse returns normalized address`` () =
        match MxTagParser.Parse "D1" with
        | Some addr -> Assert.Equal("D1", addr)
        | None -> Assert.True(false, "Expected successful parse")

    [<Fact>]
    let ``MxTagParser TryParseToMxTag handles lowercase input`` () =
        match MxTagParser.TryParseToMxTag "d2" with
        | Some tag -> Assert.Equal("D2", tag.Address)
        | None -> Assert.True(false, "Expected tag parse for lowercase d2")
