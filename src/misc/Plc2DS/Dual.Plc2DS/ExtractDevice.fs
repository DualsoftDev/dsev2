namespace Dual.Plc2DS

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open Dual.Plc2DS.Common.FS
open Dual.Common.Core.FS
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


    [<AutoOpen>]
    module (*internal*) rec ExtractDeviceImplModule =
        type SemanticCategory =
            | Nope
            | Action
            | Device
            | Flow
            | Modifier
            | State

        type AnalyzedNameSemantic = {
            /// 이름 원본
            FullName: string
            /// '_' 기준 분리된 이름
            SplitNames: string[]
            /// SplitNames 각각에 대한 SemanticCategory
            SplitSemanticCategories: SemanticCategory[]
            Flows     : NameWithNumber[]
            Actions   : NameWithNumber[]      // e.g "ADV"
            Devices   : NameWithNumber[]      // e.g "ADV"
            States    : NameWithNumber[]      // e.g "ERR"
            Modifiers : NameWithNumber[]
        }
        type AnalyzedNameSemantic with
            static member Create(name:string, ?semantics:Semantic) =
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
                        Modifiers = [||]
                    }
                match semantics with
                | Some sm ->
                    let standardPNames = baseline.SplitNames |> map sm.StandardizePName

                    let categories = Array.copy baseline.SplitSemanticCategories

                    let procReusults (cat:SemanticCategory) (nns:NameWithNumber[]) =
                        for nn in nns do
                            categories[nn.OptPosition.Value] <- cat


                    let flow      = sm.GuessFlowName      standardPNames |> tee(fun nns -> procReusults Flow     nns)
                    let action    = sm.GuessActionName    standardPNames |> tee(fun nns -> procReusults Action   nns)
                    let state     = sm.GuessStateName     standardPNames |> tee(fun nns -> procReusults State    nns)
                    let device    = sm.GuessDeviceName    standardPNames |> tee(fun nns -> procReusults Device   nns)
                    let modifiers = sm.GuessModifierNames standardPNames |> tee(fun nns -> procReusults Modifier nns)

                    noop()
                    { baseline with
                        Flows = flow
                        Actions = action
                        Devices = device
                        //InputAuxNumber = splitNames.[1] |> GetAuxNumber
                        States = state
                        Modifiers = modifiers
                        SplitSemanticCategories = categories
                    }
                | None -> baseline

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



            member x.Categorize() =
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



                {| Multiples = multiples; Nopes = nopes; Uniqs = uniqCats; Showns = shownCategories; NotShowns = notShownCategories|}
                //multiples, nopes, uniqCats

            member x.PostProcess(semantic:AppSettings): AnalyzedNameSemantic = x


    type Builder =
        static member ExtractDevices(plcTags:#IPlcTag[], semantics:Semantic): Device[] =
            let anals:AnalyzedNameSemantic[] =
                plcTags
                |> map (fun t ->
                    AnalyzedNameSemantic.Create(t.GetAnalysisField(), semantics))
            [||]
