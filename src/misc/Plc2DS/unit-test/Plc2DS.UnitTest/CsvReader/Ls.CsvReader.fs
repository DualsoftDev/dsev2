namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Common.Core.FS
open Dual.Plc2DS.LS

module LsCsv =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", "LS", file)


    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let csvPath = getFile("min.csv")
            let data = CsvReader.ReadCommentCSV(csvPath)
            data |> Array.iter (tracefn "%A")

            data.Length === 1
            data[0].Type     === "Tag"
            data[0].Scope    === "GlobalVariable"
            data[0].Variable === "AGV_M_I_AUTO_MODE"
            data[0].Address  === "%MW24000.0"
            data[0].DataType === "BOOL"
            data[0].Property === ""
            data[0].Comment  === "AGV 자동모드"




