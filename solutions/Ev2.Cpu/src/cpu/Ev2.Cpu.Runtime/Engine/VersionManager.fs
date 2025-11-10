namespace Ev2.Cpu.Runtime

open System
open System.Collections.Concurrent
open Ev2.Cpu.Core
open Ev2.Cpu.Core.UserDefined

// ═════════════════════════════════════════════════════════════════════════════
// Version Manager - 런타임 스냅샷 및 버전 관리
// ═════════════════════════════════════════════════════════════════════════════
// 런타임 상태의 스냅샷을 생성하고 관리하여 롤백 기능 지원
// ═════════════════════════════════════════════════════════════════════════════

/// 버전 관리자
type VersionManager(maxHistory: int) =

    let history = ConcurrentStack<RuntimeSnapshot>()
    let mutable currentSnapshot = RuntimeSnapshot.Empty

    /// 현재 스냅샷 생성
    member _.CreateSnapshot
        (
            userLib: UserLibrary,
            programBody: DsStmt list option,
            ?description: string
        ) : RuntimeSnapshot =

        let fcMap =
            userLib.GetAllFCs()
            |> List.map (fun fc -> (fc.Name, fc))
            |> Map.ofList

        let fbMap =
            userLib.GetAllFBs()
            |> List.map (fun fb -> (fb.Name, fb))
            |> Map.ofList

        let instanceMap =
            userLib.GetAllInstances()
            |> List.map (fun inst -> (inst.Name, inst))
            |> Map.ofList

        {
            Timestamp = DateTime.UtcNow
            Description = description
            UserFCs = fcMap
            UserFBs = fbMap
            FBInstances = instanceMap
            ProgramBody = programBody
        }

    /// 스냅샷 저장
    member this.SaveSnapshot(snapshot: RuntimeSnapshot) =
        // MEDIUM FIX: Treat maxHistory = 0 as unlimited, not disabled
        // Only apply limit if maxHistory > 0; otherwise keep all snapshots
        if maxHistory > 0 then
            // Apply history size limit - remove oldest (bottom of stack)
            while history.Count >= maxHistory do
                let items = history.ToArray()  // Newest first (top of stack)
                if items.Length > 0 then
                    history.Clear()
                    // Keep all except the oldest (last in array)
                    // Push in reverse order to maintain chronological order
                    items
                    |> Array.take (items.Length - 1)
                    |> Array.rev  // Reverse to push oldest first
                    |> Array.iter history.Push
                else
                    () // Empty history, nothing to remove

        // Always push snapshot to history (even if maxHistory = 0)
        history.Push(snapshot)
        currentSnapshot <- snapshot

    /// 스냅샷 생성 및 저장
    member this.CreateAndSave
        (
            userLib: UserLibrary,
            programBody: DsStmt list option,
            ?description: string
        ) : RuntimeSnapshot =
        let snapshot = this.CreateSnapshot(userLib, programBody, ?description = description)
        this.SaveSnapshot(snapshot)
        snapshot

    /// 스냅샷 복원
    member this.RestoreSnapshot
        (
            snapshot: RuntimeSnapshot,
            userLib: UserLibrary,
            getProgramBody: (unit -> DsStmt list option) option,
            updateProgramBody: (DsStmt list -> unit) option
        ) : Result<unit, string> =
        try
            // Create backup before clearing - capture current program body
            let currentProgramBody =
                match getProgramBody with
                | Some getter -> getter ()
                | None -> None
            let backup = this.CreateSnapshot(userLib, currentProgramBody, "Backup before restore")

            // UserLibrary 완전 초기화
            userLib.Clear()

            // UserFC 복원
            let fcResults =
                snapshot.UserFCs
                |> Map.toList
                |> List.map (fun (name, fc) ->
                    match userLib.RegisterFC(fc) with
                    | Ok () -> Ok ()
                    | Error err -> Error (sprintf "Failed to restore UserFC '%s': %s" name (err.Format())))

            let fcError = fcResults |> List.tryPick (fun r -> match r with | Error e -> Some e | _ -> None)

            match fcError with
            | Some err ->
                // Restore from backup on failure
                userLib.Clear()
                backup.UserFCs |> Map.iter (fun _ fc -> userLib.RegisterFC(fc) |> ignore)
                backup.UserFBs |> Map.iter (fun _ fb -> userLib.RegisterFB(fb) |> ignore)
                backup.FBInstances |> Map.iter (fun _ inst -> userLib.RegisterInstance(inst) |> ignore)
                // Restore backup program body
                match backup.ProgramBody, updateProgramBody with
                | Some body, Some updateFn -> updateFn body
                | _ -> ()
                Error err
            | None ->

            // UserFB 복원
            let fbResults =
                snapshot.UserFBs
                |> Map.toList
                |> List.map (fun (name, fb) ->
                    match userLib.RegisterFB(fb) with
                    | Ok () -> Ok ()
                    | Error err -> Error (sprintf "Failed to restore UserFB '%s': %s" name (err.Format())))

            let fbError = fbResults |> List.tryPick (fun r -> match r with | Error e -> Some e | _ -> None)

            match fbError with
            | Some err ->
                // Restore from backup on failure
                userLib.Clear()
                backup.UserFCs |> Map.iter (fun _ fc -> userLib.RegisterFC(fc) |> ignore)
                backup.UserFBs |> Map.iter (fun _ fb -> userLib.RegisterFB(fb) |> ignore)
                backup.FBInstances |> Map.iter (fun _ inst -> userLib.RegisterInstance(inst) |> ignore)
                // Restore backup program body
                match backup.ProgramBody, updateProgramBody with
                | Some body, Some updateFn -> updateFn body
                | _ -> ()
                Error err
            | None ->

            // FB 인스턴스 복원
            let instResults =
                snapshot.FBInstances
                |> Map.toList
                |> List.map (fun (name, inst) ->
                    match userLib.RegisterInstance(inst) with
                    | Ok () -> Ok ()
                    | Error err -> Error (sprintf "Failed to restore FB instance '%s': %s" name (err.Format())))

            let instError = instResults |> List.tryPick (fun r -> match r with | Error e -> Some e | _ -> None)

            match instError with
            | Some err ->
                // Restore from backup on failure
                userLib.Clear()
                backup.UserFCs |> Map.iter (fun _ fc -> userLib.RegisterFC(fc) |> ignore)
                backup.UserFBs |> Map.iter (fun _ fb -> userLib.RegisterFB(fb) |> ignore)
                backup.FBInstances |> Map.iter (fun _ inst -> userLib.RegisterInstance(inst) |> ignore)
                // Restore backup program body
                match backup.ProgramBody, updateProgramBody with
                | Some body, Some updateFn -> updateFn body
                | _ -> ()
                Error err
            | None ->

            // Program.Body 복원 (콜백 제공된 경우)
            match snapshot.ProgramBody, updateProgramBody with
            | Some body, Some updateFn ->
                updateFn body
            | _ -> ()

            Ok ()

        with ex ->
            Error (sprintf "Snapshot restoration failed: %s" ex.Message)

    /// 최근 스냅샷 가져오기
    member _.GetLatestSnapshot() : RuntimeSnapshot option =
        let mutable snapshot = Unchecked.defaultof<RuntimeSnapshot>
        if history.TryPeek(&snapshot) then
            Some snapshot
        else
            None

    /// 스냅샷 히스토리 가져오기
    member _.GetHistory() : RuntimeSnapshot list =
        history.ToArray() |> Array.toList

    /// 현재 저장된 스냅샷 개수
    member _.Count = history.Count

    /// 히스토리 초기화
    member _.ClearHistory() =
        history.Clear()
        currentSnapshot <- RuntimeSnapshot.Empty

    /// 특정 시점의 스냅샷 가져오기
    member _.GetSnapshotAt(index: int) : RuntimeSnapshot option =
        let arr = history.ToArray()
        if index >= 0 && index < arr.Length then
            Some arr.[index]
        else
            None

    /// 특정 타임스탬프에 가장 가까운 스냅샷 찾기
    member _.FindClosestSnapshot(timestamp: DateTime) : RuntimeSnapshot option =
        history.ToArray()
        |> Array.sortBy (fun s -> abs (s.Timestamp - timestamp).Ticks)
        |> Array.tryHead

    /// 스냅샷 요약 정보 출력
    member _.GetSummary() : string =
        let snapshots = history.ToArray()
        if Array.isEmpty snapshots then
            "No snapshots available"
        else
            let summaries =
                snapshots
                |> Array.mapi (fun i s ->
                    sprintf "%d. %s" (i + 1) (s.Summary()))
                |> String.concat "\n"
            sprintf "Version History (%d snapshots):\n%s" snapshots.Length summaries

/// 버전 관리 모듈
module VersionManager =

    /// 기본 최대 히스토리 크기
    [<Literal>]
    let DefaultMaxHistory = 10

    /// 버전 관리자 생성
    let create maxHistory = VersionManager(maxHistory)

    /// 기본 버전 관리자 생성
    let createDefault () = create DefaultMaxHistory

    /// 스냅샷 생성
    let createSnapshot (mgr: VersionManager) userLib programBody description =
        mgr.CreateSnapshot(userLib, programBody, description)

    /// 스냅샷 저장
    let saveSnapshot (mgr: VersionManager) snapshot =
        mgr.SaveSnapshot(snapshot)

    /// 스냅샷 생성 및 저장
    let createAndSave (mgr: VersionManager) userLib programBody description =
        mgr.CreateAndSave(userLib, programBody, description)

    /// 스냅샷 복원
    let restoreSnapshot (mgr: VersionManager) snapshot userLib getBodyFn updateBodyFn =
        mgr.RestoreSnapshot(snapshot, userLib, getBodyFn, updateBodyFn)

    /// 최근 스냅샷 가져오기
    let getLatest (mgr: VersionManager) =
        mgr.GetLatestSnapshot()

    /// 히스토리 가져오기
    let getHistory (mgr: VersionManager) =
        mgr.GetHistory()

    /// 요약 정보
    let getSummary (mgr: VersionManager) =
        mgr.GetSummary()
