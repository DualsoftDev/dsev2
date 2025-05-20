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
            let dbApi = DbApi(connStr)
            use conn = dbApi.CreateConnection()
            conn.TruncateAllTables()
            //use conn = createMemoryConnection()
            let newGuid() = Guid.NewGuid().ToString()

            // system 삽입
            let sysGuid = newGuid()
            let sysName = "MainSystem"
            conn.Execute($"INSERT INTO {Tn.System} (guid, name) VALUES (@guid, @name)",
                         dict ["guid", box sysGuid; "name", box sysName]) |> ignore



            //let grDic = x.EnumerateDsObjects() |> groupByToDictionary _.GetType()
            //let works = grDic.[typeof<DsWork>] |> Seq.cast<DsWork> |> List.ofSeq
            //let calls = grDic.[typeof<DsCall>] |> Seq.cast<DsCall> |> List.ofSeq
            //let flows = grDic.[typeof<DsFlow>] |> Seq.cast<DsFlow> |> List.ofSeq
            //let dbApi = DbApi(connStr)
            //checkHandlers()
            //use conn = dbApi.CreateConnection()
            //use tr = conn.BeginTransaction()

            //let sysName = "MainSystem"
            //conn.Execute($"INSERT INTO {Tn.System} (guid, name) VALUES (@guid, @name)",
            //             dict ["guid", box (Guid.NewGuid()); "name", box sysName]) |> ignore


            ////let guid = x.Guid
            ////let sysId = conn.InsertAndQueryLastRowId($"INSERT INTO {Tn.System} (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", x, tr)


            ////let x = {| Guid = Guid.NewGuid(); DateTime = DateTime.Now; Name = "HelloWorld" |}
            ////conn.Execute("INSERT INTO system (guid, dateTime, name) VALUES (@Guid, @DateTime, @Name);", x, tr) |> ignore


            ////conn.Execute($"INSERT INTO {Tn.System} (guid, dateTime, name) VALUES (@guid, @dateTime, @name);",
            ////    dict ["guid", box (Guid.NewGuid()); "dateTime", box DateTime.Now; "name", box "Hello"], tr)|>ignore
            ////    //{|Guid=Guid.NewGuid(); DateTime=DateTime.Now; Name="Hello"|}, tr) |> ignore

            ////// works 삽입
            ////conn.Execute($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, {sysId});", works, tr) |> ignore
            //tr.Commit()
            ()

        [<Obsolete("DB 에서 읽어 들이는 것은 금지!!!  Debugging 전용")>]
        static member FromSqlite3(connStr:string) =
            ()
