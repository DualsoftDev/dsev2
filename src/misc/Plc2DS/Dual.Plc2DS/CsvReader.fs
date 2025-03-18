namespace Dual.Plc2DS

open System.Text.RegularExpressions
open Dual.Common.Core.FS
open Dual.Plc2DS

module private PrivateFwdDeclImpl =
    let mutable fwdGetAddress: IPlcTag -> string = let dummy (tag:IPlcTag) = failwithlog "Should be reimplemented." in dummy


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
            //| Some pred -> tags |> filter (fun t ->  pred (t.GetAddress()))
            | Some pred -> tags |> filter (PrivateFwdDeclImpl.fwdGetAddress >> pred)
            | None -> tags
        filtered
    static member CsRead(vendor:Vendor, filePath:string): IPlcTag[] = CsvReader.Read(vendor, filePath)

    static member ReadLs(filePath:string): LS.PlcTagInfo[] = LS.CsvReader.ReadCommentCSV(filePath)
    static member ReadAb(filePath:string): AB.PlcTagInfo[] = AB.CsvReader.ReadCommentCSV(filePath)
    static member ReadS7(filePath:string): S7.PlcTagInfo[] = S7.CsvReader.ReadCommentSDF(filePath)
    static member ReadMx(filePath:string): MX.PlcTagInfo[] = MX.CsvReader.ReadCommentCSV(filePath)


[<AutoOpen>]
module rec ReaderModule =
    type Vendor with
        member x.GetVendorTagType() =
            match x with
            | LS -> typeof<LS.PlcTagInfo>
            | AB -> typeof<AB.PlcTagInfo>
            | S7 -> typeof<S7.PlcTagInfo>
            | MX -> typeof<MX.PlcTagInfo>

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
            | :? AB.PlcTagInfo as ab -> ab.Specifier |? ab.DataType
            | :? S7.PlcTagInfo as s7 -> s7.Address
            | :? MX.PlcTagInfo as mx -> mx.Device        // Device, Comment, Label 중 어느 것??
            | _ -> failwith "Invalid PlcTagInfo"

        member x.GetAnalysisField() =
            match x with
            | :? MX.PlcTagInfo as mx -> mx.Comment
            | _ -> x.GetName()

        member x.IsValid() = x.GetName().NonNullAny() && x.GetAddress().NonNullAny()

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
            with get() = (x :?> PlcTagBaseFDA).Temporary
            and  set v = (x :?> PlcTagBaseFDA).Temporary <- v


        member x.FlowName
            with get() = (x :?> PlcTagBaseFDA).FlowName
            and  set v = (x :?> PlcTagBaseFDA).FlowName <- v


        member x.DeviceName
            with get() = (x :?> PlcTagBaseFDA).DeviceName
            and  set v = (x :?> PlcTagBaseFDA).DeviceName <- v

        member x.ActionName
            with get() = (x :?> PlcTagBaseFDA).ActionName
            and  set v = (x :?> PlcTagBaseFDA).ActionName <- v

        member x.SetFDA(optFDA:PlcTagBaseFDA option) =
            match optFDA with
            | Some fda -> (x :?> PlcTagBaseFDA).Set(fda.FlowName, fda.DeviceName, fda.ActionName)
            | None     -> (x :?> PlcTagBaseFDA).Set(null, null, null)

        member x.TryGetFDA(): PlcTagBaseFDA option = (x :?> PlcTagBaseFDA).TryGet()

    let initialize() =
        PrivateFwdDeclImpl.fwdGetAddress <- fun (tag:IPlcTag) -> tag.GetAddress()
