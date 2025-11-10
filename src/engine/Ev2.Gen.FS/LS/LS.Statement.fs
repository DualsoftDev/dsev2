namespace Ev2.Gen
open System.Linq
open Dual.Common.Base
open System



[<AutoOpen>]
module StatementHelperModule =
    let trueValue  = Literal<bool>(true)
    let falseValue = Literal<bool>(false)
    let boolContact name  = Variable<bool>(name) :> IVariable<bool>
    let coil<'T> name value  = Variable<'T>(name, Value=value) :> IVariable<'T>
    let literal<'T> (value:'T) = Literal<'T>(value) :> ITerminal<'T>


[<AutoOpen>]
module ProgramBlockModule =
    [<AbstractClass>]
    type Statement(?cond:IExpression<bool>, ?comment:string) =
        interface IStatement
        member x.Comment = comment |? nullString
        member x.Condition = cond |? null

    [<AbstractClass>]
    type AssignStatementOpaque(src:IExpression, tgt: IVariable, ?cond, ?comment:string) =
        inherit Statement(?cond=cond, ?comment=comment)
        member x.Source = src
        member val Target = tgt with get, set

    type AssignStatement<'T>(src:IExpression<'T>, tgt: IVariable<'T>, ?cond, ?comment:string) =
        inherit AssignStatementOpaque(src, tgt, ?cond=cond, ?comment=comment)
        new () = AssignStatement<'T>(null, null)
        member x.TSource = src
        member val TTarget = tgt with get, set

    type SetCoilStatement(cond:IExpression<bool>, coil: IVariable<bool>, ?comment:string) =
        inherit Statement(cond, ?comment=comment)
        member x.Coil = coil
    type ResetCoilStatement(cond:IExpression<bool>, coil: IVariable<bool>, ?comment:string) =
        inherit Statement(cond, ?comment=comment)
        member x.Coil = coil

    type TimerStatement(timerCall:TimerCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.TimerCall = timerCall

    type CounterStatement(counterCall:ICounterInstance, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.CounterCall = counterCall


    type ISnippet = interface end

    [<AbstractClass>]
    type Snippet(body:Statement[]) =
        interface ISnippet
        member val Body = body with get, set

    [<AbstractClass>]
    type ForLoopSnippet(from:IExpression<uint16>, to_:IExpression<uint16>, step:IExpression<uint16>, body) =   // NEXT 로 종료, BREAK 지원
        inherit Snippet(body)
        member x.From = from
        member x.To = to_
        member x.Step = step
        member x.Counter = (to_.TValue - from.TValue ) % step.TValue

    type SimpleForLoop(counter:IExpression<uint16>, body) =
        inherit ForLoopSnippet(literal 0us, counter, literal 1us, body)

    [<Obsolete("XGI/XGK only supports SimpleForLoop")>]
    type FullForLoop(from:IExpression<uint16>, to_:IExpression<uint16>, step:IExpression<uint16>, body) =
        inherit ForLoopSnippet(from, to_, step, body)

    /// Subroutine code snippet
    type Subroutine(name:string, ?body) =    // RET 로 종료
        inherit Snippet(body |? [||])
        member x.Name = name

    type BreakStatement(snippet:Snippet, cond:IExpression<bool>, ?comment:string) =
        inherit Statement(cond, ?comment=comment)
        member x.Snippet = snippet

    type SubroutineCallStatement(cond:IExpression<bool>, subroutine:Subroutine, ?comment:string) =
        inherit Statement(cond, ?comment=comment)
        member x.Subroutine = subroutine


    type ForLoopStatement(cond:IExpression<bool>, snippet:ForLoopSnippet, ?comment:string) =
        inherit Statement(cond, ?comment=comment)
        member x.Snippet = snippet
        //static member Create(cond, from, to_, step, body) = ForLoopStatement(cond, FullForLoop(from, to_, step, body))
        static member Create(cond, counter, body) = ForLoopStatement(cond, SimpleForLoop(counter, body))