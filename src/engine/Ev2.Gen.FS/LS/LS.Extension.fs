namespace Ev2.Gen

open System
open Dual.Common.Base


[<AutoOpen>]
module GenExtensionModule =
    type Type with
        /// System.Type default 값 반환
        member dataType.DefaultValue =
            if obj.ReferenceEquals(dataType, null) then null
            elif dataType.IsValueType then Activator.CreateInstance dataType        // Unchecked.defaultof<'T>
            else null

    type IVariable with
        /// 초기값 제공자(e.g Variable<'T>)에서 초기값을 가져오거나, 없으면 데이터 타입의 기본값을 반환.
        member x.InitValue:obj = x |> tryCast<IInitValueProvider> >>= _.InitValue |? x.DataType.DefaultValue
        member x.ResetValue() = x.Value <- x.InitValue
        member x.IsInternal() = x.VarType.IsOneOf(VarType.Var, VarType.VarConstant)

    type FBInstance with
        /// Function Block instance 의 state(내부 메모리 state) 를 반환
        member x.State =
            let me = x
            let program = x.Program
            let project = program.Project :?> IECProject
            let states = project.FBInstanceStates
            match states.TryGet x with
            | Some state -> state
            | _ ->
                let key = $"{program.Name}|{x.InstanceName}"
                match project.FBInstanceStatesByName.TryGet key with
                | Some cached ->
                    // 현재 FBInstance 객체 x 에 대한 state 는 없지만, x 와 동일 instance 이름으로 존재하는 state 가 있으면, 이름 동일로 state 공유 연결
                    states.Add(x, cached)
                    cached
                | _ ->
                    let created = StateDic(StringComparer.OrdinalIgnoreCase)
                    states.Add(x, created)
                    project.FBInstanceStatesByName[key] <- created
                    created

    module private StatementRuntime =
        /// src 의 Value 값을 dst 의 Value 에 복사
        let inline private (:=) (dst: IVariable) (src: IExpression) = dst.Value <- src.Value

        let private restoreInternals (state: StateDic) (variables: IVariable array) =
            for v in variables do
                if v.IsInternal() then
                    v.Value <- state.TryGet v.Name |? v.InitValue

        let private persistInternals (state: StateDic) (variables: IVariable array) =
            for v in variables do
                if v.IsInternal() then
                    state[v.Name] <- v.Value

        let private initialiseFunctionVariables (call: FunctionCall) (locals: IVariable array) =
            for v in locals do
                match v.VarType with
                | vt when vt.IsOneOf(VarType.VarInput, VarType.VarInOut) ->
                    v := call.Inputs[v.Name]
                | vt when vt = VarType.VarOutput
                       || vt = VarType.VarReturn
                       || vt = VarType.Var
                       || vt = VarType.VarConstant ->
                    v.ResetValue()
                | _ -> ()

        let private flushFunctionOutputs (call: FunctionCall) (locals: IVariable array) =
            for v in locals do
                if v.VarType.IsOneOf(VarType.VarOutput, VarType.VarInOut, VarType.VarReturn) then
                    match call.Outputs.TryGet(v.Name) with
                    | Some terminal -> terminal := v
                    | None when v.VarType = VarType.VarInOut ->
                        match call.Inputs.TryGet(v.Name) with
                        | Some (:? IVariable as terminal) -> terminal := v
                        | _ -> () // InOut 인데 매핑이 없으면 무시
                    | _ ->
                        () // 반환 값은 출력에 매핑되지 않을 수 있음

        let private initialiseFBInputs (call: FBCall) (locals: IVariable array) =
            for v in locals do
                match v.VarType with
                | vt when vt.IsOneOf(VarType.VarInput, VarType.VarInOut) ->
                    v := call.Inputs[v.Name]
                | vt when vt = VarType.VarOutput ->
                    v.ResetValue()
                | _ -> ()

        let private flushFBOutputs (call: FBCall) (locals: IVariable array) =
            for v in locals do
                if v.VarType.IsOneOf(VarType.VarOutput, VarType.VarInOut) then
                    call.Outputs.TryGet(v.Name)
                    |> iter (fun terminal -> terminal := v)

        let rec executeStatement (statement: Statement) =
            let condition = statement.Condition |> toOption |-> _.TValue |? true
            if condition then
                match statement with
                | :? AssignStatementOpaque as stmt ->
                    stmt.Target := stmt.Source
                | :? FunctionCallStatement as fcs ->
                    executeFunctionCall fcs
                | :? FBCallStatement as fbcs ->
                    executeFBCall fbcs
                | :? SetCoilStatement
                | :? ResetCoilStatement
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
            let program = call.FunctionProgram :?> FunctionProgram
            let locals = program.LocalStorage.Values |> toArray

            initialiseFunctionVariables call locals
            runStatements program.Rungs
            flushFunctionOutputs call locals

        and executeFBCall (statement: FBCallStatement) =
            let call = statement.FBCall
            let program = call.FBInstance.Program
            let locals = program.LocalStorage.Values |> toArray
            let state = call.FBInstance.State

            restoreInternals    state locals
            initialiseFBInputs  call locals
            runStatements       program.Rungs
            persistInternals    state locals
            flushFBOutputs      call locals

    type Statement with
        member x.Do() =
            StatementRuntime.executeStatement x

    type FunctionProgram with
        static member Create<'T>(name, globalStorage, localStorage, returnVar:IVariable, rungs, subroutines) =
            assert (returnVar.Name = name)
            assert (returnVar.VarType=VarType.VarReturn)
            FunctionProgram<'T>(name, globalStorage, localStorage, returnVar, rungs, subroutines)
            |> tee(fun _ -> localStorage.Add(name, returnVar))

        static member Create<'T>(name, globalStorage, localStorage, rungs, subroutines) =
            let returnVar = Variable<'T>(name, varType=VarType.VarReturn)
            FunctionProgram.Create<'T>(name, globalStorage, localStorage, returnVar, rungs, subroutines)

    type Project with
        member this.AddScanProgram(program:ScanProgram) =
            (program :> Program).Project <- this :> IProject
            this.ScanPrograms.Add( { Storage = program.LocalStorage; Program = program }:POU )

    type IECProject with
        /// IEC Project 에 Function 추가.
        member this.Add(funcProgram:FunctionProgram) =
            (funcProgram :> Program).Project <- this :> IProject
            this.FunctionPrograms.Add( { Storage = funcProgram.LocalStorage; Program = funcProgram }:POU )

        /// IEC Project 에 Function Block 추가.
        member this.Add(fbProgram:FBProgram) =
            (fbProgram :> Program).Project <- this :> IProject
            this.FBPrograms.Add( { Storage = fbProgram.LocalStorage; Program = fbProgram}:POU )


    /// *TEST* 용 add 2 function 생성.   실제로 Runtime 사용시에는 Operator<'T> 를 사용.  Generation 시에 Operator 를 XGI/XGK 함수로 변환함.
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
