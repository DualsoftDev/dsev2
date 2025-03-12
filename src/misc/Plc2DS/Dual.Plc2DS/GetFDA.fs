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
        member x.TryGetFDA(semantic:Semantic): (string*string*string) option =
            let sm = semantic
            let fs = sm.Flows   |> toArray
            let ds = sm.Devices |> toArray
            let zs = sm.Actions |> toArray
            let name = x.GetName()

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
