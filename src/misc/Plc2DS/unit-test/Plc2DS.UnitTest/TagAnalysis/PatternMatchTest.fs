namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS

module PatternMatchTest =
    let dataDir = "Z:/dsev2/src/misc/Plc2DS/unit-test/Plc2DS.UnitTest/Samples/LS/Autoland광명2"
    let sm = Semantic.Create()

    let regexMatch (sm:Semantic) (name:string) =
        StringSearch.MatchRegexFDA(name, sm.CompiledRegexPatterns)
        |> map _.Stringify()

    type B() =
        [<Test>]
        member _.``Minimal`` () =
            "STATION2_1ST_SHT_ADV" |> regexMatch sm === [|
                "STATION2@0"
                "1ST_SHT@9"
                "ADV@17"
            |]

            let xxx = "MY_SPECIAL_FLOW_1ST_SHT_ADV" |> regexMatch sm

            "MY_SPECIAL_FLOW_1ST_SHT_ADV" |> regexMatch sm === [|
                "MY@0"
                "SPECIAL_FLOW_1ST_SHT@3"
                "ADV@24"
            |]

            do
                let sm = Semantic.Create()
                [ "MY_SPECIAL_FLOW"; "ANOTHER_SPECIAL_FLOW"; ] |> iter (fun w -> sm.SpecialFlows.Add(w) |> ignore)
                [ "MY_ACTION"; "ANOTHER_ACTION"; ] |> iter (fun w -> sm.SpecialActions.Add(w) |> ignore)
                sm.CompileRegexPatterns()

                do

                    "MY_SPECIAL_FLOW_1ST_SHT_ADV" |> regexMatch sm === [|
                        "MY_SPECIAL_FLOW@0"
                        "1ST_SHT@16"
                        "ADV@24"
                    |]

                    "MY_SPECIAL_FLOW_1ST_SHT_ANOTHER_ACTION" |> regexMatch sm === [|
                        "MY_SPECIAL_FLOW@0"
                        "1ST_SHT@16"
                        "ANOTHER_ACTION@24"
                    |]

                do
                    // special name 에 정규식 패턴 테스트
                    "STATION2_1ST_SHT_ACTION_12345" |> regexMatch sm === [|
                        "STATION2@0"
                        "1ST_SHT_ACTION@9"
                        "12345@24"
                    |]

                    [ "ACTION_\\d+"; ] |> iter (fun w -> sm.SpecialActions.Add(w) |> ignore)
                    sm.CompileRegexPatterns()

                    "STATION2_1ST_SHT_ACTION_12345" |> regexMatch sm === [|
                        "STATION2@0"
                        "1ST_SHT@9"
                        "ACTION_12345@17"
                    |]


