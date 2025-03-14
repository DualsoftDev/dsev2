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
        TagString.MatchRegexFDA(name, sm.CompiledFDARegexPatterns)
        |> map _.Text

    type B() =
        [<Test>]
        member _.``Minimal`` () =
            "STATION2_1ST_SHT_ADV" |> regexMatch sm === [| "STATION2"; "1ST_SHT"; "ADV"; |]


            do
                "MY_SPECIAL_FLOW_1ST_SHT_ADV" |> regexMatch sm === [| "MY"; "SPECIAL_FLOW_1ST_SHT"; "ADV" |]

                let sm = Semantic.Create()
                sm.SpecialFlowPatterns <- sm.SpecialFlowPatterns @ [| "MY_SPECIAL_FLOW"; "ANOTHER_SPECIAL_FLOW"; |]
                sm.SpecialActionPatterns <- sm.SpecialActionPatterns @ [| "MY_ACTION"; "ANOTHER_ACTION"; |]
                sm.CompileAllRegexPatterns()

                do

                    "MY_SPECIAL_FLOW_1ST_SHT_ADV" |> regexMatch sm === [| "MY_SPECIAL_FLOW"; "1ST_SHT"; "ADV"; |]

                    "MY_SPECIAL_FLOW_1ST_SHT_ANOTHER_ACTION" |> regexMatch sm === [| "MY_SPECIAL_FLOW"; "1ST_SHT"; "ANOTHER_ACTION"; |]

                do
                    // special name 에 정규식 패턴 테스트
                    "STATION2_1ST_SHT_ACTION_12345" |> regexMatch sm === [| "STATION2"; "1ST_SHT_ACTION"; "12345"; |]

                    sm.SpecialActionPatterns <- sm.SpecialActionPatterns @ [| "ACTION_\\d+"; |]
                    sm.CompileAllRegexPatterns()

                    "STATION2_1ST_SHT_ACTION_12345" |> regexMatch sm === [| "STATION2"; "1ST_SHT"; "ACTION_12345"; |]

        [<Test>]
        member _.``SpecialAction`` () =
            do
                do
                    let sm = Semantic.Create()
                    sm.SpecialFlowPatterns <- [||]
                    sm.SpecialActionPatterns <- [||]
                    sm.CompileAllRegexPatterns()
                    "DNDL_Q_RB3_CN_2000" |> regexMatch sm === [| "DNDL"; "Q_RB3_CN"; "2000"; |]
                do
                    let sm = Semantic.Create()
                    sm.SpecialActionPatterns <- sm.SpecialActionPatterns @ [| "CN_\\d+"; "BT_(\\d+M|CHANGE|NORMAL|AS|EMPTY|EMPTY_CRR|NG_BODY|NG_CRR|OUT_CRR|STOCK)" |]
                    sm.CompileAllRegexPatterns()

                    "DNDL_Q_RB3_CN_2000" |> regexMatch sm === [| "DNDL"; "Q_RB3"; "CN_2000"; |]
                    "S506_WRS_LH_ARG_CT_CLP_ADV_BT_3M" |> regexMatch sm === [| "S506"; "WRS_LH_ARG_CT_CLP_ADV"; "BT_3M"; |]


        [<Test>]
        member _.``AbnormalCases`` () =
            "MES_재투입BODY_서열[0]" |> regexMatch sm === [| "MES"; "재투입BODY"; "서열[0]"; |]

            "S302_ROBOT2.M_RBT_CLEANNER_BYPASS" |> regexMatch sm === [| "S302"; "ROBOT2.M_RBT_CLEANNER"; "BYPASS"; |]

            "S100-1_RBT_WELD_OK" |> regexMatch sm === [| "S100-1"; "RBT_WELD"; "OK"; |]

