        ///// 문자열에서 `flows`, `devices`, `actions`을 검색하고 `MatchResult` 배열 반환
        //static member private findMatches (searchMethod: string -> string -> int) (name: string) (cat: SemanticCategory) (patterns: string[]): MatchResult[] =
        //    patterns
        //    |> Seq.map (fun pattern ->
        //        let index = searchMethod name pattern
        //        if index >= 0 then Some (MatchResult.Create(pattern, index, cat)) else None)
        //    |> Seq.choose id
        //    |> Seq.sortByDescending (fun mr -> mr.Start)
        //    |> Seq.toArray

        ///// 정규식 검색을 수행하여 `MatchResult` 배열 반환
        //static member private findRegexMatches (searchMethod: string -> Regex -> Match seq) (name: string) (cat: SemanticCategory) (patterns: Regex[]): MatchResult[] =
        //    patterns
        //    |> Seq.collect (fun regex ->
        //        searchMethod name regex |> Seq.map (fun m -> MatchResult.Create(m.Value, m.Index, cat)))
        //    |> Seq.sortByDescending (fun mr -> mr.Start)
        //    |> Seq.toArray

        ///// 점수 계산 및 정렬 수행
        //static member private computeMatches (name: string) (flows: MatchResult[]) (devices: MatchResult[]) (actions: MatchResult[]): (Score * MatchResult[])[] =
        //    let maxInspection = 3
        //    let xss = [flows; devices; actions] |> List.sortByDescending (fun arr -> arr.Length)
        //    let heystack = (0, name.Length)

        //    [|
        //        for xs0 in xss[0] do
        //            let w0, s0, c0 = xs0.ToTuple()
        //            let r0 = (s0, s0 + w0.Length)
        //            let score = w0.Length
        //            yield score, [|xs0|]

        //            for xs1 in xss[1] do
        //                let w1, s1, c1 = xs1.ToTuple()
        //                let r1 = (s1, s1 + w1.Length)

        //                let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1|]) |> rangesSum
        //                let m = StringSearch.FindNegativeRangeIntersects([|r0; r1|]) |> rangesSum
        //                let score = w0.Length + w1.Length + p - 10 * m
        //                yield score, [|xs0; xs1|]

        //                for xs2 in xss[2] do
        //                    let w2, s2, c2 = xs2.ToTuple()
        //                    let r2 = (s2, s2 + w2.Length)

        //                    let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1; r2|]) |> rangesSum
        //                    let m = StringSearch.FindNegativeRangeIntersects([|r0; r1; r2|]) |> rangesSum

        //                    let score = w0.Length + w1.Length + w2.Length + p - 10 * m
        //                    yield score, [|xs0; xs1; xs2|]
        //    |] |> Array.sortByDescending fst



        ///// name 에서 flow, device, action 이름에 해당하는 부분을 찾아서, 각각의 위치를 반환
        /////
        ///// name: flow, device, action 이름을 잠재적으로 포함할 수 있는 문자열
        /////
        ///// flows: flow 의 이름이 될 수 있는 단어들
        //(*
        //    - 길게 match 될 수록 가점
        //    - 3개 다 match 될 수록 가점
        //    - 겹치는 구간 있으면 감점
        //    - 반환 값은 score 순서에 따라 정렬
        //*)
        //static member MatchRawFDA (name: string, flows: Words, devices: Words, actions: Words): (Score * MatchResult[])[] =
        //    let indexOfUnique (name: string) (word: string) = StringSearch.IndexOfUnique(name, word)

        //    let ifs = StringSearch.findMatches indexOfUnique name Flow flows
        //    let ids = StringSearch.findMatches indexOfUnique name Device devices
        //    let ias = StringSearch.findMatches indexOfUnique name Action actions

        //    StringSearch.computeMatches name ifs ids ias

        ///// `MatchRegexFDA`: 정규식을 활용하여 FDA 정보를 찾음
        //static member MatchRegexFDA (remainingName: string, flows: Regex[], devices: Regex[], actions: Regex[]): (Score * MatchResult[])[] =
        //    let regexSearch (name: string) (pattern: Regex) = pattern.Matches(name) |> Seq.cast<Match>

        //    let ifs = StringSearch.findRegexMatches regexSearch remainingName Flow flows
        //    let ids = StringSearch.findRegexMatches regexSearch remainingName Device devices
        //    let ias = StringSearch.findRegexMatches regexSearch remainingName Action actions

        //    StringSearch.computeMatches remainingName ifs ids ias
