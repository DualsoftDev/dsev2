namespace Ev2.Gen
open Dual.Common.Base

type Arguments = IExpression[]
type Arguments<'T> = IExpression<'T>[]

type PureFunction<'T>(name:string, arguments:Arguments) =
    new() = PureFunction<'T>(nullString, [||])
    member x.Name = name
    member x.Arguments = arguments
    member x.ReturnType = typeof<'T>
    interface IExpression<'T> with
        member x.DataType = x.ReturnType
        //member x.Value = fail()