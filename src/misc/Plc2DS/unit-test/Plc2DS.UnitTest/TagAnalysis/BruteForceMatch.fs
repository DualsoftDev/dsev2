namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS

module BruteForceMatch =
    type B() =
        [<Test>]
        member _.``Minimal`` () =
            //let sm = Semantic()
            //sm.Flows   <- WordSet(["STN01"], ic)
            //sm.Devices <- WordSet(["LAMP"; "CYL"], ic)
            //sm.Actions <- WordSet(["CLAMP"], ic)

            do
                let name = "STN01_B_CYL_CLAMP1"
                let rs:MatchSet[] = StringSearch.MatchRawFDA(name, [|"STN01"|], [|"LAMP"; "CYL"|], [|"CLAMP"|])
                rs[0].Matches === [|
                    { Text = "STN01"; Start =  0; Category = DuFlow}
                    { Text = "CYL"  ; Start =  8; Category = DuDevice}
                    { Text = "CLAMP"; Start = 12; Category = DuAction}
                |]

                do
                    let text = "This_is original:Text"
                    let separators = [| "_"; ":" |]
                    let partialMatches = [| { Text = "is"; Start = 5; Category = DuModifier } |]
                    let unmatched:PartialMatch[] = PartialMatch.ComputeUnmatched(text, partialMatches, separators)
                    unmatched === [|
                        { Text = "This"      ; Start =  0; Category = DuUnmatched }
                        { Text = " original" ; Start =  7; Category = DuUnmatched }
                        { Text = "Text"      ; Start = 17;  Category = DuUnmatched }
                    |]

                    unmatched |> Array.iter (fun pm -> printfn "Text: '%s', Start: %d" pm.Text pm.Start)


                PartialMatch.ComputeUnmatched(name, rs[0].Matches) === [|
                    { Text = "_B_"; Start =  5; Category = DuUnmatched }
                    { Text = "_"  ; Start = 11; Category = DuUnmatched }
                    { Text = "1"  ; Start = 17; Category = DuUnmatched }
                |]

                // Separator 적용
                let name = "STN01_B_CYL_CLAMP1"
                PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|]) === [|
                    { Text = "B"; Start =  6; Category = DuUnmatched }
                    { Text = "1"; Start = 17; Category = DuUnmatched }
                |]

                // Discards: 버릴 목록.  (주로 "I", "Q" 등)
                PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|], discards=[|"A"; "B"|]) === [|
                    { Text = "1"; Start = 17; Category = DuUnmatched }
                |]

            do
                let name = "CYL_CLAMP1_STN01_B"
                let rs = StringSearch.MatchRawFDA(name, [|"STN01"|], [|"CYL"; "LAMP"|], [|"CLAMP"|])    // CYL <-> STN, [CYL <-> LAMP] 위치 변경
                rs[0].Matches === [|
                    { Text = "STN01"; Start = 11; Category = DuFlow}
                    { Text = "CYL"  ; Start =  0; Category = DuDevice}
                    { Text = "CLAMP"; Start =  4; Category = DuAction}
                |]
                PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|]) === [|
                    { Text = "1"; Start =  9; Category = DuUnmatched }
                    { Text = "B"; Start = 17; Category = DuUnmatched }
                |]


            do
                let name = "STN01_CLOCK_LOCK"
                let rs = StringSearch.MatchRawFDA(name, [|"STN01"|], [|"LOCK"; "CLOCK"|], [|"LOCK"|])
                rs[0].Matches === [|
                    { Text = "STN01"; Start =  0; Category = DuFlow}
                    { Text = "CLOCK"; Start =  6; Category = DuDevice}    // CLOCK, LOCK 가능하나, CLOCK 이 더 길어서 선택됨
                    { Text = "LOCK" ; Start = 12; Category = DuAction}
                |]
                PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|]) === [||]

            do
                let name = "S211_I_RB4_1ST_WORK_COMP"
                do
                    let rs = StringSearch.MatchRawFDA(name, [|"S211"|], [|"RB4"|], [|"1ST_WORK_COMP"|])
                    rs[0].Matches === [|
                        { Text = "S211"          ; Start =  0; Category = DuFlow}
                        { Text = "RB4"           ; Start =  7; Category = DuDevice}
                        { Text = "1ST_WORK_COMP" ; Start = 11; Category = DuAction}
                    |]
                    PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|]) === [|
                        { Text = "I"; Start = 5; Category = DuUnmatched }
                    |]

                    PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|], discards=[|"I"; "Q"|]) === [||]
                do
                    let rs = StringSearch.MatchRawFDA(name, [|"S211"|], [|"RB4"|], [|"WORK_COMP"|])
                    rs[0].Matches === [|
                        { Text = "S211"      ; Start =  0; Category = DuFlow}
                        { Text = "RB4"       ; Start =  7; Category = DuDevice}
                        { Text = "WORK_COMP" ; Start = 15; Category = DuAction}
                    |]
                    PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|]) === [|
                        { Text = "I"  ; Start =  5; Category = DuUnmatched }
                        { Text = "1ST"; Start = 11; Category = DuUnmatched }
                    |]

                    PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|], discards=[|"I"; "Q"|]) === [|
                        { Text = "1ST"; Start = 11; Category = DuUnmatched }
                    |]

                do
                    let name = "BR_I_EM_STOP_X"
                    let rs = StringSearch.MatchRawFDA(name, [||], [||], [| "EM_STOP"|])
                    rs[0].Matches === [|
                        { Text = "EM_STOP"   ; Start =  5; Category = DuAction}
                    |]
                    PartialMatch.ComputeUnmatched(name, rs[0].Matches, separators=[|"_"|], discards=[|"I"; "Q"|]) === [|
                        { Text = "BR"; Start = 0;  Category = DuUnmatched }
                        { Text = "X"; Start = 13; Category = DuUnmatched }
                    |]

            noop()

