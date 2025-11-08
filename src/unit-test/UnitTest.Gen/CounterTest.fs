namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

[<AutoOpen>]
module private CounterTestHelpers =

    let inline pulse (variable:IVariable<bool>) (call:CounterCall) =
        variable.Value <- true
        call.Evaluate()
        variable.Value <- false
        call.Evaluate()

type CounterTest() =
    [<Test>]
    member _.``CTU increments_and_overflow``() =
        let ctu = createCTU "CTU1" 2u

        ctu.Evaluate()
        ctu.ACC.TValue === 0u
        ctu.DN.TValue === false

        pulse ctu.CU ctu
        ctu.ACC.TValue === 1u
        ctu.DN.TValue === false

        pulse ctu.CU ctu
        ctu.ACC.TValue === 2u
        ctu.DN.TValue === true

        pulse ctu.CU ctu
        ctu.ACC.TValue === 3u
        ctu.OV.TValue === true

        ctu.RES.Value <- true
        ctu.Evaluate()
        ctu.RES.Value <- false
        ctu.Evaluate()

        ctu.ACC.TValue === 0u
        ctu.DN.TValue === false
        ctu.OV.TValue === false

    [<Test>]
    member _.``CTD_counts_down_and_load``() =
        let ctd = createCTD "CTD1" 3u

        ctd.Evaluate()
        ctd.ACC.TValue === 3u
        ctd.DN.TValue === false

        pulse ctd.CD ctd
        ctd.ACC.TValue === 2u

        pulse ctd.CD ctd
        ctd.ACC.TValue === 1u

        pulse ctd.CD ctd
        ctd.ACC.TValue === 0u
        ctd.DN.TValue === true

        pulse ctd.CD ctd
        ctd.ACC.TValue === 0u
        ctd.UN.TValue === true

        ctd.PRE.Value <- 5u
        pulse ctd.LD ctd
        ctd.ACC.TValue === 5u
        ctd.UN.TValue === false

    [<Test>]
    member _.``CTUD_balances_up_and_down``() =
        let ctud = createCTUD "CTUD1" 2u

        pulse ctud.CU ctud
        ctud.ACC.TValue === 1u
        ctud.DN.TValue === false
        ctud.DNDown.TValue === false

        pulse ctud.CU ctud
        ctud.ACC.TValue === 2u
        ctud.DN.TValue === true

        pulse ctud.CD ctud
        ctud.ACC.TValue === 1u

        pulse ctud.CD ctud
        ctud.ACC.TValue === 0u
        ctud.DNDown.TValue === true

        pulse ctud.CD ctud
        ctud.ACC.TValue === 0u
        ctud.UN.TValue === true
