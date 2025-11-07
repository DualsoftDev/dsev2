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




    type POU = {
        Storage:Storage
        Program:Program
    }

    type Project(?globalStorage:Storage, ?scanPrograms:POU seq) =
        interface IProject
        member val ScanPrograms = ResizeArray(scanPrograms |? [])
        member val GlobalStorage = globalStorage |? Storage() with get, set

    type IECProject(?globalStorage:Storage, ?scanPrograms:POU seq, ?udts:Struct seq, ?functions:POU seq, ?functionBlocks:POU seq) =
        inherit Project(?globalStorage=globalStorage, ?scanPrograms=scanPrograms)
        member val UDTs = ResizeArray(udts |? [])
        member val FunctionPrograms = ResizeArray(functions |? [])
        member val FBPrograms = ResizeArray(functionBlocks |? [])

