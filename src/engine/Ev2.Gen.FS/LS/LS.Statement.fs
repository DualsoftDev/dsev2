namespace Ev2.Gen
open System.Linq
open Dual.Common.Base
open System

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


[<Obsolete("Non-IEC constructs are deprecated and will be removed in future versions.")>]
[<AutoOpen>]
module NonIECModule =
    type ISnippet = interface end

    [<AbstractClass>]
    type Snippet(body:Statement[]) =
        interface ISnippet
        member x.Body = body

    type ForLoopSnippet(counter:IExpression<uint16>, body) =   // NEXT 로 종료
        inherit Snippet(body)
        member x.Counter = counter

    /// Subroutine code snippet
    type Subroutine(name:string, body) =    // RET 로 종료
        inherit Snippet(body)
        member x.Name = name

    type BreakStatement(exp:IExpression<bool>, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.Exp = exp
    type SubroutineCallStatement(exp:IExpression<bool>, subroutine:Subroutine, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.Exp = exp
        member x.Subroutine = subroutine
