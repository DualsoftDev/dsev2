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
            //let dbApi = DbApi(connStr)
            //use conn = dbApi.CreateConnection()
            //conn.TruncateAllTables()
            ////use conn = createMemoryConnection()
            //let newGuid() = Guid.NewGuid().ToString()

            //// system 삽입
            //let sysGuid = newGuid()
            //let sysName = "MainSystem"
            //conn.Execute($"INSERT INTO {Tn.System} (guid, name) VALUES (@guid, @name)",
            //             dict ["guid", box sysGuid; "name", box sysName]) |> ignore



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

            // works, calls 삽입
            for w in works do
                let ormWork = w.ToORM() :?> ORMWork
                ormWork.SystemId <- Nullable sysId
                let workId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormWork, tr)
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
