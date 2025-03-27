namespace Dual.Plc2DS.AB

open System
open System.Text.RegularExpressions
open System.Runtime.Serialization

open Dual.Common.Core.FS
open Dual.Common.Base
open Dual.Plc2DS

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

[<DataContract>]
type PlcTagInfo(?typ, ?scope, ?name, ?description, ?dataType, ?specifier, ?attributes, ?owningElement) =
    inherit PlcTagBaseFDA()

    let typ         = typ         |? ""
    let scope       = scope       |? ""
    let name        = name        |? ""
    let description = description |? ""
    let dataType    = dataType    |? ""
    let specifier   = specifier   |? ""
    let attributes  = attributes  |? ""
    let owningElement  = owningElement  |? ""

    new() = PlcTagInfo(null, null, null, null, null, null, null, null)    // for JSON parameterless constructor
    [<DataMember>] member val Type        = typ         with get, set
    [<DataMember>] member val Scope       = scope       with get, set
    [<DataMember>] member val Name        = name        with get, set
    [<DataMember>] member val Description = description with get, set
    [<DataMember>] member val DataType    = dataType    with get, set
    [<DataMember>] member val Specifier   = specifier   with get, set
    [<DataMember>] member val Attributes  = attributes  with get, set
    [<DataMember>] member val OwningElement  = owningElement  with get, set




[<AutoOpen>]
module private Ab =
    let doDecodeHangulString (encoded: string) =
        //let pattern = @"\$(\w{4})" // $XXXX 패턴 감지  (특수문자를 먼저 제거했다면 이 수식만으로 OK.  그러나 특수문자가 남아 있는 상태라면 부족)

        let pattern = @"\$(?!\$|[QNT'])([0-9A-Fa-f]{4})"  // ${$, Q, N, T, '} 를 제외한 $XXXX 패턴 감지

        Regex.Replace(encoded, pattern, fun m ->
            let hexValue = m.Groups.[1].Value
            let unicodeChar = Convert.ToInt32(hexValue, 16) |> char
            unicodeChar.ToString()
        )

    let doDecodeSpecialChar (encoded: string) =
        encoded
            .Replace("$$", "$")
            .Replace("$Q", "\"")
            .Replace("$N", "\n")
            .Replace("$T", "\t")
            .Replace("$'", "'")

    /// $XXXX를 유니코드 문자로 디코딩
    let decodeEncodedString (encoded: string) = encoded |> doDecodeSpecialChar |> doDecodeHangulString

    let isHeader (line:string) = !! line.Contains("\"") && line.Contains(",")

    /// AB CSV 에서 header 가 가변적으로 변동되는 것을 처리하기 위함
    type VariableHeader(headerLine:string, ?separator) =
        let separator = separator |? ","
        let mutable fields:string[] = [||]
        let reset(headerLine:string) = fields <- headerLine.Split(separator)
        do
            reset(headerLine)

        member x.Reset(line:string) = reset(line)
        member x.TryGetField(field:string, data:string[]) =
            match fields.TryFindIndex((=) field) with
            | Some n when n < data.Length -> Some data[n]
            | _ -> None


/// AB.CsvReader
type CsvReader =
    static member private TryCreatePlcTagInfo(line: string, variableHeader:VariableHeader) : PlcTagInfo option =
        let cols = Csv.ParseLine line |> map decodeEncodedString

        /// get field data
        let gf (filedNames:string[]) =
            // 여러 fieldName 중에서 제일 먼저 match 되는 것 반환.  없으면 ""
            filedNames |> Array.tryPick(fun f -> variableHeader.TryGetField(f, cols)) |? ""

        if isHeader line then
            variableHeader.Reset line
            None
        else
            let typ           = gf([|"TYPE"|])
            let scope         = gf([|"SCOPE"|])
            let name          = gf([|"NAME"; "COMMENT"|])
            let dataType      = gf([|"DATATYPE"|])
            let description   = gf([|"DESCRIPTION"|])
            let routine       = gf([|"ROUTINE"|])
            let specifier     = gf([|"SPECIFIER"|])
            let owningElement = gf([|"OWNING_ELEMENT"|])

            let location      = gf([|"LOCATION"|])
            let attributes    = gf([|"ATTRIBUTES"|])

            Some <| PlcTagInfo(typ = typ, scope = scope, name = name, description = description,
                dataType = dataType, specifier = specifier, attributes = attributes, owningElement = owningElement)


    static member ReadCommentCSV(filePath: string): PlcTagInfo[] =

        //let header = "TYPE,SCOPE,NAME,DESCRIPTION,DATATYPE,SPECIFIER,ATTRIBUTES"
        match File.TryReadUntil(filePath, isHeader) with
        | Some headers ->
            let variableHeader = VariableHeader(headers |> last)
            let skipLines = headers.Length
            File.PeekLines(filePath, skipLines)
            |> toArray
            |> choose (fun line -> CsvReader.TryCreatePlcTagInfo(line, variableHeader))
        | None ->
            failwith $"ERROR: failed to find header on file {filePath}"


    /// AB CSV 파일의 특수문자, 한글 decoding
    /// CSV 를 decoding 해서 CSV 로 저장하려면, 특수문자까지 decoding 하면 CSV 파일 포맷이 망가지므로, 한글만 decoding 한다.
    static member Decode(encoded: string, ?decodeSpecialChar, ?decodeHangule): string =
        let mutable decoded = encoded
        let decodeSpecialChar = decodeSpecialChar |? true
        let decodeHangule = decodeHangule |? true

        if decodeSpecialChar then
            decoded <- doDecodeSpecialChar decoded
        if decodeHangule then
            decoded <- doDecodeHangulString decoded

        decoded



