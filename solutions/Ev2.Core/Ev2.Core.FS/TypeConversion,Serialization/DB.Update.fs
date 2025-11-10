namespace Ev2.Core.FS

open Dapper

open Dual.Common.Core.FS
open System.Diagnostics
open Dual.Common.Base
open System

[<AutoOpen>]
module internal rec DbUpdateImpl =
    type IRtUnique with // rTryUpdateProjectToDB
        /// Runtime 객체의 변경된 부분(diff result) 만 DB에 반영
        member x.rTryUpdateProjectToDB (dbApi:AppDbApi, diffs:CompareResult []): DbCommitResult =
            assert (diffs.any())
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
                    // 확장 처리 훅 - 확장 속성이 변경된 경우에도 UPDATE 수행
                    getTypeFactory() |> iter (fun factory -> factory.HandleAfterUpdate(x, conn, tr))
                    Ok (Updated diffs)
            )


    type CompareResult with // rTryCommitToDB
        /// CompareResult 객체(diff 결과물)를 DB에 반영
        member x.rTryCommitToDB(dbApi:AppDbApi): DbCommitResult =
            dbApi.With(fun (conn, tr) ->
                match x with
                // DB 수정
                | Diff (cat, dbEntity, newEntity, (updateSql, parameter)) ->
                    let dbColumnName = cat
                    let propertyName = getPropertyNameForDB(dbApi, cat)
                    assert(dbEntity.GetType() = newEntity.GetType())
                    let sql = updateSql |> Option.ofObj |?? (fun () -> $"UPDATE {dbEntity.getTableName()} SET {dbColumnName}=@{propertyName} WHERE id=@Id")
                    let parameter = parameter |> Option.ofObj |? newEntity
                    let count = conn.Execute(sql, parameter, tr)
                    if count <= 0 then
                        let tableName = dbEntity.getTableName()
                        let entityId = dbEntity |> tryCast<Unique> >>= _.Id
                        let debugLine =
                            match parameter with
                            | :? Project as p -> sprintf "project properties=%s id=%A" p.PropertiesJson p.Id
                            | :? DsSystem as s -> sprintf "system properties=%s id=%A" s.PropertiesJson s.Id
                            | :? Flow as f -> sprintf "flow properties=%s id=%A" f.PropertiesJson f.Id
                            | :? Work as w -> sprintf "work properties=%s id=%A" w.PropertiesJson w.Id
                            | :? Call as c -> sprintf "call properties=%s id=%A" c.PropertiesJson c.Id
                            | :? ApiCall as ac -> sprintf "apiCall properties=%s id=%A" ac.PropertiesJson ac.Id
                            | :? ApiDef as ad -> sprintf "apiDef properties=%s id=%A" ad.PropertiesJson ad.Id
                            | _ -> sprintf "parameter=%A" parameter
                        try System.IO.File.AppendAllText("/tmp/ds_update_debug.log", debugLine + Environment.NewLine) with _ -> ()
                        match parameter with
                        | :? Project as p -> printfn "[DB.Update] project diff failure. properties=%s id=%A" p.PropertiesJson p.Id
                        | :? DsSystem as s -> printfn "[DB.Update] system diff failure. properties=%s id=%A" s.PropertiesJson s.Id
                        | :? Flow as f -> printfn "[DB.Update] flow diff failure. properties=%s id=%A" f.PropertiesJson f.Id
                        | :? Work as w -> printfn "[DB.Update] work diff failure. properties=%s id=%A" w.PropertiesJson w.Id
                        | :? Call as c -> printfn "[DB.Update] call diff failure. properties=%s id=%A" c.PropertiesJson c.Id
                        | :? ApiCall as ac -> printfn "[DB.Update] apiCall diff failure. properties=%s id=%A" ac.PropertiesJson ac.Id
                        | :? ApiDef as ad -> printfn "[DB.Update] apiDef diff failure. properties=%s id=%A" ad.PropertiesJson ad.Id
                        | _ -> printfn "[DB.Update] diff failure. parameter=%A" parameter
                        printfn "[DB.Update] No rows updated: table=%s column=%s id=%A" tableName dbColumnName entityId
                        failwithf "Update affected no rows. table=%s column=%s id=%A" tableName dbColumnName entityId
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
