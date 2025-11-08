namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base


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
        member x.Value with get() = box x.Value and set(v:obj) = x.Value <- (v :?> 'T)
        member x.TValue = x.Value

[<AbstractClass>]
type VarBase<'T>(name:string, ?varType:VarType, ?initValue:'T) =
    member x.Name = name
    member x.DataType = typeof<'T>
    member x.VarType = varType |? VarType.Var
    member x.InitValue = initValue
    member val Comment = null:string with get, set
    interface IVariable<'T> with
        member x.Name = x.Name
        member x.DataType = x.DataType
        member x.VarType = x.VarType
        member x.Value with get() = fail() and set v = fail()
        member x.TValue = fail()
    interface IInitValueProvider with
        member x.InitValue = x.InitValue |-> box


type Variable<'T>(name, ?value:'T, ?varType:VarType) =
    inherit VarBase<'T>(name, ?varType=varType)
    new() = Variable<'T>(nullString)

    member val Value = value |? Unchecked.defaultof<'T> with get, set
    interface IVariable<'T> with
        member x.Value with get() = box x.Value and set(v:obj) = x.Value <- (v :?> 'T)
        member x.TValue = x.Value

    member val Retain = false with get, set
    member val Address = null:string with get, set

    member val Hmi = false with get, set
    member val Eip = false with get, set    // EIP/OPC UA


type Storage() =
    inherit Dictionary<string, IVariable>(StringComparer.OrdinalIgnoreCase)

    static member Create(variables:IVariable seq) =
        Storage()
        |> tee(fun storage ->
            for var in variables do
                storage.Add(var.Name, var))
