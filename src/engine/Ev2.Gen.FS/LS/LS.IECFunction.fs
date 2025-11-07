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
    module private CallValidation =
        let inline ensureNotNull paramName value message =
            if obj.ReferenceEquals(value, null) then invalidArg paramName message
            value

        let inline ensureMapping paramName (mapping: Mapping) =
            if obj.ReferenceEquals(mapping, null) then
                invalidArg paramName "매핑 사전은 null 일 수 없습니다."
            mapping

    /// IEC 함수 호출 메타데이터
    type FunctionCall(program: IFunctionProgram, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        let program = CallValidation.ensureNotNull "program" program "함수 프로그램은 null 일 수 없습니다."
        let inputMapping = CallValidation.ensureMapping "inputMapping" inputMapping
        let outputMapping = CallValidation.ensureMapping "outputMapping" outputMapping
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
    type FBInstanceReference(fbProgram: FBProgram, ?instanceName: string) =
        let program = CallValidation.ensureNotNull "fbProgram" fbProgram "FBProgram 은 null 일 수 없습니다."
        member _.Program = program
        member val InstanceName = instanceName |? program.Name with get, set
        interface IFBInstance

    /// IEC Function Block 호출 메타데이터
    type FBCall(fbInstance: IFBInstance, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>) =
        let fbInstance =
            CallValidation.ensureNotNull "fbInstance" fbInstance "FB 인스턴스는 null 일 수 없습니다."
        let inputMapping = CallValidation.ensureMapping "inputMapping" inputMapping
        let outputMapping = CallValidation.ensureMapping "outputMapping" outputMapping
        let enoVar = eno |? null

        member _.EN = en
        member _.ENO = enoVar
        member _.IFBInstance = fbInstance
        member val Inputs = inputMapping with get, set
        member val Outputs = outputMapping with get, set
        interface IFBCall

    /// IEC Function Block 호출 Statement
    type FBCallStatement(fbInstance: IFBInstance, inputMapping: Mapping, outputMapping: Mapping, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
        inherit Statement(?cond = en, ?comment = comment)
        let call = FBCall(fbInstance, inputMapping, outputMapping, ?en = en, ?eno = eno)
        member _.FBCall = call

        new(fbProgram: IFBProgram, inputMapping: Mapping, outputMapping: Mapping, ?instanceName: string, ?en: IExpression<bool>, ?eno: IVariable<bool>, ?comment: string) =
            let concreteProgram =
                match fbProgram with
                | :? FBProgram as concrete -> concrete
                | null -> invalidArg "fbProgram" "FB 프로그램은 null 일 수 없습니다."
                | _ -> invalidArg "fbProgram" "FBCallStatement 는 FBProgram 기반 구현만 허용합니다."

            let instance = FBInstanceReference(concreteProgram, ?instanceName = instanceName) :> IFBInstance
            FBCallStatement(instance, inputMapping, outputMapping, ?en = en, ?eno = eno, ?comment = comment)
