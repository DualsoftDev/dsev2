namespace Dual.Plc2DS

open System.Text.RegularExpressions

open Dual.Plc2DS
open Dual.Common.Core.FS

[<AutoOpen>]
module GetFDA =
    let private compiledRegexPattern = Regex(@"^(?<flow>[^_]+)_(?<device>.+)_(?<action>[^_]+)$", RegexOptions.Compiled)
    type StringSearch =
        /// 위치 기반 정규식 매칭: (Flow)_(Device)_(Action) 형식의 문자열에서 각 부분을 추출
        ///
        /// baseline 으로, 다른 방법이 통하지 않을 때 마지막 수단으로 사용
        // Regex(@"^(?<flow>[^_]+)_(?<device>.*)_(?<action>[^_]+)$", RegexOptions.Compiled)
        static member MatchRegexFDA (name:string, ?tagFDAPattern:Regex): PartialMatch[] =
            let tagFDAPattern = tagFDAPattern |? compiledRegexPattern
            [|
                match tagFDAPattern.Match(name) with
                | m when m.Success ->
                    for groupName in compiledRegexPattern.GetGroupNames() do
                        if groupName.IsOneOf("flow", "device", "action") then
                            let group = m.Groups.[groupName]
                            if group.Success then
                                yield { Text = group.Value; Start = group.Index; Category = DuUnmatched }
                | _ ->
                    logWarn $"WARN: {name} 에서 Flow/Device/Action 추출 실패"
            |]



    type IPlcTag with
        /// PlcTag 정보로부터 flow, device, action 명 추출
        // - 기본 : (flow)_(device_)+_(action).  즉 '_' 기준으로 맨처음과 맨마지막을 제외한 나머지는 device로 간주
        // - 변칙 :
        //  . "action_숫자" 형식으로 끝날 경우, action으로 간주
        //  . device 명에서 discards 처리 ("_I_", "_Q_", "_LS_", ... 등 무시할 것 처리)
        //  . flow 및 action 이름이 "_" 를 포함하는 multi-word 인 경우, semantic 에 따로 등록한 경우만 처리
        member x.TryGetFDA(semantic:Semantic): (string*string*string) option =
            let sm = semantic
            let fs = sm.Flows   |> toArray
            let ds = sm.Devices |> toArray
            let zs = sm.Actions |> toArray
            let name = x.GetName()

            // "DNDL_Q_RB3_CN_2000" 처럼 _ 뒤에 숫자로 끝날 경우, action 에 귀속
            let tailNumber = Regex.Match(name, "_\d+$")
            let nname, ntail =
                if tailNumber.Success then
                    let tail = tailNumber.Value
                    let head = name.Substring(0, name.Length - tail.Length)
                    head, tail
                else
                    name, ""

            StringSearch.MatchRegexFDA(nname)
            |> map _.Text
            |> fun xs ->
                if xs.Length = 3 then
                    Some (xs.[0], xs.[1], (xs.[2] + ntail))
                else
                    None
