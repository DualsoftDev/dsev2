namespace Dual.Plc2DS

open System.Collections.Generic
open Dual.Common.Core.FS
open System.Text.RegularExpressions
open System
open System.Reactive.Joins

module Obsoleted =
    type PartialMatch with
        member x.ToTuple() = x.Text, x.Start, x.Category

        // - partialMatches 배열을 Start 기준으로 정렬
        // - text에서 매칭되지 않은 부분을 찾고 PartialMatch 객체로 변환하여 리스트에 추가
        // - 마지막 매치 이후 남은 텍스트도 처리
        static member ComputeUnmatched(text:string, partialMatches:PartialMatch[], ?separators:string[], ?discards:string[]):PartialMatch[] =
            let separators = separators |? [||]
            let discards = discards |? [||]
            let sortedMatches = partialMatches |> Array.sortBy (fun pm -> pm.Start)
            let result = ResizeArray<PartialMatch>()
            let mutable lastEnd = 0

            let addResult (unmatchedText: string, startIndex: int) =
                if unmatchedText <> "" then
                    result.Add({ Text = unmatchedText; Start = startIndex; Category = DuUnmatched })

            for pm in sortedMatches do
                if lastEnd < pm.Start then
                    let unmatchedText = text.Substring(lastEnd, pm.Start - lastEnd)
                    addResult (unmatchedText, lastEnd)
                lastEnd <- pm.Start + pm.Text.Length

            if lastEnd < text.Length then
                let unmatchedText = text.Substring(lastEnd)
                addResult (unmatchedText, lastEnd)

            // 여기서 한 번에 filtering 및 가공 수행
            result
            |> collect (fun pm ->
                pm.Text.Split(separators, System.StringSplitOptions.RemoveEmptyEntries)
                |> Array.fold (fun (acc, (offset:StringIndex)) (word:string) ->
                    let actualStart = text.IndexOf(word, offset)
                    let newMatch = { pm with Text = word; Start = actualStart }
                    (newMatch :: acc, actualStart + word.Length)
                ) ([], pm.Start)
                |> fst
                |> List.rev
            )
            |> toArray
            |> Array.filter (fun pm -> not (Array.contains pm.Text discards))




    type MatchSet = {
        Score:Score
        Matches:PartialMatch[]
    }

[<AutoOpen>]
module StringUtils =
    open Obsoleted

    type Range with
        member x.ToTuple() = x.Min, x.Max
        static member FromTuple (min, max) = { Min = min; Max = max }

    let private rangesSum (xs: (StringIndex*StringIndex)[]) =
        xs |> sumBy (fun (min, max) -> max - min)


    type internal StringSearch =
        static member MatchAllWithRegex(target:string, pattern:string): Match[] =
            Regex.Matches(target, pattern) |> Seq.cast<Match> |> Seq.toArray

        /// heystack 에서 needle 이 한번만 나오는 경우에만 해당 위치 값 반환.  그렇지 않으면 -1
        static member AllIndices (heystack:string, needle:string): StringIndex[] =
            let rec findIndices (start:int) (acc:int list) =
                let index = heystack.IndexOf(needle, start)  // start를 int로 명확히 지정
                if index = -1 then
                    acc |> List.toArray
                else
                    findIndices (index + needle.Length) (index :: acc)

            findIndices 0 [] |> Array.rev



        /// heystack(tag name) 에서 여러개의 needle(flow, device, action) 들을 찾아서, 각 needle 의 위치를 반환
        /// match 된 range 의 길이가 길 수록 positive
        static member GetIntersectionRanges (heystack: TRange, needles: (TRange)[]): (TRange)[] =
            let (h, hh) = heystack
            needles
            |> Array.choose (fun (n, nn) -> // min, max
                let i = max h n     // minIntersect
                let ii = min hh nn  // maxIntersect
                if i <= ii then
                    Some (i, ii)
                else
                    None
            )

        /// 이미 match 가 끝난 needle 들의 ranges 간의 intersection.  동일 요소를 중복 match 한 것이므로 range 길이가 길수록 감점 요인
        static member GetIntersectionRangesNew (ranges: (TRange)[]): (TRange)[] =
            ranges
            |> Array.collect (fun (a, aa) ->    // aMin, aMax
                ranges
                |> Array.choose (fun (b, bb) -> // bMin, bMax
                    if (a, aa) = (b, bb) then
                        Some (a, aa)
                    else
                        let i = max a b     // minIntersect
                        let ii = min aa bb  // maxIntersect
                        if i <= ii then
                            Some (i, ii)
                        else
                            None
                )
            )
            |> Array.distinct  // 중복 제거

        static member GetIntersectionRanges (ranges: (TRange)[]): (TRange)[] =
            let sameRanges = ranges |> Array.countBy id |> Array.choose (fun (key, count) -> if count > 1 then Some key else None)
            let overlaps =
                ranges
                |> Array.collect (fun (a, aa) ->    // aMin, aMax
                    ranges
                    |> Array.choose (fun (b, bb) -> // bMin, bMax
                        let i = max a b     // minIntersect
                        let ii = min aa bb  // maxIntersect
                        if i <= ii && (a, aa) <> (b, bb) then
                            Some (i, ii)
                        else
                            None
                    )
                )
            sameRanges @ overlaps
            |> Array.distinct  // 중복 제거

        static member FindPositiveRangeIntersects (heystack: TRange, needles: (TRange)[]): (TRange)[] = StringSearch.GetIntersectionRanges (heystack, needles)
        static member FindNegativeRangeIntersects (ranges: (TRange)[]): (TRange)[] = StringSearch.GetIntersectionRanges ranges


        /// Matching score 계산
        ///
        /// - heystack: 검색 대상 문자열의 범위.  tag name 의 범위
        ///
        /// - xss: 부분 match 들 : Text 및 Start index
        ///
        /// - xss 가 겹치지 않고, heystack 영역을 최대한 많이 커버할 수록 높은 점수
        static member ComputeScores (heystack:TRange, xss:PartialMatch[][]): MatchSet[] =
            let matches: MatchSet[] =
                [|
                    for xs in xss do
                        let ranges =
                            xs
                            |> map (fun x ->
                                let w, s, c = x.ToTuple()
                                (s, s + w.Length))

                        // FDA 셋다 포함하면서, FDA match 간 overwrap 이 없고, 긴 match를 가장 선호
                        if StringSearch.FindNegativeRangeIntersects(ranges) |> rangesSum = 0 then
                            let p = StringSearch.FindPositiveRangeIntersects(heystack, ranges) |> rangesSum
                            let score = (xs |> sumBy _.Text.Length)  + p
                            yield { Score=score; Matches=xs}
                |] |> sortByDescending _.Score
            matches

        /// name 에서 flow, device, action 이름에 해당하는 부분을 찾아서, 각각의 위치를 반환
        ///
        /// name: flow, device, action 이름을 잠재적으로 포함할 수 있는 문자열
        ///
        /// flows: flow 의 이름이 될 수 있는 단어들
        (*
            - 길게 match 될 수록 가점
            - 3개 다 match 될 수록 가점
            - 겹치는 구간 있으면 감점
            - 반환 값은 score 순서에 따라 정렬
        *)
        static member MatchRawFDA (name:string, flows:Words, devices:Words, actions:Words): MatchSet[] =
            let indices (cat:SemanticCategory) (xs:Words): PartialMatch[] =
                xs |> seq
                //|> fun xs ->
                //    noop()
                //    xs
                |> bind (fun w ->
                    StringSearch.AllIndices(name, w)
                    //|> map (fun i -> PartialMatch.Create(w, i, cat)))
                    |> choose(fun i ->
                        let headOk = i = 0 || name[i-1] = '_'
                        let tailOk = i + w.Length = name.Length || name[i+w.Length] = '_'
                        if headOk && tailOk then
                            Some <| PartialMatch.Create(w, i, cat)
                        else
                            None))
                |> filter (fun mr -> mr.Start >= 0)       // start >= 0
                |> sortByDescending _.Start
                |> toArray

            let ifs = flows   |> indices DuFlow      // flow 의 이름 후보군이 name 에 포함되는 모든 위치 검색
            let ids = devices |> indices DuDevice
            let ias = actions |> indices DuAction

            let combineFDA (fs: 't[]) (ds: 't[]) (zs: 't[]) : 't array array =
                match fs, ds, zs with
                | [||], [||], [||] -> [||]
                | _,    [||], [||] -> fs |> map (fun f -> [| f |])
                | [||], _   , [||] -> ds |> map (fun d -> [| d |])
                | [||], [||], _    -> zs |> map (fun z -> [| z |])
                | _,    [||], _    -> allPairs fs zs |> map (fun (f, z) -> [| f; z |])
                | [||], _   , _    -> allPairs ds zs |> map (fun (d, z) -> [| d; z |])
                | _   , _   , [||] -> allPairs fs ds |> map (fun (f, d) -> [| f; d |])
                | _   , _   , _    ->
                    allPairs fs ds
                    |> collect (fun (f, d) -> zs |> map (fun z -> [| f; d; z |]))



            let xss = combineFDA ifs ids ias |> sortByDescending Array.length
            let heystack = (0, name.Length)

            StringSearch.ComputeScores(heystack, xss)


