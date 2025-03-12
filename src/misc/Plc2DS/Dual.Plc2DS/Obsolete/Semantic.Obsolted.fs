namespace Dual.Plc2DS

open System
open System.Linq
open System.Collections.Generic
open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Core
open System.Text.RegularExpressions

module ObsoletedSemantic =

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

        member x.CaseSensitiveName = name        // 대소문자 변환 전의 이름
        member x.OptPrefixNumber = optPrefixNumber
        member x.OptPostfixNumber = optPostfixNumber

        /// PName 의 position
        member val OptPosition:PIndex option = None with get, set

        member x.Name = x.CaseSensitiveName.ToUpper()
        override x.ToString (): string =
            let o2s (n:int option) = n |> map toString |? "~"
            $"{o2s x.OptPrefixNumber}:{x.Name}:{o2s x.OptPostfixNumber}@{x.OptPosition.Value}"

    type NameWithNumber with
        static member Create(name, ?optPrefixNumber:int, ?optPostfixNumber:int) =
            NameWithNumber(name, optPrefixNumber, optPostfixNumber)

        member x.PName =
            let mutable r = ""
            x.OptPrefixNumber.Iter (fun prefix -> r <- $"{prefix}")
            r <- r + x.Name
            x.OptPostfixNumber.Iter (fun postfix -> r <- $"{r}{postfix}")
            r


    type NameWithNumbers = NameWithNumber[]

    type NwN = NameWithNumber
    type NwNs = NameWithNumbers

    /// "STN1_CYL1_ACTION1" 에 대해 device name (e.g CYL1) matching 하였을 때,
    ///
    /// 성공하면 Some (=> "CYL1", STN1_", "_ACTION1")     // Name, Prolog, Epilog
    /// 실패하면 None
    type NameMatchResult = { Name:string; Prolog:string; Epilog:string}


    let zeroNN = NwN.Create("")

    type Semantic with
        /// pName 에서 뒤에 붙은 숫자 부분 제거 후, 표준어로 변환
        member x.StandardizePName(pName:string): NwN =   // pName : partial name: '_' 로 분리된 이름 중 하나
            let (preNumber:int option), name, (postNumber:int option) = splitNumber pName
            match x.Dialects.TryGet(name) with
            | Some standard -> NwN(standard, preNumber, postNumber)
            | None -> NwN(name, preNumber, postNumber)

        /// 공통 검색 함수: standardPNames 배열에서 targetSet에 있는 첫 번째 단어 반환 (없으면 null)
        member private x.GuessNames(targetSet: WordSet, standardPNames: NwNs): NwNs =
            [|
                for (i, nn) in standardPNames.Indexed() do
                    if targetSet.Contains (nn.PName) || targetSet.Contains (nn.Name) then
                        nn.OptPosition <- Some i
                        nn
            |]

        // standardPNames : 표준화된 부분(*P*artial) 이름
        /// standardPNames 중에서 Flow 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessFlowNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.Flows, standardPNames)

        /// standardPNames 중에서 Device 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessDeviceNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.Devices, standardPNames)

        /// standardPNames 중에서 Action 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessActionNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.Actions, standardPNames)

        /// standardPNames 중에서 State 에 해당하는 것이 존재하면, 그것과 index 반환
        member x.GuessStateNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.States, standardPNames)

        /// standardPNames 중에서 Modifiers 에 해당하는 것들이 존재하면, (그것과 index) 배열 반환
        member x.GuessModifierNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.Modifiers, standardPNames)

        member x.GuessDiscards(standardPNames: NwNs): NwNs =
            x.GuessNames(x.Discards, standardPNames)


        /// standardPNames 중에서 PrefixModifiers 에 해당하는 것들이 존재하면, (그것과 index) 배열 반환
        member x.GuessPrefixModifierNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.PrefixModifiers, standardPNames)


        /// standardPNames 중에서 PostfixModifiers 에 해당하는 것들이 존재하면, (그것과 index) 배열 반환
        member x.GuessPostfixModifierNames(standardPNames: NwNs): NwNs =
            x.GuessNames(x.PostfixModifiers, standardPNames)



        member x.ExpandDialects(targetSet:WordSet) =
            [|
                for w in targetSet do
                    yield w
                    for (KeyValue(dialect, standard)) in x.Dialects do
                        if w = standard then
                            yield dialect
            |]


        /// 공통 word to word 직접 검색 함수: name 배열에서 targetSet에 있는 첫 번째 단어 반환 (없으면 null)
        member private x.TryMatchName(targetSet: WordSet, name:string): NameMatchResult option =

            x.ExpandDialects(targetSet)
            |> Seq.tryPick (fun n ->
                match name.Split(n) |> List.ofArray with
                | [prolog; epilog] -> Some { Name = n; Prolog = prolog; Epilog = epilog }
                | [prolog] when name.EndsWith(n) -> Some { Name = n; Prolog = prolog; Epilog = "" }
                | [epilog] when name.StartsWith(n) -> Some { Name = n; Prolog = ""; Epilog = epilog }
                | _ -> None)

        member private x.TryPatternMatchName(patternSet: WordSet, name: string): NameMatchResult option =
            let convertPattern (pattern: string) : string =
                // 패턴을 명명된 그룹을 포함한 전체 매칭 패턴으로 변환
                $"^(?<Prolog>.*?)(?<Name>{pattern})(?<Epilog>.*?)$"

            patternSet
            |> Seq.map convertPattern  // 자동 변환된 정규식 패턴 배열 생성
            |> Seq.tryPick (fun pattern ->
                let regex = Regex(pattern)
                let matchResult = regex.Match(name)
                if matchResult.Success then
                    let prolog = matchResult.Groups.["Prolog"].Value
                    let matchedName = matchResult.Groups.["Name"].Value
                    let epilog = matchResult.Groups.["Epilog"].Value
                    Some { Name = matchedName; Prolog = prolog; Epilog = epilog }
                else None
            )
