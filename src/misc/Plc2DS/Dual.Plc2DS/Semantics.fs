namespace Dual.Plc2DS

open System
open System.Linq
open System.Collections.Generic
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Core
open System.Text.RegularExpressions

[<AutoOpen>]
module AppSettingsModule =

    /// StringComparer.OrdinalIgnoreCase
    let internal ic = StringComparer.OrdinalIgnoreCase

    //// 삭제 대상
    //[<DataContract>]
    //type SemanticUnused() =
    //    /// Modifier 가 앞에 붙는 걸 선호.  default 는 false 로 뒤에 붙는 걸 선호.  e.g "STN3_B_CYL1" => STN3_B
    //    [<DataMember>] member val PreferPrefixModifier = false with get, set


    //    [<DataMember>] member val Flows            = WordSet(ic) with get, set
    //    [<DataMember>] member val FlowPatterns     = WordSet(ic) with get, set
    //    [<DataMember>] member val Devices          = WordSet(ic) with get, set
    //    [<DataMember>] member val DevicePatterns   = WordSet(ic) with get, set
    //    /// 행위 keyword. e.g "ADV", "RET",
    //    [<DataMember>] member val Actions          = WordSet(ic) with get, set
    //    [<DataMember>] member val ActionPatterns   = WordSet(ic) with get, set

    //    /// 상태 keyword. e.g "ERR"
    //    [<DataMember>] member val States           = WordSet(ic) with get, set
    //    [<DataMember>] member val Modifiers        = WordSet(ic) with get, set
    //    [<DataMember>] member val PrefixModifiers  = WordSet(ic) with get, set
    //    [<DataMember>] member val PostfixModifiers = WordSet(ic) with get, set


    //    [<JsonProperty("PositionHints")>]
    //    [<DataMember>] member val PositionHintsDTO = Dictionary<string, Range>() with get, set
    //    [<JsonIgnore>] member val PositionHints    = Dictionary<SemanticCategory, Range>() with get, set

    //    member x.OnDeserialized() =
    //        x.PositionHintsDTO |> iter (fun (KeyValue(k, v)) ->
    //            match DU.tryParse<SemanticCategory>(k) with
    //            | Some cat -> x.PositionHints.Add(cat, v)
    //            | None -> logWarn $"Invalid PositionHint: {k}")

    /// Tag 기반 semantic 정보 추출용
    [<DataContract>]
    type Semantic() =
        //inherit SemanticUnused()
        [<DataMember>] member val SplitOnCamelCase = false with get, set

        [<DataMember>] member val NameSeparators = ResizeArray ["_"] with get, set
        [<DataMember>] member val Discards       = WordSet(ic) with get, set

        [<DataMember>] member val SpecialFlows   = WordSet(ic) with get, set
        [<DataMember>] member val SpecialActions = WordSet(ic) with get, set


        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [<JsonProperty("Dialects")>] // JSON에서는 "Dialects"라는 이름으로 저장
        [<DataMember>] member val DialectsDTO:Words[] = [||] with get, set
        /// 표준어 사전: Dialect => Standard
        [<JsonIgnore>] member val Dialects    = Dictionary<string, string>(ic) with get, set

        /// Mutual Reset Pairs. e.g ["ADV"; "RET"]
        [<DataMember>] member val MutualResetTuples = ResizeArray<WordSet> [||] with get, set

        [<OnDeserialized>]
        member x.OnDeserializedMethod(context: StreamingContext) =
            //x.OnDeserialized()
            match x.NameSeparators.TryFind(fun sep -> sep.Length <> 1) with
            | Some sep -> failwith $"Invalid NameSeparators: {sep}"
            | None -> ()

            for ds in x.DialectsDTO do
                let std = ds.[0]
                let dialects = ds[1..]
                dialects |> iter (fun d -> x.Dialects.Add(d, std))

    type Semantic with
        member x.Duplicate() =
            let y = Semantic()
            //// deep copy
            //y.Actions           <- WordSet(x.Actions, ic)
            //y.States            <- WordSet(x.States, ic)
            //y.Flows             <- WordSet(x.Flows, ic)
            //y.Devices           <- WordSet(x.Devices, ic)
            //y.FlowPatterns      <- WordSet(x.FlowPatterns, ic)
            //y.DevicePatterns    <- WordSet(x.DevicePatterns, ic)
            //y.Modifiers         <- WordSet(x.Modifiers, ic)
            //y.PrefixModifiers   <- WordSet(x.PrefixModifiers, ic)
            //y.PostfixModifiers  <- WordSet(x.PostfixModifiers, ic)
            //y.PositionHints     <- Dictionary(x.PositionHints)

            y.Discards          <- WordSet(x.Discards, ic)
            y.NameSeparators <- x.NameSeparators.Distinct() |> ResizeArray
            y.Dialects          <- Dictionary(x.Dialects, ic)
            y.MutualResetTuples <- x.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ic)) |> ResizeArray
            y

        /// addOn 을 x 에 합침
        member x.Merge(addOn:Semantic): unit =
            //x.Actions         .UnionWith(addOn.Actions)
            //x.States          .UnionWith(addOn.States)
            //x.Flows           .UnionWith(addOn.Flows)
            //x.Devices         .UnionWith(addOn.Devices)
            //x.FlowPatterns    .UnionWith(addOn.FlowPatterns)
            //x.DevicePatterns  .UnionWith(addOn.DevicePatterns)
            //x.Modifiers       .UnionWith(addOn.Modifiers)
            //x.PrefixModifiers .UnionWith(addOn.PrefixModifiers)
            //x.PostfixModifiers.UnionWith(addOn.PostfixModifiers)

            //addOn.PositionHints |> iter (fun (KeyValue(k, v)) -> x.PositionHints.Add (k, v))


            x.Discards.UnionWith(addOn.Discards)
            x.NameSeparators <- (x.NameSeparators @ addOn.NameSeparators).Distinct() |> ResizeArray
            addOn.Dialects |> iter (fun (KeyValue(k, v)) -> x.Dialects.Add (k, v))

            // x.MutualResetTuples 에 addOn.MutualResetTuples 의 항목을 deep copy 해서 추가
            addOn.MutualResetTuples
            |> Seq.map (fun set -> WordSet(set, ic))
            |> Seq.iter (fun set -> x.MutualResetTuples.Add(set))

        member x.Override(replace:Semantic): unit =
            //if replace.Actions.NonNullAny() then
            //    x.Actions <- WordSet(replace.Actions, ic)
            //if replace.States.NonNullAny() then
            //    x.States <- WordSet(replace.States, ic)
            //if replace.PositionHints.NonNullAny() then
            //    x.PositionHints <- Dictionary(replace.PositionHints)
            //if replace.Flows.NonNullAny() then
            //    x.Flows <- WordSet(replace.Flows, ic)
            //if replace.Devices.NonNullAny() then
            //    x.Devices <- WordSet(replace.Devices, ic)
            //if replace.FlowPatterns.NonNullAny() then
            //    x.FlowPatterns <- WordSet(replace.FlowPatterns, ic)
            //if replace.DevicePatterns.NonNullAny() then
            //    x.DevicePatterns <- WordSet(replace.DevicePatterns, ic)
            //if replace.Modifiers.NonNullAny() then
            //    x.Modifiers <- WordSet(replace.Modifiers, ic)
            //if replace.PrefixModifiers.NonNullAny() then
            //    x.PrefixModifiers <- WordSet(replace.PrefixModifiers, ic)
            //if replace.PostfixModifiers.NonNullAny() then
            //    x.PostfixModifiers <- WordSet(replace.PostfixModifiers, ic)

            if replace.NameSeparators.NonNullAny() then
                x.NameSeparators <- ResizeArray(replace.NameSeparators.Distinct())
            if replace.Discards.NonNullAny() then
                x.Discards <- WordSet(replace.Discards, ic)
            if replace.Dialects.NonNullAny() then
                x.Dialects <- Dictionary(replace.Dialects, ic)
            if replace.MutualResetTuples.NonNullAny() then
                x.MutualResetTuples <- replace.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ic)) |> ResizeArray

    /// Vendor 별 Tag Semantic 별도 적용 용도
    type Semantics = Dictionary<string, Semantic>   // string : Vendor type 이지만, JSON 편의를 위해 string 으로.

    type SemanticSettings() =
        inherit Semantic()
        /// Vendor 별 Tag Semantic: 부가, additional
        [<DataMember>] member val AddOn    = Semantics() with get, set
        /// Vendor 별 Tag Semantic: override.  this 의 항목 override
        [<DataMember>] member val Override = Semantics() with get, set

    type SemanticSettings with
        member x.CreateVendorSemantic(vendor:Vendor): Semantic =
            let vendor = vendor.ToString()
            let addOn = x.AddOn.TryGet(vendor)
            let ovrride = x.Override.TryGet(vendor)
            if addOn.IsNone && ovrride.IsNone then
                x
            else
                let y = x.Duplicate()
                match addOn, ovrride with
                | Some a, Some o ->
                    y.Merge(a)
                    y.Override(o)
                    y
                | Some a, None -> a
                | None, Some o -> o
                | _ -> failwith "ERROR"

