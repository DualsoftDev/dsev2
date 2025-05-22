namespace T

open System
open System.IO
open System.Data.SQLite

open NUnit.Framework
open Dapper

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open System.Collections


[<AutoOpen>]
module SchemaTestModule =
    let dbFilePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "test.sqlite3")
    let path2ConnectionString (dbFilePath:string) = $"Data Source={dbFilePath};Version=3;BusyTimeout=20000"    //

    [<SetUpFixture>]
    type GlobalTestSetup() =
        [<OneTimeSetUp>]
        member _.GlobalSetup() =
            Ev2.Core.FS.ModuleInitializer.Initialize(null)
            DcLogger.EnableTrace <- true
            //DapperTypeHandler.AddHandlers()
            //checkHandlers()
            AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)


        [<OneTimeTearDown>]
        member _.Cleanup() =
            //File.Delete(dbFilePath)
            ()

    let createMemoryConnection () =
        let conn = new SQLiteConnection("Data Source=:memory:")
        conn.Open()
        conn.Execute("PRAGMA foreign_keys = ON;") |> ignore
        conn.Execute(getSqlCreateSchema()) |> ignore
        conn

    let dbApi = path2ConnectionString dbFilePath |> DbApi

    [<Test>]
    let dbCreateTest() =
        use conn = dbApi.CreateConnection()
        ()

    [<Test>]
    let upsertTest() =
        use conn = dbApi.CreateConnection()
        tracefn $"SQL version: {conn.GetVersionString()}"
        conn.Upsert(
            "flow",
            [| "id", box 1
               "name", box "Alice"
               "systemId", 1
               "guid", "b544dcdc-ca2f-43db-9d90-93843269bd3f"
               "dateTime", box DateTime.Now
            |],
            [|"id"|]
        ) |> ignore

        let row = {| id = 1; name = "Bob"; systemId = 1; guid = "b544dcdc-ca2f-43db-9d90-93843269bd3f"; DateTime = DateTime.Now |}
        conn.Upsert(
            "flow", row, [ "id"; "name"; "systemId"; "guid" ],
            [|"id"|]
        ) |> ignore

        let row = {| id = null; name = "Tom"; systemId = 1; guid = "aaaaaaaa-ca2f-43db-9d90-93843269bd3f"; DateTime = DateTime.Now |}
        conn.Upsert(
            "flow", row, [ "id"; "name"; "systemId"; "guid" ],
            [|"id"|]
        ) |> ignore
        ()



    [<Test>]
    let ``insert test`` () =
        use conn = dbApi.CreateConnection()
        conn.TruncateAllTables()
        //use conn = createMemoryConnection()
        let newGuid() = Guid.NewGuid().ToString()

        // project 삽입
        let prjGuid = newGuid()
        let prjName = "MainProject"
        let prjId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.Project} (guid, name) VALUES (@Guid, @Name)",
                        {| Guid=prjGuid; Name=prjName; |})


        // system 삽입
        let sysGuid = newGuid()
        let sysName = "MainSystem"
        let systemId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.System} (guid, name) VALUES (@Guid, @Name)",
                        {| Guid=sysGuid; Name=sysName;|})

        // flow 삽입
        let flowGuid = newGuid()
        let flowId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.Flow} (guid, name, systemId) VALUES (@Guid, @Name, @SystemId)",
                        {| Guid=flowGuid; Name="MainFlow"; SystemId=systemId|})

        // work 삽입 (flow 연결된 경우)
        let workGuid1 = newGuid()
        let workId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@Guid, @Name, @SystemId, @FlowId)",
                        {| Guid=workGuid1; Name="Work1"; SystemId=systemId; FlowId=flowId|})

        // work 삽입 (flow 연결 없는 경우 - flowId = NULL)
        let workGuid2 = newGuid()
        conn.Execute($"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@guid, @name, @systemId, NULL)",
                     dict ["guid", box workGuid2; "name", box "Work2"; "systemId", box systemId]) |> ignore


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


    [<Test>]
    let ``EdObject -> DsObject -> OrmObject -> DB insert test`` () =
        let edProject = EdProject.Create("MainProject")
        let edSystem  = EdSystem .Create("MainSystem"  , edProject, asActive=true)
        let edFlow    = EdFlow   .Create("MainFlow"    , edSystem)
        let edWork1   = EdWork   .Create("BoundedWork1", edSystem)
        let edWork2   = EdWork   .Create("BoundedWork2", edSystem, ownerFlow=edFlow)
        let edWork3   = EdWork   .Create("FreeWork1"   , edSystem)
        let edCall1a  = EdCall   .Create("Call1a"      , edWork1)
        let edCall1b  = EdCall   .Create("Call1b"      , edWork1)
        let edCall2a  = EdCall   .Create("Call2a"      , edWork2)
        let edCall2b  = EdCall   .Create("Call2b"      , edWork2)
        //edProject.AddSystems([edSystem])
        //edWork1.AddCalls([edCall1])
        edFlow.AddWorks([edWork1])

        let edArrow1 = EdArrowBetweenCalls(edCall1a, edCall1b, DateTime.Now, Guid.NewGuid())
        edWork1.AddArrows([edArrow1])
        let edArrow2 = EdArrowBetweenCalls(edCall2a, edCall2b, DateTime.Now, Guid.NewGuid())
        edWork2.AddArrows([edArrow2])

        //edWork2.AddCalls([edCall2])
        //edSystem.AddFlows([edFlow])
        //edSystem.AddWorks([edWork1; edWork2; edWork3])

        let dsProject = edProject.ToDsProject()
        let dsSystem = dsProject.Systems[0]
        let dsFlow = dsSystem.Flows[0]
        dsFlow.Guid === edFlow.Guid
        dsFlow.Works.Length === 2
        dsSystem.Works.Length === 3
        let dsWork1 = dsSystem.Works |> Seq.find(fun w -> w.Name = "BoundedWork1")
        let dsWork2 = dsSystem.Works |> Seq.find(fun w -> w.Name = "BoundedWork2")
        let dsWork3 = dsSystem.Works |> Seq.find(fun w -> w.Name = "FreeWork1")
        dsWork1.Guid === edWork1.Guid
        dsWork2.Guid === edWork2.Guid
        dsWork3.Guid === edWork3.Guid
        dsWork1.Name === edWork1.Name
        dsWork2.Name === edWork2.Name
        dsWork3.Name === edWork3.Name

        dsFlow.Works.Length === 2
        dsFlow.Works[0].Guid === edWork1.Guid
        dsFlow.Works[1].Guid === edWork2.Guid
        dsFlow.Name === edFlow.Name

        edWork1.Calls.Length === 2
        edWork2.Calls.Length === 2
        let dsCall1 = dsWork1.Calls[0]
        let dsCall2 = dsWork2.Calls[0]
        dsCall1.Guid === edCall1a.Guid
        dsCall2.Guid === edCall2a.Guid
        dsCall1.PGuid.Value === dsWork1.Guid
        dsCall2.PGuid.Value === dsWork2.Guid

        dsCall1.Name === edCall1a.Name
        dsCall2.Name === edCall2a.Name


        let json = dsProject.ToJson()
        tracefn $"---------------------- json:\r\n{json}"
        let dsProject2 = DsProject.FromJson json
        let json2 = dsProject2.ToJson()

        json === json2

        dsSystem.ToAasJson() |> ignore

        let removeExistingData = true
        let connStr =
            Path.Combine(__SOURCE_DIRECTORY__, "..", "test_dssystem.sqlite3")
            |> path2ConnectionString
        dsProject2.ToSqlite3(connStr, removeExistingData)

        ()
