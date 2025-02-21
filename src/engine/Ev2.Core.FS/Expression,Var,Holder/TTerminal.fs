namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System
open Gnu.Getopt

[<AutoOpen>]
module rec TTerminalModule =

    type ITerminal =
        inherit IExpression
    type ITerminal<'T> =
        inherit ITerminal
        inherit IExpression<'T>


    // 기존 Terminal<'T> 에 해당.
    type TTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface ITerminal<'T>
        new() = TTerminal(Unchecked.defaultof<'T>)   // for Json


    // 임시 구현
    type IExpression with
        [<Obsolete("임시")>] member x.Terminal = x :?> ITerminal |> Option.ofObj
        [<Obsolete("임시")>] member x.DataType = x.Type
        [<Obsolete("임시")>] member x.FunctionName = if x :? INonTerminal then Some x.Type.Name else None
        [<Obsolete("임시")>] member x.BoxedEvaluatedValue = tryGetPropertyValueDynmaically(x, "Value") |? null

        [<Obsolete("임시")>] member x.IsLiteral = false
    type IExpression<'T> with
        [<Obsolete("임시")>] member x.Evaluate() = Unchecked.defaultof<'T>


    type Arguments = IExpression list
    type Args      = Arguments

    type INonTerminal =
        inherit IExpression
    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpression<'T>

    and IEvaluator = Arguments -> IExpression
    /// Evaluator : Operator
    // 기존 FlatExpression.Op 와 어떻게 병합?? (Symbolic)
    and TEvaluator<'T> = Arguments -> 'T


    type Op =
    | OpUnit // Logical XOR 는 function 인 '<>' 로 구현됨

    | And
    | Or
    | Neg

    | RisingAfter
    | FallingAfter

    | OpCompare of operator: string
    | OpArithmetic of operator: string

    /// 정상 범주에서 지원되지 않는 operator
    | OpOutOfService of IEvaluator


    // 기존 FunctionSpec<'T> 에 해당.
    type TNonTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface INonTerminal<'T>

        new() = TNonTerminal(Unchecked.defaultof<'T>)   // for Json

        member val Operator: Op = Op.OpUnit with get, set
        member val Arguments: IExpression[] = [||] with get, set


    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynmaically(x, "FunctionBody") :?> (TEvaluator<'T>)
            and set (v:Arguments -> 'T) = setPropertyValueDynmaically(x, "FunctionBody", v)


    type TExpression<'T> =
        | DuTerminal of TTerminal<'T>
        | DuNonTerminal of TNonTerminal<'T>
        with
            interface IExpression<'T>
            static member Create(?name:string, ?arguments:Arguments, ?functionBody:TEvaluator<'T>) =
                TNonTerminal<'T>(Unchecked.defaultof<'T>)
                |> tee(fun nt ->
                    nt.Name <- name |? null)
