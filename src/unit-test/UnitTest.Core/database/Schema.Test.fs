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
open Newtonsoft.Json


[<AutoOpen>]
module SchemaTestModule =
    let testDataDir() = Path.Combine(__SOURCE_DIRECTORY__, @"..\test-data")
    let dbFilePath = Path.Combine(testDataDir(), "test.sqlite3")
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
            Directory.CreateDirectory(testDataDir()) |> ignore
            createEditableProject()


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



    type Row = {
        mutable Id: Nullable<int>
        Name: string
        SystemId: int
        Guid: string
        DateTime: DateTime
    }
    [<Test>]
    let upsertTest() =
        use conn = dbApi.CreateConnection()
        tracefn $"SQL version: {conn.GetVersionString()}"
        let r1 = conn.Upsert(
            "flow",
            [| "id", box 1
               "name", box "Alice"
               "systemId", 1
               "guid", "b544dcdc-ca2f-43db-9d90-93843269bd3f"
               "dateTime", box DateTime.Now
            |],
            [|"id"|]
        )

        let row = {| id = 1; name = "Bob"; systemId = 1; guid = "b544dcdc-ca2f-43db-9d90-93843269bd3f"; DateTime = DateTime.Now |}
        let r2 = conn.Upsert(
            "flow", row, [ "id"; "name"; "systemId"; "guid" ],
            [|"id"|]
        )

        let row = { Id = Nullable(); Name = "Tom"; SystemId = 1; Guid = guid2str <| Guid.NewGuid(); DateTime = DateTime.Now }
        let r3 = conn.Upsert(
            "flow", row, [ "Id"; "Name"; "SystemId"; "Guid" ],
            [|"id"|],
            onInserted = fun id -> row.Id <- Nullable id
        )
        ()



    [<Test>]
    let ``insert test`` () =
        use conn = dbApi.CreateConnection()
        conn.TruncateAllTables()
        //use conn = createMemoryConnection()
        let newGuid() = Guid.NewGuid().ToString()

        let ver = Version().ToString()
        // project 삽입
        let prjGuid = newGuid()
        let prjName = "MainProject"
        let prjId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.Project} (guid, name, author, version) VALUES (@Guid, @Name, @Author, @Version)",
                        {| Guid=prjGuid; Name=prjName; Author=Environment.UserName; Version=ver|})


        // system 삽입
        let sysGuid = newGuid()
        let sysName = "MainSystem"
        let systemId = conn.InsertAndQueryLastRowId(
                        $"INSERT INTO {Tn.System} (guid, name, author, langVersion, engineVersion) VALUES (@Guid, @Name, @Author, @LangVersion, @EngineVersion)",
                        {| Guid=sysGuid; Name=sysName; Author=Environment.UserName;LangVersion = ver; EngineVersion=ver;|})

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
        conn.Execute($"INSERT INTO {Tn.Call} (guid, name, workId, autoPre, safety) VALUES (@Guid, @Name, @WorkId, @AutoPre, @Safety)",
                    {| Guid=callGuid; Name="call1"; WorkId=workId; AutoPre="AutoPre"; Safety="Safety"|}) |> ignore

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
    let ``EdObject -> DsObject -> OrmObject -> DB insert -> JSON test`` () =
        createEditableProject()

        let removeExistingData = true
        let connStr =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> path2ConnectionString

        let dsProject = edProject.ToRuntimeProject() |> validateRuntime
        //let json = dsProject.ToJson(Path.Combine(testDataDir(), "dssystem.json"))

        let edProjectBack = EdProject.FromRuntime dsProject

        let rtObjs = dsProject.EnumerateRtObjects()
        for rtObj in rtObjs do
            tracefn $"{rtObj.GetType().Name}: {rtObj.GetFQDN()}"

        let rtObjDic = rtObjs.ToDictionary(_.Guid, id)
        dsProject.Validate(rtObjDic)
        dsProject.EnumerateRtObjects()
        |> iter (fun dsobj ->
            // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
            dsobj.Id.IsNone === true
        )

        dsProject.ToSqlite3(connStr, removeExistingData)

        dsProject.EnumerateRtObjects()
        |> iter (fun dsobj ->
            if dsobj.Id.IsNone then
                noop()
            // DB 삽입 후이므로 Id 가 Some 이어야 함
            dsobj.Id.IsSome === true
        )

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

        let flowWors = dsFlow.Works
        flowWors.Length === 2
        flowWors[0].Guid === edWork1.Guid
        flowWors[1].Guid === edWork2.Guid
        dsFlow.Name === edFlow.Name

        edWork1.Calls.Count === 2
        edWork2.Calls.Count === 2
        let dsCall1 = dsWork1.Calls[0]
        let dsCall2 = dsWork2.Calls[0]
        dsCall1.Guid === edCall1a.Guid
        dsCall2.Guid === edCall2a.Guid
        dsCall1.PGuid.Value === dsWork1.Guid
        dsCall2.PGuid.Value === dsWork2.Guid

        dsCall1.Name === edCall1a.Name
        dsCall2.Name === edCall2a.Name

        let json = dsProject.ToJson(Path.Combine(testDataDir(), "dssystem.json"))
        tracefn $"---------------------- json:\r\n{json}"
        let dsProject2 = RtProject.FromJson json
        validateRuntime dsProject2 |> ignore
        let json2 = dsProject2.ToJson(Path.Combine(testDataDir(), "json-deserialized-dssystem.json"))

        json === json2

        dsSystem.ToAasJson() |> ignore

        //(fun () -> dsProject2.ToSqlite3(connStr, removeExistingData)) |> ShouldFailWithSubstringT "UNIQUE constraint failed"


        dsProject2.ToJson(Path.Combine(testDataDir(), "db-inserted-dssystem.json")) |> ignore
        validateRuntime dsProject2 |> ignore

        let dsProject3 = dsProject2.Replicate()
        validateRuntime dsProject3 |> ignore

        dsProject3.ToJson(Path.Combine(testDataDir(), "replica-of-db-inserted-dssystem.json")) |> ignore
        //(fun () -> dsProject3.ToSqlite3(connStr, removeExistingData)) |> ShouldFailWithSubstringT "UNIQUE constraint failed"
        do
            let dsProj = dsProject2.Duplicate()
            dsProj.Name <- $"Replica of {dsProj.Name}"
            validateRuntime dsProj |> ignore
            dsProj.ToJson(Path.Combine(testDataDir(), "duplicate-of-db-inserted-dssystem.json")) |> ignore
            dsProj.ToSqlite3(connStr, removeExistingData)

        let dsSystem4 = dsProject3.Systems[0].Duplicate()
        validateRuntime dsSystem4 |> ignore
        dsSystem4.Name <- "DuplicatedSystem"

        let dsProject4 = dsProject3.Duplicate(additionalPassiveSystems=[dsSystem4]) |> tee (fun z -> z.Name <- "CopiedProject")
        validateRuntime dsProject4 |> ignore
        dsProject4.Systems[0].IsPrototype <- false
        dsProject4.ToSqlite3(connStr, removeExistingData)

        ()


    [<Test>]
    let ``JSON -> DsObject -> DB update test`` () =
        //let jsonPath = Path.Combine(testDataDir(), "db-inserted-dssystem.json")
        let jsonPath = Path.Combine(testDataDir(), "dssystem.json")
        let json = File.ReadAllText(jsonPath)
        let dsProject1 = NjProject.FromJson json
        let dsProject2 = RtProject.FromJson json |> _.Duplicate()
        let sys = dsProject2.ActiveSystems[0]
        sys.Flows.Length === 1
        let flow = sys.Flows[0]
        dsProject2.Name <- "UpdatedProject"
        let removeExistingData = true
        let connStr =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> path2ConnectionString
        dsProject2.ToSqlite3(connStr, removeExistingData)


    [<Test>]
    let ``DB Delete preview test`` () =
        let projectId = 1
        let dbApi = Path.Combine(testDataDir(), "test_dssystem.sqlite3") |> path2ConnectionString |> DbApi

        dbApi.With(fun (conn, tr) ->
            let result =
                conn.CalculateCascadeDeleteAffected(Tn.System, 1)
            noop()
        )


    [<Test>]
    let ``설계 문서 위치에 샘플 생성`` () =
        let dsProject = edProject.ToRuntimeProject() |> validateRuntime
        // 설계 문서 위치에 drop
        Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec\dssystem.json")
        |> dsProject.ToJson |> ignore

        let dbPath = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec\dssystem.sqlite3")
        File.Delete(dbPath) |> ignore
        let connStr = dbPath |> path2ConnectionString
        dsProject.ToSqlite3(connStr, true)

        let rawJsonPath = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec\dssystem-raw.json")
        let json =
            let settings = JsonSerializerSettings(
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            )
            JsonConvert.SerializeObject(dsProject, Formatting.Indented, settings)

        File.WriteAllText(rawJsonPath, json)


    (*
        Project 에 cylinder subsystem 을 reference (loaded system) 으로 추가하는 예제
    *)
    [<Test>]
    let ``Cylinder 추가 test`` () =
        createEditableProject()
        createEditableSystemCylinder()
        let edProject = edProject.Replicate() |> validateEditable
        let edSysCylProto1 = edSystemCyl.Replicate() |> tee(fun z -> z.IsSaveAsReference <- true)
        let edSysCylProto2 = edSystemCyl.Replicate() |> tee(fun z -> z.IsSaveAsReference <- true; z.Name <- "Cylinder2")
        let edSysCylProto3 = edSystemCyl.Replicate() |> tee(fun z -> z.IsSaveAsReference <- true; z.Name <- "ActiveCylinder2")
        edProject.PassiveSystems.AddRange([edSysCylProto1; edSysCylProto2; ])
        edProject.ActiveSystems.Add(edSysCylProto3)
        let xxx = edProject
        let yyy = edSystemCyl
        let zzz = xxx, yyy
        let json=
            edProject.ToRuntimeProject()
            |> validateRuntime
            |> _.ToJson(Path.Combine(testDataDir(), "dssystem-with-cylinder.json"))
        ()

