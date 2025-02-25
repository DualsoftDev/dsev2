namespace Dual.Ev2

open System
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS
open Dual.Common.Core

[<AutoOpen>]
module rec ExpressionInterfaceModule =

    type ITerminal =
        inherit IExpression
    type ITerminal<'T> =
        inherit ITerminal
        inherit IExpression<'T>

    // 임시 구현
    type IExpression with
        [<Obsolete("임시")>] member x.Terminal = x :?> ITerminal |> Option.ofObj
        [<Obsolete("임시")>] member x.DataType = x.Type
        [<Obsolete("임시")>] member x.FunctionName = if x :? INonTerminal then Some x.Type.Name else None
        [<Obsolete("임시")>] member x.BoxedEvaluatedValue = tryGetPropertyValueDynamically(x, "Value") |? null

        [<Obsolete("임시")>] member x.IsLiteral = false

    type IExpression<'T> with
        [<Obsolete("임시")>]
        member x.Evaluate() =
            let xxx = x
            Unchecked.defaultof<'T>


    type Arguments = IExpression list
    type Args      = Arguments
    type TArguments<'T> = IExpression<'T> list
    type TArgs<'T> = TArguments<'T>

    type INonTerminal =
        inherit IExpression
        //abstract member Operator: Op with get, set
        //abstract member Arguments: IExpression[] with get, set

    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpression<'T>

    /// IEvaluator: 기본 평가 클래스 (Arguments -> obj)
    type IEvaluator =
        abstract Evaluate: Arguments -> obj

    // TEvaluator<'T>: IEvaluator를 상속하여 Arguments -> 'T 를 구현
    type TEvaluator<'T>(evaluator:Arguments -> 'T) =
        interface IEvaluator with
            member x.Evaluate(args) = x.TEvaluate(args) |> box

        member x.TEvaluate(args) = evaluator args

    type TExpressionEvaluator<'T>(evaluator:Arguments -> 'T, args:Args) =
        inherit TEvaluator<'T>(evaluator)
        interface IExpression<'T> with
            member x.Evaluate() = x.TEvaluate(args) |> box
            member x.TEvaluate() = x.TEvaluate(args)


    type Op =
    | Unit // Logical XOR 는 function 인 '<>' 로 구현됨

    //| And
    //| Or
    //| Neg

    //| RisingAfter
    //| FallingAfter

    //| OpCompare of operator: string
    | PredefinedOperator of operator: string

    /// 정상 범주에서 지원되지 않는 operator
    | CustomOperator of IEvaluator

    // 기존 Terminal<'T> 에 해당.
    type TTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface ITerminal<'T>
        new() = TTerminal(Unchecked.defaultof<'T>)   // for Json




