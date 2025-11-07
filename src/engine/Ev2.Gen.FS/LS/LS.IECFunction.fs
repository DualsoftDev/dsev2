namespace Ev2.Gen
open System.Linq
open Dual.Common.Base
open System

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


    type FBInstance(name: string, program: FBProgram) =
        member _.Name = name
        member _.Program = program
        interface IFBInstance



    [<AllowNullLiteral>]
    type IStorages =
        abstract GlobalStorage : IVariable[]
        abstract LocalStorage : IVariable[]

    /// Function/Fuction Block 의 Call Box
    type CallBox(storages:IStorages, en:IExpression<bool>, inputs:IExpression[], outputs:IExpression[]) =
        member val Inputs = inputs with get, set
        member val Outputs = outputs with get, set

        member x.EN = x.Inputs[0]
        member x.ENO = x.Outputs[0]

    /// XGI 기준 함수 호출.  expression 이 아니다.
    type FunctionCall(storages, funDef:IFunctionProgram, en, inputs, outputs) =
        inherit CallBox(storages, en, inputs, outputs)
        interface IFunctionCall
        new() = FunctionCall(null, null, null, [||], [||])        // for serialization
        member x.IFunctionProgram = funDef


    type FunctionCallStatement(functionCall:FunctionCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.FunctionCall = functionCall


    /// XGI 기준 함수 호출.  expression 이 아니다.
    type FBCall(storages, fbInstance:IFBInstance, en, inputs, outputs) =
        inherit CallBox(storages, en, inputs, outputs)
        interface IFBCall
        new() = FBCall(null, null, null, [||], [||])        // for serialization
        member x.IFBInstance = fbInstance

    type FBCallStatement(fbCall:FBCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.FBCall = fbCall
