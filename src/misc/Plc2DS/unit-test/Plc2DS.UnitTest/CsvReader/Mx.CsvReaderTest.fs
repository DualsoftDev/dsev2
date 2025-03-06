namespace T


open System.IO
open NUnit.Framework

open Dual.Common.UnitTest.FS
open Dual.Plc2DS.MX

module MxCsv =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Samples", "MX", file)

    type T() =
        [<Test>]
        member _.``Minimal`` () =
            let csvPath = getFile("GxWorks3.Tab.COMMENT.csv")
            let data = Mx.CsvReader.ReadCommentCSV(csvPath)
            data.Length === 1
            data[0].Device === "SD2037"
            data[0].Label === ""
            data[0].Comment === "로깅 설정 No.10 전송 기능 에러"


            let csvPath = getFile("GxWorks3.NoQuote3.COMMENT.csv")
            let data = Mx.CsvReader.ReadCommentCSV(csvPath)
            data.Length === 4
            data[0].Device === "M1000"
            data[0].Label === ""
            data[0].Comment === "#312    차종    RB"

            data[3].Device === "M1004"
            data[3].Label === "테스트라벨"
            data[3].Comment === "#312    차종    3DR"


            let csvPath = getFile("GxWorks3.DoubleQuote.COMMENT.csv")
            let data = Mx.CsvReader.ReadCommentCSV(csvPath)
            data.Length === 5
            data[0].Device === "X5C"
            data[0].Label === ""
            data[0].Comment === "FR:U4   Ethernet이상신호"

            data[4].Device === "X0AF"
            data[4].Label === ""
            data[4].Comment === "CC-LINK #1 Ready"



        [<Test>]
        member _.``EucKR`` () =
            let csvPath = getFile("GxWorks3.NoQuote3.EucKR.COMMENT.csv")
            let data = Mx.CsvReader.ReadCommentCSV(csvPath)
            data.Length === 4
            data[0].Device === "M1000"
            data[0].Label === ""
            data[0].Comment === "#312    차종    RB"

            data[3].Device === "M1004"
            data[3].Label === "테스트라벨"
            data[3].Comment === "#312    차종    3DR"
