namespace Ev2.Gen
open System
open System.Linq
open Dual.Common.Base

[<AutoOpen>]
module private ShiftOperatorExtensions =
    type Int32 with
        static member op_LessLessLess(value:int, shift:int) = value <<< shift
        static member op_GreaterGreaterGreater(value:int, shift:int) = value >>> shift

    type UInt32 with
        static member op_LessLessLess(value:uint32, shift:int) = value <<< shift
        static member op_GreaterGreaterGreater(value:uint32, shift:int) = value >>> shift

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


    /// ShiftLeft 연산자 (<<<) 구현
    let inline shl<'T> (value:IExpression<'T>) (shift:IExpression<int>) =
        Operator<'T>("SHL", [| value :> IExpression; shift :> IExpression |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression< 'T >
                let shift = args[1] :?> IExpression<int> |> _.TValue
                match box valueExpr.TValue with
                | :? int8   as v -> v <<< shift |> unbox<'T>
                | :? int16  as v -> v <<< shift |> unbox<'T>
                | :? int32  as v -> v <<< shift |> unbox<'T>
                | :? int64  as v -> v <<< shift |> unbox<'T>
                | :? uint8  as v -> v <<< shift |> unbox<'T>
                | :? uint16 as v -> v <<< shift |> unbox<'T>
                | :? uint32 as v -> v <<< shift |> unbox<'T>
                | :? uint64 as v -> v <<< shift |> unbox<'T>
                | _ -> failwithf "지원하지 않는 형식에 대한 ShiftLeft 연산입니다: %s" (valueExpr.DataType.FullName)
        )


    /// ShiftRight 연산자 (>>>) 구현
    let inline shr<'T> (value:IExpression<'T>) (shift:IExpression<int>) =
        Operator<'T>("SHR", [| value :> IExpression; shift :> IExpression |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression< 'T >
                let shift = args[1] :?> IExpression<int> |> _.TValue
                match box valueExpr.TValue with
                | :? int8   as v -> v >>> shift |> unbox<'T>
                | :? int16  as v -> v >>> shift |> unbox<'T>
                | :? int32  as v -> v >>> shift |> unbox<'T>
                | :? int64  as v -> v >>> shift |> unbox<'T>
                | :? uint8  as v -> v >>> shift |> unbox<'T>
                | :? uint16 as v -> v >>> shift |> unbox<'T>
                | :? uint32 as v -> v >>> shift |> unbox<'T>
                | :? uint64 as v -> v >>> shift |> unbox<'T>
                | _ -> failwithf "지원하지 않는 형식에 대한 ShiftRight 연산입니다: %s" (valueExpr.DataType.FullName)
        )
