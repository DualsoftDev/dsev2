namespace T

open System
open System.Linq
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
open System.Reactive.Concurrency


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
            AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)

            Directory.CreateDirectory(testDataDir()) |> ignore
            createEditableProject()


        [<OneTimeTearDown>]
        member _.Cleanup() =
            //File.Delete(dbFilePath)
            ()


    let createSqliteDbApi (path:string) =
        path
        |> path2ConnectionString
        |> DbProvider.Sqlite
        |> AppDbApi


    let dbApi = createSqliteDbApi dbFilePath


    // sqlite <--> pgsql 전환시마다 dbApi 를 새로 생성해야 함.  SqlMapper 가 다름.
    let pgsqlDbApi() =
            "Host=localhost;Database=ds;Username=ds;Password=ds;Search Path=ds"
            |> DbProvider.Postgres
            |> AppDbApi

    // sqlite <--> pgsql 전환시마다 dbApi 를 새로 생성해야 함.  SqlMapper 가 다름.
    let sqliteDbApi() =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> createSqliteDbApi


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
            [|
               "name", box "Alice"
               "systemId", 1
               "guid", "b544dcdc-ca2f-43db-9d90-93843269bd3f"
               "dateTime", box DateTime.Now
            |],
            [|"id"|]
        )

        let row = {| id = 1; name = "Bob"; systemId = 1; guid = "b544dcdc-ca2f-43db-9d90-93843269bd3f"; DateTime = DateTime.Now |}
        let r2 = conn.Upsert(
            "flow", row, [ "name"; "systemId"; "guid" ],
            [|"id"|]
        )

        let row = { Id = Nullable(); Name = "Tom"; SystemId = 1; Guid = guid2str <| Guid.NewGuid(); DateTime = DateTime.Now }
        let r3 = conn.Upsert(
            "flow", row, [ "Name"; "SystemId"; "Guid" ],
            [|"id"|],
            onInserted = fun id -> row.Id <- Nullable id
        )
        ()



    [<Test>]
    let ``insert test`` () =
        use conn = dbApi.CreateConnection()
        dbApi.With(fun (conn, tr) ->
            conn.Execute($"DELETE FROM {Tn.Project};", tr)
        ) |> ignore


        dbApi.VendorDB.TruncateAllTables(conn)
        let newGuid() = Guid.NewGuid().ToString()

        let ver = Version().ToString()
        // project 삽입
        let prjGuid = newGuid()
        let prjName = "MainProject"
        let prjId = conn.Insert(
                        $"INSERT INTO {Tn.Project} (guid, name, author, version) VALUES (@Guid, @Name, @Author, @Version)",
                        {| Guid=prjGuid; Name=prjName; Author=Environment.UserName; Version=ver|})


        // system 삽입
        let sysGuid = newGuid()
        let sysName = "MainSystem"
        let systemId = conn.Insert(
                        $"INSERT INTO {Tn.System} (guid, name, iri, author, langVersion, engineVersion) VALUES (@Guid, @Name, @IRI, @Author, @LangVersion, @EngineVersion)",
                        {| Guid=sysGuid; Name=sysName; Author=Environment.UserName;LangVersion = ver; EngineVersion=ver; IRI="http://dualsoft.com/unique/12345"|})

        // flow 삽입
        let flowGuid = newGuid()
        let flowId = conn.Insert(
                        $"INSERT INTO {Tn.Flow} (guid, name, systemId) VALUES (@Guid, @Name, @SystemId)",
                        {| Guid=flowGuid; Name="MainFlow"; SystemId=systemId|})

        // work 삽입 (flow 연결된 경우)
        let workGuid1 = newGuid()
        let workId = conn.Insert(
                        $"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@Guid, @Name, @SystemId, @FlowId)",
                        {| Guid=workGuid1; Name="Work1"; SystemId=systemId; FlowId=flowId|})

        // work 삽입 (flow 연결 없는 경우 - flowId = NULL)
        let workGuid2 = newGuid()
        conn.Execute($"INSERT INTO {Tn.Work} (guid, name, systemId, flowId) VALUES (@guid, @name, @systemId, NULL)",
                     dict ["guid", box workGuid2; "name", box "Work2"; "systemId", box systemId]) |> ignore


        // call 삽입
        let callGuid = newGuid()
        conn.Execute($"INSERT INTO {Tn.Call} (guid, name, workId, autoConditions, commonConditions) VALUES (@Guid, @Name, @WorkId, @AutoConditions, @CommonConditions)",
                    {| Guid=callGuid; Name="call1"; WorkId=workId; AutoConditions="""[ "AutoConditions" ]"""; CommonConditions="""[ "CommonConditions" ]"""|}) |> ignore

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


    let ``basic_test`` (dbApi:AppDbApi) =
        createEditableProject()

        let removeExistingData = true

        let dsProject = edProject |> validateRuntime
        //let json = dsProject.ToJson(Path.Combine(testDataDir(), "dssystem.json"))

        let rtObjs = dsProject.EnumerateRtObjects()
        for rtObj in rtObjs do
            tracefn $"{rtObj.GetType().Name}: {rtObj.GetFQDN()}"

        let rtObjDic = rtObjs.ToDictionary(_.Guid, fun z -> z :> Unique)
        dsProject.Validate(rtObjDic)
        dsProject.EnumerateRtObjects()
        |> iter (fun dsobj ->
            // 최초 생성시, DB 삽입 전이므로 Id 가 None 이어야 함
            dsobj.Id.IsNone === true
        )

        dsProject.Parameter <- {|
            Name = "Alice"
            Age = 30
            Skills = [ "SQL"; "Python" ] |} |> JsonConvert.SerializeObject


        dbApi.With(fun (conn, tr) -> conn.Execute($"DELETE FROM {Tn.Project}")) |> ignore
        dsProject.CommitToDB(dbApi, removeExistingData)

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

        edWork1.Calls.Length === 2
        edWork2.Calls.Length === 2
        let dsCall1 = dsWork1.Calls[0]
        let dsCall2 = dsWork2.Calls[0]
        dsCall1.Guid === edCall1a.Guid
        dsCall2.Guid === edCall2a.Guid
        dsCall1.RawParent |->_.Guid === Some dsWork1.Guid
        dsCall2.RawParent |->_.Guid === Some dsWork2.Guid

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
            let dsProj = dsProject2.Duplicate($"CC_{dsProject2.Name}")
            dsProj.Name <- $"Duplicate of {dsProj.Name}"
            validateRuntime dsProj |> ignore
            dsProj.ToJson(Path.Combine(testDataDir(), "duplicate-of-db-inserted-dssystem.json")) |> ignore
            dsProj.CommitToDB(dbApi, removeExistingData)


        let dsProject4 =
            let dsSystem4 = dsProject3.Systems[0].Duplicate()
            validateRuntime dsSystem4 |> ignore
            dsSystem4.Name <- "DuplicatedSystem"
            dsProject3.Duplicate($"CC_{dsProject3.Name}")
            |> tee(fun z ->
                z.Name <- $"{z.Name}4"
                z.EnumerateRtObjects().OfType<RtApiCall>().First().ValueSpec <- Some <| Single 3.14156952
                z.AddPassiveSystem dsSystem4)

        validateRuntime dsProject4 |> ignore
        dsProject4.Systems[0].PrototypeSystemGuid <- None
        dsProject4.CommitToDB(dbApi, removeExistingData)

        ()


    [<Test>]
    let ``SQLite: EdObject -> DsObject -> OrmObject -> DB insert -> JSON test`` () =
        sqliteDbApi() |> basic_test

    [<Test>]
    let ``PGSql: EdObject -> DsObject -> OrmObject -> DB insert -> JSON test`` () =
        pgsqlDbApi() |> basic_test


    [<Test>]
    let ``JSON -> DsObject -> DB update test`` () =
        //let jsonPath = Path.Combine(testDataDir(), "db-inserted-dssystem.json")
        let jsonPath = Path.Combine(testDataDir(), "dssystem.json")
        if not (File.Exists jsonPath) then
            edProject.ToJson(jsonPath) |> ignore

        let json = File.ReadAllText(jsonPath)
        let dsProject0 = NjProject.FromJson json


        let dsProject1 = RtProject.FromJson json |> validateRuntime
        noop()
        let dsProject2 = dsProject1 |> _.Duplicate($"CC_{dsProject1.Name}")
        let sys = dsProject2.ActiveSystems[0]
        sys.Flows.Length === 1
        let flow = sys.Flows[0]
        dsProject2.Name <- "UpdatedProject"
        let removeExistingData = true

        let dbApi = Path.Combine(testDataDir(), "test_dssystem.sqlite3") |> createSqliteDbApi

        dbApi.With(fun (conn, tr) ->
            conn.Execute($"DELETE FROM {Tn.Project} where name = @Name", {| Name=dsProject2.Name|}))
        |> ignore

        dsProject2.CommitToDB(dbApi, removeExistingData)


    [<Test>]
    let ``DB Delete preview test`` () =
        let projectId = 1
        let dbApi = Path.Combine(testDataDir(), "test_dssystem.sqlite3") |> createSqliteDbApi

        dbApi.With(fun (conn, tr) ->
            let result =
                conn.GuessCascadeDeleteAffected(Tn.System, 1)
            noop()
        )


    [<Test>]
    let ``설계 문서 위치에 샘플 생성`` () =
        let dsProject = edProject |> validateRuntime
        // 설계 문서 위치에 drop
        Path.Combine(specDir, "dssystem.json")
        |> dsProject.ToJson |> ignore

        let dbPath = Path.Combine(specDir, "dssystem.sqlite3")
        File.Delete(dbPath) |> ignore
        let dbApi = createSqliteDbApi dbPath

        dsProject.CommitToDB(dbApi, removeExistingData=true)

        //let rawJsonPath = Path.Combine(specDir, "dssystem-raw.json")
        //let json =
        //    let settings = JsonSerializerSettings(
        //        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        //        TypeNameHandling = TypeNameHandling.Auto,
        //        NullValueHandling = NullValueHandling.Ignore,
        //        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        //    )
        //    JsonConvert.SerializeObject(dsProject, Formatting.Indented, settings)

        //File.WriteAllText(rawJsonPath, json)


    (*
        Project 에 cylinder subsystem 을 reference (loaded system) 으로 추가하는 예제
    *)
    [<Test>]
    let ``Cylinder 추가 test`` () =
        createEditableProject()
        createEditableSystemCylinder()
        let edProject = edProject.Replicate() |> validateRuntime
        let protoGuid = edSystemCyl.Guid
        edProject.AddPrototypeSystem edSystemCyl
        let edSysCyl1 = edProject.Instantiate(protoGuid, Name="실린더 instance1", asActive=false)
        let edSysCyl2 = edProject.Instantiate(protoGuid, Name="실린더 instance2", asActive=false)
        let edSysCyl3 = edProject.Instantiate(protoGuid, Name="실린더 instance3", asActive=false)

        let curernt = now()
        let rtProject = edProject.Replicate()
        rtProject |> _.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> z.DateTime <- curernt)
        let json =
            rtProject
            |> validateRuntime
            |> _.ToJson(Path.Combine(testDataDir(), "dssystem-with-cylinder.json"))

        let rtProject2 = RtProject.FromJson json
        rtProject2 |> _.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> z.DateTime <- curernt)
        // 설계 문서 위치에 drop
        let json2 =
            rtProject2
            |> validateRuntime
            |> _.ToJson(Path.Combine(testDataDir(), "dssystem-with-cylinder2.json"))

        json === json2

        let dbPath = Path.Combine(testDataDir(), "dssystem-with-cylinder.sqlite3")
        let dbApi = dbPath |> createSqliteDbApi
        dbApi.With(fun (conn, tr) -> conn.Execute("DELETE FROM project") |> ignore) |> ignore

        rtProject.CommitToDB(dbApi)

        File.Copy(dbPath, Path.Combine(specDir, "dssystem-with-cylinder.sqlite3"), overwrite=true)


        let emptyCheckTables = [ Tn.Project; Tn.MapProject2System; Tn.System;
                            Tn.Flow; Tn.Work; Tn.Call; Tn.ApiCall; Tn.ApiDef ]

        (* FK test *)
        let mutable checkDone = false
        (fun () ->
            dbApi.With(fun (conn, tr) ->
                let affected = conn.Execute($"DELETE FROM {Tn.Project}")

                // 현재 transaction 내에서는 System 갯수가 0 이 되어야 한다.
                for t in emptyCheckTables do
                    tracefn $"Checking table {t} for empty"
                    let count = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {t}")
                    if count <> 0 then
                        tracefn $"What's up with {t}??"

                    count === 0


                checkDone <- true
                // transaction 강제 rollback 유도
                failwith "Aborting")
        ) |> ShouldFailWithSubstringT "Aborting"
        checkDone === true


        checkDone <- false
        (* FK test *)
        // project id = 1 을 복사해서 project id = 2 로 만듦
        // mapping id = 1 을 복사해서 mapping id = 2 로 만듦 (systemId 는 1 에 대해서 두 project 가 참조 중)
        // project id = 1 을 삭제했을 때, project id = 2 에 대한 모든 것과 mapping id = 2 은 남아 있어야 함
        (fun () ->
            dbApi.With(fun (conn, tr) ->
                let projId = conn.ExecuteScalar<int>($"SELECT id FROM {Tn.Project} WHERE id = (SELECT MIN(id) FROM {Tn.Project})")
                let newProjId =
                    conn.Insert($"""INSERT INTO {Tn.Project} (guid, dateTime, name, author, version, description)
                                    SELECT
                                        guid || '_copy',          -- guid 중복 방지 (예: "_copy" 붙임)
                                        dateTime,
                                        name || ' 복사본',
                                        author,
                                        version,
                                        description
                                    FROM project
                                    WHERE id = {projId}
                                    ;""", null, tr)
                //let newProjId = conn.ExecuteScalar<int>($"SELECT id FROM {Tn.Project} WHERE id = (SELECT MIN(id) FROM {Tn.Project})")

                let mapId =
                    conn.ExecuteScalar<int>(
                        $"""SELECT id FROM {Tn.MapProject2System}
                            WHERE id = (SELECT MIN(id) FROM {Tn.MapProject2System})""", transaction=tr)

                conn.Execute($"""   INSERT INTO {Tn.MapProject2System} (guid, projectId, systemId, isActive, loadedName)
                                    SELECT
                                        guid || '_copy',          -- UNIQUE 제약을 피하기 위해 guid 수정
                                        {newProjId},              -- 새로 만든 project id
                                        systemId,
                                        isActive,
                                        loadedName
                                    FROM mapProject2System
                                    WHERE id = {mapId}
                                    ;
                                    """, transaction=tr) |> ignore

                conn.Execute($"DELETE FROM {Tn.Project} WHERE id = 1", transaction=tr) |> ignore

                // 현재 transaction 내에서는 임의 추가한 porject 와 System 이 하나씩 남아 있어야 한다.
                for t in emptyCheckTables do
                    tracefn $"Checking table {t} for empty"
                    conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {t}", transaction=tr) =!= 0

                checkDone <- true
                // transaction 강제 rollback 유도
                failwith "Aborting"
            )
        ) |> ShouldFailWithSubstringT "Aborting"

        checkDone === true
        noop()

    [<Test>]
    let ``비교`` () =
        let dsProject = edProject |> validateRuntime


        do
            let dsProject2 = dsProject.Replicate() |> validateRuntime
            dsProject.IsEqual dsProject2 === true

            // dsProject2 의 work 이름을 변경하고 비교
            let w = dsProject.Systems[0].Works[0]
            let w2 = dsProject2.Systems[0].Works[0]
            w2.Name <- "ChangedWorkName"
            w2.Motion <- "MyMotion"
            w2.Parameter <- """{"Age": 3}"""
            let diff = dsProject.ComputeDiff(dsProject2) |> toList
            diff.Length === 3
            diff |> contains (Diff("Name",      w, w2)) === true
            diff |> contains (Diff("Parameter", w, w2)) === true
            diff |> contains (Diff("Motion",    w, w2)) === true

        do
            // 추가한 개체 (arrow) detect 가능해야 한다.
            let dsProject2 = dsProject.Replicate() |> validateRuntime
            let w = dsProject2.Systems[0].Works[0]
            let c1, c2 = w.Calls[0], w.Calls[0]
            let arrow = RtArrowBetweenCalls(c1, c2, DbArrowType.Start)
            w.AddArrows([arrow])

            let f = dsProject2.Systems[0].Flows[0]
            let button = RtButton(Name="NewButton")
            f.AddButtons( [ button ])
            let diff = dsProject.ComputeDiff(dsProject2) |> toList
            diff |> contains (RightOnly(arrow)) === true
            diff |> contains (RightOnly(button)) === true
            noop()

        do
            // 삭제한 개체 (arrow) detect 가능해야 한다.
            let dsProject2 = dsProject.Replicate() |> validateRuntime
            let w = dsProject2.Systems[0].Works[0]
            let arrowRight = w.Arrows.Head
            let arrowLeft = dsProject.Systems[0].Works[0].Arrows.Head
            w.RemoveArrows([arrowRight])
            let diff = dsProject.ComputeDiff(dsProject2) |> toList
            diff |> contains (LeftOnly arrowLeft) === true
            noop()
        noop()

    [<Test>]
    let ``복제 비교`` () =
        let dsProject = edProject.Replicate() |> validateRuntime
        let diff = dsProject.ComputeDiff edProject
        edProject.IsEqual dsProject === true

    [<Test>]
    let ``복사 비교`` () =
        (* Project 복사:
            - Active/Passive system 들의 Guid 변경되어야 함.
            - Parent 및 OwnerSystem member 변경되어야 함.
        *)
        let dsProject = edProject.Duplicate($"CC_{edProject.Name}") |> validateRuntime
        edProject.IsEqual dsProject === false

        let diff = edProject.ComputeDiff(dsProject) |> toList
        diff.Length === 5
        diff |> contains (Diff ("Guid", edProject, dsProject)) === true
        diff |> contains (Diff ("DateTime", edProject, dsProject)) === true
        diff |> contains (Diff ("Name", edProject, dsProject)) === true
        diff |> contains (LeftOnly edProject.Systems[0]) === true
        diff |> contains (RightOnly dsProject.Systems[0]) === true

        do
            (* Project 하부의 System 은 구조적으로는 동일해야 함.
                - 복사로 인해 System 의 Guid 는 새로 생성, IRI 는 초기화되어 다름
                - 시스템 하부에 존재하는 Work, Flow, ApiDef, ApiCall 은 모두 다른 객체로 생성.
            *)
            let src = edProject.Systems[0]
            let cc = dsProject.Systems[0]
            diff |> contains (LeftOnly src) === true
            diff |> contains (RightOnly cc) === true
            let diff = src.ComputeDiff cc |> toArray
            diff |> contains (Diff ("Guid", src, cc)) === true
            diff
            |> forall(fun d ->
                match d with
                | Diff("Guid", x, y) -> verify (x :? RtSystem && y :? RtSystem); true
                | Diff("IRI",  x, y) -> verify (x :? RtSystem && y :? RtSystem); true
                | Diff("DateTime",  x, y) -> verify (x :? RtSystem && y :? RtSystem); true
                | Diff("Parent",  x, y) -> verify (x :? RtSystem && y :? RtSystem); true
                | (   LeftOnly (:? RtFlow)
                    | LeftOnly (:? RtWork)
                    | LeftOnly (:? RtArrowBetweenWorks)
                    | LeftOnly (:? RtApiDef)
                    | LeftOnly (:? RtApiCall)
                    | RightOnly (:? RtFlow)
                    | RightOnly (:? RtWork)
                    | RightOnly (:? RtArrowBetweenWorks)
                    | RightOnly (:? RtApiDef)
                    | RightOnly (:? RtApiCall) ) -> true
                | _ -> false
            ) === true

        ()


    let guid = Guid.Parse "42dad0ec-6441-47b7-829e-1487e1c89360"
    type Gender = Male | Female
    let jsonb = {| Name = "Kwak"; Gender = Male; Age=30 |} |> JsonConvert.SerializeObject
    let testRowFull = RtTypeTest(OptionGuid=Some guid, NullableGuid=Nullable<Guid>(guid), OptionInt=Some 1, NullableInt=Nullable 1, Jsonb=jsonb)
    let testRowEmpty = RtTypeTest()

    [<Test>]
    let ``Sqlite Dapper test`` () =
        let dbApi =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> createSqliteDbApi
        dbApi.With(fun (conn, tr) ->
            conn.Execute($"""INSERT INTO {Tn.TypeTest}
                                   (optionGuid,  nullableGuid, optionInt,   nullableInt,  jsonb,  dateTime)
                            VALUES (@OptionGuid, @NullableGuid, @OptionInt, @NullableInt, @Jsonb, @DateTime)""", [testRowFull; testRowEmpty]) )
        |> ignore

    [<Test>]
    let ``PGSql Dapper test`` () =
        pgsqlDbApi().With(fun (conn, tr) ->
            conn.Execute($"""INSERT INTO {Tn.TypeTest}
                                   (optionGuid,  nullableGuid, optionInt,   nullableInt,  jsonb,         dateTime)
                            VALUES (@OptionGuid, @NullableGuid, @OptionInt, @NullableInt, @Jsonb::jsonb, @DateTime)""", [testRowFull; testRowEmpty]) )
        |> ignore


    [<Test>]
    let ``X PGSql DB 수정 commit`` () =
        let dsProject = edProject.Replicate() |> validateRuntime
        dsProject.Systems[0].Works[0].Name <- "ModifiedWorkName"
        pgsqlDbApi() |> dsProject.CommitToDB

    [<Test>]
    let ``X DB (PGSql): System 수정 commit`` () =
        let json = Path.Combine(testDataDir(), "dssystem.json") |> File.ReadAllText
        let dsProject = RtProject.FromJson json |> validateRuntime
        let dsSystem = dsProject.Systems[0]
        pgsqlDbApi() |> dsSystem.CommitToDB

