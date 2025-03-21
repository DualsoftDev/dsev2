namespace Dual.Plc2DS

open System.IO
open Dual.Common.Core.FS
open System.Collections.Generic
open System.Diagnostics
open System
open System.Runtime.Serialization
open Newtonsoft.Json
open System.ComponentModel

//type IDataReader = interface end
//type ILogicReader = interface end

/// 주로 CSV 를 통해 읽어 들인, vendor 별 PLC 태그 정보를 담는 인터페이스
type IPlcTag = interface end

type Choice =
    /// 아무런 결정도 내려지지 않은 상태
    | Stage
    /// 버릴 tags
    | Discarded
    /// 선택된 tags
    | Chosen
    /// FDA 이름까지 확정된 tags
    | Categorized


//[<AbstractClass>]
[<DebuggerDisplay("{Stringify()}")>]
[<DataContract>]
type PlcTagBaseFDA(flow:string, device:string, action:string) =
    new () = PlcTagBaseFDA(null, null, null)

    interface IPlcTag

    [<DataMember>] member val FlowName = flow with get, set
    [<DataMember>] member val DeviceName = device with get, set
    [<DataMember>] member val ActionName = action with get, set
    [<DataMember>] member val Choice = Choice.Stage with get, set
    [<JsonIgnore>] [<Browsable(false)>] member val Temporary :obj = null with get, set

    member x.Set(flow, device, action) =
        x.FlowName <- flow
        x.DeviceName <- device
        x.ActionName <- action

    member x.TryGet() =
        if x.FlowName <> null && x.DeviceName <> null && x.ActionName <> null then
            Some x
        else
            None

    member x.GetTuples() = x.FlowName, x.DeviceName, x.ActionName

    abstract member Stringify: unit -> string
    abstract member Csvify: unit -> string
    abstract member OnDeserialized: unit -> unit
    abstract member OnSerializing: unit -> unit
    default x.Stringify() = $"{x.FlowName}:{x.DeviceName}:{x.ActionName}"
    default x.Csvify() = $"{x.FlowName},{x.DeviceName},{x.ActionName}"
    default x.OnDeserialized() = ()
    default x.OnSerializing() = ()

    [<OnDeserialized>] member x.OnDeserializedMethod(context: StreamingContext) = x.OnDeserialized()
    [<OnSerializing>]  member x.OnSerializingMethod(context: StreamingContext) = x.OnSerializing()


type Vendor =
    | AB
    | S7
    | LS
    | MX
type Vendor with
    static member FromString(vendor:string) =
        match vendor with
        | "LS" -> Vendor.LS
        | "AB" -> Vendor.AB
        | "S7" -> Vendor.S7
        | "MX" -> Vendor.MX
        | _ -> failwith "ERROR"



type SemanticCategory =
    | DuNone
    | DuAction
    | DuDevice
    | DuFlow
    | DuModifier
    | DuDiscard
    | DuPrefixModifier
    | DuPostfixModifier
    | DuState
    | DuUnmatched
    with
        member x.IsMandatory = x.IsDuAction || x.IsDuDevice || x.IsDuFlow


/// Positional index type
type PIndex = int

type CategorySummary = {
    Multiples: (SemanticCategory * PIndex[])[]    // e.g. [Action, [2; 4]]
    Nopes: PIndex[]
    Uniqs: (PIndex * SemanticCategory)[]    // e.g. (1, Modifier)
    Showns: SemanticCategory[]
    NotShowns: SemanticCategory[]
} with
    member x.ShownsMandatory    = x.Showns    |> filter _.IsMandatory
    member x.NotShownsMandatory = x.NotShowns |> filter _.IsMandatory



type WordSet = HashSet<string>
type Words = string[]

/// 범위 지정: 백분율 Min/Max : [0..100]
type Range = { Min: int; Max: int }
/// tuple'ed range
type TRange = int * int

/// word 내에서의 위치
type StringIndex = int
type Score = int

type PartialMatch = {
    Text:string
    Start:StringIndex
    Category:SemanticCategory
} with
    static member Create(text:string, start:StringIndex, category:SemanticCategory) = { Text = text; Start = start; Category = category }
    member x.Stringify() = $"{x.Text}@{x.Start}"