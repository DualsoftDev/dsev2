namespace Ev2.Gen

open System
open System.Collections.Generic

[<AllowNullLiteral>] type IProgram = interface end
[<AllowNullLiteral>] type IValue = interface end
[<AllowNullLiteral>] type IExpression = interface end
[<AllowNullLiteral>] type ITerminal = inherit IExpression
[<AllowNullLiteral>] type IVariable = inherit ITerminal
[<AllowNullLiteral>] type ILiteral = inherit ITerminal
[<AllowNullLiteral>] type IFunctionCall = inherit IExpression
[<AllowNullLiteral>] type IFBCall = interface end

[<AllowNullLiteral>] type IVariable<'T> = inherit IVariable
[<AllowNullLiteral>] type ILiteral<'T> = inherit ILiteral
[<AllowNullLiteral>] type IExpression<'T> = inherit IExpression
//[<AllowNullLiteral>] type IStorage = interface end


(*
 * 글로벌/직접 변수: VAR_GLOBAL, VAR_GLOBAL_CONST
 * Program 로컬변수: VAR, VAR_CONST, VAR_EXTERNAL, VAR_EXTERNAL_CONST
 * FB 로컬변수: VAR, VAR_CONSTANT, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_EXTERNAL, VAR_EXTERNAL_CONSTANT
 * Func 로컬변수: VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_RETURN
 *)

/// 변수 종류
type VarType =
    | VarUndefined
    | Var
    | VarConstant
    | VarInput
    | VarOutput
    | VarInOut
    | VarExternal
    | VarExternalConstant
    | VarGlobal
    | VarGlobalConstant

type DataType =
    | DtUndefined
    | DtBool        // of bool     option
    | DtInt8        // of int8     option
    | DtInt16       // of int16    option
    | DtInt32       // of int32    option
    | DtInt64       // of int64    option
    | DtUint8       // of uint8    option  // byte
    | DtUInt16      // of uint16   option
    | DtUInt32      // of uint32   option
    | DtUInt64      // of uint64   option
    | DtSingle      // of single   option
    | DtDouble      // of double   option
    | DtTime        // of TimeSpan option
    | DtDate        // of DateOnly option
    | DtTimeOfDay   // of TimeOnly option
    | DtDateAndTime // of DateTime option
    | DtString      // of string   option
    | DtArray       // of IArray // of DataType * ArraySizeSpec
    | DtFBInstance  // of IFBInstance  // e.g: TON, ..
    | DtStruct      // of IStruct

    //| DtSubroutine
    //| DtCustom // of string  // User-defined type

type ValuedDataType = DataType * obj


type Literal(dataType:DataType, value:obj) =
    interface ILiteral
    member val DataType = dataType with get, set
    member val Value = value with get, set

type Var(name:string, dataType:DataType) =
    interface IVariable
    new() = Var(nullString, DtUndefined)        // for serialization

    member val Name = name with get, set
    member val DataType = dataType with get, set
    member val Comment = null:string with get, set

type Variable(name, dataType) =
    inherit Var(name, dataType)
    new() = Variable(nullString, DtUndefined)        // for serialization

    member val VarType = VarUndefined with get, set
    member val Retain = false with get, set
    member val Address = null:string with get, set
    member val Init: obj option = None with get, set

    member val Hmi = false with get, set
    member val Eip = false with get, set    // EIP/OPC UA

type Storage() =
    inherit Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)

[<AutoOpen>]
module TimerCounterModule =
    type TimerType = Undefined | TON | TOF | TMR        // AB 에서 TMR 은 RTO 에 해당

    [<AbstractClass>]
    type TimerCounterStruct (isTimer:bool, name, dn:IVariable<bool>, pre:IVariable<bool>, acc:IVariable<bool>, res:IExpression<bool>, sys) =
            member _.Name:string = name
            /// Done bit
            member _.DN = dn
            /// Preset value
            member _.PRE = pre
            member _.ACC = acc
            /// Reset bit.
            member _.RES = res
            /// XGI load
            member _.LD = res
    type TimerStruct internal(typ:TimerType, name, en:IExpression<bool>, tt:IVariable<bool>, dn, pre, acc, res, sys) =
        inherit TimerCounterStruct(true, name, dn, pre, acc, res, sys)
        /// Enable
        member _.EN = en
        /// Timing
        member _.TT = tt
        member _.Type = typ


    type TimerCall(timerType:TimerType, rungIn: IExpression<bool>, reset:IExpression<bool>, preset:IVariable<bool>) =
        let ts = TimerStruct(timerType, nullString, rungIn, null, null, preset, null, reset, null)
        interface IFBCall
        new() = TimerCall(TimerType.Undefined, null, null, null)        // for serialization
        member val TimerType = timerType with get, set
        member val RungIn = rungIn with get, set
        member val Preset = preset with get, set
        member val Reset = reset with get, set

    type CountUnitType = uint32
    type CounterType =
        /// UP Counter
        CTU
        /// DOWN Counter
        | CTD
        /// UP/DOWN Counter
        | CTUD
        /// Ring Counter
        | CTR

    type CounterParams = {
        Type: CounterType
        Storage:Storage
        Name:string
        Preset: CountUnitType
        Accumulator: CountUnitType
        CU: IVariable<bool>
        CD: IVariable<bool>
        OV: IVariable<bool>
        UN: IVariable<bool>
        DN: IVariable<bool>
        /// XGI load
        LD: IVariable<bool>
        DNDown: IVariable<bool>

        RES: IVariable<bool>
        PRE: IVariable<CountUnitType>
        ACC: IVariable<CountUnitType>
    }


type ISnippet = interface end
type IStatement = interface end

[<AbstractClass>]
type Snippet(body:IStatement[]) =
    interface ISnippet
    member x.Body = body

type ForLoopSnippet(counter:IExpression<uint16>, body:IStatement[]) =   // NEXT 로 종료
    inherit Snippet(body)
    member x.Counter = counter

type SubroutineSnippet(name:string, body:IStatement[]) =    // RET 로 종료
    inherit Snippet(body)
    member x.Name = name


//type ExtendedFunction =
//    | ForLoop of counter:IExpression<uint16>
//    | Next

//    | Call of exp:IExpression<bool> * funcCall:IFunctionCall        // Call - End
//    | End

//    | SBRT of exp:IExpression<bool>     // SBRT - RET
//    | Ret



//and FunctionBody =
//    | StatementsBody of Statement list
//    | ExpressionBody of IExpression
//    interface IProgram

/// XGK 기준 대소 비교 등.
type OperatorCall(name:string, arguments:IExpression list, returnType:DataType) =
    interface IExpression
    new() = OperatorCall(nullString, [], DtUndefined)        // for serialization
    member val Name = name with get, set
    member val Arguments = arguments with get, set
    member val ReturnType = returnType with get, set

// Function/Fuction Block 의 Call Box
type CallBox(inputs:IExpression[], outputs:IExpression[]) =
    member val Inputs = inputs with get, set
    member val Outputs = outputs with get, set

    member x.EN = x.Inputs[0]
    member x.ENO = x.Outputs[0]

/// XGI 기준 함수 호출.  expression 이 아니다.
type FunctionCall(name:string, inputs:IExpression[], outputs:IExpression[]) =
    inherit CallBox(inputs, outputs)
    interface IFunctionCall
    new() = FunctionCall(nullString, [||], [||])        // for serialization
    member val Name = name with get, set

/// XGI 기준 함수 호출.  expression 이 아니다.
type FBCall(name:string, inputs:IExpression[], outputs:IExpression[]) =
    inherit CallBox(inputs, outputs)
    interface IFBCall
    new() = FBCall(nullString, [||], [||])        // for serialization
    member val Name = name with get, set

type Statement =
    | StAssign of exp:IExpression<bool> * lValue: IVariable<bool>
    | StSetCoil of exp:IExpression<bool> * coil: IVariable<bool>
    | StResetCoil of exp:IExpression<bool> * coil: IVariable<bool>
    | StTimer of timerType:TimerType * rungIn: IExpression<bool> * reset:IExpression<bool> * preset: IExpression<CountUnitType>
    | StBreak of exp:IExpression<bool>    // for loop 내에서 사용
    | StSubroutineCall of exp:IExpression<bool> * subroutine:SubroutineSnippet
    | StFunctionCall of FunctionCall
    | StFBCall of FBCall
    interface IStatement



type ProgramType =
    | PtScanProgram
    | PtFunction
    | PtFunctionBlock


[<AbstractClass>]
type Program() =
    interface IProgram
    member val Name = nullString with get, set
    member val Statements = [] : Statement list with get, set
    member val SubroutineSnippets = [] : SubroutineSnippet list with get, set

type ScanProgram() =
   inherit Program()
type FunctionDefinition() =
   inherit Program()
type FBDefinition() =
   inherit Program()



type Range = int * int

type ArraySizeSpec =
    | UndefinedDim
    | SingleDim of Range
    | MultiDim of Range list

type Array(dataType:DataType, sizeSpec:ArraySizeSpec) =
    interface IArray
    new() = Array(DtUndefined, UndefinedDim)
    member val SizeSpec = sizeSpec with get, set
    member val ElementType = dataType with get, set


type Struct(name:string, fields:Var list) =
    interface IStruct
    new() = Struct(nullString, [])
    member val Name = name with get, set
    member val Fields = fields with get, set



//type IExpression<'T when 'T: equality> = inherit IExpression
//type ITerminal<'T when 'T: equality> = inherit IExpression<'T>
//type IVariable<'T  when 'T: equality> = inherit ITerminal<'T>

//type Terminal<'T when 'T:equality> =
//    | DuLiteral of LiteralHolder<'T>
//    | DuVariable of TypedValueStorage<'T>
//    interface ITerminal<'T>

//type Expression<'T when 'T:equality> =
//    | DuTerminal of Terminal<'T>
//    | DuFunction of FunctionSpec<'T>  //FunctionBody:(Arguments -> 'T) * Name * Arguments

