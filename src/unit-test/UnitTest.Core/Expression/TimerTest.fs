module TimerTestModule

//namespace T.Statement
//open Dual.Common.UnitTest.FS

//open NUnit.Framework

//open T
//open T.Expression
//open Engine.Core
//open Engine.Parser.FS
//open Dual.Common.Core.FS

////[<AutoOpen>]
//module TimerTestModule =
//    let evaluateRungInputs (timer:Timer) =
//        for s in timer.InputEvaluateStatements do
//            s.Do()

//    //[<Collection("SeparatedTestGroup")>]
//    type TimerTest() =
//        inherit ExpressionTestBaseClass()

//        [<Test>]
//        member x.``TON creation test`` () =
//            let storages = Storages()
//            let t1 = createTag("my_timer_control_tag", "%M1.1", false)
//            let condition = var2expr t1
//            let tcParam = {Storages=storages; Name="myTon"; Preset=200u; RungInCondition=condition; FunctionName="createWinTON"}
//            let timer = TimerStatement.CreateTON(tcParam) XGI |> toTimer       // 2000ms = 2sec
//            timer.TT.Value === false
//            timer.EN.Value === false
//            timer.DN.Value === false
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//            (* Timer struct 의 내부 tag 들이 생성되고, 등록되었는지 확인 *)
//            let internalTags =
//                [
//                    timer.TT :> IStorage
//                    timer.RES
//                    timer.EN
//                    timer.DN
//                    timer.PRE
//                    timer.ACC
//                ]

//            storages.ContainsKey("myTon") === true
//            for t in internalTags do
//                storages.ContainsKey(t.Name) === true

//            // rung 입력 조건이 true
//            t1.Value <- true
//            evaluateRungInputs timer

//            timer.DN.Value === false
//            timer.EN.Value === true
//            timer.TT.Value === true

//            // 설정된 timer 시간 경과를 기다림
//            System.Threading.Thread.Sleep(250)
//            timer.TT.Value === false
//            timer.DN.Value === true
//            timer.EN.Value === true
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 200u


//            // rung 입력 조건이 false
//            t1.Value <- false
//            evaluateRungInputs timer
//            timer.TT.Value === false
//            timer.DN.Value === false
//            timer.EN.Value === false
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//        [<Test>]
//        member x.``TON creation with text test`` () =
//            use _ = setRuntimeTarget WINDOWS
//            let t1 = createTag("my_timer_control_tag", "%M1.1", false)
//            let storages = Storages()
//            storages.Add(t1.Name, t1)

//            let statement:Statement = "ton myTon = createWinTON(200u, $my_timer_control_tag);" |> tryParseStatement4UnitTest WINDOWS storages |> Option.get
//            let timer = toTimer statement

//            timer.TT.Value === false
//            timer.EN.Value === false
//            timer.DN.Value === false
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//            // rung 입력 조건이 true
//            t1.Value <- true
//            evaluateRungInputs timer

//            timer.DN.Value === false
//            timer.EN.Value === true
//            timer.TT.Value === true

//            // 설정된 timer 시간 경과를 기다림
//            System.Threading.Thread.Sleep(250)
//            timer.TT.Value === false
//            timer.DN.Value === true
//            timer.EN.Value === true
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 200u


//            // rung 입력 조건이 false
//            t1.Value <- false
//            evaluateRungInputs timer
//            timer.TT.Value === false
//            timer.DN.Value === false
//            timer.EN.Value === false
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//        [<Test>]
//        member __.``TOF creation with initial TRUE test`` () =
//            let storages = Storages()
//            let t1 = createTag("my_timer_control_tag", "%M1.1", true)
//            let condition = var2expr t1
//            let tcParam = {Storages=storages; Name="myTof"; Preset=200u; RungInCondition=condition; FunctionName="createWinTOF"}
//            let timer = TimerStatement.CreateTOF(tcParam) XGI|> toTimer       // 2000ms = 2sec
//            timer.EN.Value === true
//            timer.TT.Value === false
//            timer.DN.Value === true
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//        [<Test>]
//        member __.``TOF creation with initial FALSE test`` () =
//            let storages = Storages()
//            let t1 = createTag("my_timer_control_tag", "%M1.1", false)
//            let condition = var2expr t1
//            let tcParam = {Storages=storages; Name="myTof"; Preset=200u; RungInCondition=condition; FunctionName="createWinTON"}
//            let timer = TimerStatement.CreateTON(tcParam) XGI|> toTimer       // 2000ms = 2sec
//            timer.TT.Value === false
//            timer.EN.Value === false
//            timer.DN.Value === false
//            timer.PRE.Value === 200u
//            timer.ACC.Value === 0u

//        [<Test>]
//        member __.``TOF creation with t -> f -> t -> F -> t test`` () =
//            let storages = Storages()
//            let t1 = createTag("my_timer_control_tag", "%M1.1", true)
//            let condition = var2expr t1
//            let tcParam = {Storages=storages; Name="myTof"; Preset=200u; RungInCondition=condition; FunctionName="createWinTOF"}
//            let timer = TimerStatement.CreateTOF(tcParam) XGI|> toTimer       // 2000ms = 2sec
//            // rung 입력 조건이 false
//            t1.Value <- false
//            evaluateRungInputs timer

//            timer.EN.Value === false
//            timer.TT.Value === true
//            timer.DN.Value === true
//            timer.PRE.Value === 200u
//            (0u <= timer.ACC.Value && timer.ACC.Value <= 100u) === true

//            t1.Value <- true
//            evaluateRungInputs timer
//            System.Threading.Thread.Sleep(50)
//            timer.EN.Value === true
//            timer.TT.Value === false
//            timer.DN.Value === true

//            t1.Value <- false
//            evaluateRungInputs timer
//            System.Threading.Thread.Sleep(50)
//            timer.EN.Value === false
//            timer.TT.Value === true
//            timer.DN.Value === true

//            System.Threading.Thread.Sleep(220)
//            timer.EN.Value === false
//            timer.TT.Value === false
//            timer.DN.Value === false
//            timer.ACC.Value === 200u

//            t1.Value <- true
//            evaluateRungInputs timer
//            System.Threading.Thread.Sleep(100)
//            timer.EN.Value === true
//            timer.TT.Value === false
//            timer.DN.Value === true
//            timer.ACC.Value <= 100u === true


//        [<Test>]
//        member __.``RTO creation test`` () =
//            let storages = Storages()
//            let rungConditionInTag = createTag("my_timer_control_tag", "%M1.1", true)
//            let resetTag = createTag("my_timer_reset_tag", "%M1.1", false)
//            let condition = var2expr rungConditionInTag
//            let reset = var2expr resetTag
//            let tcParam = {Storages=storages; Name="myTmr"; Preset=100u; RungInCondition=condition; FunctionName="createWinTMR"}
//            let timer = TimerStatement.CreateTMR(tcParam, reset) XGI|> toTimer       // 1000ms = 1sec

//            timer.EN.Value === true
//            timer.TT.Value === true
//            timer.DN.Value === false
//            timer.PRE.Value === 100u
//            timer.ACC.Value <= 50u === true
//            timer.RES.Value === false

//            // rung 입력 조건이 false : Pause
//            rungConditionInTag.Value <- false
//            evaluateRungInputs timer
//            timer.EN.Value === false
//            timer.TT.Value === false
//            System.Threading.Thread.Sleep(120)
//            timer.ACC.Value < 100u === true
//            timer.DN.Value === false

//            // rung 입력 조건이 false
//            rungConditionInTag.Value <- true
//            evaluateRungInputs timer
//            timer.EN.Value === true
//            timer.TT.Value === true
//            timer.DN.Value === false
//            System.Threading.Thread.Sleep(120)
//            timer.DN.Value === true
//            timer.ACC.Value === 100u


//            // reset 입력 조건이 true
//            resetTag.Value <- true
//            evaluateRungInputs timer
//            timer.EN.Value === true
//            timer.ACC.Value === 0u



//        [<Test>]
//        member __.``TIMER structure AB platform test`` () =
//            use _ = setRuntimeTarget AB
//            let storages = Storages()
//            let code = """
//                bool x0 = createTag("%MX0", false);
//                ton myTon = createWinTON(200u, $x0);
//"""

//            let statement = parseCodeForTarget storages code AB
//            [ "EN"; "DN"; "PRE"; "ACC"; "TT" ] |> iter (fun n -> storages.ContainsKey($"myTon.{n}") === true)       // TimerStruct.Create() 에서 storage 에 추가함.
//            [ "IN"; "Q"; "ET"; ] |> iter (fun n -> storages.ContainsKey($"myTon.{n}") === false)

//        [<Test>]
//        member __.``TIMER structure WINDOWS, XGI platform test`` () =
//            for platform in [WINDOWS; XGI] do
//                use _ = setRuntimeTarget platform
//                let storages = Storages()
//                let code = """
//                    bool x0 = createTag("%MX0", false);
//                    ton myTon = createXgiTON(200u, $x0);
//    """

//                let statement = parseCodeForWindows storages code
//                [ "IN"; "Q"; "ET"; ] |> iter (fun n -> storages.ContainsKey($"myTon.{n}") === true)
//                [ "EN"; "DN"; "PRE"; "ACC"; "TT" ] |> iter (fun n -> storages.ContainsKey($"myTon.{n}") === false)
