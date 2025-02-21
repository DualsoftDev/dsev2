namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS

[<AutoOpen>]
module TTerminalModule =

    type ITerminal = interface end
    type INonTerminal = interface end

    type ITerminal<'T> =
        inherit ITerminal
        inherit IExpression<'T>

    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpression<'T>

    type Arguments = IExpression list


    // 기존 Terminal<'T> 에 해당.
    type TTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface ITerminal<'T>
        new() = TTerminal(Unchecked.defaultof<'T>)   // for Json

    // 기존 FunctionSpec<'T> 에 해당.
    type TNonTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface INonTerminal<'T>

        new() = TNonTerminal(Unchecked.defaultof<'T>)   // for Json

        member val Arguments: ITHolder[] = [||] with get, set

    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynmaically(x, "FunctionBody") :?> (Arguments -> 'T)
            and set (v:Arguments -> 'T) = setPropertyValueDynmaically(x, "FunctionBody", v)



