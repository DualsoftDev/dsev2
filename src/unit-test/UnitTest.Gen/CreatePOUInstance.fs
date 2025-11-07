namespace T

open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

type PouInstanceTest() =
    let globalStorage = Storage()
    let localStorage = Storage()
    [<Test>]
    member _.``ScanProgram 생성``() =
        let mainRung = SetCoilStatement(trueValue, boolContact "MainCoil", "메인 스캔")
        let subroutineBody = [| BreakStatement(falseValue) :> Statement |]
        let stopRoutine = Subroutine("StopRoutine", subroutineBody)

        let program = ScanProgram("MainProgram", globalStorage, localStorage, [| mainRung |], [| stopRoutine |])
        program.Comment <- "메인 프로그램"

        program.Name === "MainProgram"
        program.Comment === "메인 프로그램"
        program.Rungs.Length === 1
        program.Subroutines.Length === 1
        match program.Rungs[0] with
        | :? SetCoilStatement as st -> st.Coil.Name === "MainCoil"
        | _ -> Assert.Fail("첫 번째 Rung 이 StSetCoil 이 아닙니다.")

    [<Test>]
    member _.``FunctionProgram 생성``() =
        let proj = IECProject()
        let returnRung = AssignStatement(trueValue, boolContact "Return", comment="주석:반환 설정")
        let helperRoutine = Subroutine("Helper", [| BreakStatement(trueValue) :> Statement |])

        let funcProgram = FunctionProgram.Create<int>("Calculate", proj.GlobalStorage, localStorage, [| returnRung |], [| helperRoutine |])

        funcProgram.Name === "Calculate"
        funcProgram.DataType === typeof<int>
        funcProgram.UseEnEno === true
        funcProgram.ColumnWidth === 1
        funcProgram.Subroutines.Length === 1

    [<Test>]
    member _.``FunctionProgram 생성 후 호출``() =
        let proj = IECProject()
        let addProgram = createAdd2Function<int32>(proj.GlobalStorage, Some "AddTwoIntegers")
        proj.AddFunction(addProgram)

        let scanLocalStorage =
            let a = Variable<int32>("A", 10)
            let b = Variable<int32>("B", 20)
            let sum = Variable<int32>("Sum", 0)
            Storage.Create( [a; b; sum])
        let scanProgram = ScanProgram("MainScan", proj.GlobalStorage, scanLocalStorage, [||], [||])
        proj.AddScanProgram(scanProgram)

    [<Test>]
    member _.``POU 레코드 및 Project 구성``() =
        //let fbInstance =
        //let rung = Rung(StFBCall(FBCall("Mixer", [||], [||])), "FB 호출")
        //let fbProgram = FBProgram("MixerProgram", globalStorage, localStorage, [| rung |], [||])
        let fbProgram = FBProgram("MixerProgram", globalStorage, localStorage, [||], [||])
        localStorage.Add("MixerReady", Variable<bool>("MixerReady") :> IVariable)

        let pou = { Storage = localStorage; Program = fbProgram :> Program }
        let project = IECProject(globalStorage)
        project.ScanPrograms.Add pou

        project.ScanPrograms.Count === 1
        project.ScanPrograms[0].Program.Name === "MixerProgram"
        project.ScanPrograms[0].Storage.ContainsKey("MixerReady") === true
