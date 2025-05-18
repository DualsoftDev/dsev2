namespace T

open System.IO
open System.Reactive.Disposables
open Xunit


open Dual.Common.Base
open Dual.Common.Core.FS

open Ev2.Core.FS
open NUnit.Framework
open System.Data.SQLite
open Dapper
open System
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS


[<AutoOpen>]
module SchemaTestModule =
    do
        DcLogger.EnableTrace <- true


    let createMemoryConnection () =
        let conn = new SQLiteConnection("Data Source=:memory:")
        conn.Open()
        conn.Execute("PRAGMA foreign_keys = ON;") |> ignore
        conn.Execute(sqlCreateSchema) |> ignore
        conn

    let connectionString = "Data Source=Z:\\ds\\tmp\\ev2.sqlite3;Version=3;BusyTimeout=20000"
    let dbApi = DbApi(connectionString)

    [<Test>]
    //[<Fact>]
    let dbCreateTest() =
        use conn = dbApi.CreateConnection()
        ()

    [<Test>]
    let ``insert test`` () =
        use conn = dbApi.CreateConnection()
        let newGuid() = Guid.NewGuid().ToString("N")

        // system 삽입
        let sysGuid = newGuid()
        let sysName = "MainSystem"
        conn.Execute($"INSERT INTO {Tn.System} (guid, name) VALUES (@guid, @name)",
                     dict ["guid", box sysGuid; "name", box sysName]) |> ignore

        let systemId = conn.ExecuteScalar<int>($"SELECT id FROM {Tn.System} WHERE guid = @guid",
                                               dict ["guid", box sysGuid])  // Dapper의 파라미터 바인딩에 사용하는 매우 유용한 방법입니다. 이 패턴은 **익명 객체 대신 IDictionary<string, obj>**를 사용하여 매개변수를 지정

        // flow 삽입
        let flowGuid = newGuid()
        conn.Execute($"INSERT INTO {Tn.Flow} (guid, name, systemId) VALUES (@guid, @name, @systemId)",
                     dict ["guid", box flowGuid; "name", box "MainFlow"; "systemId", box systemId]) |> ignore

        let flowId = conn.ExecuteScalar<int>($"SELECT id FROM {Tn.Flow} WHERE guid = @guid",
                                             dict ["guid", box flowGuid])

        // work 삽입 (flow 연결된 경우)
        let workGuid1 = newGuid()
        conn.Execute($"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@guid, @name, @systemId, @flowId)",
                     dict ["guid", box workGuid1; "name", box "Work1"; "systemId", box systemId; "flowId", box flowId]) |> ignore

        // work 삽입 (flow 연결 없는 경우 - flowId = NULL)
        let workGuid2 = newGuid()
        conn.Execute($"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@guid, @name, @systemId, NULL)",
                     dict ["guid", box workGuid2; "name", box "Work2"; "systemId", box systemId]) |> ignore

        let workId = conn.ExecuteScalar<int>($"SELECT id FROM {Tn.Work} WHERE guid = @guid",
                                             dict ["guid", box workGuid1])

        // call 삽입
        let callGuid = newGuid()
        conn.Execute($"INSERT INTO {Tn.Call} (guid, name, workId) VALUES (@guid, @name, @workId)",
                     dict ["guid", box callGuid; "name", box "Call1"; "workId", box workId]) |> ignore

        // 확인: 총 system = 1, flow = 1, work = 2, call = 1
        let countSystem = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.System}")
        let countFlow   = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Flow}")
        let countWork   = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Work}")
        let countCall   = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Call}")

        1 === countSystem
        1 === countFlow
        2 === countWork
        1 === countCall


