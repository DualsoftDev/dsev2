namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Newtonsoft.Json
open System
open Dapper

[<AutoOpen>]
module Ds2SqliteModule =
    type DsSystem with
        member x.ToSqlite3(connStr:string) =
            let grDic = x.EnumerateDsObjects() |> groupByToDictionary _.GetType()
            let works = grDic.[typeof<DsWork>] |> Seq.cast<DsWork> |> List.ofSeq
            let calls = grDic.[typeof<DsCall>] |> Seq.cast<DsCall> |> List.ofSeq
            let flows = grDic.[typeof<DsFlow>] |> Seq.cast<DsFlow> |> List.ofSeq
            let dbApi = DbApi(connStr)
            checkHandlers()
            use conn = dbApi.CreateConnection()
            use tr = conn.BeginTransaction()
            conn.TruncateAllTables()


            let ormSystem = x.ToORM()
            let sysId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.System} (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", ormSystem, tr)
            x.Id <- Some sysId

            // flows 삽입
            for f in flows do
                let ormFlow = f.ToORM() :?> ORMFlow
                ormFlow.SystemId <- Nullable sysId
                let flowId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Flow} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormFlow, tr)
                f.Id <- Some flowId

            // works, calls 삽입
            for w in works do
                let ormWork = w.ToORM() :?> ORMWork
                ormWork.SystemId <- Nullable sysId

                // work 에 flow guid 가 설정된 (즉 flow 에 소속된) work 에 대해서
                // work 의 flowId 를 설정한다.
                w.OptFlowGuid
                |> iter (fun flowGuid ->
                    flows
                    |> List.tryFind(fun f -> f.Guid = flowGuid)
                    |> iter (fun f ->
                        ormWork.FlowId <- f.Id.Value ))


                let workId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId, flowId) VALUES (@Guid, @DateTime, @Name, @SystemId, @FlowId);", ormWork, tr)
                w.Id <- Some workId

                for c in w.Calls do
                    let ormCall = c.ToORM() :?> ORMCall
                    ormCall.WorkId <- Nullable workId
                    let callId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Call} (guid, dateTime, name, workId) VALUES (@Guid, @DateTime, @Name, @WorkId);", ormCall, tr)
                    c.Id <- Some callId

            //let ormWorks = works |-> _.ToORM() |> tee(fun ws -> ws |> iter (fun w -> w.Pid <- Nullable sysId))
            //conn.Execute($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormWorks, tr) |> ignore

            //// calls 삽입
            //let ormCalls = calls |-> _.ToORM() |> tee(fun cs -> cs |> iter (fun c -> c.Pid <- Nullable sysId))
            //conn.Execute($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormWorks, tr) |> ignore


            tr.Commit()
            ()

        [<Obsolete("DB 에서 읽어 들이는 것은 금지!!!  Debugging 전용")>]
        static member FromSqlite3(connStr:string) =
            ()
