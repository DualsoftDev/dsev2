namespace Dual.Plc2DS

open Dual.Common.Core.FS
open Dual.Plc2DS
open System.Text.RegularExpressions

[<AutoOpen>]
module ReaderModule =
    type CsvReader =
        static member ReadLs(filePath:string) = LS.CsvReader.ReadCommentCSV(filePath)
        static member ReadAb(filePath:string) = AB.CsvReader.ReadCommentCSV(filePath)
        static member ReadS7(filePath:string) = S7.CsvReader.ReadCommentSDF(filePath)
        static member ReadMx(filePath:string) = MX.CsvReader.ReadCommentCSV(filePath)

    type IPlcTag with
        member x.GetName() =
            match x with
            | :? LS.PlcTagInfo as ls -> ls.Variable
            | :? AB.PlcTagInfo as ab -> ab.Name
            | :? S7.PlcTagInfo as s7 -> s7.Name
            | :? MX.PlcTagInfo as mx -> mx.Device
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetComment() =
            match x with
            | :? LS.PlcTagInfo as ls -> ls.Comment
            | :? AB.PlcTagInfo as ab -> ab.Description
            | :? S7.PlcTagInfo as s7 -> s7.Comment
            | :? MX.PlcTagInfo as mx -> mx.Comment
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetAddress() =
            match x with
            | :? LS.PlcTagInfo as ls -> ls.Address
            | :? AB.PlcTagInfo as ab -> ab.Specifier
            | :? S7.PlcTagInfo as s7 -> s7.Address
            | :? MX.PlcTagInfo as mx -> mx.Device        // Device, Comment, Label 중 어느 것??
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetAnalysisField() =
            match x with
            | :? MX.PlcTagInfo as mx -> mx.Comment
            | _ -> x.GetName()

        static member private xxx = ()
        // I 여부
        member x.IsInput(): bool =
            let addr = x.GetAddress()
            if addr.IsNullOrEmpty() then
                false
            else
                let pattern =
                    match x with
                    | :? LS.PlcTagInfo -> @"^%[Ii]"
                    | :? AB.PlcTagInfo -> @":I"
                    | :? S7.PlcTagInfo -> @"^%[Ii]"
                    | :? MX.PlcTagInfo -> @"^[Xx]"
                    | _ -> failwith "Invalid PlcTagInfo"
                Regex.IsMatch(addr, pattern)


        // Q 여부
        member x.IsOutput(): bool =
            let addr = x.GetAddress()
            if addr.IsNullOrEmpty() then
                false
            else
                let pattern =
                    match x with
                    | :? LS.PlcTagInfo -> @"^%[Qq]"
                    | :? AB.PlcTagInfo -> @":Q"
                    | :? S7.PlcTagInfo -> @"^%[Qq]"
                    | :? MX.PlcTagInfo -> @"^[Yy]"
                    | _ -> failwith "Invalid PlcTagInfo"
                Regex.IsMatch(addr, pattern)

        // Input 이면 Some true, Output 이면 Some false, 아니면 None
        member x.OptIOType: bool option =
            if x.IsInput() then
                Some true
            elif x.IsOutput() then
                Some false
            else
                None
