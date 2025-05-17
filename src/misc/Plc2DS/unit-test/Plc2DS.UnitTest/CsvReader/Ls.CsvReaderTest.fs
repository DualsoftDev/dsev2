namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS
open Dual.Plc2DS
open Dual.Common.Base

module LsCsv =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", "LS", file)


    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let csvPath = getFile("min.csv")
            let data = CsvReader.ReadLs(csvPath)
            data |> Array.iter (tracefn "%A")

            data.Length === 1
            data[0].Type     === "Tag"
            data[0].Scope    === "GlobalVariable"
            data[0].Variable === "AGV_M_I_AUTO_MODE"
            data[0].Address  === "%MW24000.0"
            data[0].DataType === "BOOL"
            data[0].Property === ""
            data[0].Comment  === "AGV 자동모드"


module LsCsvLargeScale =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let csvFiles = Directory.GetFiles(dataDir, "*.csv")


    type T() =
        [<Test>]
        member _.``ReadMassCsvs`` () =
            for f in csvFiles do
                try
                    let data = CsvReader.ReadLs(f)
                    ()
                with ex ->
                    failwith $"Failed to parse file {f}: {ex}"

        [<Test>]
        member x.``ReadMassCsvs2`` () =
            let inputTags:IPlcTag[] =
                let csv = Path.Combine(dataDir, "BB 메인제어반.csv")
                CsvReader.Read(Vendor.LS, csv, addressFilter = fun addr -> addr.StartsWith("%I"))
            let inputTagNames = inputTags |> map _.GetName()
            noop()




