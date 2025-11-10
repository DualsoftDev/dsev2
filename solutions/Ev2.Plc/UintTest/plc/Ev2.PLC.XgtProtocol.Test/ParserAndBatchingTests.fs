namespace Ev2.PLC.XgtProtocol.Tests

open Xunit
open Ev2.PLC.XgtProtocol
open Ev2.PLC.XgtProtocol.XgtBatching

module ParserAndBatchingTests =

    [<Fact>]
    let ``Detects XGI address style`` () =
        let tags = seq { "%MX0"; "%MW10" }
        let isXgi = LsTagParser.IsXGI tags
        Assert.True(isXgi, "Addresses with % prefix should be treated as XGI")

    [<Fact>]
    let ``Parses XGI tag data`` () =
        let device, dataBits, offset = LsXgiTagParser.Parse "%MD100"
        Assert.Equal("M", device)
        Assert.Equal(32, dataBits)
        Assert.Equal(100 * 32, offset)

    [<Fact>]
    let ``Parses XGK tag data`` () =
        let device, dataBits, offset = LsXgkTagParser.Parse("D100", false)
        Assert.Equal("D", device)
        Assert.Equal(16, dataBits)
        Assert.Equal(100 * 16, offset)

    [<Fact>]
    let ``Normalises shorthand XGK tag`` () =
        let normalised = LsXgkTagParser.ParseValidText("M1", true)
        Assert.Equal("M00001", normalised)

    [<Fact>]
    let ``prepareLWordBatches groups tags by L-word`` () =
        let tags = [| for i in 0..31 -> XgtTag(sprintf "%%ML%05d" i, true, false) |]
        let batches = prepareLWordBatches tags
        Assert.Equal(2, batches.Length)
        let firstBatchTags = batches |> Array.map (fun batch -> batch.Tags.[0].LWordTag)
        Assert.Equal<string[]>([| "%ML0"; "%ML16" |], firstBatchTags)

    [<Fact>]
    let ``prepareQWordBatches groups tags by Q-word`` () =
        let tags = [| for i in 0..127 -> XgtTag(sprintf "%%MX%d" (i * 128), true, false) |]
        let batches = prepareQWordBatches tags
        Assert.Equal(2, batches.Length)
        let firstBatchTags = batches |> Array.map (fun batch -> batch.Tags.[0].QWordTag)
        Assert.Equal<string[]>([| "%MQ0"; "%MQ64" |], firstBatchTags)
