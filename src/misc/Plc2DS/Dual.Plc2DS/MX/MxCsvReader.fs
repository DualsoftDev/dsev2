namespace Dual.Plc2DS.MX

open Dual.Common.Core.FS
open Dual.Plc2DS


type PlcTagInfo = {
    Device: string
    Comment: string
    Label: string
    mutable Temporary:obj
} with
    interface IPlcTag


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

        {   Device = device; Comment = comment; Label = label |? ""
            Temporary = null
        }

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
