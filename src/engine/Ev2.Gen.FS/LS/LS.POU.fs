namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base

//[<AutoOpen>]
//module XGKBasedModule =
//    [<AbstractClass>]
//    type OperatorCallOrCommand(name:string, exp:IExpression<bool>, arguments:IExpression[]) =
//        new() = OperatorCallOrCommand(nullString, null, [||])
//        member val Name = name with get, set
//        member val Arguments = arguments with get, set
//        member x.Expression = exp

//    ///// XGK 기준 대소 비교 등.
//    //type OperatorCall(name:string, exp:IExpression<bool>, arguments:IExpression[], returnType:Type) =
//    //    inherit OperatorCallOrCommand(name, exp, arguments)
//    //    interface IExpression
//    //    new() = OperatorCall(nullString, null, [||], typeof<bool>)
//    //    member val ReturnType = returnType with get, set

//    type Command(name:string, exp:IExpression<bool>, arguments:IExpression[]) =
//        inherit OperatorCallOrCommand(name, exp, arguments)
//        new() = Command(nullString, null, [||])


[<AutoOpen>]
module POUModule =
    //type Statement =
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
    //    interface IRung



    [<AbstractClass>]
    type Program(name:string, globalStorage:Storage, localStorage:Storage, rungs:Statement[], subroutines:Subroutine[]) =
        interface IProgram
        member x.Name = name
        member x.Rungs = rungs
        member x.Subroutines = subroutines
        member x.GlobalStorage = globalStorage
        member x.LocalStorage = localStorage
        member val Comment = null:string with get, set

    type ScanProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit Program(name, globalStorage, localStorage, rungs, subroutines)

    [<AbstractClass>]
    type SubProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit Program(name, globalStorage, localStorage, rungs, subroutines)
        member val UseEnEno = true with get, set
        member val ColumnWidth = 1 with get, set

    type FunctionProgram<'T>(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFunctionProgram with
            member x.DataType = x.DataType
        member x.DataType = typeof<'T>

    type FBProgram(name, globalStorage, localStorage, rungs, subroutines) =
        inherit SubProgram(name, globalStorage, localStorage, rungs, subroutines)
        interface IFBProgram

    type POU = {
        Storage:Storage
        Program:Program
    }

    type Project(globalStorage:Storage, ?scanPrograms:POU[]) =
        interface IProject
        member val ScanPrograms = scanPrograms |? [||] with get, set
        member x.GlobalVars = globalStorage

    type IECProject(globalStorage:Storage, ?scanPrograms:POU[]) =
        inherit Project(globalStorage, ?scanPrograms=scanPrograms)
        member val UDTs:Struct[] = [||] with get, set
        member val FunctionPrograms:POU[] = [||] with get, set
        member val FBPrograms:POU[] = [||] with get, set

