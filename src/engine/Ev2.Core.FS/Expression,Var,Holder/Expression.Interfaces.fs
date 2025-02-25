namespace Dual.Ev2

open System

open Dual.Common.Base.FS
open Dual.Common.Core.FS

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

    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpression<'T>


    type Op =
    | Unit // Logical XOR 는 function 인 '<>' 로 구현됨

    | PredefinedOperator of operator: string

    /// 정상 범주에서 지원되지 않는 operator
    | CustomOperator of (Args -> obj)



