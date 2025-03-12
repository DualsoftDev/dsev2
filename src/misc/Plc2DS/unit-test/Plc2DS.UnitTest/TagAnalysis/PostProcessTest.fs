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
            let si = semantic.CreateDefault("S301RH_B_ADV_LOCK_CLAMP1_ERR")
            do
                //anals[1] : "S301RH_B_ADV_LOCK_CLAMP1_ERR"
                // "LOCK" 이 Action 으로 등록되어 있지 않고, Device 로 등록되어 있는 상태
                semantic.Actions |> contains "LOCK" |> ShouldBeFalse
                semantic.Devices |> contains "LOCK" |> ShouldBeTrue
                semantic.States  |> contains "ERR"  |> ShouldBeTrue

                si.Devices |> exactlyOne |> toString === "~:LOCK:~@3"
                si.Actions |> map toString           === [| "~:ADV:~@2"; "~:CLP:1@4" |]
                si.States  |> exactlyOne |> toString === "~:ERR:~@5"
                si.Stringify(withDeviceNumber=true, withState=false) === "LOCK"
                si.Stringify(withDeviceNumber=true, withState=true) === "LOCK_ERR"

            do
                let c = si.Categorize()
                c.Nopes     === [|0|]
                c.Multiples === [| DuAction, [|2; 4|] |]
                c.Uniqs  === [|
                    (1, DuModifier)
                    (3, DuDevice)
                    (5, DuState)
                |]

                c.Showns             === [| DuModifier; DuAction; DuDevice; DuState |]
                c.ShownsMandatory    === [| DuAction; DuDevice |]
                c.NotShownsMandatory === [| DuFlow |]
                noop()

            do
                let semantic2 = semantic.Duplicate()
                semantic2.PositionHints.Add(DuFlow,   { Min = 0;  Max = 40 })
                semantic2.PositionHints.Add(DuDevice, { Min = 20; Max = 80 })
                semantic2.PositionHints.Add(DuAction, { Min = 50; Max = 100 })
                semantic2.PositionHints.Add(DuState,  { Min = 70; Max = 100 })

                do
                    si.Flows === [||]
                    let si2 = si.FillEmptyPName(semantic2)
                    si2.Flows  |> exactlyOne |> toString === "~:S301RH:~@0"

                do
                    si.Modifiers  |> exactlyOne |> toString === "~:B:~@1"
                    noop()


            noop()


        [<Test>]
        member _.``X Pattern`` () =
            do
                //let semantic = AnalTest.createSemantic()
                let semantic = AnalTest.semantic
                let si = semantic.CreateDefault("S301RH_B_ADV_LOCK_CLAMP1_ERR")
                si.Flows === [||]

            do
                let semantic = AnalTest.createSemantic()

                semantic.FlowPatterns.Add(@"\w+RH") |> ignore  // "RH" 는 Flow name fragments 로 등록

                let si = semantic.CreateDefault("S301RH_B_ADV_LOCK_CLAMP1_ERR")
                si.Flows  |> exactlyOne |> toString === "~:S301RH:~@0"
