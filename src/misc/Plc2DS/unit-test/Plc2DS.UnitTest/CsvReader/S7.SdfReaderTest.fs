namespace T

open System.IO
open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Plc2DS
open Dual.Plc2DS.S7

module S7Sdf =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", "S7", file)

    type T() =
        [<Test>]
        member _.``Col9Format`` () =
            // "A","%I0.0","Bool","True","True","False","A_Comment","","True"
            let col9 = "\"A\",\"%I0.0\",\"Bool\",\"True\",\"True\",\"False\",\"A_Comment\",\"\",\"True\""
            let data0 = CsvReader.CreatePlcTagInfo(col9)
            data0.Name === "A"
            data0.Address === "%I0.0"
            data0.DataType === "Bool"
            data0.Comment === "A_Comment"

        [<Test>]
        member _.``Col4Format`` () =
            // "#5_M TL 핀전진단이상","M 750.0","BOOL",""
            let col4 = "\"#5_M TL 핀전진단이상\",\"M 750.0\",\"BOOL\",\"\""
            let data0 = CsvReader.CreatePlcTagInfo(col4)
            data0.Name === "#5_M TL 핀전진단이상"
            data0.Address === "M 750.0"
            data0.DataType === "BOOL"
            data0.Comment === ""

        [<Test>]
        member _.``ColXFormat`` () =
            let col4 = "\"\",\"#5_M TL 핀전진단이상\",\"M 750.0\",\"BOOL\",\"\""
            (fun () -> CsvReader.CreatePlcTagInfo(col4) |> ignore) |> ShouldFailWithSubstringT "Invalid file format"


        [<Test>]
        member _.``Minimal`` () =
            let sdfPath = getFile("S7.min.sdf")
            let data = CsvReader.ReadCommentCSV(sdfPath)
            data.Length === 4
            data[0].Name === "#5_M TL 핀전진단이상"
            data[0].Address === "M 750.0"
            data[0].DataType === "BOOL"
            data[0].Comment === ""

            data[3].Name === "#5_Q LM 1차 클1 풀림"
            data[3].Address === "Q 216.3"
            data[3].DataType === "BOOL"
            data[3].Comment === "SOL2"


        member _.``Error`` () =
            let errCase = "\"한글 심볼\",\"%I0.3\",\"Bool\",\"True\",\"True\",\"False\",\"큰따옴표 \"한글\",\"코멘트\", 코마, 'single'\",\"\",\"True\""
            let cols = Csv.ParseLine(errCase)
            cols.Length === 4
