namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Newtonsoft.Json
open System
open Dapper

[<AutoOpen>]
module Ds2SqliteModule =
    let project2Sqlite (x:DsProject) (connStr:string) =
        let grDic = x.EnumerateDsObjects() |> groupByToDictionary _.GetType()
        let systems = grDic.[typeof<DsSystem>] |> Seq.cast<DsSystem> |> List.ofSeq

        let dbApi = DbApi(connStr)
        checkHandlers()
        use conn = dbApi.CreateConnection()
        use tr = conn.BeginTransaction()
        try
            conn.TruncateAllTables()

            let cache, ormProject = x.ToORM()
            let projId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Project} (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", ormProject, tr)
            x.Id <- Some projId
            cache[x.Guid].Id <- projId

            for s in systems do
                let ormSystem = s.ToORM(cache) :?> ORMSystem
                let sysId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.System} (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", ormSystem, tr)
                s.Id <- Some sysId
                cache[x.Guid].Id <- sysId

                // update projectSystemMap table
                let isActive = x.ActiveSystems |> Seq.contains s
                let isPassive = x.PassiveSystems |> Seq.contains s
                assert(isActive <> isPassive)   // XOR
                match conn.TryQuerySingle<ORMProjectSystemMap>($"SELECT * FROM {Tn.ProjectSystemMap} WHERE projectId = {projId} AND systemId = {sysId}") with
                | Some row when row.IsActive = isActive -> ()
                | Some row ->
                    conn.Execute($"UPDATE {Tn.ProjectSystemMap} SET active = {isActive}, dateTime = @DateTime WHERE id = {row.Id}",
                                {| DateTime = now() |}) |> ignore
                | None -> conn.Execute(
                            $"INSERT INTO {Tn.ProjectSystemMap} (projectId, systemId, active, guid, dateTime) VALUES (@ProjectId, @SystemId, @Active, @Guid, @DateTime)",
                            {| ProjectId = projId; SystemId = sysId; Active = isActive; Guid=Guid.NewGuid(); DateTime=now() |}, tr) |> ignore


                // flows 삽입
                for f in s.Flows do
                    let ormFlow = f.ToORM(cache) :?> ORMFlow
                    ormFlow.SystemId <- Nullable sysId
                    let flowId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Flow} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormFlow, tr)
                    f.Id <- Some flowId
                    cache[f.Guid].Id <- flowId

                // works, calls 삽입
                for w in s.Works do
                    let ormWork = w.ToORM(cache) :?> ORMWork
                    ormWork.SystemId <- Nullable sysId

                    // work 에 flow guid 가 설정된 (즉 flow 에 소속된) work 에 대해서
                    // work 의 flowId 를 설정한다.
                    w.OptFlowGuid
                    |> iter (fun flowGuid ->
                        s.Flows
                        |> List.tryFind(fun f -> f.Guid = flowGuid)
                        |> iter (fun f ->
                            ormWork.FlowId <- f.Id.Value ))


                    let workId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId, flowId) VALUES (@Guid, @DateTime, @Name, @SystemId, @FlowId);", ormWork, tr)
                    w.Id <- Some workId
                    cache[w.Guid].Id <- workId

                    for c in w.Calls do
                        let ormCall = c.ToORM(cache) :?> ORMCall
                        ormCall.WorkId <- Nullable workId
                        let callId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Call} (guid, dateTime, name, workId) VALUES (@Guid, @DateTime, @Name, @WorkId);", ormCall, tr)
                        c.Id <- Some callId
                        cache[c.Guid].Id <- callId

            tr.Commit()
        with ex ->
            tr.Rollback()
            logError $"project2Sqlite failed: {ex.Message}"
            raise ex


    type DsProject with
        member x.ToSqlite3(connStr:string) = project2Sqlite x connStr

        [<Obsolete("DB 에서 읽어 들이는 것은 금지!!!  Debugging 전용")>]
        static member FromSqlite3(connStr:string) =
            ()


    //type DsSystem with
    //    member x.ToSqlite3(connStr:string) =
    //        let grDic = x.EnumerateDsObjects() |> groupByToDictionary _.GetType()
    //        let works = grDic.[typeof<DsWork>] |> Seq.cast<DsWork> |> List.ofSeq
    //        let calls = grDic.[typeof<DsCall>] |> Seq.cast<DsCall> |> List.ofSeq
    //        let flows = grDic.[typeof<DsFlow>] |> Seq.cast<DsFlow> |> List.ofSeq
    //        let dbApi = DbApi(connStr)
    //        checkHandlers()
    //        use conn = dbApi.CreateConnection()
    //        use tr = conn.BeginTransaction()
    //        conn.TruncateAllTables()


    //        let cache, ormSystem = x.ToORM()
    //        let sysId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.System} (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", ormSystem, tr)
    //        x.Id <- Some sysId
    //        cache[x.Guid].Id <- sysId

    //        // flows 삽입
    //        for f in flows do
    //            let ormFlow = f.ToORM(cache) :?> ORMFlow
    //            ormFlow.SystemId <- Nullable sysId
    //            let flowId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Flow} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormFlow, tr)
    //            f.Id <- Some flowId
    //            cache[f.Guid].Id <- flowId

    //        // works, calls 삽입
    //        for w in works do
    //            let ormWork = w.ToORM(cache) :?> ORMWork
    //            ormWork.SystemId <- Nullable sysId

    //            // work 에 flow guid 가 설정된 (즉 flow 에 소속된) work 에 대해서
    //            // work 의 flowId 를 설정한다.
    //            w.OptFlowGuid
    //            |> iter (fun flowGuid ->
    //                flows
    //                |> List.tryFind(fun f -> f.Guid = flowGuid)
    //                |> iter (fun f ->
    //                    ormWork.FlowId <- f.Id.Value ))


    //            let workId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId, flowId) VALUES (@Guid, @DateTime, @Name, @SystemId, @FlowId);", ormWork, tr)
    //            w.Id <- Some workId
    //            cache[w.Guid].Id <- workId

    //            for c in w.Calls do
    //                let ormCall = c.ToORM(cache) :?> ORMCall
    //                ormCall.WorkId <- Nullable workId
    //                let callId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Call} (guid, dateTime, name, workId) VALUES (@Guid, @DateTime, @Name, @WorkId);", ormCall, tr)
    //                c.Id <- Some callId
    //                cache[c.Guid].Id <- callId

    //        //let ormWorks = works |-> _.ToORM() |> tee(fun ws -> ws |> iter (fun w -> w.Pid <- Nullable sysId))
    //        //conn.Execute($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormWorks, tr) |> ignore

    //        //// calls 삽입
    //        //let ormCalls = calls |-> _.ToORM() |> tee(fun cs -> cs |> iter (fun c -> c.Pid <- Nullable sysId))
    //        //conn.Execute($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormWorks, tr) |> ignore


    //        tr.Commit()
    //        ()

    //    [<Obsolete("DB 에서 읽어 들이는 것은 금지!!!  Debugging 전용")>]
    //    static member FromSqlite3(connStr:string) =
    //        ()
