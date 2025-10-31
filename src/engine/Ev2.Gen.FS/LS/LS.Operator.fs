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
    let inline createAddFunction<'T when 'T : (static member (+) : 'T * 'T -> 'T)> (args:Arguments) =
        Operator<'T>("+", args, Evaluator=fun args -> args.Cast<IExpression<'T>>().Map _.TValue |> Seq.reduce (+) )
    let inline createSubFunction<'T when 'T : (static member (-) : 'T * 'T -> 'T)> (args:Arguments) =
        Operator<'T>("-", args, Evaluator=fun args -> args.Cast<IExpression<'T>>().Map _.TValue |> Seq.reduce (-) )
    let inline createMulFunction<'T when 'T : (static member (*) : 'T * 'T -> 'T)> (args:Arguments) =
        Operator<'T>("-", args, Evaluator=fun args -> args.Cast<IExpression<'T>>().Map _.TValue |> Seq.reduce (*) )
    let inline createDivFunction<'T when 'T : (static member (/) : 'T * 'T -> 'T)> (args:Arguments) =
        Operator<'T>("-", args, Evaluator=fun args -> args.Cast<IExpression<'T>>().Map _.TValue |> Seq.reduce (/) )

    let add_f32 (args:Arguments<single>)  = createAddFunction<single> (opaqueArgs args)
    let add_f64 (args:Arguments<double>)  = createAddFunction<double> (opaqueArgs args)
    let add_n16 (args:Arguments<int16>)  = createAddFunction<int16> (opaqueArgs args)
    let add_N16 (args:Arguments<uint16>) = createAddFunction<uint16>(opaqueArgs args)
    let add_n32 (args:Arguments<int32>)  = createAddFunction<int32> (opaqueArgs args)
    let add_N32 (args:Arguments<uint32>) = createAddFunction<uint32>(opaqueArgs args)
    let add_String (args:Arguments<string>) = createAddFunction<string> (args.Cast<IExpression>().ToArray())
    let add = add_n32

    let sub_n16 (args:Arguments<int16>)  = createSubFunction<int16> (opaqueArgs args)
    let mul_n16 (args:Arguments<int16>)  = createMulFunction<int16> (opaqueArgs args)
    let div_n16 (args:Arguments<int16>)  = createDivFunction<int16> (opaqueArgs args)
    let sub_n32 (args:Arguments<int32>)  = createSubFunction<int32> (opaqueArgs args)
    let mul_n32 (args:Arguments<int32>)  = createMulFunction<int32> (opaqueArgs args)
    let div_n32 (args:Arguments<int32>)  = createDivFunction<int32> (opaqueArgs args)

    let inline createGeFunction<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) =
        Operator<bool>(">=", [| a :> IExpression; b |],
            Evaluator=fun args ->
                        let args = args.Cast<IExpression<'T>>().ToArray()
                        args[0].TValue >= args[1].TValue)

    let ge_f32    (a:IExpression<single>) (b:IExpression<single>) = createGeFunction<single>     a b
    let ge_f64    (a:IExpression<double>) (b:IExpression<double>) = createGeFunction<double>     a b
    let ge_n16    (a:IExpression<int16>)  (b:IExpression<int16>)  = createGeFunction<int16>      a b
    let ge_N16    (a:IExpression<uint16>) (b:IExpression<uint16>) = createGeFunction<uint16>     a b
    let ge_n32    (a:IExpression<int32>)  (b:IExpression<int32>)  = createGeFunction<int32>      a b
    let ge_N32    (a:IExpression<uint32>) (b:IExpression<uint32>) = createGeFunction<uint32>     a b
    let ge_String (a:IExpression<string>) (b:IExpression<string>) = createGeFunction<string>     a b
