namespace Ev2.Gen

open System
open System.Collections.Generic
open System.Reflection
open Dual.Common.Base


[<AutoOpen>]
module GenExtensionModule =
    type Type with
        /// System.Type default 값 반환
        member dataType.DefaultValue =
            if obj.ReferenceEquals(dataType, null) then null
            elif dataType.IsValueType then Activator.CreateInstance dataType
            else null

    type IVariable with
        /// 초기값 제공자(e.g Variable<'T>)에서 초기값을 가져오거나, 없으면 데이터 타입의 기본값을 반환.
        member x.InitValue = x |> tryCast<IInitValueProvider> >>= _.InitValue |? x.DataType.DefaultValue
        member x.ResetValue() = x.Value <- x.InitValue
        member x.IsInternal() = x.VarType.IsOneOf(VarType.Var, VarType.VarConstant)

    module private StatementRuntime =
        let inline private copyValue (src: ITerminal) (dst: IVariable) =
            dst.Value <- src.Value

        let private getFBState (program: FBProgram) (fbInstance: FBInstance) =
            let project = program.Project :?> IECProject
            let states = project.FBInstanceStates
            match states.TryGetValue fbInstance with
            | true, state -> state
            | _ ->
                let state =
                    let key = $"{program.Name}|{fbInstance.InstanceName}"
                    match project.FBInstanceStatesByName.TryGetValue key with
                    | true, cached ->
                        states.Add(fbInstance, cached)
                        cached
                    | _ ->
                        let created = StateDic(StringComparer.OrdinalIgnoreCase)
                        states.Add(fbInstance, created)
                        project.FBInstanceStatesByName[key] <- created
                        created
                state

        let private restoreInternals (state: StateDic) (variables: IVariable array) =
            for variable in variables do
                if variable.IsInternal() then
                    match state.TryGetValue variable.Name with
                    | true, value -> variable.Value <- value
                    | _ -> variable.Value <- variable.InitValue

        let private persistInternals (state: StateDic) (variables: IVariable array) =
            for variable in variables do
                if variable.IsInternal() then
                    if state.ContainsKey variable.Name then
                        state[variable.Name] <- variable.Value
                    else
                        state.Add(variable.Name, variable.Value)

        let private initialiseFunctionVariables (call: FunctionCall) (locals: IVariable array) =
            for variable in locals do
                match variable.VarType with
                | vt when vt.IsOneOf(VarType.VarInput, VarType.VarInOut) ->
                    variable.Value <- call.Inputs[variable.Name].Value
                | vt when vt = VarType.VarOutput
                       || vt = VarType.VarReturn
                       || vt = VarType.Var
                       || vt = VarType.VarConstant ->
                    variable.ResetValue()
                | _ -> ()

        let private flushFunctionOutputs (call: FunctionCall) (locals: IVariable array) =
            for variable in locals do
                if variable.VarType.IsOneOf(VarType.VarOutput, VarType.VarInOut, VarType.VarReturn) then
                    match call.Outputs.TryGet(variable.Name) with
                    | Some terminal -> copyValue variable terminal
                    | None when variable.VarType = VarType.VarInOut ->
                        match call.Inputs.TryGet(variable.Name) with
                        | Some (:? IVariable as terminal) -> copyValue variable terminal
                        | _ -> () // InOut 인데 매핑이 없으면 무시
                    | _ ->
                        () // 반환 값은 출력에 매핑되지 않을 수 있음

        let private initialiseFBInputs (call: FBCall) (locals: IVariable array) =
            for variable in locals do
                match variable.VarType with
                | vt when vt.IsOneOf(VarType.VarInput, VarType.VarInOut) ->
                    variable.Value <- call.Inputs[variable.Name].Value
                | vt when vt = VarType.VarOutput ->
                    variable.ResetValue()
                | _ -> ()

        let private flushFBOutputs (call: FBCall) (locals: IVariable array) =
            for variable in locals do
                match variable.VarType with
                | vt when vt.IsOneOf(VarType.VarOutput, VarType.VarInOut) ->
                    call.Outputs.TryGet(variable.Name)
                    |> iter (fun terminal -> copyValue variable terminal)
                | _ -> ()

        let rec executeStatement (statement: Statement) =
            let condition = statement.Condition |> toOption |-> _.TValue |? true
            if condition then
                match statement with
                | :? AssignStatementOpaque as stmt ->
                    stmt.Target.Value <- stmt.Source.Value
                | :? FunctionCallStatement as fcs ->
                    executeFunctionCall fcs
                | :? FBCallStatement as fbcs ->
                    executeFBCall fbcs
                | :? TimerStatement
                | :? CounterStatement
                | :? BreakStatement
                | :? SubroutineCallStatement
                | _ ->
                    ()

        and runStatements (statements: Statement array) =
            statements |> iter executeStatement

        and executeFunctionCall (statement: FunctionCallStatement) =
            let call = statement.FunctionCall
            let program = call.IFunctionProgram :?> FunctionProgram
            let locals = program.LocalStorage.Values |> toArray
            initialiseFunctionVariables call locals
            runStatements program.Rungs
            flushFunctionOutputs call locals

        and executeFBCall (statement: FBCallStatement) =
            let call = statement.FBCall
            let program = call.FBInstance.Program
            let locals = program.LocalStorage.Values |> toArray
            let state = getFBState program call.FBInstance
            restoreInternals state locals
            initialiseFBInputs call locals
            runStatements program.Rungs
            persistInternals state locals
            flushFBOutputs call locals

    type Statement with
        member x.Do() =
            StatementRuntime.executeStatement x

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

    let inline createAdd2Function<'T when 'T : (static member (+) : 'T * 'T -> 'T)>(globalStorage:Storage, name:string option) =
        let name = name |? $"Add2_{typeof<'T>.Name}"
        let num1 = Variable<'T>("Num1", varType=VarType.VarInput)
        let num2 = Variable<'T>("Num2", varType=VarType.VarInput)
        let sum = Variable<'T>("Sum", varType=VarType.VarOutput)
        let returnVar = Variable<'T>(name, varType=VarType.VarReturn)
        let localStorage = Storage.Create([num1 :> IVariable; num2; sum])
        let stmt1 = AssignStatement( add<'T> [| num1; num2 |], sum) :> Statement
        let stmt2 = AssignStatement( sum, returnVar)
        FunctionProgram.Create<'T>(name, globalStorage, localStorage, [|stmt1; stmt2|], [||])

    type Project with
        member this.AddScanProgram(program:ScanProgram) =
            (program :> Program).Project <- this :> IProject
            this.ScanPrograms.Add( { Storage = program.LocalStorage; Program = program }:POU )

    type IECProject with
        member this.Add(funcProgram:FunctionProgram) =
            (funcProgram :> Program).Project <- this :> IProject
            this.FunctionPrograms.Add( { Storage = funcProgram.LocalStorage; Program = funcProgram }:POU )
        member this.Add(fbProgram:FBProgram) =
            (fbProgram :> Program).Project <- this :> IProject
            this.FBPrograms.Add( { Storage = fbProgram.LocalStorage; Program = fbProgram}:POU )
