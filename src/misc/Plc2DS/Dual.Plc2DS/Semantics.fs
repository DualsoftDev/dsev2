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

    /// Tag 기반 semantic 정보 추출용
    [<DataContract>]
    type Semantic() =
        [<DataMember>] member val SplitOnCamelCase = false with get, set

        [<DataMember>] member val NameSeparators = ResizeArray ["_"] with get, set  // JSON 편의상 string.  실제는 char

        [<JsonProperty("DeviceNameErasePatterns")>] // JSON에서는 "DeviceNameErasePatterns"라는 이름으로 저장
        [<DataMember>] member val DeviceNameErasePatternsDTO:Words = [||]  with get, set

        [<DataMember>] member val FDARegexPatterns        :Words = [||] with get, set
        [<DataMember>] member val SpecialFlowPatterns     :Words = [||] with get, set
        [<DataMember>] member val SpecialActionPatterns   :Words = [||] with get, set

        [<JsonProperty("DefinitelyActionPatterns")>] // JSON에서는 "DefinitelyActionPatterns"라는 이름으로 저장
        [<DataMember>] member val DefinitelyActionPatternsDTO:Words = [||] with get, set


        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [<JsonProperty("Dialects")>] // JSON에서는 "Dialects"라는 이름으로 저장
        [<DataMember>] member val DialectsDTO:Words[] = [||] with get, set

        /// Mutual Reset Pairs. e.g ["ADV"; "RET"]
        [<DataMember>] member val MutualResetTuples = ResizeArray<WordSet> [||] with get, set

        static member BaselineFDARegexPattern = @"^(?<flow>[^_]+)_(?<device>.+)_(?<action>[^_]+)$"
        [<JsonIgnore>] member val CompiledFDARegexPatterns:Regex[] = [||] with get, set

        [<JsonIgnore>] member val DeviceNameErasePatterns:Regex[] = [||]  with get, set
        [<JsonIgnore>] member val DefinitelyActionPatterns:Regex[] = [||] with get, set

        /// 표준어 사전: Dialect => Standard
        [<JsonIgnore>] member val Dialects    = Dictionary<string, string>(ic) with get, set

        member x.CompileFDARegexPattern(?deviceNameCandidates:string[]) =
            let deviceNameCandidates = deviceNameCandidates |? [||]
            let sfs = let ss = x.SpecialFlowPatterns   |> joinWith "|" in ss.EncloseWith("(", ")")     // [A; B; C] => "(A|B|C)"
            let sas = let ss = x.SpecialActionPatterns |> joinWith "|" in ss.EncloseWith("(", ")")
            [|
                yield! x.FDARegexPatterns

                if deviceNameCandidates.any() then
                    let devices = deviceNameCandidates.JoinWith("|").EncloseWith("(", ")")
                    $@"^(?<flow>[^_]+)_(?<device>{devices})_(?<action>[^_]+)$"

                if x.SpecialFlowPatterns.any() && x.SpecialActionPatterns.any() then
                    $@"^(?<flow>{sfs})_(?<device>.+)_(?<action>{sas})$"
                if x.SpecialFlowPatterns.any() then
                    $@"^(?<flow>{sfs})_(?<device>.+)_(?<action>[^_]+)$"
                if x.SpecialActionPatterns.any() then
                    $@"^(?<flow>[^_]+)_(?<device>.+)_(?<action>{sas})$"

                @"^(?<flow>[^_]+)_(?<device>.+)_(?<action>[^_]+)$"
            |] |> map (fun pattern -> Regex(pattern, RegexOptions.Compiled))

        member x.CompileAllRegexPatterns() =
            x.CompiledFDARegexPatterns <- x.CompileFDARegexPattern()

            x.DeviceNameErasePatterns <-
                x.DeviceNameErasePatternsDTO
                |> map (fun p -> Regex(p, RegexOptions.Compiled))

            x.DefinitelyActionPatterns <-
                x.DefinitelyActionPatternsDTO
                |> map (fun p -> Regex(p, RegexOptions.Compiled))


        [<OnDeserialized>]
        member x.OnDeserializedMethod(context: StreamingContext) =
            match x.NameSeparators.TryFind(fun sep -> sep.Length <> 1) with
            | Some sep -> failwith $"Invalid NameSeparators: {sep}"
            | None -> ()

            for ds in x.DialectsDTO do
                let std = ds.[0]
                let dialects = ds[1..]
                dialects |> iter (fun d -> x.Dialects.Add(d, std))

            x.CompileAllRegexPatterns()



    type Semantic with
        static member Create() =
            Semantic()
            |> tee(fun sm -> sm.CompileAllRegexPatterns())

        member x.Duplicate() =
            let y = Semantic()
            // deep copy
            y.FDARegexPatterns      <- x.FDARegexPatterns
            y.SpecialFlowPatterns      <- x.SpecialFlowPatterns
            y.SpecialActionPatterns    <- x.SpecialActionPatterns
            y.DeviceNameErasePatternsDTO  <- x.DeviceNameErasePatternsDTO
            y.DefinitelyActionPatternsDTO <- x.DefinitelyActionPatternsDTO
            y.NameSeparators           <- x.NameSeparators.Distinct() |> ResizeArray
            y.Dialects                 <- Dictionary(x.Dialects, ic)
            y.MutualResetTuples        <- x.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ic)) |> ResizeArray
            y.CompileAllRegexPatterns()
            y

        member x.DuplicateWithDeviceNames(deviceNames:Words) =
            let y = x.Duplicate()
            y.CompiledFDARegexPatterns <- y.CompileFDARegexPattern(deviceNames)
            y

        /// addOn 을 x 에 합침
        member x.Merge(addOn:Semantic): unit =
            x.FDARegexPatterns      <- x.FDARegexPatterns      @ addOn.FDARegexPatterns
            x.SpecialFlowPatterns      <- x.SpecialFlowPatterns      @ addOn.SpecialFlowPatterns
            x.SpecialActionPatterns    <- x.SpecialActionPatterns    @ addOn.SpecialActionPatterns
            x.DeviceNameErasePatternsDTO  <- x.DeviceNameErasePatternsDTO  @ addOn.DeviceNameErasePatternsDTO
            x.DefinitelyActionPatternsDTO <- x.DefinitelyActionPatternsDTO @ addOn.DefinitelyActionPatternsDTO
            x.NameSeparators           <- (x.NameSeparators          @ addOn.NameSeparators).Distinct() |> ResizeArray
            addOn.Dialects |> iter (fun (KeyValue(k, v)) -> x.Dialects.Add (k, v))

            // x.MutualResetTuples 에 addOn.MutualResetTuples 의 항목을 deep copy 해서 추가
            addOn.MutualResetTuples
            |> Seq.map (fun set -> WordSet(set, ic))
            |> Seq.iter (fun set -> x.MutualResetTuples.Add(set))

            x.CompileAllRegexPatterns()


        member x.Override(replace:Semantic): unit =
            if replace.NameSeparators.NonNullAny() then
                x.NameSeparators <- ResizeArray(replace.NameSeparators.Distinct())
            if replace.DeviceNameErasePatterns.NonNullAny() then
                x.DeviceNameErasePatterns <- replace.DeviceNameErasePatterns
            if replace.DefinitelyActionPatterns.NonNullAny() then
                x.DefinitelyActionPatterns <- replace.DefinitelyActionPatterns
            if replace.FDARegexPatterns.NonNullAny() then
                x.FDARegexPatterns <- replace.FDARegexPatterns
            if replace.SpecialFlowPatterns.NonNullAny() then
                x.SpecialFlowPatterns <- replace.SpecialFlowPatterns
            if replace.SpecialActionPatterns.NonNullAny() then
                x.SpecialActionPatterns <- replace.SpecialActionPatterns
            if replace.Dialects.NonNullAny() then
                x.Dialects <- Dictionary(replace.Dialects, ic)
            if replace.MutualResetTuples.NonNullAny() then
                x.MutualResetTuples <- replace.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ic)) |> ResizeArray

            x.CompileAllRegexPatterns()


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

