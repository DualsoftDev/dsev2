namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base

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

//type DataType =
//    | DtUndefined
//    | DtBool        // of bool     option
//    | DtInt8        // of int8     option
//    | DtInt16       // of int16    option
//    | DtInt32       // of int32    option
//    | DtInt64       // of int64    option
//    | DtUint8       // of uint8    option  // byte
//    | DtUInt16      // of uint16   option
//    | DtUInt32      // of uint32   option
//    | DtUInt64      // of uint64   option
//    | DtSingle      // of single   option
//    | DtDouble      // of double   option
//    | DtTime        // of TimeSpan option
//    | DtDate        // of DateOnly option
//    | DtTimeOfDay   // of TimeOnly option
//    | DtDateAndTime // of DateTime option
//    | DtString      // of string   option
//    | DtArray       // of IArray // of DataType * ArraySizeSpec
//    | DtFBInstance  // of IFBInstance  // e.g: TON, ..
//    | DtStruct      // of IStruct

//    //| DtSubroutine
//    //| DtCustom // of string  // User-defined type

//type ValuedDataType = DataType * obj


type Literal<'T>(value:'T) =
    member x.DataType = typeof<'T>
    member val Value = value with get, set
    interface ILiteral<'T> with
        member x.DataType = x.DataType
        member x.Value = x.Value
        member x.TValue = x.Value

type Var<'T>(name:string, ?value:'T) =
    member x.Name = name
    member x.DataType = typeof<'T>
    member val Comment = null:string with get, set
    member val Value = value |? Unchecked.defaultof<'T> with get, set
    interface IVariable<'T> with
        member x.Name = x.Name
        member x.DataType = x.DataType
        member x.Value = x.Value
        member x.TValue = x.Value

type Variable<'T>(name) =
    inherit Var<'T>(name)
    new() = Variable<'T>(nullString)

    member val Retain = false with get, set
    member val Address = null:string with get, set
    //member val Init: obj option = None with get, set

    member val Hmi = false with get, set
    member val Eip = false with get, set    // EIP/OPC UA


type Storage() =
    inherit Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)

