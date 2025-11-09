namespace T

open System
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

[<AutoOpen>]
module private CounterTestHelpers =
    let inline setBool (variable:IVariable<bool>) value =
        variable.Value <- value

    let inline pulse
        (variable:IVariable<bool>)
        (call:CounterCall<_>) =
        setBool variable true
        call.Evaluate()
        setBool variable false
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

        setBool ctu.RES true
        ctu.Evaluate()
        setBool ctu.RES false
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

    [<Test>]
    member _.``CounterStatement wraps_call_and_evaluates``() =
        let call = CounterCall(CTU, "StmtCounter", 1u)
        let stmt = CounterStatement(call)

        obj.ReferenceEquals(call, stmt.CounterCall) === true

        let typedCall = stmt.CounterCall :?> CounterCall<uint32>
        pulse typedCall.CU typedCall
        typedCall.ACC.TValue === 1u
        typedCall.DN.TValue === true

    [<Test>]
    member _.``Counter_allows_input_mapping_for_custom_signals``() =
        let ctu = createCTU "CTU_InputMap" 1u
        let externalCu = Variable<bool>("External.CU")

        ctu.Inputs.["CU"] <- externalCu :> IExpression

        pulse externalCu ctu

        ctu.ACC.TValue === 1u
        ctu.DN.TValue === true

    [<Test>]
    member _.``Counter_output_mapping_updates_external_variables``() =
        let ctu = createCTU "CTU_OutputMap" 1u
        let externalDn = Variable<bool>("External.DN")
        let externalAcc = Variable<uint32>("External.ACC")

        ctu.Outputs.["DN"] <- externalDn :> IVariable
        ctu.Outputs.["ACC"] <- externalAcc :> IVariable

        pulse ctu.CU ctu

        ctu.DN.TValue === true
        externalDn.Value === true
        externalAcc.Value === 1u

    [<Test>]
    member _.``Counter_registers_internal_variables_to_global_storage``() =
        let storage = Storage()

        let autoRegistered = CounterCall(CTU, "AutoCounter", 1u, globalStorage=storage)
        storage.ContainsKey("AutoCounter.ACC") === true
        obj.ReferenceEquals(storage.["AutoCounter.ACC"], autoRegistered.ACC) === true

        let manual = createCTU "ManualCounter" 2u
        let countBefore = storage.Count

        manual.RegisterGlobalVariables(storage)
        storage.ContainsKey("ManualCounter.DN") === true

        manual.RegisterGlobalVariables(storage)
        storage.Count === countBefore + 10
