namespace Dual.Plc2DS

open Dual.Plc2DS.Common.FS

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
            | :? AB.PlcTagInfo as ab -> ab.DataType
            | :? S7.PlcTagInfo as s7 -> s7.Address
            | :? MX.PlcTagInfo as mx -> mx.Device        // Device, Comment, Label 중 어느 것??
            | _ -> failwith "Invalid PlcTagInfo"
