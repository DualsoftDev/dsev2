namespace Ev2.Cpu.Runtime

open System
open System.Collections.Concurrent
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined

/// RuntimeUpdateManager - 런타임 중 코드 및 상태 업데이트 관리
type RuntimeUpdateManager
    (
        ctx: Ev2.Cpu.Runtime.ExecutionContext,
        userLib: UserLibrary,
        config: UpdateConfig option
    ) =

    let cfg = defaultArg config UpdateConfig.Default
    let updateQueue = ConcurrentQueue<UpdateRequest>()
    let versionMgr = VersionManager(cfg.MaxSnapshotHistory)
    let mutable programBody : DsStmt list option = None

    // CRITICAL FIX (DEFECT-CRIT-7): Make programBodyUpdated volatile for thread safety
    // Previous code: mutable field without thread synchronization
    // Problem: Reader thread may see stale value due to CPU cache, missing updates
    // Solution: Use System.Threading.Volatile operations for visibility guarantee
    [<VolatileField>]
    let mutable programBodyUpdated = false  // 업데이트 발생 여부 플래그

    // CRITICAL FIX (DEFECT-CRIT-5): Add lock for atomic batch snapshot capture
    // Previous code: UserLibrary and Memory snapshots captured separately (race window)
    // Problem: Concurrent update between two snapshot calls breaks atomicity
    // Solution: Lock both captures to ensure consistent snapshot pair
    let snapshotLock = obj()

    // Statistics tracking
    let mutable totalRequests = 0
    let mutable successCount = 0
    let mutable failedCount = 0
    let mutable rolledBackCount = 0
    let mutable lastUpdateTime : DateTime option = None

    // Event log
    let eventLog = ConcurrentQueue<UpdateEvent>()

    let logEvent event =
        eventLog.Enqueue(event)

    /// Validate a UserFC
    let validateFC (fc: UserFC) : Result<unit, UserDefinitionError list> =
        match fc.Validate() with
        | Ok () -> Ok ()
        | Error err -> Error [err]

    /// Validate a UserFB
    let validateFB (fb: UserFB) : Result<unit, UserDefinitionError list> =
        match fb.Validate() with
        | Ok () -> Ok ()
        | Error err -> Error [err]

    /// Reconcile FB instance StateStorage when FB definition changes
    /// Migrates compatible values from old storage, initializes new statics, drops removed ones
    let reconcileStateStorage (oldInstance: FBInstance) (newFB: UserFB) : Map<string, obj> option =
        match oldInstance.StateStorage with
        | None ->
            // Instance wasn't initialized - create fresh state from NEW FB definition
            let newStorage =
                newFB.Statics
                |> List.map (fun (name, dataType, initValue) ->
                    let value =
                        match initValue with
                        | Some v -> v
                        | None -> TypeHelpers.getDefaultValue dataType
                    (name, value))
                |> Map.ofList
            Some newStorage
        | Some oldStorage ->
            // Create fresh storage for new FB definition
            let newStorage =
                newFB.Statics
                |> List.map (fun (name, dataType, initValue) ->
                    // Try to migrate value from old storage if it exists
                    match Map.tryFind name oldStorage with
                    | Some existingValue ->
                        // Variable exists in old storage - validate type before reusing
                        let expectedType = dataType

                        // CRITICAL FIX (DEFECT-017-1): Check for Nullable<T> before rejecting null
                        // Nullable<T> is a value type but allows null - detect generic Nullable
                        let isNullable =
                            expectedType.IsGenericType &&
                            expectedType.GetGenericTypeDefinition() = typedefof<System.Nullable<_>>

                        if existingValue = null && expectedType.IsValueType && not isNullable then
                            // null for non-nullable value type - use default instead
                            let value =
                                match initValue with
                                | Some v -> v
                                | None -> TypeHelpers.getDefaultValue dataType
                            (name, value)
                        else
                            // For non-null or reference types, validate type compatibility
                            let actualType = if existingValue = null then expectedType else existingValue.GetType()

                            if expectedType.IsAssignableFrom(actualType) || actualType = expectedType then
                                // Type compatible - reuse existing value (including null for reference types)
                                (name, existingValue)
                            else
                            // Type mismatch - use init value or default instead of corrupting state
                            let value =
                                match initValue with
                                | Some v -> v
                                | None -> TypeHelpers.getDefaultValue dataType
                            (name, value)
                    | None ->
                        // New static variable - use init value or default
                        let value =
                            match initValue with
                            | Some v -> v
                            | None -> TypeHelpers.getDefaultValue dataType
                        (name, value))
                |> Map.ofList
            Some newStorage

    /// Validate a single update request (recursive, with context of pending batch)
    let rec validateRequestWithContext (pendingFBs: Set<string>) (pendingFCs: Set<string>) (request: UpdateRequest) : Result<unit, UserDefinitionError list> =
        match request with
        | UpdateRequest.UpdateUserFC (fc, validate) ->
            // Force validation if config requires it, or if request specifies it
            if validate || cfg.ForceValidation then validateFC fc else Ok ()

        | UpdateRequest.UpdateUserFB (fb, validate) ->
            // Force validation if config requires it, or if request specifies it
            if validate || cfg.ForceValidation then validateFB fb else Ok ()

        | UpdateRequest.UpdateFBInstance (instance, validate) ->
            // Force validation if config requires it, or if request specifies it
            if validate || cfg.ForceValidation then
                // Basic validation - check if FB type exists OR is being defined in this batch
                if userLib.HasFB(instance.FBType.Name) || Set.contains instance.FBType.Name pendingFBs then Ok ()
                else
                    let err = UserDefinitionError.create "FBInstance.Validation.MissingType"
                                (sprintf "FB type '%s' not found in registry" instance.FBType.Name)
                                [instance.Name]
                    Error [err]
            else Ok ()

        | UpdateRequest.UpdateProgramBody (body, validate) ->
            // Force validation if config requires it, or if request specifies it
            if validate || cfg.ForceValidation then
                // Basic validation: ensure program body is not malformed
                if List.isEmpty body then
                    let err = UserDefinitionError.create "Program.Body.Empty"
                                "Program body cannot be empty when validation is required"
                                ["program"; "body"]
                    Error [err]
                else
                    Ok ()
            else
                Ok ()

        | UpdateRequest.UpdateMemoryValue (name, value) ->
            // MAJOR FIX: Validate variable name has valid memory domain prefix OR exists in memory
            // Valid domains: I: (Input), O: (Output), L: (Local), V: (Internal/Retain)
            if name.Length >= 2 && name.[1] = ':' then
                // Has domain prefix - validate it's a valid domain
                let prefix = name.[0]
                if prefix = 'I' || prefix = 'O' || prefix = 'L' || prefix = 'V' then
                    Ok ()  // Valid domain prefix
                else
                    let err = UserDefinitionError.create "Memory.Validation.InvalidDomain"
                                (sprintf "Invalid memory domain prefix '%c:' in variable '%s'. Valid domains: I:, O:, L:, V:" prefix name)
                                [name]
                    Error [err]
            else
                // No domain prefix - accept (Memory.Set will handle it)
                // This allows updating existing variables without domain prefix
                Ok ()

        | UpdateRequest.BatchUpdate requests ->
            // Extract FB/FC names being defined in this batch
            let extractPendingFBs reqs =
                reqs
                |> List.choose (fun r ->
                    match r with
                    | UpdateRequest.UpdateUserFB (fb, _) -> Some fb.Name
                    | _ -> None)
                |> Set.ofList

            let extractPendingFCs reqs =
                reqs
                |> List.choose (fun r ->
                    match r with
                    | UpdateRequest.UpdateUserFC (fc, _) -> Some fc.Name
                    | _ -> None)
                |> Set.ofList

            let pendingFBs' = Set.union pendingFBs (extractPendingFBs requests)
            let pendingFCs' = Set.union pendingFCs (extractPendingFCs requests)

            // Validate all sub-requests with updated context
            requests
            |> List.map (validateRequestWithContext pendingFBs' pendingFCs')
            |> List.fold (fun acc r ->
                match acc, r with
                | Ok (), Ok () -> Ok ()
                | Error errs, Ok () -> Error errs
                | Ok (), Error errs -> Error errs
                | Error errs1, Error errs2 -> Error (errs1 @ errs2)
            ) (Ok ())

    /// Validate a single update request (public wrapper)
    let rec validateRequest (request: UpdateRequest) : Result<unit, UserDefinitionError list> =
        validateRequestWithContext Set.empty Set.empty request

    /// Apply a validated update request (recursive)
    and applyRequest (request: UpdateRequest) : Result<string, string> =
        try
            match request with
            | UpdateRequest.UpdateUserFC (fc, _) ->
                // Register new FC first, then remove old one only if successful
                match userLib.RegisterFC(fc) with
                | Ok () ->
                    // Registration succeeded - if there was an old FC, it's now replaced
                    Ok (sprintf "UserFC '%s' registered successfully" fc.Name)
                | Error err ->
                    // Registration failed - old FC (if any) remains intact
                    Error (err.Format())

            | UpdateRequest.UpdateUserFB (fb, _) ->
                // Save old FB definition before updating (if it exists)
                let oldFB = userLib.GetFB(fb.Name)

                // Register new FB first - if it fails, old FB remains intact
                match userLib.RegisterFB(fb) with
                | Ok () ->
                    // Update all existing instances to use the new FB definition
                    let instances = userLib.GetAllInstances()
                    let affectedInstances =
                        instances
                        |> List.filter (fun inst -> inst.FBType.Name = fb.Name)

                    // Save original instances for potential rollback
                    let originalInstances = affectedInstances |> List.map (fun inst -> (inst.Name, inst)) |> Map.ofList

                    // Try to update all instances, collect any errors
                    let updateErrors =
                        affectedInstances
                        |> List.choose (fun instance ->
                            // Reconcile StateStorage to match new FB definition
                            let reconciledStorage = reconcileStateStorage instance fb
                            let updatedInstance = { instance with FBType = fb; StateStorage = reconciledStorage }
                            // Remove old instance temporarily
                            let wasRemoved = userLib.RemoveInstance(instance.Name)
                            // Try to register the new instance
                            match userLib.RegisterInstance(updatedInstance) with
                            | Ok () ->
                                // Registration succeeded
                                None
                            | Error err ->
                                // Registration failed, restore old instance
                                if wasRemoved then
                                    userLib.RegisterInstance(instance) |> ignore
                                Some (sprintf "Instance '%s': %s" instance.Name (err.Format())))

                    if updateErrors.IsEmpty then
                        Ok (sprintf "UserFB '%s' registered and %d instance(s) updated" fb.Name affectedInstances.Length)
                    else
                        // Instance migration failed - rollback FB to old definition AND restore all instances
                        match oldFB with
                        | Some oldDefinition -> userLib.RegisterFB(oldDefinition) |> ignore
                        | None -> userLib.RemoveFB(fb.Name) |> ignore

                        // Restore ALL instances to their original state (not just failed ones)
                        affectedInstances
                        |> List.iter (fun inst ->
                            userLib.RemoveInstance(inst.Name) |> ignore
                            match Map.tryFind inst.Name originalInstances with
                            | Some originalInst -> userLib.RegisterInstance(originalInst) |> ignore
                            | None -> ())

                        Error (sprintf "UserFB '%s' update failed - %d instance(s) could not migrate: %s. Rolled back to previous definition."
                                fb.Name updateErrors.Length (String.concat "; " updateErrors))
                | Error err ->
                    // Registration failed - old FB (if any) remains intact
                    Error (err.Format())

            | UpdateRequest.UpdateFBInstance (instance, _) ->
                // Save old instance if exists
                let oldInstance = userLib.GetInstance(instance.Name)

                // Remove existing instance if present
                if userLib.HasInstance(instance.Name) then
                    userLib.RemoveInstance(instance.Name) |> ignore

                // Refresh FBType from registry to ensure we use canonical version
                match userLib.GetFB(instance.FBType.Name) with
                | Some canonicalFB ->
                    let refreshedInstance = { instance with FBType = canonicalFB }
                    match userLib.RegisterInstance(refreshedInstance) with
                    | Ok () -> Ok (sprintf "FB instance '%s' updated with canonical FB type" instance.Name)
                    | Error err ->
                        // Restore old instance on failure
                        match oldInstance with
                        | Some old -> userLib.RegisterInstance(old) |> ignore
                        | None -> ()
                        Error (err.Format())
                | None ->
                    // FB type doesn't exist in registry - try to register as-is
                    match userLib.RegisterInstance(instance) with
                    | Ok () -> Ok (sprintf "FB instance '%s' updated (FB type not in registry)" instance.Name)
                    | Error err ->
                        // Restore old instance on failure
                        match oldInstance with
                        | Some old -> userLib.RegisterInstance(old) |> ignore
                        | None -> ()
                        Error (err.Format())

            | UpdateRequest.UpdateProgramBody (body, _) ->
                programBody <- Some body
                programBodyUpdated <- true  // 업데이트 발생 플래그 설정
                Ok "Program body updated successfully"

            | UpdateRequest.UpdateMemoryValue (name, value) ->
                // MAJOR FIX: Use SetForced to allow input domain (I:) writes for simulation/diagnostics
                // Strip area prefix (I:/O:/L:/V:) before setting value
                // CRITICAL FIX (DEFECT-020-9): Case-insensitive prefix matching
                // Previous code rejected lowercase addresses (i:Sensor), raising VariableNotDeclared
                let actualName =
                    if name.StartsWith("I:", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("O:", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("L:", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("V:", StringComparison.OrdinalIgnoreCase) then
                        name.Substring(2)  // Remove "X:" prefix
                    else
                        name
                // Use SetForced to bypass writability check (allows I: domain writes)
                ctx.Memory.SetForced(actualName, value)
                Ok (sprintf "Memory variable '%s' updated" actualName)

            | UpdateRequest.BatchUpdate requests ->
                // Validation was already done in processRequest with proper pending-FB context
                // Re-validating here would fail for instances whose FB is defined earlier in the batch

                // CRITICAL FIX (DEFECT-CRIT-5): Atomically capture UserLibrary + Memory snapshots
                // Previous code: Separate captures (race window between versionMgr and ctx calls)
                // Solution: Lock ensures both snapshots are consistent snapshot pair
                let (batchSnapshot, memorySnapshot, errorLogSnapshot) =
                    lock snapshotLock (fun () ->
                        let batchSnap = versionMgr.CreateSnapshot(userLib, programBody, "Batch update snapshot")
                        let memSnap = ctx.CreateSnapshot()  // Capture memory state
                        let errorSnap = ctx.ErrorLog.CreateSnapshot()  // MEDIUM FIX (DEFECT-018-7): Capture error log state
                        (batchSnap, memSnap, errorSnap))

                // MAJOR FIX: Reorder batch to respect FC→FB→Instance dependency order
                // 1. UpdateUserFC (function definitions) - FBs may depend on FCs
                // 2. UpdateUserFB (function block definitions) - instances depend on FBs
                // 3. UpdateFBInstance (create instances) - depends on FBs being registered
                // 4. UpdateProgramBody/UpdateMemoryValue (everything else)
                let fcDefinitions = requests |> List.filter (function
                    | UpdateRequest.UpdateUserFC _ -> true
                    | _ -> false)
                let fbDefinitions = requests |> List.filter (function
                    | UpdateRequest.UpdateUserFB _ -> true
                    | _ -> false)
                let instanceCreations = requests |> List.filter (function
                    | UpdateRequest.UpdateFBInstance _ -> true
                    | _ -> false)
                let otherRequests = requests |> List.filter (function
                    | UpdateRequest.UpdateUserFC _ | UpdateRequest.UpdateUserFB _ | UpdateRequest.UpdateFBInstance _ -> false
                    | _ -> true)
                let orderedRequests = fcDefinitions @ fbDefinitions @ instanceCreations @ otherRequests

                // Apply all requests in dependency order
                let results = orderedRequests |> List.map applyRequest
                let errors = results |> List.choose (fun r -> match r with | Error e -> Some e | _ -> None)

                if errors.IsEmpty then
                    Ok (sprintf "Batch update completed: %d items" requests.Length)
                else
                    // CRITICAL FIX: Rollback both user library AND memory (batch updates are atomic)
                    let getProgramBody () = programBody
                    let updateProgramBody (body: DsStmt list) =
                        programBody <- Some body
                        programBodyUpdated <- false  // Clear flag on rollback
                    // Rollback user library
                    let libRollbackResult = versionMgr.RestoreSnapshot(batchSnapshot, userLib, Some getProgramBody, Some updateProgramBody)
                    // Rollback memory
                    ctx.Rollback(memorySnapshot)
                    // MEDIUM FIX (DEFECT-018-7): Rollback error log to remove stale errors from failed updates
                    // Without this, errors from failed updates remain visible in ctx.GetErrors() after rollback
                    ctx.ErrorLog.RestoreSnapshot(errorLogSnapshot)
                    match libRollbackResult with
                    | Ok () ->
                        Error (String.concat "; " errors)
                    | Error rbError ->
                        Error (sprintf "Batch update failed AND rollback failed: %s. Original errors: %s"
                                rbError (String.concat "; " errors))
        with ex ->
            Error (sprintf "Exception during apply: %s" ex.Message)

    /// Process a single update request
    let processRequest (request: UpdateRequest) : UpdateResult =
        let now = DateTime.Now
        totalRequests <- totalRequests + 1
        logEvent (UpdateEvent.Requested (request, now))

        // Validation phase (before snapshot to avoid bloating history)
        logEvent (UpdateEvent.ValidationStarted (request, DateTime.Now))
        let validationResult = validateRequest request
        logEvent (UpdateEvent.ValidationCompleted (request, validationResult, DateTime.Now))

        match validationResult with
        | Error errors when errors.Length > 0 ->
            failedCount <- failedCount + 1
            UpdateResult.ValidationFailed errors

        | _ ->
            // Create snapshot AFTER validation passes (if auto-rollback is enabled)
            let snapshot =
                if cfg.AutoRollback then
                    Some (versionMgr.CreateAndSave(userLib, programBody, "Auto-snapshot before update"))
                else
                    None

            // Apply phase
            logEvent (UpdateEvent.ApplyStarted (request, DateTime.Now))
            match applyRequest request with
            | Ok message ->
                let result = UpdateResult.Success message
                successCount <- successCount + 1
                lastUpdateTime <- Some DateTime.Now
                logEvent (UpdateEvent.ApplyCompleted (request, result, DateTime.Now))
                result

            | Error error ->
                failedCount <- failedCount + 1
                lastUpdateTime <- Some DateTime.Now
                let failedResult = UpdateResult.ApplyFailed error

                // Rollback if configured
                if cfg.AutoRollback && snapshot.IsSome then
                    logEvent (UpdateEvent.RollbackStarted ("Auto-rollback after failure", DateTime.Now))

                    let getProgramBody () = programBody
                    let updateProgramBody (body: DsStmt list) =
                        programBody <- Some body
                        programBodyUpdated <- false  // Clear flag on rollback

                    match versionMgr.RestoreSnapshot(snapshot.Value, userLib, Some getProgramBody, Some updateProgramBody) with
                    | Ok () ->
                        rolledBackCount <- rolledBackCount + 1
                        logEvent (UpdateEvent.RollbackCompleted (true, DateTime.Now))
                        UpdateResult.RolledBack ("Update failed", error)
                    | Error rbError ->
                        logEvent (UpdateEvent.RollbackCompleted (false, DateTime.Now))
                        // MEDIUM FIX (DEFECT-019-10): Return ApplyFailed when rollback fails
                        // Previous code returned RolledBack, hiding inconsistent state
                        // System now in inconsistent state - caller must be informed (RuntimeSpec.md:113-118)
                        UpdateResult.ApplyFailed (sprintf "Inconsistent state: Apply failed (%s) and rollback also failed (%s)" error rbError)
                else
                    failedResult

    member _.EnqueueUpdate(request: UpdateRequest) =
        updateQueue.Enqueue(request)

    member _.ProcessPendingUpdates() : UpdateResult list =
        let mutable results = []
        let mutable request = Unchecked.defaultof<UpdateRequest>

        while updateQueue.TryDequeue(&request) do
            let result = processRequest request
            results <- result :: results

        List.rev results

    /// Program.Body 업데이트를 가져옴 (업데이트가 있었을 때만 반환)
    member _.GetProgramBody() : DsStmt list option =
        if programBodyUpdated then
            programBodyUpdated <- false  // 플래그 리셋
            programBody
        else
            None  // 업데이트가 없으면 None 반환

    member _.SetProgramBody(body: DsStmt list) =
        programBody <- Some body

    member _.GetStatistics() : UpdateStatistics =
        { TotalRequests = totalRequests
          SuccessCount = successCount
          FailedCount = failedCount
          RolledBackCount = rolledBackCount
          LastUpdateTime = lastUpdateTime }

    member _.GetEventLog() : UpdateEvent list =
        eventLog.ToArray() |> Array.toList

    member _.VersionManager = versionMgr

    member _.Config = cfg

    member _.Rollback() : Result<unit, string> =
        match versionMgr.GetLatestSnapshot() with
        | Some snapshot ->
            let getProgramBody () = programBody
            let updateProgramBody (body: DsStmt list) =
                programBody <- Some body
            versionMgr.RestoreSnapshot(snapshot, userLib, Some getProgramBody, Some updateProgramBody)
        | None ->
            Error "No snapshot available for rollback"
