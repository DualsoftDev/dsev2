namespace Ev2.Gen
open System.Linq
open Dual.Common.Base
open System
open System.Collections.Generic

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
        member x.DataType = dataType
        member x.Return = returnVar

    type FunctionProgram<'T> (name, globalStorage, localStorage, returnVar, rungs, subroutines) =
        inherit FunctionProgram(name, globalStorage, localStorage, returnVar, typeof<'T>, rungs, subroutines)
        member x.TReturn = returnVar

    type FBProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFBProgram


    [<AllowNullLiteral>]
    type IStorages =
        abstract GlobalStorage : IVariable[]
        abstract LocalStorage : IVariable[]

    type Mapping = IDictionary<string, ITerminal>

    /// IEC 함수 호출 메타데이터
    type FunctionCall(program: IFunctionProgram, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        let enoVar = eno |? null

        member _.EN = en
        member _.ENO = enoVar
        member _.IFunctionProgram = program
        member val Inputs = inputMapping with get, set
        member val Outputs = outputMapping with get, set
        interface IFunctionCall

    /// IEC Function 호출 Statement
    type FunctionCallStatement(program: IFunctionProgram, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
        inherit Statement(?cond = en, ?comment = comment)
        let call = FunctionCall(program, inputMapping, outputMapping, ?en = en, ?eno = eno)
        member _.FunctionCall = call

    /// FBInstance 헬퍼
    type FBInstance(fbProgram: FBProgram, instanceName: string) =
        member _.Program = fbProgram
        member _.InstanceName = instanceName
        interface IFBInstance

    /// IEC Function Block 호출 메타데이터
    type FBCall(fbInstance: FBInstance, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        let enoVar = eno |? null

        member _.EN = en
        member _.ENO = enoVar
        member _.IFBInstance = fbInstance
        member val Inputs = inputMapping with get, set
        member val Outputs = outputMapping with get, set
        interface IFBCall

    /// IEC Function Block 호출 Statement
    type FBCallStatement(fbInstance: FBInstance, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
        inherit Statement(?cond = en, ?comment = comment)
        let call = FBCall(fbInstance, inputMapping, outputMapping, ?en = en, ?eno = eno)
        member _.FBCall = call

        new(fbProgram: FBProgram, instanceName: string, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
            let instance = FBInstance(fbProgram, instanceName)
            FBCallStatement(instance, inputMapping, outputMapping, ?en = en, ?eno = eno, ?comment = comment)
