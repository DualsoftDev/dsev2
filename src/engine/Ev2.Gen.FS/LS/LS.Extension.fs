namespace Ev2.Gen

open System
open System.Collections.Generic
open System.Reflection
open Dual.Common.Base


[<AutoOpen>]
module GenExtensionModule =
    type IFBInstance with
        member this.GetFBProgram() =
            match this with
            | :? FBInstance as reference -> reference.Program
            | :? IFBProgram as fbProgram ->
                match fbProgram with
                | :? FBProgram as concrete -> concrete
                | _ -> failwith "FB 인스턴스에서 FBProgram 을 찾을 수 없습니다."
            | _ ->
                let flags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic
                let property = this.GetType().GetProperty("Program", flags)
                if isNull property then failwith "FB 인스턴스는 Program 속성을 제공해야 합니다."
                match property.GetValue(this) with
                | :? FBProgram as concrete -> concrete
                | _ -> failwith "FB 인스턴스의 Program 속성은 FBProgram 이어야 합니다."

        member this.TryGetInstanceName() =
            let readProperty names =
                let flags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic
                names
                |> Array.tryPick (fun name ->
                    let prop = this.GetType().GetProperty(name, flags)
                    if isNull prop then None
                    else
                        match prop.GetValue(this) with
                        | :? string as value when not (String.IsNullOrWhiteSpace value) -> Some value
                        | _ -> None)
            match this with
            | :? FBInstance as reference ->
                if String.IsNullOrWhiteSpace reference.InstanceName then None else Some reference.InstanceName
            | _ ->
                readProperty [| "InstanceName"; "Name" |]

    module private StatementRuntime =

        let inline private defaultValueOf (dataType: Type) =
            if obj.ReferenceEquals(dataType, null) then null
            elif dataType.IsValueType then Activator.CreateInstance dataType
            else null

        let private initialValueOf (variable: IVariable) =
            match variable :> obj with
            | :? IInitValueProvider as provider ->
                provider.InitValueObject |> Option.defaultValue (defaultValueOf variable.DataType)
            | _ -> defaultValueOf variable.DataType

        let inline private resetVariable (variable: IVariable) =
            variable.Value <- initialValueOf variable

        let inline private copyValue (src: ITerminal) (dst: IVariable) =
            dst.Value <- src.Value

        let inline private isInternalVar (variable: IVariable) =
            variable.VarType = VarType.Var || variable.VarType = VarType.VarConstant

        let private resolveFunctionProgram (program: IFunctionProgram) =
            match program with
            | :? FunctionProgram as concrete -> concrete
            | _ ->
                let name = if isNull program then "(null)" else program.ToString()
                failwith $"FunctionCallStatement 에서 '{name}' 을 FunctionProgram 으로 변환할 수 없습니다."

        let private resolveFBProgram (fbInstance: IFBInstance) =
            fbInstance.GetFBProgram()


        let private getFBState (program: FBProgram) (fbInstance: IFBInstance) =
            let project = program.Project :?> IECProject
            let byInstance = project.FBInstanceStates
            match byInstance.TryGetValue fbInstance with
            | true, state -> state
            | _ ->
                let state =
                    match fbInstance.TryGetInstanceName() with
                    | Some name ->
                        let key = $"{program.Name}|{name}"
                        match project.FBInstanceStatesByName.TryGetValue key with
                        | true, cached ->
                            byInstance.Add(fbInstance, cached)
                            cached
                        | _ ->
                            let created = Dictionary<string, obj>(StringComparer.OrdinalIgnoreCase)
                            byInstance.Add(fbInstance, created)
                            project.FBInstanceStatesByName[key] <- created
                            created
                    | None ->
                        let created = Dictionary<string, obj>(StringComparer.OrdinalIgnoreCase)
                        byInstance.Add(fbInstance, created)
                        created
                state

        let private restoreInternals (state: Dictionary<string, obj>) (variables: IVariable array) =
            for variable in variables do
                if isInternalVar variable then
                    match state.TryGetValue variable.Name with
                    | true, value -> variable.Value <- value
                    | _ -> variable.Value <- initialValueOf variable

        let private persistInternals (state: Dictionary<string, obj>) (variables: IVariable array) =
            for variable in variables do
                if isInternalVar variable then
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
                    resetVariable variable
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
                    resetVariable variable
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
            let program = resolveFunctionProgram call.IFunctionProgram
            let locals = program.LocalStorage.Values |> toArray
            initialiseFunctionVariables call locals
            runStatements program.Rungs
            flushFunctionOutputs call locals

        and executeFBCall (statement: FBCallStatement) =
            let call = statement.FBCall
            let program = resolveFBProgram call.IFBInstance
            let locals = program.LocalStorage.Values |> toArray
            let state = getFBState program call.IFBInstance
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
