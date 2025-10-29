namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen
open Ev2.Gen.POUModule
open Ev2.Gen.ProgramBlockModule

type private PouBoolExpression(value: bool) =
    member _.Value = value
    interface IExpression<bool>

module private PouTestHelpers =
    let boolExpr value = PouBoolExpression(value) :> IExpression<bool>
    let coil name = new Var<bool>(name) :> IVariable<bool>
    let rung comment statement = { Statement = statement; Comment = comment }

type PouInstanceTest() =
    [<Test>]
    member _.``ScanProgram 생성``() =
        let mainRung = PouTestHelpers.rung "메인 스캔" (StSetCoil(PouTestHelpers.boolExpr true, PouTestHelpers.coil "MainCoil"))
        let subroutineBody: IRung[] = [| StBreak(PouTestHelpers.boolExpr false) :> IRung |]
        let stopRoutine = SubroutineSnippet("StopRoutine", subroutineBody)

        let program = ScanProgram("MainProgram", [| mainRung |], [| stopRoutine |])
        program.Comment <- "메인 프로그램"

        program.Name === "MainProgram"
        program.Comment === "메인 프로그램"
        program.Rungs.Length === 1
        program.Subroutines.Length === 1
        match program.Rungs[0].Statement with
        | StSetCoil(_, coil) -> coil.Name === "MainCoil"
        | _ -> Assert.Fail("첫 번째 Rung 이 StSetCoil 이 아닙니다.")

    [<Test>]
    member _.``FunctionProgram 생성``() =
        let returnRung = PouTestHelpers.rung "반환 설정" (StAssign(PouTestHelpers.boolExpr true, PouTestHelpers.coil "Return"))
        let helperRoutine = SubroutineSnippet("Helper", [| StBreak(PouTestHelpers.boolExpr true) :> IRung |])

        let funcProgram = FunctionProgram("Calculate", [| returnRung |], [| helperRoutine |])
        funcProgram.ReturnType <- typeof<int>

        funcProgram.Name === "Calculate"
        funcProgram.ReturnType === typeof<int>
        funcProgram.UseEnEno === true
        funcProgram.ColumnWidth === 1
        funcProgram.Subroutines.Length === 1

    [<Test>]
    member _.``POU 레코드 및 Project 구성``() =
        let rung = PouTestHelpers.rung "FB 호출" (StFBCall(FBCall("Mixer", [||], [||])))
        let fbProgram = FBProgram("MixerProgram", [| rung |], [||])
        let storage = Storage()
        storage.Add("MixerReady", new Var<bool>("MixerReady") :> IVariable)

        let pou = { Storage = storage; Program = fbProgram :> Program }
        let project = Project()
        project.ScanPrograms <- [| pou |]

        project.ScanPrograms.Length === 1
        project.ScanPrograms[0].Program.Name === "MixerProgram"
        project.ScanPrograms[0].Storage.ContainsKey("MixerReady") === true
