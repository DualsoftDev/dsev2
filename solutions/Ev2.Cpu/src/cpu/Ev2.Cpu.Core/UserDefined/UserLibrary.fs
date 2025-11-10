namespace Ev2.Cpu.Core.UserDefined

open System
open System.Collections.Concurrent
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// User Library - Central Registry for UserFC/FB
// ═════════════════════════════════════════════════════════════════════════════
// Core 도메인 모델에 대한 단일 진실 공급원(Single Source of Truth) 역할을 한다.
// 등록/조회/검증 API는 구조화된 오류(UserDefinitionError)를 반환한다.
// ═════════════════════════════════════════════════════════════════════════════

module private RegistryHelpers =
    let normalize (name: string) =
        if String.IsNullOrWhiteSpace name then "<empty>" else name

    let fcPath (name: string) segments =
        ["FC"; normalize name] @ segments

    let fbPath (name: string) segments =
        ["FB"; normalize name] @ segments

    let instancePath (name: string) segments =
        ["FBInstance"; normalize name] @ segments

/// <summary>
/// UserFC/FB 중앙 레지스트리 (Thread-safe)
/// User-defined Functions (FC) 및 Function Blocks (FB)를 등록하고 관리합니다.
/// 타입 검증, 의존성 분석, 순환 참조 검사 기능을 제공합니다.
/// </summary>
/// <remarks>
/// 이 클래스는 thread-safe하며, 여러 스레드에서 동시에 접근할 수 있습니다.
/// 모든 이름 비교는 대소문자를 구분하지 않습니다.
/// 네임스페이스를 지원하며 "MyLib.FunctionName" 형식의 qualified name을 사용할 수 있습니다.
/// </remarks>
type UserLibrary() =

    let fcRegistry = ConcurrentDictionary<string, UserFC>(StringComparer.OrdinalIgnoreCase)
    let fbRegistry = ConcurrentDictionary<string, UserFB>(StringComparer.OrdinalIgnoreCase)
    let instanceRegistry = ConcurrentDictionary<string, FBInstance>(StringComparer.OrdinalIgnoreCase)

    /// Keep full qualified name to prevent namespace collisions
    let resolveNamespace (fullName: string) =
        fullName

    // ────────────────────────────────
    // FC 관리
    // ────────────────────────────────

    /// <summary>User Function (FC)을 레지스트리에 등록합니다.</summary>
    /// <param name="fc">등록할 FC 정의</param>
    /// <returns>성공 시 Ok (), 실패 시 UserDefinitionError</returns>
    /// <remarks>
    /// FC 이름이 이미 등록되어 있거나 FB 이름과 충돌하는 경우 에러를 반환합니다.
    /// 등록 전에 FC의 유효성을 자동으로 검증합니다.
    /// </remarks>
    member this.RegisterFC(fc: UserFC) : Result<unit, UserDefinitionError> =
        match UserDefinitionValidation.validateUserFC fc with
        | Error err -> Error err
        | Ok () ->
            let name = resolveNamespace fc.Name
            // Check for conflict with FB registry
            if fbRegistry.ContainsKey(name) then
                UserDefinitionError.create "FC.Registry.NameConflict"
                    (sprintf "Name '%s' is already used by a Function Block." name)
                    (RegistryHelpers.fcPath name ["registry"])
                |> Error
            else
                // Replace or add the FC (allows hot-swap updates)
                fcRegistry.[name] <- fc
                Ok ()

    /// <summary>등록된 FC를 이름으로 조회합니다.</summary>
    /// <param name="name">FC 이름 (네임스페이스 포함 가능)</param>
    /// <returns>FC가 존재하면 Some fc, 없으면 None</returns>
    member this.GetFC(name: string) : UserFC option =
        let resolved = resolveNamespace name
        match fcRegistry.TryGetValue(resolved) with
        | true, fc -> Some fc
        | _ -> None

    /// <summary>FC가 등록되어 있는지 확인합니다.</summary>
    /// <param name="name">확인할 FC 이름</param>
    /// <returns>등록되어 있으면 true, 아니면 false</returns>
    member this.HasFC(name: string) =
        fcRegistry.ContainsKey(resolveNamespace name)

    /// <summary>등록된 모든 FC 목록을 가져옵니다.</summary>
    /// <returns>모든 FC의 리스트</returns>
    member this.GetAllFCs() : UserFC list =
        fcRegistry.Values |> Seq.toList

    /// <summary>등록된 FC를 제거합니다.</summary>
    /// <param name="name">제거할 FC 이름</param>
    /// <returns>제거 성공 여부</returns>
    member this.RemoveFC(name: string) =
        let resolved = resolveNamespace name
        match fcRegistry.TryRemove(resolved) with
        | true, _ -> true
        | _ -> false

    // ────────────────────────────────
    // FB 관리
    // ────────────────────────────────

    /// <summary>Function Block (FB)을 레지스트리에 등록합니다.</summary>
    /// <param name="fb">등록할 FB 정의</param>
    /// <returns>성공 시 Ok (), 실패 시 UserDefinitionError</returns>
    /// <remarks>
    /// FB 이름이 이미 등록되어 있거나 FC 이름과 충돌하는 경우 에러를 반환합니다.
    /// 등록 전에 FB의 유효성을 자동으로 검증합니다.
    /// </remarks>
    member this.RegisterFB(fb: UserFB) : Result<unit, UserDefinitionError> =
        match UserDefinitionValidation.validateUserFB fb with
        | Error err -> Error err
        | Ok () ->
            let name = resolveNamespace fb.Name
            // Check for conflict with FC registry
            if fcRegistry.ContainsKey(name) then
                UserDefinitionError.create "FB.Registry.NameConflict"
                    (sprintf "Name '%s' is already used by a Function." name)
                    (RegistryHelpers.fbPath name ["registry"])
                |> Error
            else
                // Replace or add the FB (allows hot-swap updates)
                fbRegistry.[name] <- fb
                Ok ()

    member this.GetFB(name: string) : UserFB option =
        let resolved = resolveNamespace name
        match fbRegistry.TryGetValue(resolved) with
        | true, fb -> Some fb
        | _ -> None

    member this.HasFB(name: string) =
        fbRegistry.ContainsKey(resolveNamespace name)

    member this.GetAllFBs() : UserFB list =
        fbRegistry.Values |> Seq.toList

    member this.RemoveFB(name: string) =
        let resolved = resolveNamespace name
        match fbRegistry.TryRemove(resolved) with
        | true, _ -> true
        | _ -> false

    // ────────────────────────────────
    // FB 인스턴스 관리
    // ────────────────────────────────

    member this.RegisterInstance(instance: FBInstance) : Result<unit, UserDefinitionError> =
        let instanceName = resolveNamespace instance.Name
        let fbName = resolveNamespace instance.FBType.Name
        if not (this.HasFB(fbName)) then
            UserDefinitionError.create "FBInstance.Registry.MissingFB"
                (sprintf "FB '%s' is not registered." fbName)
                (RegistryHelpers.instancePath instanceName ["register"])
            |> Error
        elif not (instanceRegistry.TryAdd(instanceName, instance)) then
            UserDefinitionError.create "FBInstance.Registry.Duplicate"
                (sprintf "FB instance '%s' is already registered." instanceName)
                (RegistryHelpers.instancePath instanceName ["register"])
            |> Error
        else
            Ok ()

    member this.GetInstance(name: string) : FBInstance option =
        let resolved = resolveNamespace name
        match instanceRegistry.TryGetValue(resolved) with
        | true, inst -> Some inst
        | _ -> None

    member this.HasInstance(name: string) =
        instanceRegistry.ContainsKey(resolveNamespace name)

    member this.GetAllInstances() : FBInstance list =
        instanceRegistry.Values |> Seq.toList

    member this.RemoveInstance(name: string) =
        let resolved = resolveNamespace name
        match instanceRegistry.TryRemove(resolved) with
        | true, _ -> true
        | _ -> false

    // ────────────────────────────────
    // 호출 검증
    // ────────────────────────────────

    member this.ValidateFCCall(fcName: string, argTypes: DsDataType list) : Result<DsDataType, UserDefinitionError> =
        let resolved = resolveNamespace fcName
        match this.GetFC(fcName) with
        | None ->
            UserDefinitionError.create "FC.Call.NotFound"
                (sprintf "FC '%s' is not registered." resolved)
                (RegistryHelpers.fcPath resolved ["call"])
            |> Error
        | Some fc ->
            let requiredInputs =
                fc.Inputs
                |> List.filter (fun p -> not p.IsOptional)
            if argTypes.Length < requiredInputs.Length then
                UserDefinitionError.create "FC.Call.MissingArguments"
                    (sprintf "FC '%s' requires at least %d arguments, but %d were provided."
                        resolved requiredInputs.Length argTypes.Length)
                    (RegistryHelpers.fcPath resolved ["call"; "arity"])
                |> Error
            elif argTypes.Length > fc.Inputs.Length then
                UserDefinitionError.create "FC.Call.TooManyArguments"
                    (sprintf "FC '%s' accepts at most %d arguments, but %d were provided."
                        resolved fc.Inputs.Length argTypes.Length)
                    (RegistryHelpers.fcPath resolved ["call"; "arity"])
                |> Error
            else
                let mismatched =
                    List.zip argTypes fc.Inputs
                    |> List.tryFind (fun (argType, param) ->
                        not (argType = param.DataType ||
                             (argType = DsDataType.TInt && param.DataType = DsDataType.TDouble)))

                match mismatched with
                | Some (argType, param) ->
                    UserDefinitionError.create "FC.Call.ArgumentTypeMismatch"
                        (sprintf "Parameter '%s' expects %O but received %O." param.Name param.DataType argType)
                        (RegistryHelpers.fcPath resolved ["call"; "param"; RegistryHelpers.normalize param.Name])
                    |> Error
                | None ->
                    Ok fc.ReturnType

    member this.ValidateFBCall(fbName: string, inputArgs: Map<string, DsDataType>) : Result<unit, UserDefinitionError> =
        let resolved = resolveNamespace fbName
        match this.GetFB(fbName) with
        | None ->
            UserDefinitionError.create "FB.Call.NotFound"
                (sprintf "FB '%s' is not registered." resolved)
                (RegistryHelpers.fbPath resolved ["call"])
            |> Error
        | Some fb ->
            let requiredInputs =
                fb.Inputs
                |> List.filter (fun p -> not p.IsOptional)
                |> List.map (fun p -> p.Name)
                |> Set.ofList

            let providedInputs = inputArgs |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            let missingInputs = Set.difference requiredInputs providedInputs

            if not (Set.isEmpty missingInputs) then
                UserDefinitionError.create "FB.Call.MissingInputs"
                    (sprintf "FB '%s' missing required inputs: %s"
                        resolved (String.concat ", " (missingInputs |> Set.toList)))
                    (RegistryHelpers.fbPath resolved ["call"; "inputs"])
                |> Error
            else
                let typeErrors =
                    inputArgs
                    |> Map.toList
                    |> List.choose (fun (name, argType) ->
                        match fb.Inputs |> List.tryFind (fun p -> p.Name = name) with
                        | None ->
                            UserDefinitionError.create "FB.Call.UnknownInput"
                                (sprintf "Input '%s' is not defined on FB '%s'." name resolved)
                                (RegistryHelpers.fbPath resolved ["call"; "inputs"; RegistryHelpers.normalize name])
                            |> Some
                        | Some param when argType = param.DataType ->
                            None
                        | Some param when argType = DsDataType.TInt && param.DataType = DsDataType.TDouble ->
                            None
                        | Some param ->
                            UserDefinitionError.create "FB.Call.ArgumentTypeMismatch"
                                (sprintf "Input '%s' expects %O but received %O."
                                    param.Name param.DataType argType)
                                (RegistryHelpers.fbPath resolved ["call"; "inputs"; RegistryHelpers.normalize name])
                            |> Some)

                match typeErrors with
                | [] -> Ok ()
                | error :: _ -> Error error

    // ────────────────────────────────
    // 의존성 분석
    // ────────────────────────────────

    member this.AnalyzeFCDependencies(fcName: string) : string list =
        match this.GetFC(fcName) with
        | None -> []
        | Some fc ->
            let rec collect expr =
                match expr with
                | UConst _ | UParam _ | UStatic _ | UTemp _ -> []
                | UUnary(_, e) -> collect e
                | UBinary(_, l, r) -> collect l @ collect r
                | UCall(_, args) -> args |> List.collect collect
                | UUserFCCall(name, args) -> name :: (args |> List.collect collect)
                | UConditional(c, t, f) -> collect c @ collect t @ collect f
            collect fc.Body |> List.distinct

    member this.GetDependencyGraph() : Map<string, string list> =
        this.GetAllFCs()
        |> List.map (fun fc -> fc.Name, this.AnalyzeFCDependencies(fc.Name))
        |> Map.ofList

    member this.DetectCircularDependencies() : (string * string list) list =
        let graph = this.GetDependencyGraph()

        let rec detectCycle visited path current =
            if Set.contains current path then
                Some (current :: (path |> Set.toList))
            elif Set.contains current visited then
                None
            else
                let newPath = Set.add current path
                let newVisited = Set.add current visited
                match Map.tryFind current graph with
                | None -> None
                | Some deps ->
                    deps |> List.tryPick (detectCycle newVisited newPath)

        graph
        |> Map.toList
        |> List.choose (fun (fc, _) ->
            match detectCycle Set.empty Set.empty fc with
            | Some cycle -> Some (fc, cycle)
            | None -> None)

    // ────────────────────────────────
    // 유틸리티
    // ────────────────────────────────

    member this.Clear() =
        fcRegistry.Clear()
        fbRegistry.Clear()
        instanceRegistry.Clear()

    member this.GetStatistics() =
        sprintf "UserLibrary: %d FCs, %d FBs, %d Instances"
            fcRegistry.Count fbRegistry.Count instanceRegistry.Count

    member this.GetAllNames() : string list =
        let fcNames = fcRegistry.Keys |> Seq.toList
        let fbNames = fbRegistry.Keys |> Seq.toList
        fcNames @ fbNames |> List.distinct |> List.sort

/// 전역 UserLibrary 인스턴스 (싱글톤)
module GlobalUserLibrary =

    let mutable private instance : UserLibrary option = None

    let getInstance () =
        match instance with
        | Some lib -> lib
        | None ->
            let lib = UserLibrary()
            instance <- Some lib
            lib

    let reset () =
        instance <- Some (UserLibrary())

    let clear () =
        getInstance().Clear()
