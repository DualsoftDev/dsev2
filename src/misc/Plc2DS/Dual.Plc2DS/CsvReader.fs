namespace Dual.Plc2DS

open Dual.Common.Core.FS
open Dual.Plc2DS
open System.Text.RegularExpressions

[<AutoOpen>]
module rec ReaderModule =
    type CsvReader =
        /// vendor 별 CSV 파일에서 PlcTagInfo로 변환
        static member Read(vendor:Vendor, filePath:string, ?addressFilter:string -> bool): IPlcTag[] =
            let tags =
                match vendor with
                | LS -> LS.CsvReader.ReadCommentCSV(filePath) |> map (fun x -> x :> IPlcTag)
                | AB -> AB.CsvReader.ReadCommentCSV(filePath) |> map (fun x -> x :> IPlcTag)
                | S7 -> S7.CsvReader.ReadCommentSDF(filePath) |> map (fun x -> x :> IPlcTag)
                | MX -> MX.CsvReader.ReadCommentCSV(filePath) |> map (fun x -> x :> IPlcTag)
            let filtered =
                match addressFilter with
                | Some pred -> tags |> filter (fun t -> pred (t.GetAddress()))
                | None -> tags
            filtered

        static member ReadLs(filePath:string): LS.PlcTagInfo[] = LS.CsvReader.ReadCommentCSV(filePath)
        static member ReadAb(filePath:string): AB.PlcTagInfo[] = AB.CsvReader.ReadCommentCSV(filePath)
        static member ReadS7(filePath:string): S7.PlcTagInfo[] = S7.CsvReader.ReadCommentSDF(filePath)
        static member ReadMx(filePath:string): MX.PlcTagInfo[] = MX.CsvReader.ReadCommentCSV(filePath)


    type FDA = { Flow:string; Device:string; Action:string }

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
            let addr = x.GetAddress().ToUpper()
            if addr.IsNullOrEmpty() then
                false
            else
                let pattern =
                    match x with
                    | :? LS.PlcTagInfo -> @"^%I"
                    | :? AB.PlcTagInfo -> @":I"
                    | :? S7.PlcTagInfo -> @"^%I"
                    | :? MX.PlcTagInfo -> @"^X"
                    | _ -> failwith "Invalid PlcTagInfo"
                Regex.IsMatch(addr, pattern)


        // Q 여부
        member x.IsOutput(): bool =
            let addr = x.GetAddress().ToUpper()
            if addr.IsNullOrEmpty() then
                false
            else
                let pattern =
                    match x with
                    | :? LS.PlcTagInfo -> @"^%Q"
                    | :? AB.PlcTagInfo -> @":Q"
                    | :? S7.PlcTagInfo -> @"^%Q"
                    | :? MX.PlcTagInfo -> @"^Y"
                    | _ -> failwith "Invalid PlcTagInfo"
                Regex.IsMatch(addr, pattern)

        // M 여부
        member x.IsMemory(): bool =
            let addr = x.GetAddress().ToUpper()
            if addr.IsNullOrEmpty() then
                false
            else
                let pattern =
                    match x with
                    | :? LS.PlcTagInfo -> @"^%M"
                    | :? AB.PlcTagInfo -> @":M"
                    | :? S7.PlcTagInfo -> @"^%M"
                    | :? MX.PlcTagInfo -> @"^M"     // ???
                    | _ -> failwith "Invalid PlcTagInfo"
                Regex.IsMatch(addr, pattern)

        member x.GetMermoyType(): char =
            if   x.IsInput()  then 'I'
            elif x.IsOutput() then 'Q'
            elif x.IsMemory() then 'M'
            else '?'

        // Input 이면 Some true, Output 이면 Some false, 아니면 None
        member x.OptIOType: bool option =
            match x.GetMermoyType() with
            | 'I' -> Some true
            | 'Q' -> Some false
            | _ -> None

        member x.Temporary
            with get() =
                match x with
                | :? LS.PlcTagInfo as t -> t.Temporary
                | :? AB.PlcTagInfo as t -> t.Temporary
                | :? S7.PlcTagInfo as t -> t.Temporary
                | :? MX.PlcTagInfo as t -> t.Temporary
                | _ -> failwith "Invalid PlcTagInfo"
            and set v =
                match x with
                | :? LS.PlcTagInfo as t -> t.Temporary <- v
                | :? AB.PlcTagInfo as t -> t.Temporary <- v
                | :? S7.PlcTagInfo as t -> t.Temporary <- v
                | :? MX.PlcTagInfo as t -> t.Temporary <- v
                | _ -> failwith "Invalid PlcTagInfo"


        member x.FlowName
            with get() =
                match x with
                | :? LS.PlcTagInfo as t -> t.FlowName
                | :? AB.PlcTagInfo as t -> t.FlowName
                | :? S7.PlcTagInfo as t -> t.FlowName
                | :? MX.PlcTagInfo as t -> t.FlowName
                | _ -> failwith "Invalid PlcTagInfo"
            and set v =
                match x with
                | :? LS.PlcTagInfo as t -> t.FlowName <- v
                | :? AB.PlcTagInfo as t -> t.FlowName <- v
                | :? S7.PlcTagInfo as t -> t.FlowName <- v
                | :? MX.PlcTagInfo as t -> t.FlowName <- v
                | _ -> failwith "Invalid PlcTagInfo"


        member x.DeviceName
            with get() =
                match x with
                | :? LS.PlcTagInfo as t -> t.DeviceName
                | :? AB.PlcTagInfo as t -> t.DeviceName
                | :? S7.PlcTagInfo as t -> t.DeviceName
                | :? MX.PlcTagInfo as t -> t.DeviceName
                | _ -> failwith "Invalid PlcTagInfo"
            and set v =
                match x with
                | :? LS.PlcTagInfo as t -> t.DeviceName <- v
                | :? AB.PlcTagInfo as t -> t.DeviceName <- v
                | :? S7.PlcTagInfo as t -> t.DeviceName <- v
                | :? MX.PlcTagInfo as t -> t.DeviceName <- v
                | _ -> failwith "Invalid PlcTagInfo"

        member x.ActionName
            with get() =
                match x with
                | :? LS.PlcTagInfo as t -> t.ActionName
                | :? AB.PlcTagInfo as t -> t.ActionName
                | :? S7.PlcTagInfo as t -> t.ActionName
                | :? MX.PlcTagInfo as t -> t.ActionName
                | _ -> failwith "Invalid PlcTagInfo"
            and set v =
                match x with
                | :? LS.PlcTagInfo as t -> t.ActionName <- v
                | :? AB.PlcTagInfo as t -> t.ActionName <- v
                | :? S7.PlcTagInfo as t -> t.ActionName <- v
                | :? MX.PlcTagInfo as t -> t.ActionName <- v
                | _ -> failwith "Invalid PlcTagInfo"

        member x.SetFDA(optFDA:FDA option) =
            match optFDA with
            | Some { Flow = flow; Device = device; Action = action } ->
                x.FlowName <- flow
                x.DeviceName <- device
                x.ActionName <- action
            | None ->
                x.FlowName <- null
                x.DeviceName <- null
                x.ActionName <- null

        member x.TryGetFDA(): FDA option =
            if x.FlowName <> null && x.DeviceName <> null && x.ActionName <> null then
                Some { Flow = x.FlowName; Device = x.DeviceName; Action = x.ActionName }
            else
                None

        member x.GetFDA(): FDA =
            match x.TryGetFDA() with
            | Some fda -> fda
            | None -> { Flow = null; Device = null; Action = null }


