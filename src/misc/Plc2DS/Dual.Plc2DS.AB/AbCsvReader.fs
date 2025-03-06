namespace Dual.Plc2DS.AB

open System
open System.Text.RegularExpressions

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS
open Dual.Common.Core

[<AutoOpen>]
module Ab =
    (* 샘플 CSV format

remark,"CSV-Import-Export"
remark,"Date = Thu Mar  6 10:47:40 2025"
remark,"Version = RSLogix 5000 v33.00"
remark,"Owner = "
remark,"Company = "
0.3
TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES
TAG,,A,"$D55C$AE00A","DINT","","(RADIX := Decimal, Constant := false, ExternalAccess := Read/Write)"

    *)


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
            let skipLines = 7
            let header = "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES"
            match File.TryReadUntilHeader(filePath, header) with
            | Some headers ->
                let skipLines = headers.Length + 1
                let lines = File.PeekLines(filePath, skipLines)
                lines |> map(fun line ->
                    let cols = Csv.ParseLine line |> map decodeEncodedString
                    {   Type = cols[0]; Scope = cols[1]; Name = cols[2]; Description = cols[3]
                        DataType = cols[4]; Specifier = cols[5]; Attributes = cols[6] } )
            | None ->
                failwith $"ERROR: failed to find header {header}"





