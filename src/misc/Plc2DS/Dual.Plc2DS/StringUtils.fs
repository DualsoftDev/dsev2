namespace Dual.Plc2DS

open System.Collections.Generic
open Dual.Common.Core.FS

type WordSet = HashSet<string>
type Words = string[]

/// 범위 지정: 백분율 Min/Max : [0..100]
type Range = { Min: int; Max: int }

[<AutoOpen>]
module StringUtils =
    type Range with
        member x.ToTuple() = x.Min, x.Max
        static member FromTuple (min, max) = { Min = min; Max = max }

    let rangesSum (xs: (int*int)[]) =
        xs |> sumBy (fun (min, max) -> max - min)


    type StringSearch =
        static member IndexOfUnique (heystack:string, needle:string) =
            let index = heystack.IndexOf(needle)
            if index >= 0   // 한번 이상 존재하고
                && heystack.Substring(index+needle.Length).IndexOf(heystack) = -1   // 유일하게 존재
            then
                index
            else
                -1


        static member FindPositiveRangeIntersects (heystack: int * int, needles: (int * int)[]): (int * int)[] =
            let (hMin, hMax) = heystack
            needles
            |> Array.choose (fun (nMin, nMax) ->
                let minIntersect = max hMin nMin
                let maxIntersect = min hMax nMax
                if minIntersect <= maxIntersect then
                    Some (minIntersect, maxIntersect)
                else
                    None
            )

        static member FindNegativeRangeIntersects (ranges: (int * int)[]): (int * int)[] =
            ranges
            |> Array.collect (fun (aMin, aMax) ->
                ranges
                |> Array.choose (fun (bMin, bMax) ->
                    let minIntersect = max aMin bMin
                    let maxIntersect = min aMax bMax
                    if minIntersect <= maxIntersect && (aMin, aMax) <> (bMin, bMax) then
                        Some (minIntersect, maxIntersect)
                    else
                        None
                )
            )
            |> Array.distinct  // 중복 제거


        /// name: flow, device, action 이름을 잠재적으로 포함할 수 있는 문자열
        /// flows: flow 의 이름이 될 수 있는 단어들
        static member FindFDA (name:string, flows:Words, devices:Words, actions:Words) =
            let maxInspection = 3
            let indices (cat:SemanticCategory) (xs:Words): (string * int * SemanticCategory)[] =
                xs |> seq
                |> fun xs ->
                    noop()
                    xs
                |> map (fun w -> let i = StringSearch.IndexOfUnique(name, w) in  w, i, cat)        // word, start
                |> fun xs ->
                    noop()
                    xs
                |> filter (fun (w, i, cat) -> i >= 0)       // start >= 0
                |> fun xs ->
                    noop()
                    xs
                |> Seq.truncate maxInspection           // 최대 3개까지만 검사
                |> sortByDescending Tuple.second
                |> toArray

            let ifs   = flows   |> indices Flow      // flow 의 이름 후보군이 name 에 포함되는 모든 위치 검색
            let ids   = devices |> indices Device
            let ias   = actions |> indices Action

            let xss = [ifs; ids; ias] |> sortByDescending _.Length
            let heystack = (0, name.Length)

            // (int * (string * int * SemanticCategory)[])[]
            // int: score, string: word, int: start, SemanticCategory: category
            let matches =
                [|
                    // FDA 셋다 포함하면서, FDA match 간 overwrap 이 없고, 긴 match를 가장 선호
                    for xs0 in xss[0] do
                        let w0, s0, c0 = xs0
                        let r0 = (s0, s0 + w0.Length)
                        let score = w0.Length
                        yield score, [|xs0|]

                        for xs1 in xss[1] do
                            let w1, s1, c1 = xs1
                            let r1 = (s1, s1 + w1.Length)

                            let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1|]) |> rangesSum
                            let m = StringSearch.FindNegativeRangeIntersects([|r0; r1|])           |> rangesSum
                            let score = w0.Length + w1.Length + p - 10 * m
                            yield score, [|xs0; xs1|]

                            for xs2 in xss[2] do
                                let w2, s2, c2 = xs2
                                let r2 = (s2, s2 + w2.Length)

                                let p = StringSearch.FindPositiveRangeIntersects(heystack, [|r0; r1; r2|]) |> rangesSum
                                let m = StringSearch.FindNegativeRangeIntersects([|r0; r1; r2|])           |> rangesSum

                                let score = w0.Length + w1.Length + w2.Length + p - 10 * m
                                yield score, [|xs0; xs1; xs2|]
                |] |> sortByDescending fst
            matches


