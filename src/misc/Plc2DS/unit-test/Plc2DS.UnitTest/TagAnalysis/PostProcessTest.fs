namespace T

open NUnit.Framework

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.UnitTest.FS


module PostProcessTest =
    type P() =
        [<Test>]
        member _.``Categorize`` () =
            let semantic = AnalTest.semantic
            // "~:STN:2@0" : STN 의 prefix 숫자는 없고(~), postfix 숫자는 '2' 이고, 위치는(@) 쪼갠 이름의 0번째
            let si = AnalyzedNameSemantic.Create("S301RH_B_ADV_LOCK_CLAMP1_ERR", semantic)
            do
                //anals[1] : "S301RH_B_ADV_LOCK_CLAMP1_ERR"
                // "LOCK" 이 Action 으로 등록되어 있지 않고, Device 로 등록되어 있는 상태
                semantic.Actions     |> contains "LOCK" |> ShouldBeFalse
                semantic.DeviceNames |> contains "LOCK" |> ShouldBeTrue
                semantic.States      |> contains "ERR"  |> ShouldBeTrue

                si.Devices |> exactlyOne |> toString === "~:LOCK:~@3"
                si.Actions |> map toString           === [| "~:ADV:~@2"; "~:CLP:1@4" |]
                si.States  |> exactlyOne |> toString === "~:ERR:~@5"
                si.Stringify(withDeviceNumber=true, withState=false) === "LOCK"
                si.Stringify(withDeviceNumber=true, withState=true) === "LOCK_ERR"

            do
                let c = si.Categorize()
                c.Nopes     === [|0|]
                c.Multiples === [| Action, [|2; 4|] |]
                c.Uniqs  === [|
                    (1, SemanticCategory.Modifier)
                    (3, SemanticCategory.Device)
                    (5, SemanticCategory.State)
                |]

                c.Showns             === [| Modifier; Action; Device; SemanticCategory.State |]
                c.ShownsMandatory    === [| Action; Device |]
                c.NotShownsMandatory === [| Flow |]
                noop()

            do
                let semantic2 = semantic.Duplicate()
                semantic2.PositionHints.Add(Flow,   { Min = 0;  Max = 40 })
                semantic2.PositionHints.Add(Device, { Min = 20; Max = 80 })
                semantic2.PositionHints.Add(Action, { Min = 50; Max = 100 })
                semantic2.PositionHints.Add(SemanticCategory.State,  { Min = 70; Max = 100 })

                do
                    si.Flows === [||]
                    let si2 = si.FillEmptyPName(semantic2)
                    si2.Flows  |> exactlyOne |> toString === "~:S301RH:~@0"

                do
                    si.Modifiers  |> exactlyOne |> toString === "~:B:~@1"
                    let si2 = si.DecideModifiers(semantic2)
                    let xxx = si2.Categorize()
                    noop()


            noop()


        [<Test>]
        member _.``Fragmemnt`` () =
            do
                //let semantic = AnalTest.createSemantic()
                let semantic = AnalTest.semantic
                let si = AnalyzedNameSemantic.Create("S301RH_B_ADV_LOCK_CLAMP1_ERR", semantic)
                si.Flows === [||]

            do
                let semantic = AnalTest.createSemantic()

                semantic.FlowNameFragments.Add("RH") |> ignore  // "RH" 는 Flow name fragments 로 등록

                let si = AnalyzedNameSemantic.Create("S301RH_B_ADV_LOCK_CLAMP1_ERR", semantic)
                si.Flows  |> exactlyOne |> toString === "~:S301RH:~@0"
