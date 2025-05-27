module TestProject.UnitTest.RuntimeDB

open NUnit.Framework
open System.IO
open Dual.EV2.Core
open Dual.EV2.RuntimeDB.RuntimeDB
open Microsoft.Data.Sqlite
open System

[<TestFixture>]
type ``RuntimeDB Tests`` () =

    let dbPath = "test_runtime.db"

    [<SetUp>]
    member _.Setup() =
        if File.Exists(dbPath) then File.Delete(dbPath)

    [<TearDown>]
    member _.Cleanup() =
        GC.Collect()
        GC.WaitForPendingFinalizers()


    [<Test>]
    member _.``프로젝트를 SQLite DB로 저장하고 검증`` () =
        // 샘플 프로젝트 구성
        let project = Project("projSample")
        let system = System("sysSample", project)
        let flow = Flow("flow1", system)
        let work = Work("Work1", system, flow)
        let call = Call("Call1", work)
        let apiDefSys = System("sysAPI", project)
        let apiDef = ApiDef("API1", apiDefSys)
        let apiCall = ApiCall("ApiCall1", call, apiDef)

        work.Calls.Add(call)
        call.ApiCalls.Add(apiCall)
        system.Flows.Add(flow)
        system.Works.Add(work)
        project.Systems.Add(system)

        let usage = ProjectSystemUsage(project, system, true)
        project.SystemUsages.Add(usage)
        project.TargetSystemIds.Add(system.Name)

        initializeSchema dbPath
        let db = fromProject project
        saveToSqlite db dbPath

        use conn = new SqliteConnection($"Data Source={dbPath}")
        conn.Open()

        let count (table: string) =
            use cmd = new SqliteCommand($"SELECT COUNT(*) FROM {table}", conn)
            cmd.ExecuteScalar() :?> int64

        Assert.AreEqual(1, count "project")
        Assert.AreEqual(1, count "system")
        Assert.AreEqual(1, count "projectSystemMap")
        Assert.AreEqual(1, count "work")
        Assert.AreEqual(1, count "call")
        Assert.AreEqual(1, count "apiCall")
        Assert.AreEqual(1, count "apiDef")
