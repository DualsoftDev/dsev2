namespace Dual.Plc2DS.AB

open System
open System.Text.RegularExpressions

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS
open Dual.Common.Core

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


type PlcTagInfo = {
    Type: string
    Scope: string
    Name: string
    Description: string
    DataType: string
    Specifier: string
    Attributes: string
} with
    interface IPlcTag

[<AutoOpen>]
module Ab =
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


/// AB.CsvReader
type CsvReader =
    static member CreatePlcTagInfo(line: string) : PlcTagInfo =
        let cols = Csv.ParseLine line |> map decodeEncodedString
        assert(cols.Length = 7)
        {   Type = cols[0]; Scope = cols[1]; Name = cols[2]; Description = cols[3]
            DataType = cols[4]; Specifier = cols[5]; Attributes = cols[6] }

    static member ReadCommentCSV(filePath: string): PlcTagInfo[] =
        let header = "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES"
        match File.TryReadUntilHeader(filePath, header) with
        | Some headers ->
            let skipLines = headers.Length + 1
            let lines = File.PeekLines(filePath, skipLines)
            lines |> map CsvReader.CreatePlcTagInfo
        | None ->
            failwith $"ERROR: failed to find header {header}"





