namespace Dual.Plc2DS.S7

open Dual.Plc2DS.Common.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module S7 =
    type SDF = {
        Name: string
        Address: string
        DataType: string
        Comment: string
    } with
        interface IDeviceComment

    type Reader =
        static member ReadSDF(sdfPath: string) : SDF[] =
            File.PeekLines(sdfPath, 0)
            |> map _.Split(',')
            |> map (fun cols -> cols |> map _.Trim('"').Trim())
            |> map (fun cols ->
                if cols.Length <> 4 then
                    failwith "Invalid file format"

                let name = cols[0]
                let address =
                    match cols[1] with       // "I  200.2 ", "T 99 "  등의 불균일 요소 => "I 202.2", "T 99" 로 정리
                    | RegexPattern @"^\s*(\w+)\s+(\d+(?:\.\d+)*)\s*$" [typ; addr] ->
                        $"{typ} {addr}"
                    | _ -> cols[1]
                let dataType = cols[2]
                let comment = cols[3]
                { Name = name; Address = address; DataType = dataType; Comment = comment }
            )


