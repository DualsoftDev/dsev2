namespace Ev2.Core.FS

open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
[<AutoOpen>]

module Ds2SqliteModule =

    open Db2DsImpl

    type RtProject with // CommitToDB, CheckoutFromDB
        member x.RTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            dbApi.With(fun (conn, tr) ->
                let dbProjs = conn.Query<ORMProject>($"SELECT * FROM {Tn.Project} WHERE id = @Id OR guid = @Guid", x, tr) |> toList
                match dbProjs with
                | [] ->
                    // 신규 project 삽입
                    rTryInsertProjectToDB x dbApi

                | [dbProj] when dbProj.Guid = x.Guid ->
                    // 이미 존재하는 프로젝트는 업데이트
                    RtProject.RTryCheckoutFromDB(dbProj.Id.Value, dbApi)
                    >>= (fun dbProject ->
                        let diffs = dbProject.ComputeDiff(x) |> toArray
                        if diffs.IsEmpty() then
                            Ok NoChange
                        else
                            x.rTryUpdateProjectToDB(dbApi, diffs)
                    )

                | [dbProj] ->
                    Error $"Project with Id {x.Id} already exists with a different Guid: {dbProj.Guid}. Cannot update."

                | _ ->
                    failwith "ERROR" )

        member x.RTryRemoveFromDB(dbApi:AppDbApi): DbCommitResult =
            dbApi.With(fun (conn, tr) ->
                    rTryDo(fun () ->
                        let id = x.Id |? failwith "Project Id is not set"
                        let affectedRows = conn.Execute($"DELETE FROM {Tn.Project} WHERE id = @Id", {| Id = id |}, tr)
                        if affectedRows = 0 then
                            failwith $"Project with Id {id} not found"

                        Deleted)
                    ) |> Result.mapError _.Message

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi):DbCheckoutResult<RtProject> =
            rTryCheckoutProjectFromDB id dbApi

        static member RTryCheckoutFromDB(projectName:string, dbApi:AppDbApi):DbCheckoutResult<RtProject> =
            dbApi.With(fun (conn, tr) ->
                match conn.TryQuerySingle<int>($"SELECT id FROM {Tn.Project} WHERE name = @Name", {| Name = projectName |}, tr) with
                | Some id ->
                    RtProject.RTryCheckoutFromDB(id, dbApi)
                | None ->
                    Error $"Project not found: {projectName}" )

        static member CheckoutFromDB(projectName:string, dbApi:AppDbApi): RtProject = RtProject.RTryCheckoutFromDB(projectName, dbApi) |> Result.toObj
        static member CheckoutFromDB(id:Id, dbApi:AppDbApi): RtProject = RtProject.RTryCheckoutFromDB(id, dbApi) |> Result.toObj


    type RtSystem with  // CommitToDB, CheckoutFromDB
        member x.RTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            rTryCommitSystemToDB x dbApi

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi): DbCheckoutResult<RtSystem> =
            rTryCheckoutSystemFromDB id dbApi

        static member CheckoutFromDB(id:Id, dbApi:AppDbApi): RtSystem = RtSystem.RTryCheckoutFromDB(id, dbApi) |> Result.toObj


