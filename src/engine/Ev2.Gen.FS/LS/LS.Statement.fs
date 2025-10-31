namespace Ev2.Gen
open System.Linq
open Dual.Common.Base

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


    //    | StAssign of exp:IExpression<bool> * lValue: IVariable<bool>
    //    //| StCommand of exp:IExpression<bool> * command:Command
    //    | StSetCoil of exp:IExpression<bool> * coil: IVariable<bool>
    //    | StResetCoil of exp:IExpression<bool> * coil: IVariable<bool>
    //    | StTimer of TimerCall      // timerType:TimerType * rungIn: IExpression<bool> * reset:IExpression<bool> * preset: IExpression<CountUnitType>
    //    | StCounter of CounterCall  // counterType:CounterType * rungIn: IExpression<bool> * reset:IExpression<bool> * preset: IExpression<CountUnitType>
    //    | StBreak of exp:IExpression<bool>    // for loop 내에서 사용
    //    | StSubroutineCall of exp:IExpression<bool> * subroutine:Subroutine
    //    | StFunctionCall of FunctionCall
    //    | StFBCall of FBCall
    //    | StUndefined


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



    type TimerStatement(timerCall:TimerCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.TimerCall = timerCall

    type CounterStatement(counterCall:CounterCall, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.CounterCall = counterCall

    type BreakStatement(exp:IExpression<bool>, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.Exp = exp
    type SubroutineCallStatement(exp:IExpression<bool>, subroutine:Subroutine, ?comment:string) =
        inherit Statement(?comment=comment)
        member x.Exp = exp
        member x.Subroutine = subroutine
