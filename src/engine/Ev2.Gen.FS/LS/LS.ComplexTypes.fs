namespace Ev2.Gen
open Dual.Common.Base

/// 구조체 변수.
// - 산전 기준으로는 Struct 내에 Struct 는 허용하지 않음
type Struct(name:string, fields:IVariable[]) =
    let fieldDic = fields |> Array.map (fun f -> f.Name, f) |> dict

    new() = Struct(nullString, [||])
    member x.DataType = typeof<Struct>
    member val Name = name with get, set
    member val Fields = fields with get, set
    member x.GetField(fieldName:string): IVariable = fieldDic[fieldName]

    interface IStruct
    interface IVariable with
        member x.Name = x.Name
        member x.DataType = x.DataType
        member x.Value = x

type Array<'T>(name:string, dimensions:Ev2.Gen.Range[]) =
    new() = Array<'T>(nullString, [||])
    member x.Dimensions = dimensions
    member x.DataType = typeof<Array<'T>>
    member x.ElementDataType = typeof<'T>
    member val Value: obj = null with get, set   // 'T[], 'T[,] or 'T[,,] ...

    interface IArray with
        member x.Dimensions = x.Dimensions
        //member x.DataType = x.ElementDataType
    interface IVariable with
        member x.Name = name
        member x.Value = fail()
    interface IWithType with
        member x.DataType = x.DataType


