namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen


[<AutoOpen>]
module internal PouTestHelperModule =
    let trueValue  = Literal<bool>(true)
    let falseValue = Literal<bool>(false)
    let coil name  = Var<bool>(name) :> IVariable<bool>


type PlcStatementTest() =
    [<Test>]
    member _.``StAssign 생성``() =
        let targetVar = new Var<bool>("Output", Value=false)
        let statement = StAssign(trueValue, targetVar :> IVariable<bool>)

        match statement with
        | StAssign(exp, lValue) ->
            obj.ReferenceEquals(trueValue, exp) === true
            obj.ReferenceEquals(targetVar :> IVariable<bool>, lValue) === true
        | _ ->
            Assert.Fail("StAssign 생성에 실패했습니다.")

    [<Test>]
    member _.``StTimer 생성``() =
        let rungIn = trueValue
        let reset = falseValue
        let preset = new Var<bool>("Preset", Value=false) :> IVariable<bool>

        let timerCall = TimerCall(TimerType.TON, rungIn, reset, preset)
        let statement = StTimer(timerCall)

        match statement with
        | StTimer call ->
            call.TimerType === TimerType.TON
            obj.ReferenceEquals(rungIn, call.RungIn) === true
            obj.ReferenceEquals(reset, call.Reset) === true
            call.Preset.Name === "Preset"
        | _ ->
            Assert.Fail("StTimer 생성에 실패했습니다.")

    [<Test>]
    member _.``Rung 레코드 생성``() =
        let expr = trueValue
        let coil = new Var<bool>("MainCoil") :> IVariable<bool>
        let statement = StSetCoil(expr, coil)
        let rung:Rung = { Statement = statement; Comment = "메인 라인" }

        rung.Comment === "메인 라인"
        match rung.Statement with
        | StSetCoil(exp, target) ->
            obj.ReferenceEquals(expr, exp) === true
            target.Name === "MainCoil"
        | _ ->
            Assert.Fail("Rung Statement가 StSetCoil이 아닙니다.")
