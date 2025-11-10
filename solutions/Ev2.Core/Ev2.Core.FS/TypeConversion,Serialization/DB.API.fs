namespace Ev2.Core.FS

open System
open System.Collections.Generic
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
[<AutoOpen>]

module Ds2SqliteModule =

    open Db2DsImpl

    /// DB에서 새로 읽어온 객체의 필드 값을 기존 런타임 객체에 덮어쓴다. 부모/링크 참조는 유지한다.
    let private replicateIntoRuntime (src:Unique) (dst:Unique) =
        let rawParent = dst.RawParent
        let rtObject = dst.RtObject
        let njObject = dst.NjObject
        let ormObject = dst.ORMObject
        replicateProperties src dst |> ignore
        dst.RawParent <- rawParent
        dst.RtObject <- rtObject
        dst.NjObject <- njObject
        dst.ORMObject <- ormObject

    /// DB 커밋 후 재조회한 객체 트리의 변경점을 런타임 트리에 반영한다.
    let private syncRuntimeWithRefreshed (runtimeRoot:IRtUnique) (refreshedRoot:IRtUnique) (diffs:CompareResult array) =
        if diffs.Length = 0 then () else
            let refreshedRt = refreshedRoot :?> RtUnique
            let objects = Dictionary<Guid, Unique>()
            let inline addToMap (u:RtUnique) = objects[u.Guid] <- (u :> Unique)
            addToMap refreshedRt
            refreshedRt.EnumerateRtObjects()
            |> Seq.iter addToMap

            let processed = HashSet<Guid>()

            let rec applyRuntime (entity:IRtUnique) =
                let guid = entity.GetGuid()
                if processed.Add guid then
                    match objects.TryGetValue(guid) with
                    | true, srcUnique -> replicateIntoRuntime srcUnique (entity :?> Unique)
                    | _ -> ()

            applyRuntime runtimeRoot

            for diff in diffs do
                match diff with
                | Diff(_, _, newEntity, _) -> applyRuntime newEntity
                | RightOnly newEntity -> applyRuntime newEntity
                | _ -> ()

    type Project with // CheckoutFromDB, RTryCheckoutFromDB, RTryCommitToDB, RTryRemoveFromDB
        member x.RTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            x.DbApi <- Some dbApi
            x.Properties.Database <- dbApi.DbProvider

            let result =
                dbApi.With(fun (conn, tr) ->
                let dbProjs = conn.Query<ORMProject>($"SELECT * FROM {Tn.Project} WHERE id = @Id OR guid = @Guid", x, tr) |> toList
                match dbProjs with
                | [] ->
                    // 신규 project 삽입
                    rTryInsertProjectToDB x dbApi

                | [dbProj] when dbProj.Guid = x.Guid ->
                    // 이미 존재하는 프로젝트는 업데이트
                    Project.RTryCheckoutFromDB(dbProj.Id.Value, dbApi)
                    >>= (fun dbProject ->
                        let mutable diffs = dbProject.ComputeDiff(x) |> toArray

                        // 확장 속성 diff도 추가
                        getTypeFactory()
                        |> iter (fun factory ->
                            let extensionDiffs =
                                factory.ComputeExtensionDiff(dbProject, x)
                                |> Seq.cast<CompareResult>
                                |> toArray
                            if not (extensionDiffs.IsEmpty()) then
                                diffs <- Array.append diffs extensionDiffs )

                        if diffs.IsEmpty() then
                            Ok NoChange
                        else
                            x.rTryUpdateProjectToDB(dbApi, diffs)
                    )

                | [dbProj] ->
                    let msg = $"Project with Id {x.Id} already exists with a different Guid: {dbProj.Guid}. Cannot update."
                    logWarn "%s" msg
                    Error msg

                | _ ->
                    fail() )

            let inline tryApplyDiffToRuntime (diffs:CompareResult array) =
                match x.Id with
                | None -> ()
                | Some projId ->
                    match Project.RTryCheckoutFromDB(projId, dbApi) with
                    | Error err ->
                        logWarn "Failed to refresh project after commit: %s" err
                    | Ok refreshed ->
                        syncRuntimeWithRefreshed (x :> IRtUnique) (refreshed :> IRtUnique) diffs
                        x.DbApi <- Some dbApi

            match result with
            | Ok (Updated diffs as updated) ->
                tryApplyDiffToRuntime diffs
                Ok updated
            | other -> other

        member x.RTryRemoveFromDB(dbApi:AppDbApi): DbCommitResult =
            x.DbApi <- Some dbApi
            dbApi.With(fun (conn, tr) ->
                    rTryDo(fun () ->
                        let id = x.Id |? failwith "Project Id is not set"
                        let affectedRows = conn.Execute($"DELETE FROM {Tn.Project} WHERE id = @Id", {| Id = id |}, tr)
                        if affectedRows = 0 then
                            failwith $"Project with Id {id} not found"

                        Deleted)
                    ) |> Result.mapError _.Message

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi):DbCheckoutResult<Project> =
            rTryCheckoutProjectFromDB id dbApi

        static member RTryCheckoutFromDB(projectName:string, dbApi:AppDbApi):DbCheckoutResult<Project> =
            dbApi.With(fun (conn, tr) ->
                match conn.TryQuerySingle<int>($"SELECT id FROM {Tn.Project} WHERE name = @Name", {| Name = projectName |}, tr) with
                | Some id ->
                    Project.RTryCheckoutFromDB(id, dbApi)
                | None ->
                    Error $"Project not found: {projectName}" )

        static member CheckoutFromDB(projectName:string, dbApi:AppDbApi): Project = Project.RTryCheckoutFromDB(projectName, dbApi) |> Result.toObj
        static member CheckoutFromDB(id:Id, dbApi:AppDbApi): Project = Project.RTryCheckoutFromDB(id, dbApi) |> Result.toObj


    type DsSystem with // CheckoutFromDB, RTryCheckoutFromDB, RTryCommitToDB
        member x.RTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            let result = rTryCommitSystemToDB x dbApi
            match result with
            | Ok (Updated diffs as updated) ->
                match x.Id with
                | None -> Ok updated
                | Some systemId ->
                    match DsSystem.RTryCheckoutFromDB(systemId, dbApi) with
                    | Error err ->
                        logWarn "Failed to refresh system after commit: %s" err
                        Ok updated
                    | Ok refreshed ->
                        syncRuntimeWithRefreshed (x :> IRtUnique) (refreshed :> IRtUnique) diffs
                        Ok updated
            | other -> other

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi): DbCheckoutResult<DsSystem> =
            rTryCheckoutSystemFromDB id dbApi

        static member CheckoutFromDB(id:Id, dbApi:AppDbApi): DsSystem = DsSystem.RTryCheckoutFromDB(id, dbApi) |> Result.toObj
