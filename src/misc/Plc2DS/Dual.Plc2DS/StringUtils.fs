namespace Dual.Plc2DS

open System.Collections.Generic
open Dual.Common.Core.FS
open System.Text.RegularExpressions
open System

type WordSet = HashSet<string>
type Words = string[]

/// 범위 지정: 백분율 Min/Max : [0..100]
type Range = { Min: int; Max: int }
/// tuple'ed range
type TRange = int * int

/// word 내에서의 위치
type StringIndex = int
type Score = int

type PartialMatch = {
    Text:string
    Start:StringIndex
    Category:SemanticCategory
} with
    static member Create(text:string, start:StringIndex, category:SemanticCategory) = { Text = text; Start = start; Category = category }
    member x.ToTuple() = x.Text, x.Start, x.Category

    // - partialMatches 배열을 Start 기준으로 정렬
    // - text에서 매칭되지 않은 부분을 찾고 PartialMatch 객체로 변환하여 리스트에 추가
    // - 마지막 매치 이후 남은 텍스트도 처리
    static member ComputeUnmatched(text: string, partialMatches: PartialMatch[], ?separators: string[]): PartialMatch[] =
        let separators = separators |? [||]
        let sortedMatches = partialMatches |> Array.sortBy (fun pm -> pm.Start)
        let result = ResizeArray<PartialMatch>()
        let mutable lastEnd = 0

        let filterUnmatchedText(text: string, separators: string[]): (char * int)[] =
            text
            |> choosei (fun i c -> if separators |> contains (string c) then None else Some (c, i))
            |> toArray

        let addResult(filteredText:(char*int)[]) =
            if filteredText.Length > 0 then
                let startIndex = lastEnd + (filteredText |> map snd |> Array.min)
                let filteredStr = filteredText |> map fst |> System.String.Concat
                result.Add( { Text=filteredStr; Start=startIndex; Category=DuUnmatched } )

        for pm in sortedMatches do
            if lastEnd < pm.Start then
                let unmatchedText = text.Substring(lastEnd, pm.Start - lastEnd)
                let filteredText = filterUnmatchedText(unmatchedText, separators)
                addResult filteredText
            lastEnd <- pm.Start + pm.Text.Length

        if lastEnd < text.Length then
            let unmatchedText = text.Substring(lastEnd)
            let filteredText = filterUnmatchedText(unmatchedText, separators)
            addResult filteredText

        result.ToArray()



type MatchSet = {
    Score:Score
    Matches:PartialMatch[]
}

[<AutoOpen>]
module StringUtils =
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
        static member GetIntersectionRanges (ranges: (TRange)[]): (TRange)[] =
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
                    // FDA 셋다 포함하면서, FDA match 간 overwrap 이 없고, 긴 match를 가장 선호
                    for xs0:PartialMatch in xss[0] do
                        let w0, s0, c0 = xs0.ToTuple()
                        let r0 = (s0, s0 + w0.Length)
                        let score = w0.Length
                        yield { Score=score; Matches=[|xs0|]}

                        for xs1 in xss[1] do
                            let w1, s1, c1 = xs1.ToTuple()
                            let r1 = (s1, s1 + w1.Length)

                            // xss 끼리 겹치는 부분이 없어야 함. m = 0
                            let m = StringSearch.FindNegativeRangeIntersects([|r0; r1|]) |> rangesSum
                            if m = 0 then
                                // heystack 을 최대한 xss 들이 cover 할수록 높은 점수
                                let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1|]) |> rangesSum
                                let score = w0.Length + w1.Length + p
                                yield { Score=score; Matches=[|xs0; xs1|]}

                                for xs2 in xss[2] do
                                    let w2, s2, c2 = xs2.ToTuple()
                                    let r2 = (s2, s2 + w2.Length)

                                    let m = StringSearch.FindNegativeRangeIntersects([|r0; r1; r2|]) |> rangesSum
                                    if m = 0 then
                                        let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1; r2|]) |> rangesSum

                                        let score = w0.Length + w1.Length + w2.Length + p
                                        let matches = [|xs0; xs1; xs2|] |> sortByDescending _.Category
                                        yield { Score=score; Matches=matches }     // flow, device, action 순서로 정렬
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
                    StringSearch.AllIndices(name, w) |> map (fun i -> PartialMatch.Create(w, i, cat)))
                |> filter (fun mr -> mr.Start >= 0)       // start >= 0
                |> sortByDescending _.Start
                |> toArray

            let ifs = flows   |> indices DuFlow      // flow 의 이름 후보군이 name 에 포함되는 모든 위치 검색
            let ids = devices |> indices DuDevice
            let ias = actions |> indices DuAction

            let xss = [|ifs; ids; ias|] |> sortByDescending Array.length
            let heystack = (0, name.Length)

            StringSearch.ComputeScores(heystack, xss)


        static member MatchRegexFDA (remainingName:string, catRegexs:(SemanticCategory * Regex[])[]): MatchSet[] =
            let indices (cat, patterns: Regex[]) =
                patterns
                |> Seq.collect (fun pattern ->
                    pattern.Matches(remainingName)
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> PartialMatch.Create(m.Value, m.Index, cat))
                )
                |> filter (fun mr -> mr.Start >= 0)
                |> sortByDescending (fun mr -> mr.Start)
                |> toArray

            let xss:PartialMatch[][] =
                catRegexs
                |> map indices
                |> sortByDescending Array.length

            let heystack = (0, remainingName.Length)

            StringSearch.ComputeScores(heystack, xss)