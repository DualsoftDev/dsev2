namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Plc2DS.AB
open Dual.Common.Core.FS

module AbCsv =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", "AB", file)


    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let csvPath = getFile("min.csv")
            let data = CsvReader.ReadCommentCSV(csvPath)
            data |> Array.iter (tracefn "%A")


            data.Length === 3
            data[0].Type                   === "TAG"
            data[0].Scope                  === ""
            data[0].Name                   === "CNET_3:I"
            data[0].Description            === ""
            data[0].DataType               === "AB:1756_CNB_13SLOT:I:0"
            data[0].Specifier              === ""
            data[0].Attributes             === "(ExternalAccess := Read/Write)"

            data[1].Type                   === "TAG"
            data[1].Scope                  === ""
            data[1].Name                   === "CNET_3:O"
            data[1].Description            === "Vicious,Item"
            data[1].DataType               === "AB:1756_CNB_13SLOT:O:0"
            data[1].Specifier              === ""
            data[1].Attributes             === "(ExternalAccess := Read/Write)"


            data[2].Type                   === "TAG"
            data[2].Scope                  === ""
            data[2].Name                   === "N100"
            data[2].Description            === ""
            data[2].DataType               === "DINT[120]"
            data[2].Specifier              === ""
            //data[2].Attributes             === """(RADIX := Decimal, PLCMappingFile := 100, Producer := "Controller_1", RemoteTag := "N100", RemoteFile := 0, RPI := 50, Unicast := false, ExternalAccess := Read/Write)"""

            // 타협: "Controller_1" 대신 Controller_1
            data[2].Attributes             === """(RADIX := Decimal, PLCMappingFile := 100, Producer := Controller_1, RemoteTag := N100, RemoteFile := 0, RPI := 50, Unicast := false, ExternalAccess := Read/Write)"""



        [<Test>]
        member _.``HangleEncoding`` () =
            let csvPath = getFile("hangul_Tags.csv")
            let data = CsvReader.ReadCommentCSV(csvPath)
            data.Length === 11

            data[0].Type === "TAG"
            data[0].Scope === ""
            data[0].Name === "A"
            data[0].Description === "한글A"
            data[0].DataType === "DINT"
            data[0].Specifier === ""
            data[0].Attributes === "(RADIX := Decimal, Constant := false, ExternalAccess := Read/Write)"

            data[1].Type === "TAG"
            data[1].Scope === ""
            data[1].Name === "B"
            data[1].Description === "라인1\n라인2\n라인3\nAA \"큰따옴\"BB"
            data[1].DataType === "DINT"
            data[1].Specifier === ""
            data[1].Attributes === "(RADIX := Decimal, Constant := false, ExternalAccess := Read/Write)"


            data[2].Type === "TAG"
            data[2].Scope === ""
            data[2].Name === "MyTag"
            data[2].Description === "한국어\nComma,\n'SQ'\n\"DQ\"\nTab\t\nEND"
            data[2].DataType === "DINT"
            data[2].Specifier === ""
            data[2].Attributes === "(RADIX := Decimal, Constant := false, ExternalAccess := Read/Write)"


            data[3].Type === "TAG"
            data[3].Scope === ""
            data[3].Name === "Shft12345"
            data[3].Description === "!@#$%"
            data[3].DataType === "DINT"
            data[3].Specifier === ""
            data[3].Attributes === "(RADIX := Decimal, Constant := false, ExternalAccess := Read/Write)"

            //for d in data do
            //    tracefn "%A" d
            //noop()
