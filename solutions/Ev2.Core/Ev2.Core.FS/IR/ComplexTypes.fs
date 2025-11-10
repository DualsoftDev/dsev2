namespace Ev2.Core.FS.IR
open Dual.Common.Base
open Ev2.Core.FS.IR

/// 구조체 변수.
// - 산전 기준으로는 Struct 내에 Struct 는 허용하지 않음
type Struct(name:string, fields:IVariable[]) =
    let fieldDic = fields |> Array.map (fun f -> f.Name, f) |> dict

    new() = Struct(nullString, [||])
    member x.DataType = typeof<Struct>
    member val VarType = VarType.VarUndefined with get, set
    member val Name = name with get, set
    member val Fields = fields with get, set
    member x.GetField(fieldName:string): IVariable = fieldDic[fieldName]

    interface IStruct
    interface IVariable with
        member x.Name = x.Name
        member x.DataType = x.DataType
        member x.VarType = x.VarType
        member x.Value with get() = fail() and set v = fail()

type IRArray<'T>(name:string, dimensions:DimRange[]) =
    new() = IRArray<'T>(nullString, [||])
    member x.Dimensions = dimensions
    member x.DataType = typeof<IRArray<'T>>
    member val VarType = VarType.VarUndefined with get, set
    member x.ElementDataType = typeof<'T>
    member val Value: obj = null with get, set   // 'T[], 'T[,] or 'T[,,] ...

    interface IArray<'T> with
        member x.Dimensions = x.Dimensions
        member x.ElementType = x.ElementDataType
    interface IVariable with
        member x.Name = name
        member x.VarType = x.VarType
        member x.Value with get() = fail() and set v = fail()
    interface IWithType with
        member x.DataType = x.DataType
