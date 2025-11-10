namespace Ev2.Core.FS.IR

open System
open Ev2.Core.FS.IR


[<AutoOpen>]
module ProgramModule =
    [<AbstractClass>]
    type Program(name:string, globalStorage:Storage, localStorage:Storage, rungs:Statement[], subroutines:Subroutine[]) =
        interface IProgram
        member x.Name = name
        member x.Rungs = rungs
        member x.Subroutines = subroutines
        member x.GlobalStorage = globalStorage
        member x.LocalStorage = localStorage
        member val Comment = null:string with get, set
        member val Project : IProject = null with get, set

    [<AbstractClass>]
    type SubProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit Program(name, globalStorage, localStorage, rungs, subroutines)
        member val UseEnEno = true with get, set
        member val ColumnWidth = 1 with get, set

    type ScanProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IScanProgram



/// IEC only
[<AutoOpen>]
module IECFunctionFunctionBlockModule =
    type FunctionProgram internal (name, globalStorage, localStorage, returnVar:IVariable, dataType:Type, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        do
            assert(name = returnVar.Name)
        interface IFunctionProgram with
            member x.DataType = x.DataType
        member _.DataType = dataType
        member _.Return = returnVar

    type FunctionProgram<'T> (name, globalStorage, localStorage, returnVar, rungs, subroutines) =
        inherit FunctionProgram(name, globalStorage, localStorage, returnVar, typeof<'T>, rungs, subroutines)
        member x.TReturn = returnVar

    type FBProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFBProgram


    /// IEC 함수 호출 메타데이터
    type FunctionCall(program: FunctionProgram, inputMapping: InputMapping, outputMapping: OutputMapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        member _.EN = en
        member _.ENO = eno
        member _.FunctionProgram = program
        member val Inputs = inputMapping with get, set
        member val Outputs = outputMapping with get, set
        interface IFunctionCall

    /// IEC Function 호출 Statement
    type FunctionCallStatement(program: FunctionProgram, inputMapping: InputMapping, outputMapping: OutputMapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
        inherit Statement(?cond = en, ?comment = comment)
        let call = FunctionCall(program, inputMapping, outputMapping, ?en = en, ?eno = eno)
        member _.FunctionCall = call

    /// FBInstance 헬퍼
    type FBInstance(fbProgram: FBProgram, instanceName: string) =
        member _.Program = fbProgram
        member _.InstanceName = instanceName
        interface IFBInstance

    /// IEC Function Block 호출 메타데이터
    type FBCall(fbInstance: FBInstance, inputMapping: InputMapping, outputMapping: OutputMapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        member _.EN = en
        member _.ENO = eno
        member _.FBInstance = fbInstance
        member val Inputs = inputMapping with get, set
        member val Outputs = outputMapping with get, set
        interface IFBCall

    /// IEC Function Block 호출 Statement
    type FBCallStatement(fbInstance: FBInstance, inputMapping: InputMapping, outputMapping: OutputMapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
        inherit Statement(?cond = en, ?comment = comment)
        let call = FBCall(fbInstance, inputMapping, outputMapping, ?en = en, ?eno = eno)
        member _.FBCall = call

        new(fbProgram: FBProgram, instanceName: string, inputMapping: InputMapping, outputMapping: OutputMapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
            let instance = FBInstance(fbProgram, instanceName)
            FBCallStatement(instance, inputMapping, outputMapping, ?en = en, ?eno = eno, ?comment = comment)
