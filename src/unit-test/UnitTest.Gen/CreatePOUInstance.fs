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
        let globalStorage = Storage()
        let project = IECProject(globalStorage)

        let addFunctionProgram: FunctionProgram<int32> =
            createAdd2Function<int32>(globalStorage, Some "AddTwoIntegers")
        project.Add(addFunctionProgram)

        let accumulatorFb =
            let fbInput = Variable<int32>("InValue", varType = VarType.VarInput)
            let fbOutput = Variable<int32>("Acc", varType = VarType.VarOutput)
            let fbMemory = Variable<int32>("Buffer", varType = VarType.Var)
            let fbLocalStorage =
                Storage.Create([ fbInput :> IVariable; fbOutput; fbMemory ])

            let fbRungs: Statement[] =
                [|
                    AssignStatement(add<int32> [| fbMemory :> IExpression<int32>; fbInput |], fbMemory :> IVariable<int32>) :> Statement
                    AssignStatement(fbMemory :> IExpression<int32>, fbOutput :> IVariable<int32>)
                |]

            FBProgram("AccumulatorFB", project.GlobalStorage, fbLocalStorage, fbRungs, [||])
        project.Add(accumulatorFb)

        let a = Variable<int32>("A", 10)
        let b = Variable<int32>("B", 20)
        let sum = Variable<int32>("Sum", 0)
        let accumulated = Variable<int32>("Accumulated", 0)
        let spareAccumulated = Variable<int32>("SpareAccumulated", 0)

        let scanLocalStorage =
            Storage.Create([ a :> IVariable; b; sum; accumulated; spareAccumulated ])

        let fcRung: Statement =
            let inputMapping: Mapping =
                dict [ "Num1", a :> ITerminal
                       "Num2", b :> ITerminal ]

            let outputMapping: Mapping =
                dict [ "Sum", sum :> ITerminal ]

            FunctionCallStatement(addFunctionProgram, inputMapping, outputMapping) :> Statement

        fcRung.Do()
        sum.Value === 30    // 10 + 20

        let rungMainFb: Statement =
            let inputMapping: Mapping = dict [ "InValue", (literal 1) :> ITerminal ]
            let outputMapping: Mapping = dict [ "Acc", accumulated :> ITerminal ]
            FBCallStatement(accumulatorFb, inputMapping, outputMapping, ?instanceName = Some "MainAccumulator") :> Statement
        rungMainFb.Do()
        accumulated.Value === 1

        let rungSpareFb: Statement =
            let inputMapping: Mapping = dict [ "InValue", (literal 99) :> ITerminal ]
            let outputMapping: Mapping = dict [ "Acc", spareAccumulated :> ITerminal ]
            FBCallStatement(accumulatorFb, inputMapping, outputMapping, ?instanceName = Some "SpareAccumulator") :> Statement

        rungSpareFb.Do()
        spareAccumulated.Value === 99

        a.Value <- 5
        b.Value <- 5
        fcRung.Do()
        sum.Value === 10    // 5 + 5

        rungMainFb.Do()
        accumulated.Value === 2
        spareAccumulated.Value === 99

        rungSpareFb.Do()
        spareAccumulated.Value === 198  // 99 + 99

        let rungSpareFb2: Statement =
            let inputMapping: Mapping = dict [ "InValue", (literal 3) :> ITerminal ]
            let outputMapping: Mapping = dict [ "Acc", spareAccumulated :> ITerminal ]
            FBCallStatement(accumulatorFb, inputMapping, outputMapping, ?instanceName = Some "SpareAccumulator") :> Statement
        rungSpareFb2.Do()
        spareAccumulated.Value === 201  // 99 + 3


        let scanProgram =
            ScanProgram("MainScan", project.GlobalStorage, scanLocalStorage, [| fcRung; rungMainFb; rungSpareFb |], [||])

        project.AddScanProgram(scanProgram)
        project |> ignore

        ()

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
