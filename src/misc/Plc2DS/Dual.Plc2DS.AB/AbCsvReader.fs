namespace Dual.Plc2DS.AB

open System
open System.Text.RegularExpressions

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS
open Dual.Common.Core

[<AutoOpen>]
module Ab =
    let private sampleHeaders = [|
        "remark,\"CSV-Import-Export\""
        "remark,\"Date = Thu Mar  6 09:32:18 2025\""
        "remark,\"Version = RSLogix 5000 v33.00\""
        "remark,\"Owner = \""
        "remark,\"Company = \""
        "0.3"
        "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES"
    |]
    type DeviceComment = {
        Type: string
        Scope: string
        Name: string
        Description: string
        DataType: string
        Specifier: string
        Attributes: string
    } with
        interface IDeviceComment

    /// $XXXX를 유니코드 문자로 디코딩
    let decodeEncodedString (encoded: string) =
        let encoded =
            encoded
                .Replace("$$", "$")
                .Replace("$Q", "\"")
                //.Replace("$C", ",")
                .Replace("$N", "\n")
                .Replace("$T", "\t")
                .Replace("$'", "'")
        let pattern = @"\$(\w{4})" // $XXXX 패턴 감지
        Regex.Replace(encoded, pattern, fun m ->
            let hexValue = m.Groups.[1].Value
            let unicodeChar = Convert.ToInt32(hexValue, 16) |> char
            unicodeChar.ToString()
        )


    type CsvReader =
        static member ReadCommentCSV(filePath: string): DeviceComment[] =
            let parseCsvLine (line: string) =
                let pattern = """(?:^|,)(?:"([^"]*)"|([^,]*))""" // 쉼표로 시작하는 경우도 감지
                Regex.Matches(line, pattern)
                |> Seq.cast<Match>
                |> Seq.map (fun m ->
                    if m.Groups.[1].Success then m.Groups.[1].Value // 따옴표 안 값
                    else if m.Groups.[2].Success then m.Groups.[2].Value // 일반 값
                    else "" // 빈 값
                )
                |> Seq.map decodeEncodedString
                |> Seq.toArray


            let skipLines = 7
            let headers = File.PeekLines(filePath, 0, skipLines)
            match headers[skipLines-1] with     // header 마지막 줄
            | "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES" ->
                let lines = File.PeekLines(filePath, skipLines)
                lines |> map(fun line ->
                    let cols = parseCsvLine line
                    {   Type = cols[0]; Scope = cols[1]; Name = cols[2]; Description = cols[3]
                        DataType = cols[4]; Specifier = cols[5]; Attributes = cols[6] } )

            | _ -> failwith "Invalid file format"




