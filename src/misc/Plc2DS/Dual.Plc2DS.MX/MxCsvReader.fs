namespace Dual.Plc2DS.MX

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS


//[<AutoOpen>]
//module GxWorks =
//        static member Create(device, comment, ?label:string) =
//            { Device = device; Comment = comment; Label = label |? "" }


[<AutoOpen>]
module Mx =
    type DeviceComment = {
        Device: string
        Comment: string
        Label: string
    } with
        interface IDeviceComment


    type CsvReader =
        static member ReadCommentCSV(filePath: string): DeviceComment[] =
            let headers = File.PeekLines(filePath, 0, 2)
            let delimeter, trimDoubleQuote, hasLabel, skipLines =
                match headers with
                | [| "Device,Label,Comment"; _ |]         -> ',',  false, true,  1
                | [| "Device\tLabel\tComment"; _ |]       -> '\t', false, true,  1
                | [| _; "Device Name,Comment" |]          -> ',',  false, false, 2
                | [| _; "Device Name\tComment" |]         -> '\t', false, false, 2
                | [| _; "\"Device Name\",\"Comment\"" |]  -> ',',  true,  false, 2
                | [| _; "\"Device Name\"\t\"Comment\"" |] -> '\t', true,  false, 2
                | _ -> failwith "Invalid file format"

            let removeDq (x:string) =
                let trimDoubleQuote = trimDoubleQuote || (x.StartsWith("\"") && x.EndsWith("\""))
                if trimDoubleQuote then x.Trim('"') else x

            File.PeekLines(filePath, skipLines)
            |> map _.Split(delimeter)
            |> map (fun cols ->
                let device = cols[0] |> removeDq
                let label, comment =
                    if hasLabel then
                        if cols.Length <> 3 then failwith "Invalid file format"
                        cols[1] |> removeDq, cols[2]  |> removeDq
                    else
                        if cols.Length <> 2 then failwith "Invalid file format"
                        "", cols[1] |> removeDq

                { Device = device; Comment = comment; Label = label |? "" }
            )
