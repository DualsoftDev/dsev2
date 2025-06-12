namespace Ev2.Core.FS

open System
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS

[<AutoOpen>]
module internal rec DbUpdateImpl =

    type IRtUnique with
        member x.getTableName() =
            match x with
            | :? RtProject -> Tn.Project
            | :? RtSystem  -> Tn.System
            | :? RtFlow    -> Tn.Flow
            | :? RtButton  -> Tn.Button
            | :? RtLamp    -> Tn.Lamp
            | :? RtCondition -> Tn.Condition
            | :? RtAction -> Tn.Action
            | :? RtApiDef -> Tn.ApiDef
            | :? RtApiCall -> Tn.ApiCall
            | :? RtWork -> Tn.Work
            | :? RtCall -> Tn.Call
            | :? RtArrowBetweenCalls -> Tn.ArrowCall
            | :? RtArrowBetweenWorks -> Tn.ArrowWork
            | _ -> failwith $"Unknown RtUnique type: {x.GetType().Name}"

        member x.rTryUpdateProjectToDB (dbApi:AppDbApi, diffs:CompareResult []): DbCommitResult =
            assert (!! diffs.IsNullOrEmpty())
            assert (x :? RtProject || x :? RtSystem)
            dbApi.With(fun (conn, tr) ->
                let firstError =
                    seq {
                        for d in diffs do
                            d.rTryCommitToDB dbApi
                    } |> Result.chooseError
                    |> tryHead
                match firstError with
                | Some e ->
                    Error e
                | None ->
                    Ok (Updated diffs)
            )


    type CompareResult with
        [<Obsolete("구현 중..")>]
        member x.rTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            dbApi.With(fun (conn, tr) ->
                match x with
                // DB 수정
                | Diff (cat, dbEntity, newEntity) ->
                    let dbColumnName = tryGetDBColumnName(dbApi, cat) |?? (fun () -> failwith $"Unknown property name: {cat}")
                    let propertyName = getPropertyNameForDB(dbApi, cat)
                    assert(dbEntity.GetType() = newEntity.GetType())
                    let tableName = dbEntity.getTableName()
                    let sql = $"UPDATE {tableName} SET {dbColumnName}=@{propertyName} WHERE id=@Id"
                    let count = conn.Execute(sql, newEntity, tr)
                    verify(count > 0 )
                    Ok (Updated [|x|])

                // DB 삭제
                | LeftOnly dbEntity ->
                    conn.Execute($"DELETE FROM {dbEntity.getTableName()} WHERE id=@Id", dbEntity, tr) |> ignore
                    Ok Deleted

                // DB 삽입
                | RightOnly newEntity ->
                    Error "Not yet!"

                | _ ->
                    failwith $"ERROR: Unknown CompareResult type: {x.GetType().Name}"
            )

