namespace Dual.Plc2DS.MX

open Dual.Common.Core.FS
open Dual.Plc2DS.Common.FS


[<AutoOpen>]
module GxWorks =
    type DeviceComment(device:string, comment:string, ?label:string) =
        interface IDeviceComment
        member val Device = device
        member val Comment = comment
        member val Label = label |? null


// 추후 확장?
//[<AutoOpen>]
//module GxWorks3 =

//    type DeviceComment(device:string, comment:string, ?label:string, ?klass:string, ?dataType:string, ?address:string) =
//        inherit GxWorks.DeviceComment(device, comment, ?label=label)

//        member val Class = klass |? "VAR_GLOBAL"
//        member val DataType = dataType |? "BOOL"
//        member val Address = address |? ""



[<AutoOpen>]
module Mx =
    type Reader =
        static member ReadCommentCSV(filePath: string): GxWorks.DeviceComment[] =
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
                        cols[1] |> removeDq, cols[2]  |> removeDq
                    else
                        "", cols[1] |> removeDq

                GxWorks.DeviceComment(device, comment, label=label))
