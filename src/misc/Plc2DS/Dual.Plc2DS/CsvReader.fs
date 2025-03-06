namespace Dual.Plc2DS

open Dual.Plc2DS.Common.FS

[<AutoOpen>]
module ReaderModule =
    type CsvReader =
        static member ReadLs(filePath:string) = LS.Ls.CsvReader.ReadCommentCSV(filePath)
        static member ReadAb(filePath:string) = AB.Ab.CsvReader.ReadCommentCSV(filePath)
        static member ReadS7(filePath:string) = S7.S7.CsvReader.ReadCommentSDF(filePath)
        static member ReadMx(filePath:string) = MX.Mx.CsvReader.ReadCommentCSV(filePath)

    type IPlcTag with
        member x.GetName() =
            match x with
            | :? LS.Ls.PlcTagInfo as ls -> ls.Variable
            | :? AB.Ab.PlcTagInfo as ab -> ab.Name
            | :? S7.S7.PlcTagInfo as s7 -> s7.Name
            | :? MX.Mx.PlcTagInfo as mx -> mx.Device
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetComment() =
            match x with
            | :? LS.Ls.PlcTagInfo as ls -> ls.Comment
            | :? AB.Ab.PlcTagInfo as ab -> ab.Description
            | :? S7.S7.PlcTagInfo as s7 -> s7.Comment
            | :? MX.Mx.PlcTagInfo as mx -> mx.Comment
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetAddress() =
            match x with
            | :? LS.Ls.PlcTagInfo as ls -> ls.Address
            | :? AB.Ab.PlcTagInfo as ab -> ab.DataType
            | :? S7.S7.PlcTagInfo as s7 -> s7.Address
            | :? MX.Mx.PlcTagInfo as mx -> mx.Device        // Device, Comment, Label 중 어느 것??
            | _ -> failwith "Invalid PlcTagInfo"
