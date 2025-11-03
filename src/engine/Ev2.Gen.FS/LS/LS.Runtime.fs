namespace Ev2.Gen

open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.CompilerServices
open Dual.Common.Base

[<AutoOpen>]
module private RuntimeHelpers =
    type Mapping = IDictionary<string, ITerminal>

    let inline tryGetValue<'T> (dictionary: IDictionary<string, 'T>) (key: string) =
        match dictionary with
        | :? Dictionary<string, 'T> as concrete ->
            match concrete.TryGetValue key with
            | true, value -> Some value
            | _ -> None
        | _ ->
            let mutable temp = Unchecked.defaultof<'T>
            if dictionary.TryGetValue(key, &temp) then Some temp else None

    let inline ensureMapping (dictionary: IDictionary<string, 'T>) (name: string) =
        match tryGetValue dictionary name with
        | Some value -> value
        | None -> failwith $"매핑에서 '{name}' 항목을 찾을 수 없습니다."

    let defaultValueOf (dataType: Type) =
        if dataType.IsValueType then Activator.CreateInstance dataType else null

    let inline unwrapInitValue (value: obj option) (dataType: Type) =
        value |? defaultValueOf dataType

    let isInputVar = function
        | VarType.VarInput
        | VarType.VarInOut -> true
        | _ -> false

    let isOutputVar = function
        | VarType.VarOutput
        | VarType.VarInOut -> true
        | _ -> false

    let isInOutVar = (=) VarType.VarInOut

    let updateDictionary (dictionary: Dictionary<string, obj>) (name: string) (value: obj) =
        if dictionary.ContainsKey name then
            dictionary[name] <- value
        else
            dictionary.Add(name, value)

    let getVarType (variable: IVariable) =
        match variable with
        | :? VarBase<_> as varBase -> varBase.VarType
        | _ ->
            let property =
                variable.GetType().GetProperty(
                    "VarType",
                    BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.FlattenHierarchy)
            if isNull property then VarType.VarUndefined
            else
                match property.GetValue(variable) with
                | :? VarType as value -> value
                | _ -> VarType.VarUndefined

    let tryGetInitValue (variable: IVariable) =
        match variable with
        | :? VarBase<_> as varBase -> varBase.InitValue |> Option.map box
        | _ -> None

type internal FunctionDefinition =
    { Program: FunctionProgram
      Globals: IVariable list
      Parameters: IVariable list
      Locals: IVariable list
      Body: Statement array }

type internal FBDefinition =
    { Program: FBProgram
      Inputs: IVariable list
      Outputs: IVariable list
      Internals: IVariable list
      Body: Statement array }

module private DefinitionBuilder =
    let private storageValues (storage: Storage) =
        storage.Values |> Seq.toList

    let private isLocalVariable varType =
        match varType with
        | VarType.Var
        | VarType.VarConstant -> true
        | _ -> false

    let private isParameterVariable varType =
        match varType with
        | VarType.VarInput
        | VarType.VarOutput
        | VarType.VarInOut -> true
        | _ -> false

    let buildFunctionDefinition (program: FunctionProgram) : FunctionDefinition =
        let locals = storageValues program.LocalStorage
        let globals = storageValues program.GlobalStorage
        { Program = program
          Globals = globals
          Parameters =
              locals
              |> List.filter (fun variable -> variable |> RuntimeHelpers.getVarType |> isParameterVariable)
          Locals =
              locals
              |> List.filter (fun variable -> variable |> RuntimeHelpers.getVarType |> isLocalVariable)
          Body = program.Rungs }

    let private isFBInput varType =
        match varType with
        | VarType.VarInput
        | VarType.VarInOut -> true
        | _ -> false

    let private isFBOutput varType =
        match varType with
        | VarType.VarOutput
        | VarType.VarInOut -> true
        | _ -> false

    let private isFBInternal varType =
        match varType with
        | VarType.Var
        | VarType.VarConstant -> true
        | _ -> false

    let buildFBDefinition (program: FBProgram) : FBDefinition =
        let locals = storageValues program.LocalStorage
        { Program = program
          Inputs    = locals |> List.filter (getVarType >> isFBInput)
          Outputs   = locals |> List.filter (getVarType >> isFBOutput)
          Internals = locals |> List.filter (getVarType >> isFBInternal)
          Body = program.Rungs }

type internal ExecutionScope =
    { Resolver: IRuntimeResolver }

and internal FunctionRuntimeTemplate(definition: FunctionDefinition, resolver: IRuntimeResolver) =
    member internal _.Definition = definition

    member this.CreateRuntime(
        inputMapping: Mapping,
        outputMapping: Mapping) : FunctionRuntime =
        FunctionRuntime.Create(definition, resolver, inputMapping, outputMapping)

    member this.Invoke(inputMapping: Mapping, outputMapping: Mapping) =
        let runtime = this.CreateRuntime(inputMapping, outputMapping)
        runtime.Do()

and internal FBInstanceRuntime(definition: FBDefinition, resolver: IRuntimeResolver, state: Dictionary<string, obj>) =
    member internal _.Definition = definition
    member internal _.State = state

    member internal this.CreateCall(
        inputMapping: Mapping,
        outputMapping: Mapping) =
        FBInstanceRuntimeCall(definition, resolver, state, inputMapping, outputMapping)

    member this.Invoke(inputMapping: Mapping, outputMapping: Mapping) =
        this.CreateCall(inputMapping, outputMapping).Do()

and [<AllowNullLiteral>] internal IRuntimeResolver =
    abstract ResolveFunction : IFunctionProgram -> FunctionRuntimeTemplate
    abstract ResolveFBInstance : IFBInstance -> FBInstanceRuntime

and internal FunctionRuntimeCall
    ( definition: FunctionDefinition,
      resolver: IRuntimeResolver,
      inputMapping: Mapping,
      outputMapping: Mapping) =

    let scope = { Resolver = resolver }

    let initialiseParameters () =
        definition.Parameters
        |> List.iter (fun variable ->
            let varType = getVarType variable
            if isInputVar varType then
                let source = ensureMapping inputMapping variable.Name
                variable.Value <- source.Value
            elif isOutputVar varType && not (isInOutVar varType) then
                let initial = unwrapInitValue (tryGetInitValue variable) variable.DataType
                variable.Value <- initial
            else
                ())

        definition.Parameters
        |> List.filter (getVarType >> isInOutVar)
        |> List.iter (fun variable ->
            let source =
                ensureMapping (if inputMapping.ContainsKey variable.Name then inputMapping else outputMapping) variable.Name
            variable.Value <- source.Value)

    let initialiseLocals () =
        definition.Locals
        |> List.iter (fun variable ->
            let initial = unwrapInitValue (tryGetInitValue variable) variable.DataType
            variable.Value <- initial)

    let flushOutputs () =
        definition.Parameters
        |> List.filter (getVarType >> isOutputVar)
        |> List.iter (fun variable ->
            let value = variable.Value
            match tryGetValue outputMapping variable.Name with
            | Some terminal ->
                terminal.Value <- value
            | None ->
                let varType = getVarType variable
                if isInOutVar varType then
                    let source = ensureMapping inputMapping variable.Name
                    source.Value <- value
                else
                    failwith $"출력 매핑에서 '{variable.Name}' 을(를) 찾을 수 없습니다.")

    member _.Do() =
        initialiseParameters ()
        initialiseLocals ()
        StatementExecutor.runFunction(definition, scope)
        flushOutputs ()

and FunctionRuntime private (call: FunctionRuntimeCall) =
    member _.Do() = call.Do()

    static member internal Create(
        definition: FunctionDefinition,
        resolver: IRuntimeResolver,
        inputMapping: Mapping,
        outputMapping: Mapping) =
        let call = FunctionRuntimeCall(definition, resolver, inputMapping, outputMapping)
        FunctionRuntime(call)

and internal FBInstanceRuntimeTemplate(definition: FBDefinition, resolver: IRuntimeResolver) =
    member internal _.Definition = definition

    member internal _.CreateInstance() =
        let state = Dictionary<string, obj>(StringComparer.OrdinalIgnoreCase)
        FBInstanceRuntime(definition, resolver, state)

and internal FBInstanceRuntimeCall
    ( definition: FBDefinition,
      resolver: IRuntimeResolver,
      state: Dictionary<string, obj>,
      inputMapping: Mapping,
      outputMapping: Mapping) =

    let scope = { Resolver = resolver }

    let restoreInternals () =
        definition.Internals
        |> List.iter (fun variable ->
            let value =
                match tryGetValue state variable.Name with
                | Some stored -> stored
                | None -> unwrapInitValue (tryGetInitValue variable) variable.DataType
            variable.Value <- value)

    let applyInputs () =
        definition.Inputs
        |> List.iter (fun variable ->
            let source = ensureMapping inputMapping variable.Name
            variable.Value <- source.Value)

        definition.Outputs
        |> List.filter (fun variable -> variable |> getVarType |> isInOutVar)
        |> List.iter (fun variable ->
            let source =
                ensureMapping (if inputMapping.ContainsKey variable.Name then inputMapping else outputMapping) variable.Name
            variable.Value <- source.Value)

    let initialiseOutputs () =
        definition.Outputs
        |> List.filter (fun variable -> variable |> getVarType |> isInOutVar |> not)
        |> List.iter (fun variable ->
            let initial = unwrapInitValue (tryGetInitValue variable) variable.DataType
            variable.Value <- initial)

    let persistInternals () =
        definition.Internals
        |> List.iter (fun variable -> updateDictionary state variable.Name variable.Value)

    let flushOutputs () =
        definition.Outputs
        |> List.iter (fun variable ->
            let value = variable.Value
            match tryGetValue outputMapping variable.Name with
            | Some terminal -> terminal.Value <- value
            | None when variable |> getVarType |> isInOutVar ->
                let source = ensureMapping inputMapping variable.Name
                source.Value <- value
            | None ->
                failwith $"FB 출력 매핑에서 '{variable.Name}' 을(를) 찾을 수 없습니다.")

    member _.Do() =
        restoreInternals ()
        applyInputs ()
        initialiseOutputs ()
        StatementExecutor.runFB(definition, scope)
        persistInternals ()
        flushOutputs ()

and ReferenceEqualityComparer<'T when 'T : not struct>() =
    interface IEqualityComparer<'T> with
        member _.Equals(x, y) = obj.ReferenceEquals(x, y)
        member _.GetHashCode(x) = RuntimeHelpers.GetHashCode x

and internal StatementExecutor =
    static member private evaluateCondition (condition: IExpression) =
        if isNull condition then true
        else
            match condition with
            | :? IExpression<bool> as boolExpr -> boolExpr.TValue
            | _ ->
                match condition.Value with
                | :? bool as value -> value
                | _ -> failwith "조건식은 bool 이어야 합니다."

    static member private executeAssign (statement: AssignStatementOpaque) =
        if StatementExecutor.evaluateCondition statement.Condition then
            let value = statement.Source.Value
            statement.Target.Value <- value

    static member private trimCallInputs (program: SubProgram) (expressions: IExpression[]) =
        if program.UseEnEno && expressions.Length > 0 then
            expressions |> Array.skip 1
        else
            expressions

    static member private trimCallOutputs (program: SubProgram) (expressions: IExpression[]) =
        if program.UseEnEno && expressions.Length > 0 then
            expressions |> Array.skip 1
        else
            expressions

    static member private toTerminal (expr: IExpression) =
        match expr with
        | :? ITerminal as terminal -> terminal
        | _ -> failwith "입출력 매핑은 ITerminal이어야 합니다."

    static member private buildMapping (decls: IVariable list) (expressions: IExpression[]) =
        if decls.Length <> expressions.Length then
            failwith $"매핑 개수가 일치하지 않습니다. 기대: {decls.Length}, 실제: {expressions.Length}"

        let dictionary = Dictionary<string, ITerminal>(StringComparer.OrdinalIgnoreCase)
        decls
        |> List.iteri (fun index variable ->
            let terminal = StatementExecutor.toTerminal expressions[index]
            dictionary.Add(variable.Name, terminal))
        dictionary :> IDictionary<_, _>

    static member private executeFunctionCall
        (scope: ExecutionScope)
        (statement: FunctionCallStatement) =

        let template = scope.Resolver.ResolveFunction statement.FunctionCall.IFunctionProgram
        let callee = template.Definition.Program
        let filteredInputs =
            StatementExecutor.trimCallInputs callee statement.FunctionCall.Inputs
        let filteredOutputs =
            StatementExecutor.trimCallOutputs callee statement.FunctionCall.Outputs

        let inputDecls =
            template.Definition.Parameters |> List.filter (getVarType >> isInputVar)
        let outputDecls =
            template.Definition.Parameters |> List.filter (getVarType >> isOutputVar)

        let inputMapping = StatementExecutor.buildMapping inputDecls filteredInputs
        let outputMapping = StatementExecutor.buildMapping outputDecls filteredOutputs
        template.CreateRuntime(inputMapping, outputMapping).Do()

    static member private executeFBCall (scope: ExecutionScope) (statement: FBCallStatement) =
        let runtime = scope.Resolver.ResolveFBInstance statement.FBCall.IFBInstance
        let definition = runtime.Definition
        let callee = definition.Program

        let filteredInputs =
            StatementExecutor.trimCallInputs callee statement.FBCall.Inputs
        let filteredOutputs =
            StatementExecutor.trimCallOutputs callee statement.FBCall.Outputs

        let inputDecls = definition.Inputs
        let outputDecls = definition.Outputs

        let inputMapping = StatementExecutor.buildMapping inputDecls filteredInputs
        let outputMapping = StatementExecutor.buildMapping outputDecls filteredOutputs
        runtime.Invoke(inputMapping, outputMapping)

    static member private execute(statements: Statement array, scope: ExecutionScope) =
        for statement in statements do
            match statement with
            | :? AssignStatementOpaque as assign ->
                StatementExecutor.executeAssign assign
            | :? FunctionCallStatement as functionCall ->
                StatementExecutor.executeFunctionCall scope functionCall
            | :? FBCallStatement as fbCall ->
                StatementExecutor.executeFBCall scope fbCall
            | :? TimerStatement
            | :? CounterStatement
            | :? BreakStatement
            | :? SubroutineCallStatement
            | _ -> ()

    static member runFunction(definition: FunctionDefinition, scope: ExecutionScope) =
        StatementExecutor.execute(definition.Body, scope)

    static member runFB(definition: FBDefinition, scope: ExecutionScope) =
        StatementExecutor.execute(definition.Body, scope)

type ProjectRuntime(project: IECProject) =
    let functionCache =
        Dictionary<FunctionProgram, FunctionRuntimeTemplate>(ReferenceEqualityComparer<FunctionProgram>())
    let fbTemplateCache =
        Dictionary<FBProgram, FBInstanceRuntimeTemplate>(ReferenceEqualityComparer<FBProgram>())
    let fbInstanceCache =
        Dictionary<IFBInstance, FBInstanceRuntime>(ReferenceEqualityComparer<IFBInstance>())
    let fbInstanceNameCache =
        Dictionary<string, FBInstanceRuntime>(StringComparer.OrdinalIgnoreCase)

    let tryCastFBProgram (value: obj) =
        match value with
        | :? FBProgram as concrete -> Some concrete
        | :? IFBProgram as iface ->
            match iface with
            | :? FBProgram as concrete -> Some concrete
            | _ -> None
        | _ -> None

    let tryExtractFBProgram (fbInstance: IFBInstance) =
        let instanceObj = fbInstance :> obj
        if isNull instanceObj then None
        else
            match tryCastFBProgram fbInstance with
            | Some _ as result -> result
            | None ->
                let flags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic
                let properties = fbInstance.GetType().GetProperties(flags)
                match
                    properties
                    |> Array.tryPick (fun property ->
                        if typeof<IFBProgram>.IsAssignableFrom(property.PropertyType)
                           || typeof<FBProgram>.IsAssignableFrom(property.PropertyType) then
                            try
                                property.GetValue(fbInstance) |> tryCastFBProgram
                            with _ ->
                                None
                        else
                            None)
                with
                | Some _ as result -> result
                | None ->
                    fbInstance.GetType().GetFields(flags)
                    |> Array.tryPick (fun field ->
                        if typeof<IFBProgram>.IsAssignableFrom(field.FieldType)
                           || typeof<FBProgram>.IsAssignableFrom(field.FieldType) then
                            tryCastFBProgram (field.GetValue(fbInstance))
                        else
                            None)

    let tryGetInstanceName (fbInstance: IFBInstance) =
        let instanceObj = fbInstance :> obj
        if isNull instanceObj then None
        else
            let flags = BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic

            let readMember (value: obj) =
                match value with
                | :? string as name when not (String.IsNullOrWhiteSpace name) -> Some name
                | _ -> None

            let propertyNames = [| "InstanceName"; "Name" |]

            let tryProperty () =
                propertyNames
                |> Array.tryPick (fun propertyName ->
                    try
                        let property = fbInstance.GetType().GetProperty(propertyName, flags)
                        if isNull property then None
                        else readMember (property.GetValue(fbInstance))
                    with _ ->
                        None)

            match tryProperty () with
            | Some _ as result -> result
            | None ->
                fbInstance.GetType().GetFields(flags)
                |> Array.tryPick (fun field ->
                    if typeof<string>.IsAssignableFrom field.FieldType then
                        try
                            field.GetValue(fbInstance) |> readMember
                        with _ ->
                            None
                    else
                        None)

    let buildInstanceNameKey (program: FBProgram) (instanceName: string) =
        $"{program.Name}|{instanceName}"

    let getFBProgram (fbInstance: IFBInstance) =
        let instanceObj = fbInstance :> obj
        match tryExtractFBProgram fbInstance with
        | Some program -> program
        | None ->
            let typeName =
                if isNull instanceObj then "(null)" else fbInstance.GetType().FullName |? "(unknown)"
            failwith $"FB 인스턴스 '{typeName}' 에서 FBProgram 을 찾을 수 없습니다."

    member private this.EnsureFunctionTemplate(program: FunctionProgram) =
        match functionCache.TryGetValue program with
        | true, template -> template
        | _ ->
            let definition = DefinitionBuilder.buildFunctionDefinition program
            let template = FunctionRuntimeTemplate(definition, this :> IRuntimeResolver)
            functionCache.Add(program, template)
            template

    member private this.ResolveTemplate(program: IFunctionProgram) =
        match program with
        | :? FunctionProgram as concrete -> this.EnsureFunctionTemplate concrete
        | _ ->
            let programName = if isNull program then "(null)" else program.ToString()
            failwith $"프로젝트에서 함수 '{programName}' 을(를) 해석할 수 없습니다."

    member private this.EnsureFBTemplate(program: FBProgram) =
        match fbTemplateCache.TryGetValue program with
        | true, template -> template
        | _ ->
            let definition = DefinitionBuilder.buildFBDefinition program
            let template = FBInstanceRuntimeTemplate(definition, this :> IRuntimeResolver)
            fbTemplateCache.Add(program, template)
            template

    member private this.EnsureFBInstanceRuntime(fbInstance: IFBInstance) =
        match fbInstanceCache.TryGetValue fbInstance with
        | true, runtime -> runtime
        | _ ->
            let program = getFBProgram fbInstance
            let template = this.EnsureFBTemplate program
            let nameKey =
                match tryGetInstanceName fbInstance with
                | Some name -> Some (buildInstanceNameKey program name)
                | None -> None

            let runtime =
                match nameKey with
                | Some key ->
                    match fbInstanceNameCache.TryGetValue key with
                    | true, cached ->
                        if not (fbInstanceCache.ContainsKey fbInstance) then
                            fbInstanceCache.Add(fbInstance, cached)
                        cached
                    | _ ->
                        let created = template.CreateInstance()
                        fbInstanceCache.Add(fbInstance, created)
                        fbInstanceNameCache[key] <- created
                        created
                | None ->
                    let created = template.CreateInstance()
                    fbInstanceCache.Add(fbInstance, created)
                    created

            runtime

    member this.CreateFunctionRuntime(
        program: IFunctionProgram,
        inputMapping: Mapping,
        outputMapping: Mapping) =
        let template = this.ResolveTemplate program
        template.CreateRuntime(inputMapping, outputMapping)

    member this.CreateFunctionRuntime(
        program: FunctionProgram,
        inputMapping: Mapping,
        outputMapping: Mapping) =
        this.CreateFunctionRuntime(program :> IFunctionProgram, inputMapping, outputMapping)

    member this.CreateFunctionRuntime<'T>
        (
            program: FunctionProgram<'T>,
            inputMapping: Mapping,
            outputMapping: Mapping
        ) =
        this.CreateFunctionRuntime(program :> FunctionProgram, inputMapping, outputMapping)

    member this.InvokeFBInstance(
        fbInstance: IFBInstance,
        inputMapping: Mapping,
        outputMapping: Mapping) =
        let runtime = this.EnsureFBInstanceRuntime fbInstance
        runtime.Invoke(inputMapping, outputMapping)

    interface IRuntimeResolver with
        member this.ResolveFunction(program: IFunctionProgram) =
            this.ResolveTemplate program

        member this.ResolveFBInstance(fbInstance: IFBInstance) =
            this.EnsureFBInstanceRuntime fbInstance
