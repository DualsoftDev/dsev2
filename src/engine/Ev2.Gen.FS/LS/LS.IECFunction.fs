namespace Ev2.Gen
open System.Linq
open Dual.Common.Base

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

/// IEC only
[<AutoOpen>]
module IECFunctionFunctionBlockModule =
    type FunctionProgram<'T>(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFunctionProgram with
            member x.DataType = x.DataType
        member x.DataType = typeof<'T>

    type FBProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFBProgram

    /// '로컬변수' section 정의용 var's
    [<AbstractClass>]
    type VarBindingBase<'T>(name:string, ?varType, ?initValue:'T) =
        inherit VarBase<'T>(name, ?varType=varType, ?initValue=initValue)

    /// Function 의 '로컬변수' section 정의용 var's
    type VarBindingF<'T>(name:string, ?varType) =
        inherit VarBindingBase<'T>(name, ?varType=varType)

    /// Function Block 의 '로컬변수' section 정의용 var's
    type VarBindingFB<'T>(name:string, ?varType, ?initValue:'T) =
        inherit VarBindingBase<'T>(name, ?varType=varType, ?initValue=initValue)


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

    /// XGI 기준 함수 호출.  expression 이 아니다.
    type FBCall(storages, fbInstance:IFBInstance, en, inputs, outputs) =
        inherit CallBox(storages, en, inputs, outputs)
        interface IFBCall
        new() = FBCall(null, null, null, [||], [||])        // for serialization
        member x.IFBInstance = fbInstance


    type FunctionCallStatement(functionCall:FunctionCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.FunctionCall = functionCall
    type FBCallStatement(fbCall:FBCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.FBCall = fbCall
