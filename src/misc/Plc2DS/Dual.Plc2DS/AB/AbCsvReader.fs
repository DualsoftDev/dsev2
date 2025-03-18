namespace Dual.Plc2DS.AB

open System
open System.Text.RegularExpressions

open Dual.Common.Core.FS
open Dual.Plc2DS
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

type PlcTagInfo(?typ, ?scope, ?name, ?description, ?dataType, ?specifier, ?attributes) =
    inherit PlcTagBaseFDA()

    let typ         = typ         |? ""
    let scope       = scope       |? ""
    let name        = name        |? ""
    let description = description |? ""
    let dataType    = dataType    |? ""
    let specifier   = specifier   |? ""
    let attributes  = attributes  |? ""

    member val Type        = typ         with get, set
    member val Scope       = scope       with get, set
    member val Name        = name        with get, set
    member val Description = description with get, set
    member val DataType    = dataType    with get, set
    member val Specifier   = specifier   with get, set
    member val Attributes  = attributes  with get, set




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
        let attributes =
            match cols.Length with
            | 6 ->
                assert(cols[0] = "COMMENT")
                ""
            | 7 -> cols[6]
            | _ -> failwith $"Incorrect format: {line}"
        PlcTagInfo(typ = cols[0], scope = cols[1], name = cols[2], description = cols[3],
            dataType = cols[4], specifier = cols[5], attributes = attributes)


    static member ReadCommentCSV(filePath: string): PlcTagInfo[] =
        let header = "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES"
        match File.TryReadUntilHeader(filePath, header) with
        | Some headers ->
            let skipLines = headers.Length
            File.PeekLines(filePath, skipLines)
            |> toArray
            |> map CsvReader.CreatePlcTagInfo
        | None ->
            failwith $"ERROR: failed to find header {header}"





