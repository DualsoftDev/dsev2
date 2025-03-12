namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS

module Batch =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let sm = Semantic.Create()


    type B() =
        [<Test>]
        member _.``Minimal`` () =
            let inputTags:IPlcTag[] =
                let csv = Path.Combine(dataDir, "BB 메인제어반.csv")
                CsvReader.Read(Vendor.LS, csv, addressFilter = fun addr -> addr.StartsWith("%Q"))
            let inputTagNames = inputTags |> map _.GetName()


            do
                for tag in inputTags do
                    match tag.TryGetFDA(sm) with
                    | Some (f, d, a) ->
                        tracefn $"{tag.GetName()}: {f}, {d}, {a}"
                    | None ->
                        logWarn $"------------ {tag.GetName()}: Failed to match"
            noop()




