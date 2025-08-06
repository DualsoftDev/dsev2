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
            | :? Project -> Tn.Project
            | :? DsSystem  -> Tn.System
            | :? Flow    -> Tn.Flow
            | :? DsButton  -> Tn.Button
            | :? Lamp    -> Tn.Lamp
            | :? DsCondition -> Tn.Condition
            | :? DsAction -> Tn.Action
            | :? ApiDef -> Tn.ApiDef
            | :? ApiCall -> Tn.ApiCall
            | :? Work -> Tn.Work
            | :? Call -> Tn.Call
            | :? ArrowBetweenCalls -> Tn.ArrowCall
            | :? ArrowBetweenWorks -> Tn.ArrowWork
            | _ -> failwith $"Unknown RtUnique type: {x.GetType().Name}"

        member x.rTryUpdateProjectToDB (dbApi:AppDbApi, diffs:CompareResult []): DbCommitResult =
            assert (!! diffs.IsNullOrEmpty())
            assert (x :? Project || x :? DsSystem)
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
                    // 확장 처리 훅
                    ExtensionDbHandler |> Option.iter (fun h -> h.HandleAfterUpdate(x, conn, tr))
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
                    newEntity.InsertToDB(dbApi)
                    Ok Inserted

                | _ ->
                    failwith $"ERROR: Unknown CompareResult type: {x.GetType().Name}"
            )

