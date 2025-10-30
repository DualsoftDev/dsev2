namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

type PouInstanceTest() =
    [<Test>]
    member _.``ScanProgram 생성``() =
        let mainRung = Rung.Create(StSetCoil(trueValue, boolContact "MainCoil"), "메인 스캔")
        let subroutineBody: IRung[] = [| StBreak(falseValue) :> IRung |]
        let stopRoutine = Subroutine("StopRoutine", subroutineBody)

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
        let returnRung = Rung.Create(StAssign(trueValue, boolContact "Return"), "반환 설정")
        let helperRoutine = Subroutine("Helper", [| StBreak(trueValue) :> IRung |])

        let funcProgram = FunctionProgram("Calculate", [| returnRung |], [| helperRoutine |])
        funcProgram.ReturnType <- typeof<int>

        funcProgram.Name === "Calculate"
        funcProgram.ReturnType === typeof<int>
        funcProgram.UseEnEno === true
        funcProgram.ColumnWidth === 1
        funcProgram.Subroutines.Length === 1

    [<Test>]
    member _.``POU 레코드 및 Project 구성``() =
        let rung = Rung.Create(StFBCall(FBCall("Mixer", [||], [||])), "FB 호출")
        let fbProgram = FBProgram("MixerProgram", [| rung |], [||])
        let storage = Storage()
        storage.Add("MixerReady", new Var<bool>("MixerReady") :> IVariable)

        let pou = { Storage = storage; Program = fbProgram :> Program }
        let project = Project()
        project.ScanPrograms <- [| pou |]

        project.ScanPrograms.Length === 1
        project.ScanPrograms[0].Program.Name === "MixerProgram"
        project.ScanPrograms[0].Storage.ContainsKey("MixerReady") === true
