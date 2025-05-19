namespace T



open System
open System.Data.SQLite
open NUnit.Framework
open Dapper

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS

open Ev2.Core.FS
open System.IO


[<AutoOpen>]
module SchemaTestModule =
    let dbFilePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "test.sqlite3")
    //let connectionString = "Data Source=Z:\\ds\\tmp\\ev2.sqlite3;Version=3;BusyTimeout=20000"    //
    let connectionString = $"Data Source={dbFilePath};Version=3;BusyTimeout=20000"    //

    [<SetUpFixture>]
    type GlobalTestSetup() =
        [<OneTimeSetUp>]
        member _.GlobalSetup() =
            DcLogger.EnableTrace <- true
            DapperTypeHandler.AddHandlers()
            checkHandlers()


        [<OneTimeTearDown>]
        member _.Cleanup() =
            //File.Delete(dbFilePath)
            ()

    let createMemoryConnection () =
        let conn = new SQLiteConnection("Data Source=:memory:")
        conn.Open()
        conn.Execute("PRAGMA foreign_keys = ON;") |> ignore
        conn.Execute(sqlCreateSchema) |> ignore
        conn

    let dbApi = DbApi(connectionString)

    [<Test>]
    //[<Fact>]
    let dbCreateTest() =
        use conn = dbApi.CreateConnection()
        ()

    [<Test>]
    let ``insert test`` () =
        use conn = dbApi.CreateConnection()
        conn.TruncateAllTables()
        //use conn = createMemoryConnection()
        let newGuid() = Guid.NewGuid().ToString()

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


        let systems = conn.EnumerateRows<ORMSystem>(Tn.System) |> List.ofSeq
        let flows = conn.EnumerateRows<ORMFlow>(Tn.Flow) |> List.ofSeq
        let works = conn.EnumerateRows<ORMWork>(Tn.Work) |> List.ofSeq
        let calls = conn.EnumerateRows<ORMCall>(Tn.Call) |> List.ofSeq

        systems.Length === 1
        systems[0].Name === sysName

        ()


