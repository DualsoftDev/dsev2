module CounterTestModule


open Dual.Common.UnitTest.FS

open NUnit.Framework
open Dual.Ev2
open System
open T
open Dual.Common.Core.FS



//[<AutoOpen>]
//module CounterTestModule =

    type CounterTest() =
        //inherit ExpressionTestBaseClass()
        //do
        //    let ``강제 reference 추가용`` = XGITag.createSymbolInfo
        //    ()


        let evaluateRungInputs (counter:Counter) =
            for s in counter.InputEvaluateStatements do
                s.Do()

        [<Test>]
        member __.``CTU creation test`` () =
            use _ = setRuntimeTarget AB
            let storages = Storages()
            let condition = TValue<bool>(false, Comment="my_counter_control_tag", Address="%M1.1")
            let tcParam = {Storages=storages; Name="myCTU"; Preset=100u; RungInCondition=condition; FunctionName="createWinCTU"}
            let ctu = (CounterStatement.CreateAbCTU tcParam) ExpressionFixtures.runtimeTarget |> toCounter
            ctu.OV.Value === false
            ctu.UN.Value === false
            ctu.DN.Value === false
            ctu.PRE.Value === 100u
            ctu.ACC.Value === 0u


            (* Counter struct 의 내부 tag 들이 생성되고, 등록되었는지 확인 *)
            let internalTags =
                [
                    // CTU 및 CTD 에서는 .CU 와 .CD tag 는 internal 로 숨겨져 있다.
                    ctu.OV :> IStorage
                    ctu.UN
                    ctu.DN
                    ctu.PRE
                    ctu.ACC
                    ctu.RES
                ]

            storages.ContainsKey("myCTU") === true
            for t in internalTags do
                storages.ContainsKey(t.Name) === true


            for i in [1..50] do
                condition.Value <- true
                evaluateRungInputs ctu
                ctu.ACC.Value === uint32 i
                condition.Value <- false
                evaluateRungInputs ctu
                ctu.DN.Value === false

            ctu.ACC.Value === 50u
            ctu.DN.Value === false
            for i in [51..100] do
                condition.Value <- true
                evaluateRungInputs ctu
                ctu.ACC.Value === uint32 i
                condition.Value <- false
                evaluateRungInputs ctu
            ctu.ACC.Value === 100u
            ctu.DN.Value === true

        [<Test>]
        member __.``CTUD creation test`` () =
            use _ = setRuntimeTarget AB
            let storages = Storages()

            let upCondition = TValue<bool>(false, Comment="my_counter_up_tag", Address="%M1.1")
            let downCondition = TValue<bool>(false, Comment="my_counter_down_tag", Address="%M1.1")
            let resetCondition = TValue<bool>(false, Comment="my_counter_reset_tag", Address="%M1.1")


            let tcParam = {Storages=storages; Name="myCTU"; Preset=100u; RungInCondition=upCondition; FunctionName="createWinCTUD"}
            let ctu = CounterStatement.CreateAbCTUD(tcParam, downCondition, resetCondition) ExpressionFixtures.runtimeTarget|> toCounter
            ctu.OV.Value === false
            ctu.UN.Value === false
            ctu.DN.Value === false
            ctu.PRE.Value === 100u
            ctu.ACC.Value === 0u


            (* Counter struct 의 내부 tag 들이 생성되고, 등록되었는지 확인 *)
            let internalTags =
                [
                    ctu.CU :> IStorage
                    ctu.CD
                    ctu.OV
                    ctu.UN
                    ctu.DN
                    ctu.PRE
                    ctu.ACC
                    ctu.RES
                ]

            storages.ContainsKey("myCTU") === true
            for t in internalTags do
                storages.ContainsKey(t.Name) === true

        [<Test>]
        member __.``CTU with reset creation test`` () =
            use _ = setRuntimeTarget WINDOWS
            let storages = Storages()
            let condition = TValue<bool>(false, Comment="my_counter_control_tag", Address="%M1.1")
            let reset = TValue<bool>(false, Comment="my_counter_reset_tag", Address="%M1.1")
            let tcParam = {Storages=storages; Name="myCTU"; Preset=100u; RungInCondition=condition; FunctionName="createWinCTU"}
            let ctu = CounterStatement.CreateCTU(tcParam, reset) ExpressionFixtures.runtimeTarget|> toCounter
            ctu.OV.TValue === false
            ctu.UN.TValue === false
            ctu.DN.TValue === false
            ctu.RES.TValue === false
            ctu.PRE.TValue === 100u
            ctu.ACC.TValue === 0u


            for i in [1..50] do
                condition.TValue <- true
                evaluateRungInputs ctu
                ctu.ACC.TValue === uint32 i
                condition.TValue <- false
                evaluateRungInputs ctu
                ctu.DN.TValue === false

            ctu.ACC.TValue === 50u
            ctu.DN.TValue === false

            // counter reset
            reset.TValue <- true
            evaluateRungInputs ctu
            ctu.OV.TValue === false
            ctu.UN.TValue === false
            ctu.DN.TValue === false
            ctu.RES.TValue === true
            ctu.PRE.TValue === 100u
            ctu.ACC.TValue === 0u


        [<Test>]
        member __.``CTR with reset creation test`` () =
            use _ = setRuntimeTarget WINDOWS
            let storages = Storages()
            let condition = TValue<bool>(false, Comment="my_counter_control_tag", Address="%M1.1")
            let reset = TValue<bool>(false, Comment="my_counter_reset_tag", Address="%M1.1")
            let tcParam = {Storages=storages; Name="myCTR"; Preset=100u; RungInCondition=condition; FunctionName="createWinCTR"}
            let ctr = CounterStatement.CreateXgiCTR(tcParam, reset) ExpressionFixtures.runtimeTarget |> toCounter
            ctr.OV.TValue === false
            ctr.UN.TValue === false
            ctr.DN.TValue === false
            ctr.RES.TValue === false
            ctr.PRE.TValue === 100u
            ctr.ACC.TValue === 0u


            for i in [1..50] do
                condition.TValue <- true
                evaluateRungInputs ctr
                ctr.ACC.TValue === uint32 i
                condition.TValue <- false
                evaluateRungInputs ctr
                ctr.DN.TValue === false
            ctr.ACC.TValue === 50u
            ctr.DN.TValue === false

            for i in [51..99] do
                condition.TValue <- true
                evaluateRungInputs ctr
                ctr.ACC.TValue === uint32 i
                condition.TValue <- false
                evaluateRungInputs ctr
                ctr.DN.TValue === false

            ctr.ACC.TValue === 99u
            ctr.DN.TValue === false

            condition.TValue <- true        // last straw that broken ...
            evaluateRungInputs ctr
            ctr.ACC.TValue === 100u
            ctr.DN.TValue === true

            // counter preset + 1 : ring counter : auto reset
            condition.TValue <- false
            evaluateRungInputs ctr
            condition.TValue <- true
            evaluateRungInputs ctr
            ctr.ACC.TValue === 1u
            ctr.DN.TValue === false




            // force counter reset
            reset.TValue <- true
            evaluateRungInputs ctr
            ctr.OV.TValue === false
            ctr.UN.TValue === false
            ctr.DN.TValue === false
            ctr.RES.TValue === true
            ctr.PRE.TValue === 100u
            ctr.ACC.TValue === 0u


(*
 Timer/Counter 의 Parser 관련 test 부분 : EV2 에서는 불필요
 *)


//        [<Test>]
//        member x.``CTU on AB platform test`` () =
//            use _ = setRuntimeTarget AB
//            let storages = Storages()
//            let code = """
//                bool x0 = createTag("%MX0.0.0", false);
//                ctu myCTU = createAbCTU(2000u, $x0);
//"""

//            let statement = parseCodeForTarget storages code AB
//            [ "CU"; "DN"; "OV"; "UN"; "PRE"; "ACC"; "RES" ] |> iter (fun n -> storages.ContainsKey($"myCTU.{n}") === true)
//            [ "CD"; "Q"; "PT"; "ET"; ] |> iter (fun n -> storages.ContainsKey($"myCTU.{n}") === false)

//        [<Test>]
//        member x.``CTU on XGI platform test`` () =
//            use _ = setRuntimeTarget XGI
//            let storages = Storages()
//            let code = """
//                bool cu = createTag("%MX0", false);
//                bool r  = createTag("%MX1", false);
//                ctu myCTU = createXgiCTU(2000u, $cu, $r);
//"""

//            let statement = parseCodeForTarget storages code XGI
//            [ "CU"; "Q"; "PV"; "CV"; "R"; ] |> iter (fun n -> storages.ContainsKey($"myCTU.{n}") === true)
//            [ "DN"; "PRE"; "ACC"; ] |> iter (fun n -> storages.ContainsKey($"myCTU.{n}") === false)

//        [<Test>]
//        member x.``CTD on WINDOWS platform test`` () =
//            use _ = setRuntimeTarget WINDOWS
//            let storages = Storages()
//            let code = """
//                bool cd = true;
//                bool ld = false;
//                ctd myCTD = createWinCTD(2000u, $cd, $ld);
//"""

//            let statement = parseCodeForWindows storages code
//            [ "CD"; "LD"; "PV"; "Q"; "CV"; ] |> iter (fun n -> storages.ContainsKey($"myCTD.{n}") === true)
//            [ "DN"; "OV"; "UN"; "PRE"; "ACC"; ] |> iter (fun n -> storages.ContainsKey($"myCTD.{n}") === false)

//        [<Test>]
//        member x.``CTD on WINDOWS, XGI platform test`` () =
//            use _ = setRuntimeTarget XGI
//            let storages = Storages()
//            let code = """
//                bool cd = createTag("%MX0", false);
//                bool ld = createTag("%MX1", false);
//                ctd myCTD = createXgiCTD(2000u, $cd, $ld);
//"""

//            let statement = parseCodeForTarget storages code XGI
//            [ "CD"; "LD"; "PV"; "Q"; "CV"; ] |> iter (fun n -> storages.ContainsKey($"myCTD.{n}") === true)
//            [ "DN"; "PRE"; "ACC"; ] |> iter (fun n -> storages.ContainsKey($"myCTD.{n}") === false)


//        [<Test>]
//        member x.``CTUD on WINDOWS, XGI platform test`` () =
//            for platform in [WINDOWS; XGI] do
//                use _ = setRuntimeTarget platform
//                let storages = Storages()
//                let code = """
//                    bool cu = false;
//                    bool cd = false;
//                    bool r__ = false; // 'r'
//                    bool ld = false;
//                    ctud myCTUD = createWinCTUD(2000u, $cu, $cd, $r__, $ld);
//    """

//                let statement = parseCodeForWindows storages code
//                [ "CU"; "CD"; "R"; "LD"; "PV"; "QU"; "QD"; "CV";] |> iter (fun n -> storages.ContainsKey($"myCTUD.{n}") === true)
//                [ "DN"; "OV"; "UN"; "PRE"; "ACC"; "RES"; "PT"; "ET"; ] |> iter (fun n -> storages.ContainsKey($"myCTUD.{n}") === false)

//        [<Test>]
//        member x.``CTUD on AB platform test`` () =
//            use _ = setRuntimeTarget AB
//            let storages = Storages()
//            let code = """
//                bool cu = createTag("%MX0.0.0", false);
//                bool cd = createTag("%MX0.0.1", false);
//                bool r  = createTag("%MX0.0.2", false);
//                bool ld = createTag("%MX0.0.3", false);
//                ctud myCTUD = createXgiCTUD(2000u, $cu, $cd, $r, $ld);
//"""

//            let statement = parseCodeForTarget storages code AB
//            [ "CU"; "CD"; "OV"; "UN"; "DN"; "PRE"; "ACC"; "RES" ] |> iter (fun n -> storages.ContainsKey($"myCTUD.{n}") === true)
//            [ "R"; "LD"; "PV"; "QU"; "QD"; "CV"; ] |> iter (fun n -> storages.ContainsKey($"myCTUD.{n}") === false)




//        [<Test>]
//        member x.``CTR on AB platform test`` () =
//            use _ = setRuntimeTarget AB
//            let storages = Storages()
//            let code = """
//                bool cd = createTag("%MX0.0.0", false);
//                bool ld = createTag("%MX0.0.0", false);
//                ctr myCTR = createWinCTR(2000u, $cd, $ld);
//"""

//            let statement = parseCodeForTarget storages code AB
//            [ "CD"; "DN"; "OV"; "UN"; "PRE"; "ACC"; "RES" ] |> iter (fun n -> storages.ContainsKey($"myCTR.{n}") === true)
//            [ "CU"; "Q"; "PT"; "ET"; ] |> iter (fun n -> storages.ContainsKey($"myCTR.{n}") === false)

//        [<Test>]
//        member x.``CTR on WINDOWS, XGI platform test`` () =
//            for platform in [WINDOWS; XGI] do
//                use _ = setRuntimeTarget platform
//                let storages = Storages()
//                let code = """
//                    bool cd = createTag("%IX0.0.0", false);
//                    bool rst = createTag("%QX0.0.1", false);
//                    ctr myCTR = createXgiCTR(2000u, $cd, $rst);
//    """

//                let statement = parseCodeForTarget storages code XGI
//                [ "CD"; "PV"; "RST"; "Q"; "CV"; ] |> iter (fun n -> storages.ContainsKey($"myCTR.{n}") === true)
//                [ "CU"; "DN"; "LD"; "PRE"; "ACC"; ] |> iter (fun n -> storages.ContainsKey($"myCTR.{n}") === false)