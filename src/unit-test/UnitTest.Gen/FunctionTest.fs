namespace T

open System
open System.Collections.Generic
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Ev2.Gen

module private RuntimeTestHelpers =
    let mkDictionary (pairs: seq<string * ITerminal>) : IDictionary<string, ITerminal> =
        let dictionary = Dictionary<string, ITerminal>(StringComparer.OrdinalIgnoreCase)
        for (key, value) in pairs do
            dictionary.Add(key, value)
        dictionary :> IDictionary<_, _>

open RuntimeTestHelpers

type FunctionRuntimeTest() =

    [<Test>]
    member _.``FunctionRuntime 입력을 출력으로 전달``() =
        // FunctionProgram 정의 단계
        let inputVar = Variable<int>("IN", varType=VarType.VarInput)
        let outputVar = Variable<int>("OUT", varType=VarType.VarOutput)

        let localStorage = Storage()
        localStorage.Add(inputVar.Name, inputVar :> IVariable)
        localStorage.Add(outputVar.Name, outputVar :> IVariable)

        let assignStmt =
            AssignStatement(inputVar :> IExpression<int>, outputVar :> IVariable<int>)
            :> Statement

        let project = IECProject()
        let functionProgram =
            FunctionProgram<int>.Create("EchoFunc", project.GlobalStorage, localStorage, [| assignStmt |], [||])

        project.FunctionPrograms.Add({ Storage = localStorage; Program = functionProgram :> Program })

        // Function 호출 단계
        let runtimeContext = ProjectRuntime(project)

        let externalInput = Variable<int>("ExternalInput", 42)
        let externalOutput = Variable<int>("ExternalOutput", 0)

        let inputs = mkDictionary [ inputVar.Name, externalInput :> ITerminal ]
        let outputs = mkDictionary [ outputVar.Name, externalOutput :> ITerminal ]

        let functionRuntime =
            runtimeContext.CreateFunctionRuntime(
                functionProgram :> IFunctionProgram,
                inputs,
                outputs)

        functionRuntime.Do()

        externalOutput.Value === 42
        outputVar.Value === 42
        inputVar.Value === 42

    [<Test>]
    member _.``FunctionRuntime InOut 매핑은 입력 버퍼를 갱신``() =
        // FunctionProgram 정의 단계
        let project = IECProject()
        let inoutVar = Variable<int>("ACC", varType=VarType.VarInOut)

        let localStorage = Storage()
        localStorage.Add(inoutVar.Name, inoutVar :> IVariable)

        let literal = Literal(123)
        let assignStmt =
            AssignStatement(literal :> IExpression<int>, inoutVar :> IVariable<int>)
            :> Statement

        let functionProgram =
            FunctionProgram<int>.Create("AccumulateFunc", project.GlobalStorage, localStorage, [| assignStmt |], [||])

        project.FunctionPrograms.Add({ Storage = localStorage; Program = functionProgram :> Program })

        // Function 호출 단계
        let runtimeContext = ProjectRuntime(project)

        let externalValue = Variable<int>("ExtACC", 5)

        let inputs = mkDictionary [ inoutVar.Name, externalValue :> ITerminal ]
        let outputs = mkDictionary (Seq.empty<string * ITerminal>)

        let functionRuntime =
            runtimeContext.CreateFunctionRuntime(
                functionProgram :> IFunctionProgram,
                inputs,
                outputs)

        functionRuntime.Do()

        inoutVar.Value === 123
        externalValue.Value === 123

type FBRuntimeTest() =
    [<Test>]
    member _.``FBRuntime 입력을 출력으로 전달``() =
        // FBProgram 정의 단계
        let project = IECProject()
        let localStorage = Storage()
        let inputVar = Variable<int>("IN", varType=VarType.VarInput)
        let localVar = Variable<int>("Local", varType=VarType.Var)
        let outputVar = Variable<int>("OUT", varType=VarType.VarOutput)

        localStorage.Add(inputVar.Name, inputVar :> IVariable)
        localStorage.Add(localVar.Name, localVar :> IVariable)
        localStorage.Add(outputVar.Name, outputVar :> IVariable)

        let assignStmts = [|
            AssignStatement(add<int32> [| localVar :> IExpression<int>; literal 1 |], localVar) :> Statement
            AssignStatement(add<int32> [| localVar :> IExpression<int>; inputVar |], outputVar :> IVariable<int>)
        |]

        let fbProgram = FBProgram("IncrFB", project.GlobalStorage, localStorage, assignStmts, [||])
        project.FBPrograms.Add({ Storage = localStorage; Program = fbProgram :> Program })
        let runtimeContext = ProjectRuntime(project)

        let invoke name inputValue =
            let fbInstance = FBInstance(name, fbProgram) :> IFBInstance
            let externalInput = Variable<int>($"{name}_IN", inputValue)
            let externalOutput = Variable<int>($"{name}_OUT", 0)

            let inputs = mkDictionary [ inputVar.Name, externalInput :> ITerminal ]
            let outputs = mkDictionary [ outputVar.Name, externalOutput :> ITerminal ]

            runtimeContext.InvokeFBInstance(fbInstance, inputs, outputs)
            externalOutput.Value

        // 동일한 이름의 새로운 인스턴스를 만들어도 상태가 이어진다.
        invoke "IncrFBInstance" 7 === 8
        invoke "IncrFBInstance" 7 === 9
        invoke "IncrFBInstance" 4 === 7

        // 다른 이름을 사용하면 별도의 상태가 유지된다.
        invoke "IncrFBInstance2" 17 === 18
        invoke "IncrFBInstance2" 1 === 3



    [<Test>]
    member _.``FBRuntime 내부 변수는 인스턴스 상태로 유지``() =
        // FBProgram 정의 단계
        let project = IECProject()
        let localStorage = Storage()
        let inputVar = Variable<int>("IN", varType=VarType.VarInput)
        let outputVar = Variable<int>("OUT", varType=VarType.VarOutput)
        let internalVar = Variable<int>("MEM", varType=VarType.Var)

        localStorage.Add(inputVar.Name, inputVar :> IVariable)
        localStorage.Add(outputVar.Name, outputVar :> IVariable)
        localStorage.Add(internalVar.Name, internalVar :> IVariable)

        let flushOutput =
            AssignStatement(internalVar :> IExpression<int>, outputVar :> IVariable<int>)
            :> Statement
        let captureInput =
            AssignStatement(inputVar :> IExpression<int>, internalVar :> IVariable<int>)
            :> Statement

        let fbProgram =
            FBProgram("DelayFB", project.GlobalStorage, localStorage, [| flushOutput; captureInput |], [||])
        project.FBPrograms.Add({ Storage = localStorage; Program = fbProgram :> Program })

        let runtimeContext = ProjectRuntime(project)

        let externalInput = Variable<int>("ExternalInput", 10)
        let externalOutput = Variable<int>("ExternalOutput", 0)

        let inputs = mkDictionary [ inputVar.Name, externalInput :> ITerminal ]
        let outputs = mkDictionary [ outputVar.Name, externalOutput :> ITerminal ]

        // 첫 호출: 내부 메모리 초기값이 출력된다.
        let fbInstanceFirst = FBInstance("DelayFBInstance", fbProgram) :> IFBInstance
        runtimeContext.InvokeFBInstance(fbInstanceFirst, inputs, outputs)
        externalOutput.Value === 0
        internalVar.Value === 10

        // 동일한 이름의 새로운 인스턴스를 만들어도 상태가 이어진다.
        externalInput.Value <- 5
        let fbInstanceSameName = FBInstance("DelayFBInstance", fbProgram) :> IFBInstance
        runtimeContext.InvokeFBInstance(fbInstanceSameName, inputs, outputs)
        externalOutput.Value === 10
        internalVar.Value === 5

        // 이름이 다르면 독립된 상태가 생성된다.
        externalInput.Value <- 8
        externalOutput.Value <- 0
        let fbInstanceOther = FBInstance("DelayFBInstance2", fbProgram) :> IFBInstance
        runtimeContext.InvokeFBInstance(fbInstanceOther, inputs, outputs)
        externalOutput.Value === 0
        internalVar.Value === 8

        // 원래 이름으로 다시 호출하면 이전 상태가 유지되어 출력된다.
        externalInput.Value <- 2
        externalOutput.Value <- 0
        let fbInstanceAgain = FBInstance("DelayFBInstance", fbProgram) :> IFBInstance
        runtimeContext.InvokeFBInstance(fbInstanceAgain, inputs, outputs)
        externalOutput.Value === 5
        internalVar.Value === 2
