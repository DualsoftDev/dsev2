namespace Ev2.Gen
open System.Linq
open Dual.Common.Base

type Arguments = IExpression[]
type Arguments<'T> = IExpression<'T>[]

type Operator<'T>(name:string, arguments:Arguments) =
    new() = Operator<'T>(nullString, [||])
    member x.Name = name
    member x.Arguments = arguments
    member x.ReturnType = typeof<'T>
    member val Evaluator: (Arguments -> 'T) = fun _ -> fail() with get, set
    member x.TValue = x.Evaluator(x.Arguments)
    interface IExpression<'T> with
        member x.DataType = x.ReturnType
        //member x.Value = x.TValue
        member x.Value with get() = box x.TValue and set v = fail()
        member x.TValue = x.TValue


[<AutoOpen>]
module OperatorEvaluators =
    let inline private opaqueArgs (args:Arguments<'T>) = args.Cast<IExpression>().ToArray()
    let inline private valueArgs<'T> (args:Arguments) = args.Cast<IExpression<'T>>().Map _.TValue

    let inline add<'T when 'T : (static member (+) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("+", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (+)) )
    let inline sub<'T when 'T : (static member (-) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("-", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (-)) )
    let inline mul<'T when 'T : (static member (*) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("*", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (*)) )
    let inline div<'T when 'T : (static member (/) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("/", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (/)) )


    let inline private createComparisonOperator<'T when 'T: comparison> (name, operator:'T -> 'T -> bool) (a:IExpression<'T>, b:IExpression<'T>) =
        Operator<bool>(name, [| a :> IExpression; b |],
            Evaluator=fun args ->
                        let args = args.Cast<IExpression<'T>>().ToArray()
                        operator args[0].TValue args[1].TValue)

    let ge<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">=", (>=)) (a, b)
    let gt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">",  (>))  (a, b)
    let eq<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("=",  (=))  (a, b)
    let lt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<",  (<))  (a, b)
    let le<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<=", (<=)) (a, b)
