namespace Dual.Plc2DS.MX

open System.Runtime.Serialization

open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Plc2DS


[<DataContract>]
type PlcTagInfo(?device, ?comment, ?label) =
    inherit PlcTagBaseFDA()

    let device  = device  |? ""
    let comment  = comment  |? ""
    let label  = label  |? ""

    new() = PlcTagInfo(null, null, null)    // for JSON parameterless constructor
    [<DataMember>] member val Device  = device  with get, set
    [<DataMember>] member val Comment = comment with get, set
    [<DataMember>] member val Label   = label   with get, set



/// MX.CsvReader
type CsvReader =
    static member CreatePlcTagInfo(line: string, delimeter: char, hasLabel:bool) : PlcTagInfo =
        let cols = Csv.ParseLine(line, delimeter)
        assert(cols.Length = if hasLabel then 3 else 2)

        let device = cols[0]
        let label, comment =
            if hasLabel then
                if cols.Length <> 3 then failwith "Invalid file format"
                cols[1], cols[2]
            else
                if cols.Length <> 2 then failwith "Invalid file format"
                "", cols[1]

        PlcTagInfo(device, comment, label)

    static member ReadCommentCSV(filePath: string): PlcTagInfo[] =
        let headers = File.PeekLines(filePath, 0, 2) |> toArray
        let delimeter, hasLabel, skipLines =
            match headers with
            | [| "Device,Label,Comment"; _ |]         -> ',',  true,  1
            | [| "Device\tLabel\tComment"; _ |]       -> '\t', true,  1
            | [| _; "Device Name,Comment" |]          -> ',',  false, 2
            | [| _; "Device Name\tComment" |]         -> '\t', false, 2
            | [| _; "\"Device Name\",\"Comment\"" |]  -> ',',  false, 2
            | [| _; "\"Device Name\"\t\"Comment\"" |] -> '\t', false, 2
            | _ -> failwith "Invalid file format"

        File.PeekLines(filePath, skipLines)
        |> toArray
        |> map (fun line -> CsvReader.CreatePlcTagInfo(line, delimeter, hasLabel))
