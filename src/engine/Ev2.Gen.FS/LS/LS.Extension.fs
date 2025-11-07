namespace Ev2.Gen

open System
open System.Collections.Generic
open Dual.Common.Base


[<AutoOpen>]
module GenExtensionModule =
    type IStatement with
        member x.Do() =
            match x with
            | :? AssignStatementOpaque as stmt ->
                if stmt.Condition |> toOption |-> _.TValue |? true then
                    stmt.Target.Value <- stmt.Source.Value
            | :? FunctionCallStatement as fcs ->
                // Implement the logic for FunctionCallStatement
                printfn "Executing FunctionCallStatement with function: %A" fcs.FunctionCall.IFunctionProgram
                ()
            | :? FBCallStatement as fbcs ->
                // Implement the logic for FBCallStatement
                printfn "Executing FBCallStatement with FB instance: %A" fbcs.FBCall.IFBInstance
                ()
            | :? TimerStatement
            | :? CounterStatement
            | :? BreakStatement
            | :? SubroutineCallStatement
            | _ ->
                // Handle other statement types or do nothing
                printfn "Executing other type of Statement"
                ()

    type FunctionProgram with
        static member Create<'T>(name, globalStorage, localStorage, returnVar:IVariable, rungs, subroutines) =
            assert (returnVar.Name = name)
            assert (returnVar.VarType=VarType.VarReturn)
            let xxx = typeof<'T>
            FunctionProgram<'T>(name, globalStorage, localStorage, returnVar, rungs, subroutines)
            |> tee(fun _ -> localStorage.Add(name, returnVar))

        static member Create<'T>(name, globalStorage, localStorage, rungs, subroutines) =
            let returnVar = Variable<'T>(name, varType=VarType.VarReturn)
            FunctionProgram.Create<'T>(name, globalStorage, localStorage, returnVar, rungs, subroutines)

