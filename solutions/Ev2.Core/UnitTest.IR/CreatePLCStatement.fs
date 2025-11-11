namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Core.FS.IR
open Ev2.Core.FS.IR

type PlcStatementTest() =
    [<Test>]
    member _.``Assign 문``() =
        let targetVar = Variable<bool>("Output", Value=false)
        let statement = AssignStatement(trueValue, targetVar)

        obj.ReferenceEquals(trueValue, statement.TSource) === true
        obj.ReferenceEquals(targetVar :> IVariable<bool>, statement.TTarget) === true
        statement.TTarget.TValue === false

        // 대입문 실행 후 값 변경 확인
        statement.Do()
        statement.TTarget.TValue === true



        let targetVar = Variable<int>("Sum", Value= -999)
        targetVar.Value === -999
        let stmt = AssignStatement( add [| literal 3; literal 5 |], targetVar)
        stmt.Do()
        targetVar.Value === 8

        // condition 이 인 경우, Do() 수행해도 실제 대입이 일어나지 않음
        let targetVar = Variable<int>("Sum", Value= -999)
        targetVar.Value === -999
        let stmt = AssignStatement( add [| literal 3; literal 5 |], targetVar, cond=falseValue)
        stmt.Do()
        targetVar.Value === -999

    [<Test>]
    member _.``SetCoil 문``() =
        let cond = Variable<bool>("Condition", Value=true) :> IVariable<bool>
        let coil = Variable<bool>("MainCoil", Value=false) :> IVariable<bool>
        coil.TValue === false
        let setStmt = SetCoilStatement(cond, coil, comment="메인 라인")
        setStmt.Do()
        coil.TValue === true

        let rstStmt = ResetCoilStatement(cond, coil, comment="메인 라인")
        rstStmt.Do()
        coil.TValue === false

        // 조건이 false 인 경우, SetCoil 수행해도 값 변경 없음
        cond.Value <- false
        setStmt.Do()
        coil.TValue === false

    [<Test>]
    member _.``StTimer 생성``() =
        let rungIn = trueValue
        let reset = falseValue
        let preset = Variable<bool>("Preset", Value=false) :> IVariable<bool>

        let timer = TimerCall(TimerType.TON, rungIn, reset, preset)
        timer.TimerType === TimerType.TON
        obj.ReferenceEquals(rungIn, timer.RungIn) === true
        obj.ReferenceEquals(reset, timer.Reset) === true
        timer.Preset.Name === "Preset"
        let statement = TimerStatement timer
        statement.TimerCall === timer

    [<Test>]
    member _.``Rung 레코드 생성``() =
        let cond = trueValue
        let coil = Variable<bool>("MainCoil") :> IVariable<bool>
        let statement = SetCoilStatement(cond, coil, "메인 라인")
        statement.Comment === "메인 라인"
        obj.ReferenceEquals(cond, statement.Condition) === true
        obj.ReferenceEquals(coil, statement.Coil) === true
        statement.Coil.Name === "MainCoil"
