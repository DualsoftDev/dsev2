namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base


[<AutoOpen>]
module ProgramBlockModule =
    type ISnippet = interface end
    type IRung = interface end

    [<AbstractClass>]
    type Snippet(body:IRung[]) =
        interface ISnippet
        member x.Body = body

    type ForLoopSnippet(counter:IExpression<uint16>, body:IRung[]) =   // NEXT 로 종료
        inherit Snippet(body)
        member x.Counter = counter

    /// Subroutine code snippet
    type Subroutine(name:string, body:IRung[]) =    // RET 로 종료
        inherit Snippet(body)
        member x.Name = name

    /// Function/Fuction Block 의 Call Box
    type CallBox(inputs:IExpression[], outputs:IExpression[]) =
        member val Inputs = inputs with get, set
        member val Outputs = outputs with get, set

        member x.EN = x.Inputs[0]
        member x.ENO = x.Outputs[0]

    /// XGI 기준 함수 호출.  expression 이 아니다.
    type FunctionCall(name:string, inputs:IExpression[], outputs:IExpression[]) =
        inherit CallBox(inputs, outputs)
        interface IFunctionCall
        new() = FunctionCall(nullString, [||], [||])        // for serialization
        member val Name = name with get, set

    /// XGI 기준 함수 호출.  expression 이 아니다.
    type FBCall(name:string, inputs:IExpression[], outputs:IExpression[]) =
        inherit CallBox(inputs, outputs)
        interface IFBCall
        new() = FBCall(nullString, [||], [||])        // for serialization
        member val Name = name with get, set

[<AutoOpen>]
module XGKBasedModule =
    [<AbstractClass>]
    type OperatorCallOrCommand(name:string, exp:IExpression<bool>, arguments:IExpression[]) =
        new() = OperatorCallOrCommand(nullString, null, [||])
        member val Name = name with get, set
        member val Arguments = arguments with get, set
        member x.Expression = exp

    ///// XGK 기준 대소 비교 등.
    //type OperatorCall(name:string, exp:IExpression<bool>, arguments:IExpression[], returnType:Type) =
    //    inherit OperatorCallOrCommand(name, exp, arguments)
    //    interface IExpression
    //    new() = OperatorCall(nullString, null, [||], typeof<bool>)
    //    member val ReturnType = returnType with get, set

    type Command(name:string, exp:IExpression<bool>, arguments:IExpression[]) =
        inherit OperatorCallOrCommand(name, exp, arguments)
        new() = Command(nullString, null, [||])


[<AutoOpen>]
module POUModule =
    type Statement =
        | StAssign of exp:IExpression<bool> * lValue: IVariable<bool>
        | StCommand of exp:IExpression<bool> * command:Command
        | StSetCoil of exp:IExpression<bool> * coil: IVariable<bool>
        | StResetCoil of exp:IExpression<bool> * coil: IVariable<bool>
        | StTimer of TimerCall      // timerType:TimerType * rungIn: IExpression<bool> * reset:IExpression<bool> * preset: IExpression<CountUnitType>
        | StCounter of CounterCall  // counterType:CounterType * rungIn: IExpression<bool> * reset:IExpression<bool> * preset: IExpression<CountUnitType>
        | StBreak of exp:IExpression<bool>    // for loop 내에서 사용
        | StSubroutineCall of exp:IExpression<bool> * subroutine:Subroutine
        | StFunctionCall of FunctionCall
        | StFBCall of FBCall
        interface IRung

    type Rung = {
        Statement:Statement
        Comment:string
    } with
        static member Create(statement:Statement, ?comment:string) =
            {
                Statement = statement
                Comment = comment |? nullString
            }



    //type POUType = PtScanProgram | PtFunction | PtFunctionBlock

    //type Program(pouType:POUType, name:string, statements:Statement[], subroutines:SubroutineSnippet[]) =
    //    interface IProgram
    //    member x.POUType = pouType
    //    member x.Name = name
    //    member x.Statements = statements
    //    member x.Subroutines = subroutines

    [<AbstractClass>]
    type Program(name:string, rungs:Rung[], subroutines:Subroutine[]) =
        interface IProgram
        member x.Name = name
        member x.Rungs = rungs
        member x.Subroutines = subroutines
        member val Comment = null:string with get, set

    [<AbstractClass>]
    type SubProgram(name:string, rungs:Rung[], subroutines:Subroutine[]) =
        inherit Program(name, rungs, subroutines)
        member val UseEnEno = true with get, set
        member val ColumnWidth = 1 with get, set

    type ScanProgram(name:string, rungs:Rung[], subroutines:Subroutine[]) =
        inherit Program(name, rungs, subroutines)

    type FunctionProgram(name:string, rungs:Rung[], subroutines:Subroutine[]) =
        inherit SubProgram(name, rungs, subroutines)
        member val ReturnType: Type = typeof<bool> with get, set

    type FBProgram(name:string, rungs:Rung[], subroutines:Subroutine[]) =
        inherit SubProgram(name, rungs, subroutines)

    type POU = {
        Storage:Storage
        Program:Program
    }

    type Project() =
        interface IProject
        member val GlobalVars = Storage() with get, set
        member val UDTs:Struct[] = [||] with get, set
        member val ScanPrograms:POU[] = [||] with get, set
        member val FunctionPrograms:POU[] = [||] with get, set
        member val FBPrograms:POU[] = [||] with get, set


