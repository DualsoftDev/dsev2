namespace Dual.Plc2DS

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open Dual.Plc2DS
open Dual.Common.Core
open Dual.Common.Core.FS

[<AutoOpen>]
module ExtractDeviceModule =
    type Call = {
        Name: string    // e.g "ADV"
        Input: HashSet<IPlcTag>
        Output: HashSet<IPlcTag>
    }

    type Device = {
        Name: string    // e.g "Cyl1"
        FlowName: string    // e.g "STN1"
        Calls: Call[]
        MutualResetTuples: Call[][]
    }

    type Range with


        (*
            점수 계산 로직 (점수가 높을 조건)

                p가 min과 max 범위 안에 있어야 점수를 받을 수 있음
                → 즉, p가 x.Min ≤ p ≤ x.Max 범위에 있을 때만 점수를 계산.

                p가 범위 중앙(center)에 가까울수록 점수가 높아야 함
                → center = (x.Min + x.Max) / 2 이므로, |p - center|가 작을수록 높은 점수.

                range가 작을수록 점수가 높아야 함
                → range = x.Max - x.Min 이므로, range가 작을수록 높은 점수를 부여.
        *)
        member x.CalculateScore(position: PIndex, size: int): double =
            let position = position + 1
            let size = size + 2
            let min, max = double x.Min, double x.Max
            let p = (double position / double size) * 100.0
            if p >= min && p <= max then  // x.Min ≤ p ≤ x.Max
                let center = (min + max) / 2.0
                let range = max - min

                // 거리 기반 점수 (중앙에 가까울수록 높은 점수)
                let distanceScore = 1.0 - (abs (p - center) / (range / 2.0))

                distanceScore
            else
                0.0  // 범위 밖이면 0점



    [<AutoOpen>]
    module (*internal*) rec ExtractDeviceImplModule =
        type AnalyzedNameSemantic = {
            /// 이름 원본
            FullName: string
            /// '_' 기준 분리된 이름
            SplitNames: string[]
            /// SplitNames 각각에 대한 SemanticCategory
            SplitSemanticCategories: SemanticCategory[]
            mutable Flows     : NameWithNumber[]
            mutable Actions   : NameWithNumber[]      // e.g "ADV"
            mutable Devices   : NameWithNumber[]      // e.g "ADV"
            mutable States    : NameWithNumber[]      // e.g "ERR"
            mutable Modifiers : NameWithNumber[]
            mutable PrefixModifiers  : NameWithNumber[]
            mutable PostfixModifiers : NameWithNumber[]
        }
        type AnalyzedNameSemantic with
            /// 이름과 Semantic 정보를 받아서, 분석된 정보를 반환.  기본 처리만 수행
            static member internal CreateDefault(name:string, ?semantics:Semantic): AnalyzedNameSemantic =
                // camelCase 분리 : aCamelCase -> [| "a"; "Camel"; "Case" |]
                let splitCamelCase (input: string) =
                    let sep = "<_sep_>"
                    Regex.Replace(input, "(?<!^)([A-Z])", $"{sep}$1") // 첫 글자는 제외하고 대문자 앞에 separator 추가
                        .Split(sep)
                let isSplitOnCamelCase = semantics.Map(_.SplitOnCamelCase) |? false
                let splitter (x:string) = if isSplitOnCamelCase then splitCamelCase x else [|x|]

                let splitNames =
                    let delimiter:string[] = semantics.Map(_.NameSeparators.ToArray()) |? [|"_"|]
                    name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    |> bind splitter
                    |> map _.ToUpper()

                let baseline =
                    {   FullName = name; SplitNames = splitNames; SplitSemanticCategories = Array.init splitNames.Length (konst Nope)
                        Flows = [||]; Actions = [||]; Devices = [||]; States = [||]
                        Modifiers = [||]; PrefixModifiers = [||]; PostfixModifiers = [||]
                    }
                match semantics with
                | Some sm ->
                    let standardPNames = baseline.SplitNames |> map sm.StandardizePName

                    let categories = Array.copy baseline.SplitSemanticCategories

                    let procReusults (cat:SemanticCategory) (nns:NameWithNumber[]) =
                        for nn in nns do
                            categories[nn.OptPosition.Value] <- cat


                    let flow             = sm.GuessFlowName             standardPNames |> tee(fun nns -> procReusults Flow     nns)
                    let action           = sm.GuessActionName           standardPNames |> tee(fun nns -> procReusults Action   nns)
                    let state            = sm.GuessStateName            standardPNames |> tee(fun nns -> procReusults SemanticCategory.State    nns)
                    let device           = sm.GuessDeviceName           standardPNames |> tee(fun nns -> procReusults Device   nns)
                    let modifiers        = sm.GuessModifierNames        standardPNames |> tee(fun nns -> procReusults Modifier nns)
                    let prefixModifiers  = sm.GuessPrefixModifierNames  standardPNames |> tee(fun nns -> procReusults Modifier nns)
                    let postfixModifiers = sm.GuessPostfixModifierNames standardPNames |> tee(fun nns -> procReusults Modifier nns)

                    noop()
                    { baseline with
                        Flows = flow
                        Actions = action
                        Devices = device
                        States = state
                        Modifiers = modifiers
                        PrefixModifiers = prefixModifiers
                        PostfixModifiers = postfixModifiers
                        SplitSemanticCategories = categories
                    }
                | None -> baseline

            /// 이름과 Semantic 정보를 받아서, 분석된 정보를 반환.  부가 처리 수행
            [<Obsolete("Prefix, Postfix modifier 위치 지정")>]
            static member Create(name:string, semantics:Semantic): AnalyzedNameSemantic =
                AnalyzedNameSemantic.CreateDefault(name, semantics)
                    .FillEmptyPName(semantics)
                    .DecideModifiers(semantics)

            /// PName 중에서 category 할당 안된 항목 채우기.  Semantic.PositinalHints 참고하여 위치 기반으로 항목 채움
            /// 채울 수 없으면 원본 그대로 반환.  변경되면 사본 반환
            member internal x.FillEmptyPName(semantic:Semantic): AnalyzedNameSemantic =
                let cs:CategorySummary = x.Categorize()
                let scores =
                    [
                        for idx in cs.Nopes do
                            for (KeyValue(cat, range)) in semantic.PositionHints do
                                let score = range.CalculateScore(idx, x.SplitNames.Length)
                                idx, cat, score
                    ] |> filter (fun (_, _, score) -> score > 0.0)
                      |> sortByDescending Tuple.third

                match scores with
                | [] -> x
                | (idx, cat, score) :: _  ->
                    let dup =
                        let ssc = Array.copy x.SplitSemanticCategories
                        ssc[idx] <- cat
                        { x with SplitSemanticCategories = ssc }

                    let guessedNames =
                        let nn = NameWithNumber.Create(x.SplitNames.[idx])
                        nn.OptPosition <- Some idx
                        [| nn |]
                    match cat with
                    | Action          -> dup.Actions          <- guessedNames
                    | Device          -> dup.Devices          <- guessedNames
                    | Flow            -> dup.Flows            <- guessedNames
                    | Modifier        -> dup.Modifiers        <- guessedNames
                    | PrefixModifier  -> dup.PrefixModifiers  <- guessedNames
                    | PostfixModifier -> dup.PostfixModifiers <- guessedNames
                    | SemanticCategory.State -> dup.States    <- guessedNames
                    | Nope -> failwith "ERROR"

                    dup

            member x.DecideModifiers(semantic:Semantic): AnalyzedNameSemantic =
                //if [ x.Modifiers; x.PrefixModifiers; x.PostfixModifiers ] |> forall _.IsNullOrEmpty() then
                //    x
                //else
                //    let dup = { x with FullName = x.FullName }
                //    x.PrefixModifiers |> sortBy _.OptPosition.Value |> List.ofSeq |> groupConsecutive

                //    let preferPrefixModifier = semantic.PreferPrefixModifier
                //    //for (idx, _) in x.SplitSemanticCategories.Indexed().Filter(snd >> ((=) Modifier)) do
                //    //    match preferPrefixModifier with
                //    //    | true when idx
                //    //    noop()

                    x

            /// PName 중에서 복수 category 할당 된 항목 처리
            member x.Disambiguate(semantic:Semantic): AnalyzedNameSemantic =
                let cs = x.Categorize()
                x

            member x.PostProcess(semantic:AppSettings): AnalyzedNameSemantic = x

            member x.Categorize() :CategorySummary =
                // x.SplitSemanticCategories 의 SemanticCategory 별 indices 를 반환
                let multiples: (SemanticCategory * PIndex[])[] =
                    // x.SplitSemanticCategories 에서 같은 SemanticCategory 가 2개 이상인 것들에 대해, key 와 index 들을 추출.
                    x.SplitSemanticCategories
                    |> mapi (fun i cat -> cat, i)  // 각 카테고리와 해당 인덱스를 튜플로 매핑
                    |> groupBy fst                 // SemanticCategory별로 그룹화
                    |> filter (fun (cat, items) -> cat <> Nope && items.Length > 1) // 2개 이상인 것만 필터링
                    |> map (fun (cat, items) -> cat, items |> map snd) // (카테고리, 인덱스 배열) 반환

                let nopes: PIndex[] =
                    x.SplitSemanticCategories
                    |> mapi (fun i cat -> cat, i)
                    |> filter (fun (cat, _) -> cat = Nope)
                    |> map snd

                let uniqs: PIndex[] =
                    let nopesOrMultiples = nopes @ (multiples |> collect snd)
                    [|0 .. x.SplitNames.Length - 1 |] |> except nopesOrMultiples

                let uniqCats: (PIndex * SemanticCategory)[] =
                    uniqs |> map (fun i -> i, x.SplitSemanticCategories[i])

                let shownCategories:SemanticCategory[] =
                    // SemanticCategory 중에 한번이라도 나타난 모든 것들 수집
                    x.SplitSemanticCategories |> filter ((<>) Nope) |> distinct

                let notShownCategories:SemanticCategory[] =
                    let allCases = DU.Cases<SemanticCategory>() |> Seq.cast<SemanticCategory>
                    // SemanticCategory 중에 한번도 나타나지 않은 모든 것들 수집
                    allCases
                    |> filter (fun c -> c <> Nope && not (shownCategories |> contains c))
                    |> toArray

                { Multiples = multiples; Nopes = nopes; Uniqs = uniqCats; Showns = shownCategories; NotShowns = notShownCategories}

            member x.Stringify(
                  ?withAction:bool
                , ?withState:bool
                , ?withModifiers:bool
                , ?withUnmatched:bool
                , ?withFlowNumber:bool
                , ?withDeviceNumber:bool
                , ?withActionNumber:bool
                , ?withStateNumber:bool
                , ?withModifierNumber:bool
              ) =
                let withAction    = withAction    |? false
                let withState     = withState     |? false
                let withModifiers = withModifiers |? false
                let withUnmatched = withUnmatched |? false

                let withFN = withFlowNumber     |? true
                let withDN = withDeviceNumber   |? false
                let withAN = withActionNumber   |? true
                let withSN = withStateNumber    |? true
                let withMN = withModifierNumber |? true

                let stringify (nn:NameWithNumber) (withNumber:bool): string =
                    withNumber ?= (nn.PName, nn.Name)
                let stringify (nns:NameWithNumber[]) (withNumber:bool): string =
                    nns |> map (fun nn -> stringify nn withNumber) |> String.concat "_"

                let flow      = stringify x.Flows withFN
                let device    = stringify x.Devices withDN
                let state     = if withState     then stringify x.States    withSN else ""
                let action    = if withAction    then stringify x.Actions   withAN else ""
                let modifiers = if withModifiers then stringify x.Modifiers withMN else ""

                let unmatched =
                    if withUnmatched then
                        x.SplitSemanticCategories
                        |> Seq.choosei (fun i c -> c = Nope ?= (Some i, None))
                        |> map id
                        |> map (fun idx -> x.SplitNames[idx])
                        |> String.concat ":"
                    else ""

                [| flow; device; action; state; modifiers; unmatched |]
                |> filter _.NonNullAny()
                |> String.concat "_"




    type Builder =
        static member ExtractDevices(plcTags:#IPlcTag[], semantics:Semantic): Device[] =
            let anals:AnalyzedNameSemantic[] =
                plcTags
                |> map (fun t ->
                    AnalyzedNameSemantic.CreateDefault(t.GetAnalysisField(), semantics))
            [||]
