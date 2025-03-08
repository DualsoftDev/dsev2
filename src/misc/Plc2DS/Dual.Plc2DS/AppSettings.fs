namespace Dual.Plc2DS

open System
open System.Linq
open System.Collections.Generic
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS

[<AutoOpen>]
module AppSettingsModule =

    type WordSet = HashSet<string>
    type Words = string[]

    let private ignoreCase = StringComparer.OrdinalIgnoreCase

    /// Tag 기반 semantic 정보 추출용
    [<DataContract>]
    type Semantic() =
        [<DataMember>] member val SplitOnCamelCase = false with get, set
        /// 행위 keyword. e.g "ADV", "RET",
        [<DataMember>] member val Actions = WordSet(ignoreCase) with get, set
        /// 상태 keyword. e.g "ERR"
        [<DataMember>] member val States = WordSet(ignoreCase) with get, set
        /// Mutual Reset Pairs. e.g ["ADV"; "RET"]
        [<DataMember>] member val MutualResetTuples = ResizeArray<WordSet> [||] with get, set
        /// Alias.  e.g [CLAMP, CLP, CMP].  [][0] 가 표준어, 나머지는 dialects
        [<JsonProperty("Dialects")>] // JSON에서는 "Dialects"라는 이름으로 저장
        [<DataMember>] member val DialectsDTO:Words[] = [||] with get, set

        /// 표준어 사전: Dialect => Standard
        [<JsonIgnore>] member val Dialects    = Dictionary<string, string>(ignoreCase) with get, set
        [<DataMember>] member val NameSeparators = ResizeArray ["_"] with get, set

        [<DataMember>] member val FlowNames   = WordSet(ignoreCase) with get, set
        [<DataMember>] member val DeviceNames = WordSet(ignoreCase) with get, set
        [<DataMember>] member val Modifiers   = WordSet(ignoreCase) with get, set

        [<OnDeserialized>]
        member x.OnDeserializedMethod(context: StreamingContext) =
            for ds in x.DialectsDTO do
                let std = ds.[0]
                let dialects = ds[1..]
                dialects |> iter (fun d -> x.Dialects.Add(d, std))

            if x.NameSeparators.Any(fun sep -> sep.Length <> 1) then
                logWarn "Invalid NameSeparators"

    type Semantic with
        member x.Duplicate() =
            let y = Semantic()
            // deep copy
            y.Actions     <- WordSet(x.Actions, ignoreCase)
            y.States      <- WordSet(x.States, ignoreCase)
            y.FlowNames   <- WordSet(x.FlowNames, ignoreCase)
            y.DeviceNames <- WordSet(x.DeviceNames, ignoreCase)
            y.Modifiers   <- WordSet(x.Modifiers, ignoreCase)
            y.Dialects    <- Dictionary(x.Dialects, ignoreCase)
            y.MutualResetTuples <- x.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ignoreCase)) |> ResizeArray
            y.NameSeparators <- x.NameSeparators.Distinct() |> ResizeArray
            y

        /// addOn 을 x 에 합침
        member x.Merge(addOn:Semantic): unit =
            x.Actions.UnionWith(addOn.Actions)
            x.States.UnionWith(addOn.States)
            // x.MutualResetTuples 에 addOn.MutualResetTuples 의 항목을 deep copy 해서 추가
            addOn.MutualResetTuples
            |> Seq.map (fun set -> WordSet(set, ignoreCase))
            |> Seq.iter (fun set -> x.MutualResetTuples.Add(set))

            //x.Dialects.AddRange(addOn.Dialects)
            addOn.Dialects |> iter (fun (KeyValue(k, v)) -> x.Dialects.Add (k, v))

            x.NameSeparators <- (x.NameSeparators @ addOn.NameSeparators).Distinct() |> ResizeArray
            x.FlowNames.UnionWith(addOn.FlowNames)
            x.DeviceNames.UnionWith(addOn.DeviceNames)
            x.Modifiers.UnionWith(addOn.Modifiers)

        member x.Override(replace:Semantic): unit =
            if replace.Actions.NonNullAny() then
                x.Actions <- WordSet(replace.Actions, ignoreCase)
            if replace.States.NonNullAny() then
                x.States <- WordSet(replace.States, ignoreCase)
            if replace.MutualResetTuples.NonNullAny() then
                x.MutualResetTuples <- replace.MutualResetTuples |> Seq.map (fun set -> WordSet(set, ignoreCase)) |> ResizeArray
            if replace.Dialects.NonNullAny() then
                x.Dialects <- Dictionary(replace.Dialects, ignoreCase)
            if replace.NameSeparators.NonNullAny() then
                x.NameSeparators <- ResizeArray(replace.NameSeparators.Distinct())
            if replace.FlowNames.NonNullAny() then
                x.FlowNames <- WordSet(replace.FlowNames, ignoreCase)
            if replace.DeviceNames.NonNullAny() then
                x.DeviceNames <- WordSet(replace.DeviceNames, ignoreCase)
            if replace.Modifiers.NonNullAny() then
                x.Modifiers <- WordSet(replace.Modifiers, ignoreCase)


    /// Vendor 별 Tag Semantic 별도 적용 용도
    type Semantics = Dictionary<string, Semantic>

    type AppSettings() =
        inherit Semantic()
        /// Vendor 별 Tag Semantic: 부가, additional
        [<DataMember>] member val AddOn    = Semantics() with get, set
        /// Vendor 별 Tag Semantic: override.  this 의 항목 override
        [<DataMember>] member val Override = Semantics() with get, set

    type AppSettings with
        member x.CreateVendorSemantic(vendor:string): Semantic =
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

    /// word 주변에 동시에 숫자가 오는 경우는 일단, 없다고 가정하고, 구현은 앞뒤 숫자를 더해서 결정
    let splitNumber(pName:string): (int option) * string * (int option) = // pName : partial name: '_' 로 분리된 이름 중 하나
        match pName with
        | RegexPattern @"^(\d+)?(\D+)(\d+)?$" [prefix; name; postfix] ->        // D+: 숫자가 아닌 임의의 것
            match prefix, postfix with
            | "", "" -> None, name, None
            | "", postfix ->
                None, name, Some (int postfix)
            | prefix, "" ->
                Some (int prefix), name, None
            | prefix, postfix ->
                Some (int prefix), name, Some (int postfix)
        | _ -> None, pName, None


    type NameWithNumber(name: string, optPrefixNumber:int option, optPostfixNumber:int option) =

        member x.Name = name
        member x.OptPrefixNumber = optPrefixNumber
        member x.OptPostfixNumber = optPostfixNumber

        /// PName 의 position
        member val OptPosition:int option = None with get, set

        static member Create(name, ?optPrefixNumber:int, ?optPostfixNumber:int) =
            NameWithNumber(name, optPrefixNumber, optPostfixNumber)

        member x.PName =
            let mutable r = ""
            x.OptPrefixNumber.Iter (fun prefix -> r <- $"{prefix}")
            r <- r + x.Name
            x.OptPostfixNumber.Iter (fun postfix -> r <- $"{r}{postfix}")
            r

        override x.ToString (): string =
            let o2s (n:int option) = n |> map toString |? "~"
            $"{o2s x.OptPrefixNumber}:{x.Name}:{o2s x.OptPostfixNumber}@{x.OptPosition.Value}"

    type NameWithNumbers = NameWithNumber[]
    type PIndex = int


    let zeroNN = NameWithNumber.Create("")

    type Semantic with
        /// pName 에서 뒤에 붙은 숫자 부분 제거 후, 표준어로 변환
        member x.StandardizePName(pName:string): NameWithNumber =   // pName : partial name: '_' 로 분리된 이름 중 하나
            let (preNumber:int option), name, (postNumber:int option) = splitNumber pName
            match x.Dialects.TryGet(name) with
            | Some standard -> NameWithNumber(standard, preNumber, postNumber)
            | None -> NameWithNumber(name, preNumber, postNumber)

        ///// 공통 검색 함수: standardPNames 배열에서 targetSet에 있는 첫 번째 단어 반환 (없으면 null)
        //[<Obsolete("추후 고려")>]
        //member private x.GuessName(targetSet: WordSet, standardPNames: NameWithNumbers): GuessResult =
        //    // 일단 match 되는 하나라도 있으면 바로 리턴.. 추후에는 갯수와 위치 등을 고려해야 함
        //    xxx
        //    standardPNames |> Array.tryFindIndex(fun (pre, n, post) -> targetSet.Contains n)
        //    |> map (fun i -> standardPNames[i], i)

        /// 공통 검색 함수: standardPNames 배열에서 targetSet에 있는 첫 번째 단어 반환 (없으면 null)
        member private x.GuessNames(targetSet: WordSet, standardPNames: NameWithNumbers): NameWithNumber[] =
            [|
                for (i, nn) in standardPNames.Indexed() do
                    if targetSet.Contains (nn.PName) || targetSet.Contains (nn.Name) then
                        nn.OptPosition <- Some i
                        nn
            |]

        // standardPNames : 표준화된 부분(*P*artial) 이름
        /// standardPNames 중에서 Flow 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessFlowName(standardPNames: NameWithNumbers): NameWithNumber[] =
            x.GuessNames(x.FlowNames, standardPNames)

        /// standardPNames 중에서 Device 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessDeviceName(standardPNames: NameWithNumbers): NameWithNumber[] =
            x.GuessNames(x.DeviceNames, standardPNames)

        /// standardPNames 중에서 Action 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessActionName(standardPNames: NameWithNumbers): NameWithNumber[] =
            x.GuessNames(x.Actions, standardPNames)

        /// standardPNames 중에서 State 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessStateName(standardPNames: NameWithNumbers): NameWithNumber[] =
            x.GuessNames(x.States, standardPNames)

        /// standardPNames 중에서 Modifiers 에 해당하는 것들이 존재하면, (그것과 index) 배열 반환
        member x.GuessModifierNames(standardPNames: NameWithNumbers): NameWithNumber[] =
            x.GuessNames(x.Modifiers, standardPNames)
