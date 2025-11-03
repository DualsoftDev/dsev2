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

    let isInputVar  (var:IVariable) = let vt = var.VarType in vt = VarType.VarInput || vt = VarType.VarInOut
    let isOutputVar (var:IVariable) = let vt = var.VarType in vt = VarType.VarOutput || vt = VarType.VarInOut
    let isInOutVar  (var:IVariable) = let vt = var.VarType in vt = VarType.VarInOut

    let updateDictionary (dictionary: Dictionary<string, obj>) (name: string) (value: obj) =
        if dictionary.ContainsKey name then
            dictionary[name] <- value
        else
            dictionary.Add(name, value)

    let tryGetInitValue (variable: IVariable) =
        match variable with
        | :? VarBase<_> as varBase -> varBase.InitValue |> Option.map box
        | _ -> None

/// FunctionProgram 실행 시 필요한 정적 정의 모음이다.
type internal FunctionDefinition =
    { Program: FunctionProgram
      Globals: IVariable list
      Parameters: IVariable list
      Locals: IVariable list
      Body: Statement array }

/// FBProgram 실행 시 필요한 정적 정의 모음이다.
type internal FBDefinition =
    { Program: FBProgram
      Inputs: IVariable list
      Outputs: IVariable list
      Internals: IVariable list
      Body: Statement array }

/// Function/FB 정의를 추출하는 유틸리티 모듈이다.
module private DefinitionBuilder =
    let private storageValues (storage: Storage) =
        storage.Values |> Seq.toList

    let private isLocalVariable (variable:IVariable) =
        match variable.VarType with
        | VarType.Var
        | VarType.VarConstant -> true
        | _ -> false

    let private isParameterVariable (variable:IVariable) =
        match variable.VarType with
        | VarType.VarInput
        | VarType.VarOutput
        | VarType.VarInOut -> true
        | _ -> false

    let buildFunctionDefinition (program: FunctionProgram) : FunctionDefinition =
        let locals = storageValues program.LocalStorage
        let globals = storageValues program.GlobalStorage
        { Program = program
          Globals = globals
          Parameters = locals |> List.filter isParameterVariable
          Locals = locals |> List.filter isLocalVariable
          Body = program.Rungs }

    let private isFBInput (variable:IVariable) =
        match variable.VarType with
        | VarType.VarInput
        | VarType.VarInOut -> true
        | _ -> false

    let private isFBOutput (variable:IVariable) =
        match variable.VarType with
        | VarType.VarOutput
        | VarType.VarInOut -> true
        | _ -> false

    let private isFBInternal (variable:IVariable) =
        match variable.VarType with
        | VarType.Var
        | VarType.VarConstant -> true
        | _ -> false

    let buildFBDefinition (program: FBProgram) : FBDefinition =
        let locals = storageValues program.LocalStorage
        { Program = program
          Inputs    = locals |> List.filter isFBInput
          Outputs   = locals |> List.filter isFBOutput
          Internals = locals |> List.filter isFBInternal
          Body = program.Rungs }

/// 런타임 호출 시 Resolver를 전달하는 컨테이너다.
type internal ExecutionScope =
    { Resolver: IRuntimeResolver }

/// Function 정의와 Resolver를 묶어 런타임 인스턴스를 제공한다.
and internal FunctionRuntimeTemplate(definition: FunctionDefinition, resolver: IRuntimeResolver) =
    member internal _.Definition = definition

    member this.CreateRuntime(
        inputMapping: Mapping,
        outputMapping: Mapping) : FunctionRuntime =
        FunctionRuntime.Create(definition, resolver, inputMapping, outputMapping)

    member this.Invoke(inputMapping: Mapping, outputMapping: Mapping) =
        let runtime = this.CreateRuntime(inputMapping, outputMapping)
        runtime.Do()

/// FB 인스턴스별 상태를 캡슐화한다.
and internal FBInstanceRuntime(definition: FBDefinition, resolver: IRuntimeResolver, state: Dictionary<string, obj>) =
    member internal _.Definition = definition
    member internal _.State = state

    member internal this.CreateCall(
        inputMapping: Mapping,
        outputMapping: Mapping) =
        FBInstanceRuntimeCall(definition, resolver, state, inputMapping, outputMapping)

    member this.Invoke(inputMapping: Mapping, outputMapping: Mapping) =
        this.CreateCall(inputMapping, outputMapping).Do()

/// Function/FB 호출을 실제 실행 객체로 해석한다.
and [<AllowNullLiteral>] internal IRuntimeResolver =
    abstract ResolveFunction : IFunctionProgram -> FunctionRuntimeTemplate
    abstract ResolveFBInstance : IFBInstance -> FBInstanceRuntime

/// Function 호출 한 건을 실행하는 로직을 담는다.
and internal FunctionRuntimeCall
    ( definition: FunctionDefinition,
      resolver: IRuntimeResolver,
      inputMapping: Mapping,
      outputMapping: Mapping) =

    let scope = { Resolver = resolver }

    let initialiseParameters () =
        definition.Parameters
        |> List.iter (fun variable ->
            if isInputVar variable then
                let source = ensureMapping inputMapping variable.Name
                variable.Value <- source.Value
            elif isOutputVar variable && not (isInOutVar variable) then
                let initial = unwrapInitValue (tryGetInitValue variable) variable.DataType
                variable.Value <- initial
            else
                ())

        definition.Parameters
        |> List.filter isInOutVar
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
        |> List.filter isOutputVar
        |> List.iter (fun variable ->
            let value = variable.Value
            match tryGetValue outputMapping variable.Name with
            | Some terminal ->
                terminal.Value <- value
            | None ->
                if isInOutVar variable then
                    let source = ensureMapping inputMapping variable.Name
                    source.Value <- value
                else
                    failwith $"출력 매핑에서 '{variable.Name}' 을(를) 찾을 수 없습니다.")

    member _.Do() =
        initialiseParameters ()
        initialiseLocals ()
        StatementExecutor.runFunction(definition, scope)
        flushOutputs ()

/// FunctionRuntimeCall을 감싸 단일 실행 API만 노출한다.
and FunctionRuntime private (call: FunctionRuntimeCall) =
    member _.Do() = call.Do()

    static member internal Create(
        definition: FunctionDefinition,
        resolver: IRuntimeResolver,
        inputMapping: Mapping,
        outputMapping: Mapping) =
        let call = FunctionRuntimeCall(definition, resolver, inputMapping, outputMapping)
        FunctionRuntime(call)

/// FB 정의와 Resolver를 묶어 인스턴스 상태를 생성한다.
and internal FBInstanceRuntimeTemplate(definition: FBDefinition, resolver: IRuntimeResolver) =
    member internal _.Definition = definition

    member internal _.CreateInstance() =
        let state = Dictionary<string, obj>(StringComparer.OrdinalIgnoreCase)
        FBInstanceRuntime(definition, resolver, state)

/// FB 호출 한 건을 수행하고 내부 상태를 유지한다.
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
        |> List.filter isInOutVar
        |> List.iter (fun variable ->
            let source =
                ensureMapping (if inputMapping.ContainsKey variable.Name then inputMapping else outputMapping) variable.Name
            variable.Value <- source.Value)

    let initialiseOutputs () =
        definition.Outputs
        |> List.filter (isInOutVar >> not)
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
            | None when isInOutVar variable ->
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

/// 참조 동일성 비교자를 제공한다.
and ReferenceEqualityComparer<'T when 'T : not struct>() =
    interface IEqualityComparer<'T> with
        member _.Equals(x, y) = obj.ReferenceEquals(x, y)
        member _.GetHashCode(x) = RuntimeHelpers.GetHashCode x

/// Statement 배열을 순차적으로 실행하는 실행기다.
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

    static member private buildMapping (decls: IVariable list) (expressions: IExpression[]) =
        if decls.Length <> expressions.Length then
            failwith $"매핑 개수가 일치하지 않습니다. 기대: {decls.Length}, 실제: {expressions.Length}"

        let dictionary = Dictionary<string, ITerminal>(StringComparer.OrdinalIgnoreCase)
        decls
        |> List.iteri (fun index variable ->
            let terminal = expressions[index] :?> ITerminal
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
            template.Definition.Parameters |> List.filter isInputVar
        let outputDecls =
            template.Definition.Parameters |> List.filter isOutputVar

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

/// 프로젝트 단위로 Function/FB 런타임을 생성하고 재사용한다.
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
