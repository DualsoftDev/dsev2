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
        type NameAnalysis = {
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
            mutable Discards  : NameWithNumber[]
            mutable PrefixModifiers  : NameWithNumber[]
            mutable PostfixModifiers : NameWithNumber[]
        }

        type Semantic with
            /// 이름과 Semantic 정보를 받아서, 분석된 정보를 반환.  기본 처리만 수행
            member internal sm.CreateDefault(name:string): NameAnalysis =
                let splitNames =
                    // camelCase 분리 : aCamelCase -> [| "a"; "Camel"; "Case" |]
                    let splitCamelCase (input: string) =
                        let sep = "<_sep_>"
                        Regex.Replace(input, "(?<!^)([A-Z])", $"{sep}$1") // 첫 글자는 제외하고 대문자 앞에 separator 추가
                            .Split(sep)

                    let splitter (x:string) = if sm.SplitOnCamelCase then splitCamelCase x else [|x|]

                    let delimiter:string[] = sm.NameSeparators.ToArray()
                    name.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                    |> bind splitter
                    |> map _.ToUpper()

                let baseline =
                    {   FullName = name; SplitNames = splitNames; SplitSemanticCategories = Array.init splitNames.Length (konst DuNone)
                        Flows = [||]; Actions = [||]; Devices = [||]; States = [||]
                        Modifiers = [||]; Discards = [||]; PrefixModifiers = [||]; PostfixModifiers = [||]
                    }


                let standardPNames = splitNames |> map sm.StandardizePName

                let categories = Array.copy baseline.SplitSemanticCategories

                let procReusults (cat:SemanticCategory) (nns:NameWithNumber[]) =
                    for nn in nns do
                        categories[nn.OptPosition.Value] <- cat


                let flows            = sm.GuessFlowNames            standardPNames |> tee(fun nns -> procReusults DuFlow     nns)
                let actions          = sm.GuessActionNames          standardPNames |> tee(fun nns -> procReusults DuAction nns)
                let states           = sm.GuessStateNames           standardPNames |> tee(fun nns -> procReusults DuState  nns)
                let devices          = sm.GuessDeviceNames          standardPNames |> tee(fun nns -> procReusults DuDevice   nns)
                let modifiers        = sm.GuessModifierNames        standardPNames |> tee(fun nns -> procReusults DuModifier nns)
                let discards         = sm.GuessDiscards             standardPNames |> tee(fun nns -> procReusults DuDiscard nns)
                let prefixModifiers  = sm.GuessPrefixModifierNames  standardPNames |> tee(fun nns -> procReusults DuModifier nns)
                let postfixModifiers = sm.GuessPostfixModifierNames standardPNames |> tee(fun nns -> procReusults DuModifier nns)

                noop()
                { baseline with
                    Flows = flows
                    Actions = actions
                    Devices = devices
                    States = states
                    Modifiers = modifiers
                    Discards = discards
                    PrefixModifiers = prefixModifiers
                    PostfixModifiers = postfixModifiers
                    SplitSemanticCategories = categories
                }

            /// 이름과 Semantic 정보를 받아서, 분석된 정보를 반환.  부가 처리 수행
            [<Obsolete("Prefix, Postfix modifier 위치 지정")>]
            member sm.Create(name:string): NameAnalysis =
                sm.CreateDefault(name)
                    .FillEmptyPName(sm)

            member sm.ExtractDevices(plcTags:#IPlcTag[]): Device[] =
                let anals:NameAnalysis[] =
                    plcTags
                    |> map (fun t ->
                        sm.CreateDefault(t.GetAnalysisField()))
                [||]

        type NameAnalysis with

            /// PName 중에서 category 할당 안된 항목 채우기.  Semantic.PositinalHints 참고하여 위치 기반으로 항목 채움
            /// 채울 수 없으면 원본 그대로 반환.  변경되면 사본 반환
            member internal x.FillEmptyPName(semantic:Semantic): NameAnalysis =
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
                    | DuAction        -> dup.Actions          <- guessedNames
                    | DuDevice          -> dup.Devices          <- guessedNames
                    | DuFlow            -> dup.Flows            <- guessedNames
                    | DuModifier        -> dup.Modifiers        <- guessedNames
                    | DuDiscard         -> dup.Discards         <- guessedNames
                    | DuPrefixModifier  -> dup.PrefixModifiers  <- guessedNames
                    | DuPostfixModifier -> dup.PostfixModifiers <- guessedNames
                    | DuState -> dup.States    <- guessedNames
                    | (DuUnmatched | DuNone) -> failwith "ERROR"

                    dup



            /// PName 중에서 복수 category 할당 된 항목 처리
            member x.Disambiguate(semantic:Semantic): NameAnalysis =
                let cs = x.Categorize()
                x

            member x.PostProcess(semantic:Semantic): NameAnalysis = x

            member x.Categorize() :CategorySummary =
                // x.SplitSemanticCategories 의 SemanticCategory 별 indices 를 반환
                let multiples: (SemanticCategory * PIndex[])[] =
                    // x.SplitSemanticCategories 에서 같은 SemanticCategory 가 2개 이상인 것들에 대해, key 와 index 들을 추출.
                    x.SplitSemanticCategories
                    |> mapi (fun i cat -> cat, i)  // 각 카테고리와 해당 인덱스를 튜플로 매핑
                    |> groupBy fst                 // SemanticCategory별로 그룹화
                    |> filter (fun (cat, items) -> cat <> DuNone && items.Length > 1) // 2개 이상인 것만 필터링
                    |> map (fun (cat, items) -> cat, items |> map snd) // (카테고리, 인덱스 배열) 반환

                let nopes: PIndex[] =
                    x.SplitSemanticCategories
                    |> mapi (fun i cat -> cat, i)
                    |> filter (fun (cat, _) -> cat = DuNone)
                    |> map snd

                let uniqs: PIndex[] =
                    let nopesOrMultiples = nopes @ (multiples |> collect snd)
                    [|0 .. x.SplitNames.Length - 1 |] |> except nopesOrMultiples

                let uniqCats: (PIndex * SemanticCategory)[] =
                    uniqs |> map (fun i -> i, x.SplitSemanticCategories[i])

                let shownCategories:SemanticCategory[] =
                    // SemanticCategory 중에 한번이라도 나타난 모든 것들 수집
                    x.SplitSemanticCategories |> filter ((<>) DuNone) |> distinct

                let notShownCategories:SemanticCategory[] =
                    let allCases = DU.Cases<SemanticCategory>() |> Seq.cast<SemanticCategory>
                    // SemanticCategory 중에 한번도 나타나지 않은 모든 것들 수집
                    allCases
                    |> filter (fun c -> c <> DuNone && not (shownCategories |> contains c))
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
                        |> Seq.choosei (fun i c -> c = DuNone ?= (Some i, None))
                        |> map id
                        |> map (fun idx -> x.SplitNames[idx])
                        |> String.concat ":"
                    else ""

                [| flow; device; action; state; modifiers; unmatched |]
                |> filter _.NonNullAny()
                |> String.concat "_"




