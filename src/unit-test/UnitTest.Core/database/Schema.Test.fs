namespace T

open System
open System.Linq
open System.IO

open NUnit.Framework
open Dapper

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open Newtonsoft.Json




[<AutoOpen>]
module Schema =
    let testDataDir() = Path.Combine(__SOURCE_DIRECTORY__, @"..\test-data") |> Path.GetFullPath
    let dbFilePath = Path.Combine(testDataDir(), "test.sqlite3")
    let path2ConnectionString (dbFilePath:string) =
        $"Data Source='{dbFilePath}';Version=3;BusyTimeout=20000"
        |> tee(tracefn "ConnectionString='%s'")

    [<SetUpFixture>]
    type GlobalTestSetup() = // Cleanup, GlobalSetup
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

    // sqlite <--> pgsql 전환시마다 dbApi 를 새로 생성해야 함.  SqlMapper 가 다름.
    let pgsqlDbApi() =
        "Host=localhost;Database=dstest;Username=dstest;Password=dstest;Search Path=dstest"
        |> DbProvider.Postgres
        |> AppDbApi


    let createSqliteDbApi (path:string) =
        path
        |> path2ConnectionString
        |> DbProvider.Sqlite
        |> AppDbApi

    // sqlite <--> pgsql 전환시마다 dbApi 를 새로 생성해야 함.  SqlMapper 가 다름.
    let inline sqliteDbApi() =
        Path.Combine(testDataDir(), $"{getTopFuncName()}.sqlite3")
        |> createSqliteDbApi

    let inline jsonPath() =
        Path.Combine(testDataDir(), $"{getTopFuncName()}.json")
        |> createSqliteDbApi


    let guid = Guid.Parse "42dad0ec-6441-47b7-829e-1487e1c89360"
    type Gender = Male | Female
    let jsonb = {| Name = "Kwak"; Gender = Male; Age=30 |} |> JsonConvert.SerializeObject
    let testRowFull = RtTypeTest(OptionGuid=Some guid, NullableGuid=Nullable<Guid>(guid), OptionInt=Some 1, NullableInt=Nullable 1, Jsonb=jsonb, DateTime=DateTime.MaxValue)
    let testRowEmpty = RtTypeTest()

    type Row = {
        mutable Id: Nullable<int>
        Name: string
        SystemId: int
        Guid: string
    }

    let ``basic_test`` (dbApi:AppDbApi) =
        createEditableProject()

        let xxx = rtProject
        let dsProject = rtProject.Replicate() |> validateRuntime
        //let json = dsProject.ToJson(Path.Combine(testDataDir(), "dssystem.json"))

        let yyy = rtProject
        let rtObjs = dsProject.EnumerateRtObjects()
        for rtObj in rtObjs do
            tracefn $"{rtObj.GetType().Name}: {rtObj.GetFQDN()}"

        let rtObjDic = rtObjs.ToDictionary(_.Guid, fun z -> z :> Unique) |> DuplicateBag
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


        dbApi.With(fun (conn, tr) ->
            conn.Execute($"DELETE FROM {Tn.Project}", null, tr) |> ignore
            dsProject.RTryCommitToDB(dbApi) ==== Ok Inserted

            dsProject.EnumerateRtObjects()
            |> iter (fun dsobj ->
                if dsobj :? BLCABase then
                    ()
                else
                    if dsobj.Id.IsNone then
                        noop()
                    // DB 삽입 후이므로 Id 가 Some 이어야 함
                    dsobj.Id.IsSome === true
            )

            let edProject = rtProject
            let diffProj = edProject.ComputeDiff dsProject |> toArray
            let diffSys = edProject.Systems[0].ComputeDiff dsProject.Systems[0] |> toArray

            let dsSystem = dsProject.Systems |> find(fun s -> s.Name = "MainSystem")
            let dsFlow = dsSystem.Flows[0]
            let yyy = rtProject
            let rtSystem = rtProject.Systems |> find(fun s -> s.Name = "MainSystem")
            let rtFlow = rtSystem.Flows[0]
            dsFlow.Guid === rtFlow.Guid
            dsFlow.Works.Length === 3
            dsSystem.Works.Length === 3
            let dsWork1 = dsSystem.Works |> Seq.find(fun w -> w.Name = "BoundedWork1")
            let dsWork2 = dsSystem.Works |> Seq.find(fun w -> w.Name = "BoundedWork2")
            let dsWork3 = dsSystem.Works |> Seq.find(fun w -> w.Name = "FreeWork1")
            dsWork1.Guid === rtWork1.Guid
            dsWork2.Guid === rtWork2.Guid
            dsWork3.Guid === rtWork3.Guid
            dsWork1.Name === rtWork1.Name
            dsWork2.Name === rtWork2.Name
            dsWork3.Name === rtWork3.Name

            let flowWorks = dsFlow.Works
            flowWorks.Length === 3
            flowWorks[0].Guid === rtWork1.Guid
            flowWorks[1].Guid === rtWork2.Guid
            dsFlow.Name === rtFlow.Name

            rtWork1.Calls.Length === 2
            rtWork2.Calls.Length === 2
            let dsCall1 = dsWork1.Calls[0]
            let dsCall2 = dsWork2.Calls[0]
            dsCall1.Guid === rtCall1a.Guid
            dsCall2.Guid === rtCall2a.Guid
            dsCall1.RawParent |->_.Guid === Some dsWork1.Guid
            dsCall2.RawParent |->_.Guid === Some dsWork2.Guid

            dsCall1.Name === rtCall1a.Name
            dsCall2.Name === rtCall2a.Name

            let json = dsProject.ToJson(Path.Combine(testDataDir(), "dssystem.json"))
            tracefn $"---------------------- json:\r\n{json}"
            let dsProject2 = Project.FromJson json
            validateRuntime dsProject2 |> ignore
            let json2 = dsProject2.ToJson(Path.Combine(testDataDir(), "json-deserialized-dssystem.json"))

            json === json2

            let r = dsProject2.RTryCommitToDB(dbApi)
            match r with
            | Ok (Updated _) -> ()
            | _ -> fail()

            dsProject2.Id.IsSome === true
            let refreshed = Project.CheckoutFromDB(dsProject2.Id.Value, dbApi)
            let refreshedDiffs = refreshed.ComputeDiff dsProject2 |> toArray

            let diffFields = refreshedDiffs |> toList >>= _.GetPropertiesDiffFields() |-> fst
            let atMostDiffDateTime = diffFields |> forall((=) "Properties::DateTime")
            ((refreshedDiffs |> Array.isEmpty) || atMostDiffDateTime)   === true


            dsProject2.ToJson(Path.Combine(testDataDir(), "db-inserted-dssystem.json")) |> ignore
            validateRuntime dsProject2 |> ignore

            let dsProject3 = dsProject2.Replicate()
            validateRuntime dsProject3 |> ignore

            dsProject3.ToJson(Path.Combine(testDataDir(), "replica-of-db-inserted-dssystem.json")) |> ignore
            let r = dsProject3.RTryCommitToDB(dbApi)
            tracefn "Result3: %A" r
            //match r with
            //| Ok (Updated _) -> ()
            //| _ -> fail()

            dsProject3.Id.IsSome === true
            let refreshed3 = Project.CheckoutFromDB(dsProject3.Id.Value, dbApi)
            let refreshedDiffs3 = refreshed3.ComputeDiff dsProject3 |> toArray
            refreshedDiffs3 |> Array.isEmpty ==== true

            do
                let dsProj = dsProject2.Duplicate()
                dsProj.Name <- $"Duplicate of {dsProj.Name}"
                validateRuntime dsProj |> ignore
                dsProj.ToJson(Path.Combine(testDataDir(), "duplicate-of-db-inserted-dssystem.json")) |> ignore
                dsProj.RTryCommitToDB(dbApi)
                |> tee(tracefn "Result: %A")
                ==== Ok Inserted


            let dsProject4 =
                let dsSystem4 = dsProject3.Systems[0].Duplicate()
                validateRuntime dsSystem4 |> ignore
                dsSystem4.Name <- "DuplicatedSystem"
                dsProject3.Duplicate()
                |> tee(fun z ->
                    z.Name <- $"{z.Name}4"
                    z.EnumerateRtObjects().OfType<ApiCall>().First().ValueSpec <- Some <| Single 3.14156952
                    z.AddPassiveSystem dsSystem4)

            validateRuntime dsProject4 |> ignore
            dsProject4.RTryCommitToDB(dbApi)
            |> tee(tracefn "Result4: %A")
            ==== Ok Inserted)

        ()


    type PGSqlTest() =     // ``[PGSql] EdObject - DsObject - OrmObject - DB insert - JSON test``, ``PGSql Dapper test``
        [<Test>]
        member x.``[PGSql] EdObject - DsObject - OrmObject - DB insert - JSON test`` () =
            pgsqlDbApi() |> basic_test

        [<Test>]
        member x.``PGSql Dapper test`` () =
            pgsqlDbApi().With(fun (conn, tr) ->
                conn.Execute($"""INSERT INTO {Tn.TypeTest}
                                       (optionGuid,  nullableGuid, optionInt,   nullableInt,  jsonb,         dateTime)
                                VALUES (@OptionGuid, @NullableGuid, @OptionInt, @NullableInt, @Jsonb::jsonb, @DateTime)""", [testRowFull; testRowEmpty], tr) )
            |> ignore


    type SQLiteTest() = // dbCreateTest, dbReadTest, upsertTest
        [<Test>]
        member x.dbCreateTest() =
            use conn = sqliteDbApi().CreateConnection()
            ()

        // DB 가 생성되어 있고, "MainProject" 가 저장되어 있어야 함.   다른 test 수행 이후에 실행되면 OK
        [<Test>]
        member x.dbReadTest() =
            Ev2.Core.FS.ModuleInitializer.Initialize(null)
            DcLogger.EnableTrace <- true
            //DapperTypeHandler.AddHandlers()
            //checkHandlers()
            AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)
            Directory.CreateDirectory(testDataDir()) |> ignore


            let dbApi = sqliteDbApi()
            let dsProject = Project.CheckoutFromDB("MainProject", dbApi)
            use conn = dbApi.CreateConnection()
            ()


        [<Test>]
        member x.upsertTest() =
            use conn = sqliteDbApi().CreateConnection()
            let nullTransaction = null
            tracefn $"SQL version: {conn.GetVersionString()}"
            let r1 = conn.Upsert(
                nullTransaction, "flow",
                [|
                   "name", box "Alice"
                   "systemId", 1
                   "guid", "b544dcdc-ca2f-43db-9d90-93843269bd3f"
                |],
                [|"id"|]
            )

            let row = {| id = 1; name = "Bob"; systemId = 1; guid = "b544dcdc-ca2f-43db-9d90-93843269bd3f" |}
            let r2 = conn.Upsert(
                nullTransaction, "flow", row, [ "name"; "systemId"; "guid" ],
                [|"id"|]
            )

            let row = { Id = Nullable(); Name = "Tom"; SystemId = 1; Guid = guid2str <| Guid.NewGuid() }
            let r3 = conn.Upsert(
                nullTransaction, "flow", row, [ "Name"; "SystemId"; "Guid" ],
                [|"id"|],
                onInserted = fun id -> row.Id <- Nullable id
            )
            ()



        [<Test>]
        member x.``insert test`` () =
            let dbApi = sqliteDbApi()
            use conn = dbApi.CreateConnection()
            dbApi.With(fun (conn, tr) ->
                conn.Execute($"DELETE FROM {Tn.Project};", null, tr)
            ) |> ignore


            dbApi.VendorDB.TruncateAllTables(conn)
            let newGuid() = Guid.NewGuid().ToString()

            let ver = Version().ToString()
            // project 삽입
            let prjGuid = newGuid()
            let prjName = "MainProject"
            let prjId = conn.Insert(
                            $"INSERT INTO {Tn.Project} (guid, name, properties) VALUES (@Guid, @Name, @Properties)",
                            {| Guid=prjGuid; Name=prjName; Properties="{}" |})


            // system 삽입
            let sysGuid = newGuid()
            let sysName = "MainSystem"
            let sysProperties = DsSystemProperties.Create()
            sysProperties.Author <- Environment.UserName
            sysProperties.LangVersion <- Version.Parse(ver)
            sysProperties.EngineVersion <- Version.Parse(ver)
            let systemId = conn.Insert(
                            $"INSERT INTO {Tn.System} (guid, name, iri, properties) VALUES (@Guid, @Name, @IRI, @Properties)",
                            {| Guid=sysGuid; Name=sysName; IRI="http://dualsoft.com/unique/12345"; Properties=sysProperties.ToJson()|})

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


            let systems = conn.QueryRows<ORMSystem>(Tn.System) |> List.ofSeq
            let flows   = conn.QueryRows<ORMFlow>  (Tn.Flow)   |> List.ofSeq
            let works   = conn.QueryRows<ORMWork>  (Tn.Work)   |> List.ofSeq
            let calls   = conn.QueryRows<ORMCall>  (Tn.Call)   |> List.ofSeq

            systems.Length === 1
            systems[0].Name === sysName

            ()


        [<Test>]
        member x.``[SQLite] EdObject - DsObject - OrmObject - DB insert - JSON test`` () =
            sqliteDbApi() |> basic_test


        [<Test>]
        member x.``JSON - DsObject - DB update test`` () =
            //let jsonPath = Path.Combine(testDataDir(), "db-inserted-dssystem.json")
            let jsonPath = Path.Combine(testDataDir(), "dssystem.json")
            if not (File.Exists jsonPath) then
                rtProject.ToJson(jsonPath) |> ignore

            let json = File.ReadAllText(jsonPath)
            let dsProject0 = NjProject.FromJson json


            let dsProject1 = Project.FromJson json |> validateRuntime
            noop()
            let dsProject2 = dsProject1 |> _.Duplicate()
            let sys = dsProject2.ActiveSystems[0]
            sys.Flows.Length === 1
            let flow = sys.Flows[0]
            dsProject2.Name <- "UpdatedProject"

            let dbApi = sqliteDbApi()

            dbApi.With(fun (conn, tr) ->
                conn.Execute($"DELETE FROM {Tn.Project} where name = @Name", {| Name=dsProject2.Name|}, tr) |> ignore

                dsProject2.RTryCommitToDB(dbApi).IsOk === true)


        [<Test>]
        member x.``DB Delete preview test`` () =
            let projectId = 1

            sqliteDbApi().With(fun (conn, tr) ->
                let result =
                    conn.GuessCascadeDeleteAffected(Tn.System, 1)
                noop()
            )


        (*
            Project 에 cylinder subsystem 을 reference (loaded system) 으로 추가하는 예제
        *)
        [<NonParallelizable>] // 클래스 단위로 병렬 실행 금지
        [<Test>]
        member x.``Cylinder 추가 test`` () =
            createEditableProject()
            createEditableSystemCylinder()
            let originalEdProject = rtProject
            let edProject = rtProject.Replicate() |> validateRuntime
            let rtCyl = rtProject.Systems |> find(fun s -> s.Name = "Cylinder")

            let edSysCyl1 = rtCyl.Duplicate(Name="실린더 instance1")
            let edSysCyl2 = rtCyl.Duplicate(Name="실린더 instance2")
            let edSysCyl3 = rtCyl.Duplicate(Name="실린더 instance3")
            [edSysCyl1; edSysCyl2; edSysCyl3] |> iter edProject.AddPassiveSystem

            edProject |> validateRuntime |> ignore
            let projJson = edProject.ToJson(Path.Combine(testDataDir(), "project.json"))

            let curernt = now()
            let rtProject = edProject.Replicate() |> validateRuntime
            rtProject |> _.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> fwdSetDateTime z curernt)
            let json =
                rtProject
                |> validateRuntime
                |> _.ToJson(Path.Combine(testDataDir(), "dssystem-with-cylinder.json"))

            let rtProject2 = Project.FromJson json
            rtProject2 |> _.EnumerateRtObjects().OfType<IWithDateTime>() |> iter (fun z -> fwdSetDateTime z curernt)

            // 디버깅: GUID 비교
            printfn "rtProject GUID: %A" rtProject.Guid
            printfn "rtProject2 GUID: %A" rtProject2.Guid
            printfn "rtProject Type: %s" (rtProject.GetType().FullName)
            printfn "rtProject2 Type: %s" (rtProject2.GetType().FullName)

            // 설계 문서 위치에 drop
            let json2 =
                rtProject2
                |> validateRuntime
                |> _.ToJson(Path.Combine(testDataDir(), "dssystem-with-cylinder2.json"))

            // 디버깅: JSON 차이 확인
            if json <> json2 then
                printfn "JSON mismatch!"
                let lines1 = json.Split('\n')
                let lines2 = json2.Split('\n')
                for i in 0 .. min lines1.Length lines2.Length - 1 do
                    if lines1.[i] <> lines2.[i] then
                        printfn "Line %d differs:" i
                        printfn "  json1: %s" lines1.[i]
                        printfn "  json2: %s" lines2.[i]
                        if i < 5 then () else failwith "First difference found"

            json === json2

            let dbApi = sqliteDbApi()
            dbApi.With(fun (conn, tr) ->
                conn.Execute("DELETE FROM project") |> ignore

                rtProject.RTryCommitToDB(dbApi).IsOk === true )


            dbApi.With(fun (conn, tr) ->
                conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Project}", null, tr) === 1
            ) |> ignore

            let emptyCheckTables = [ Tn.Project; Tn.MapProject2System; Tn.System;
                                Tn.Flow; Tn.Work; Tn.Call; Tn.ApiCall; Tn.ApiDef ]


            (* FK test *)
            let mutable checkDone = false
            (fun () ->
                dbApi.With(fun (conn, tr) ->
                    let affected = conn.Execute($"DELETE FROM {Tn.Project}", null, tr)

                    // 현재 transaction 내에서는 System 갯수가 0 이 되어야 한다.
                    for t in emptyCheckTables do
                        tracefn $"Checking table {t} for empty"
                        let count = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {t}", null, tr)
                        if count <> 0 then
                            tracefn $"What's up with {t}??"

                        count === 0


                    checkDone <- true
                    // transaction 강제 rollback 유도
                    failwith "Aborting")
            ) |> ShouldFailWithSubstringT "Aborting"
            checkDone === true

            dbApi.With(fun (conn, tr) ->
                // 위의 삭제 시도에서 abort 해서 rollback 되었으므로 기존 project 는 그대로 남아 있어야 한다.
                conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Project}", null, tr) === 1
            ) |> ignore



            checkDone <- false
            (* FK test *)
            // project id = 1 을 복사해서 project id = 2 로 만듦
            // mapping id = 1 을 복사해서 mapping id = 2 로 만듦 (systemId 는 1 에 대해서 두 project 가 참조 중)
            // project id = 1 을 삭제했을 때, project id = 2 에 대한 모든 것과 mapping id = 2 은 남아 있어야 함
            (fun () ->
                dbApi.With(fun (conn, tr) ->
                    let projId =
                        conn.ExecuteScalar<int>(
                            $"SELECT id FROM {Tn.Project} WHERE id = (SELECT MIN(id) FROM {Tn.Project})")

                    let newProjId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.Project} (guid, parameter, name, properties, aasXml)
                                SELECT
                                    '{newGuid() |> guid2str}',
                                    parameter,
                                    name || ' 복사본',
                                    properties,
                                    aasXml
                                FROM project
                                WHERE id = {projId}
                                ;""", null, tr)

                    let mapId =
                        conn.ExecuteScalar<int>( $"SELECT MIN(id) FROM {Tn.MapProject2System}", null, tr)

                    conn.Execute(
                        $"""INSERT INTO {Tn.MapProject2System} (guid, projectId, systemId, loadedName)
                            SELECT
                                '{newGuid() |> guid2str}',
                                {newProjId},              -- 새로 만든 project id
                                systemId,
                                loadedName
                            FROM mapProject2System
                            WHERE id = {mapId}
                            ;
                            """, null, tr) |> ignore

                    let dbProjs1 = conn.Query<ORMProject>($"SELECT * FROM {Tn.Project}", null, tr) |> toArray
                    let numProjects1 = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Project}", null, tr)
                    let numMap1 = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.MapProject2System}", null, tr)
                    numProjects1 === 2
                    numMap1 === 6

                    conn.Execute($"DELETE FROM {Tn.Project} WHERE id = {newProjId}", null, tr) |> ignore

                    let dbProjs2 = conn.Query<ORMProject>($"SELECT * FROM {Tn.Project}", null, tr) |> toArray
                    let numProjects2 = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Project}", null, tr)
                    let numMap2 = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.MapProject2System}", null, tr)

                    // 현재 transaction 내에서는 임의 추가한 porject 와 System 이 하나씩 남아 있어야 한다.
                    for t in emptyCheckTables.Except([Tn.System; Tn.MapProject2System]) do
                        tracefn $"Checking table {t} for empty"
                        let count = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {t}", null, tr)
                        count =!= 0

                    checkDone <- true
                    // transaction 강제 rollback 유도
                    failwith "Aborting"
                )
            ) |> ShouldFailWithSubstringT "Aborting"

            checkDone === true
            noop()




        [<Test>]
        member x.``설계 문서 위치에 샘플 생성`` () =
            let dsProject = rtProject |> validateRuntime
            let jsonPath = Path.Combine(testDataDir(), $"{getTopFuncName()}.json")
            dsProject.ToJson jsonPath |> ignore

            let dbPath = Path.Combine(testDataDir(), $"{getTopFuncName()}.sqlite3")
            File.Delete(dbPath)
            let dbApi = sqliteDbApi()
            match dsProject.RTryCommitToDB(dbApi) with
            | Ok _ ->
                // 설계 문서 위치에 drop
                File.Delete(Path.Combine(specDir, "dssystem.sqlite3")) |> ignore
                File.Delete(Path.Combine(specDir, "dssystem.json")) |> ignore
                let targetDbPath = Path.Combine(specDir, "dssystem.sqlite3") |> Path.GetFullPath
                File.Copy(dbPath, targetDbPath)
                File.Copy(jsonPath, Path.Combine(specDir, "dssystem.json"))
            | Error err ->
                failwith err

        [<Test>]
        member x.``[Sqlite] DB System 수정 commit`` () =
            let xxx = rtProject
            let dsProject = rtProject.Replicate() |> validateRuntime
            let diffs = dsProject.ComputeDiff rtProject |> toArray

            let dbApi = sqliteDbApi()
            dbApi.With(fun (conn, tr) ->
                conn.Execute($"DELETE FROM {Tn.Project}", null, tr) |> ignore

                dsProject.RTryCommitToDB(dbApi) ==== Ok Inserted

                let jsonPath = Path.Combine(testDataDir(), "dssystem.json")
                let json = dsProject.ToJson(jsonPath)

                let dsProject2 = Project.FromJson json |> validateRuntime
                let diffs2 = dsProject2.ComputeDiff dsProject |> toArray

                let dsSystem = dsProject2.Systems[0]
                dsSystem.RTryCommitToDB dbApi |> ignore

                dsSystem.Id.IsSome === true
                let refreshedSystem = DsSystem.CheckoutFromDB(dsSystem.Id.Value, dbApi)
                let refreshedSystemDiffs = refreshedSystem.ComputeDiff dsSystem |> toArray
                refreshedSystemDiffs
                |> Array.forall (function Diff("Parent", _, _, _) -> true | _ -> false)
                ==== true
            ) |> ignore

        [<Test>]
        member x.``[Sqlite] DB Project 수정 commit`` () =
            let getMainSystem(prj:Project) = prj.Systems |> find (fun s -> s.Name = "MainSystem")
            let xxx = rtProject
            let dsProject = rtProject.Replicate() |> validateRuntime
            let dbApi = sqliteDbApi()
            dbApi.With(fun (conn, tr) ->

                // 깨끗하게 삭제 후, 1번은 insert 성공, 2번째는 NoChange 가 나와야 함
                conn.Execute($"DELETE FROM {Tn.Project} WHERE name=@Name", dsProject, tr) |> ignore

                do
                    dsProject.RTryCommitToDB(dbApi)
                    |> tee (tracefn "Result2: %A")
                    ==== Ok Inserted

                do
                    let r = dsProject.RTryCommitToDB(dbApi)
                    tracefn "Result3: %A" r
                    match r with
                    | Ok NoChange
                    | Ok (Updated _) -> ()
                    | _ -> fail()

                    dsProject.Id.IsSome === true
                    let refreshed = Project.CheckoutFromDB(dsProject.Id.Value, dbApi)
                    let refreshedDiffs = refreshed.ComputeDiff dsProject |> toArray
                    refreshedDiffs |> Array.isEmpty ==== true

                let sys = getMainSystem(dsProject)
                let w = sys.Works[0]
                do
                    // 수정 후, 다시 commit 하면 Updated 가 나와야 함.
                    // Diff 결과는 db 의  work 와 update 한 work 의 이름만 달라야 함
                    w.Name <- "ModifiedWorkName"
                    match dsProject.RTryCommitToDB(dbApi) with
                    | Ok (Updated diffs) ->
                        let diffFields = diffs |> toList >>= _.GetPropertiesDiffFields()
                        diffFields |-> fst |> sort |> distinct === [ "Name" ]
                        let diffName =
                            diffs
                            |> filter (function Diff("Name", _, _, _) -> true | _ -> false)
                            |> head
                        match diffName with
                        | Diff("Name", dbW, newW, _) when newW = w -> ()
                        | _ -> fail()
                    | _ -> fail()

                do
                    // 삭제 후, 다시 commit 하면 LeftOnly 가 나와야 함.  (삭제 이전에 Db, 즉 left 에만 존재했었다는 정보 표현)
                    // 삭제 후 work 및 work 간 arrow 갯수는 1 씩 감소해야 함.  (cascade delete)
                    let nw = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Work}",      null, tr)
                    let na = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.ArrowWork}", null, tr)

                    let sysArrow0 = sys.Arrows[0]
                    sys.RemoveWorks([w])
                    match dsProject.RTryCommitToDB(dbApi) with
                    | Ok (Updated diffs) ->
                        diffs.Length.IsOneOf(2, 3, 4) === true
                        match diffs[0] with
                        | LeftOnly dbW when dbW.GetGuid() = w.Guid -> ()
                        | _ -> fail()
                        match diffs[1] with
                        | LeftOnly dbA when dbA.GetGuid() = sysArrow0.Guid -> ()
                        | _ -> fail()

                        let tail = diffs |> Array.skip 2
                        match tail with
                        | [||] -> ()
                        | [|Diff("Properties", dbSys, newSys, _)|] when dbSys.GetGuid() = newSys.GetGuid() -> ()
                        | [|Diff("Properties", dbSys, newSys, _); Diff("Properties", dbProj, newProj, _)|]
                            when dbSys.GetGuid() = newSys.GetGuid() && dbProj.GetGuid() = newProj.GetGuid() -> ()
                        | _ -> fail()
                    | _ -> fail()

                    conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Work}",      null, tr) === nw - 1
                    conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.ArrowWork}", null, tr) === na - 1

                do
                    // work 추가하면 work 갯수만 늘어남.
                    let nw = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Work}",      null, tr)
                    let na = conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.ArrowWork}", null, tr)

                    sys.AddWorks([w])
                    match dsProject.RTryCommitToDB(dbApi) with
                    | Ok (Updated diffs) ->
                        diffs |> contains (RightOnly w) === true
                        //diffs.Length === 2
                    | Error err ->
                        failwith $"ERROR: {err}"
                    | _ -> fail()

                    conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.Work}",      null, tr) === nw + 1
                    conn.ExecuteScalar<int>($"SELECT COUNT(*) FROM {Tn.ArrowWork}", null, tr) === na + 0  // no change
            )



    type IndependantTest() =     // ``비교``, ``복제 비교``, ``복사 비교``, ``Sqlite Dapper test``
        [<Test>]
        member x.``비교`` () =
            let xxx = rtProject
            let dsProject = rtProject |> validateRuntime

            let getMainSystem(prj:Project) = prj.Systems |> find (fun s -> s.Name = "MainSystem")
            let sys = getMainSystem(dsProject)

            do
                let dsProject2 = dsProject.Replicate() |> validateRuntime
                let sys2 = getMainSystem(dsProject2)
                dsProject.IsEqual dsProject2 === true

                // dsProject2 의 work 이름을 변경하고 비교
                let w = sys.Works[0]
                let w2 = sys2.Works[0]
                w2.Name <- "ChangedWorkName"
                w2.Properties.Motion <- "MyMotion"
                w2.Parameter <- """{"Age": 3}"""
                let diffs = dsProject.ComputeDiff(dsProject2) |> toList
                diffs.Length === 3
                diffs |> contains (Diff("Name",      w, w2, nullUpdateSql)) === true
                diffs |> contains (Diff("Parameter", w, w2, nullUpdateSql)) === true
                diffs |> exists (fun diff ->
                    match diff with
                    | Diff("Properties", left, right, _) -> Object.ReferenceEquals(left, w) && Object.ReferenceEquals(right, w2)
                    | _ -> false) === true

            do
                // 추가한 개체 (arrow) detect 가능해야 한다.
                let dsProject2 = dsProject.Replicate() |> validateRuntime
                let sys2 = getMainSystem(dsProject2)
                let w = sys2.Works[0]
                let c1, c2 = w.Calls[0], w.Calls[0]
                let arrow = ArrowBetweenCalls.Create(c1, c2, DbArrowType.Start)
                w.AddArrows([arrow])

                let f = sys2.Flows[0]
                let button = new DsButton(Name="NewButton")
                button.Flows.Add f
                sys2.AddEntitiy button
                let diffs = dsProject.ComputeDiff(dsProject2) |> toList
                diffs |> contains (RightOnly(arrow)) === true
                diffs |> contains (RightOnly(button)) === true
                noop()

            do
                // 삭제한 개체 (arrow) detect 가능해야 한다.
                let dsProject2 = dsProject.Replicate() |> validateRuntime
                let sys2 = getMainSystem(dsProject2)
                let w = sys2.Works[0]
                let arrowRight = w.Arrows.Head
                let arrowLeft = sys.Works[0].Arrows.Head
                w.RemoveArrows([arrowRight])
                let diffs = dsProject.ComputeDiff(dsProject2) |> toList
                diffs |> contains (LeftOnly arrowLeft) === true
                noop()
            noop()

        [<Test>]
        member x.``복제 비교`` () =
            let dsProject = rtProject.Replicate() |> validateRuntime
            let diffs = dsProject.ComputeDiff rtProject |> toArray
            printfn "Differences found: %d" (diffs |> Seq.length)
            diffs |> Seq.iteri (fun i diff -> logDebug "[%d] %A" i diff)
            rtProject.IsEqual dsProject === true

        [<Test>]
        member x.``복사 비교`` () =
            let xxx = rtProject
            (* Project 복사:
                - Active/Passive system 들의 Guid 변경되어야 함.
                - Parent 및 OwnerSystem member 변경되어야 함.
            *)
            let dsProject = rtProject.Duplicate() |> validateRuntime
            rtProject.IsEqual dsProject === false

            let diffs = rtProject.ComputeDiff(dsProject) |> toList
            (diffs.Length = 6 || diffs.Length = 7) === true
            diffs |> contains (Diff ("Guid", rtProject, dsProject, nullUpdateSql)) === true

            // 시간은 초 미만 절삭으로 동일하게 설정될 수도 있다.
            //diffs |> contains (Diff ("DateTime", rtProject, dsProject, null)) === true

            diffs |> contains (Diff ("Name", rtProject, dsProject, nullUpdateSql)) === true
            diffs |> contains (LeftOnly rtProject.Systems[0]) === true
            diffs |> contains (RightOnly dsProject.Systems[0]) === true

            do
                (* Project 하부의 System 은 구조적으로는 동일해야 함.
                    - 복사로 인해 System 의 Guid 는 새로 생성, IRI 는 초기화되어 다름
                    - 시스템 하부에 존재하는 Work, Flow, ApiDef, ApiCall 은 모두 다른 객체로 생성.
                *)
                let src = rtProject.Systems[0]
                let cc = dsProject.Systems[0]
                diffs |> contains (LeftOnly src) === true
                diffs |> contains (RightOnly cc) === true
                let diffs2 = src.ComputeDiff cc |> toArray
                diffs2 |> contains (Diff ("Guid", src, cc, nullUpdateSql)) === true
                diffs2
                |> forall(fun d ->
                    match d with
                    | Diff("Guid",       x, y, sql) -> verify (x :? DsSystem && y :? DsSystem); true
                    | Diff("IRI",        x, y, sql) -> verify (x :? DsSystem && y :? DsSystem); true
                    | Diff("Name",       x, y, sql) -> verify (x :? DsSystem && y :? DsSystem); true
                    | Diff("Properties", x, y, sql) -> verify (x :? DsSystem && y :? DsSystem); true
                    | Diff("Parent",     x, y, sql) -> verify (x :? DsSystem && y :? DsSystem); true
                    | (   LeftOnly (:? Flow)
                        | LeftOnly (:? Work)
                        | LeftOnly (:? ArrowBetweenWorks)
                        | LeftOnly (:? ApiDef)
                        | LeftOnly (:? ApiCall)
                        | RightOnly (:? Flow)
                        | RightOnly (:? Work)
                        | RightOnly (:? ArrowBetweenWorks)
                        | RightOnly (:? ApiDef)
                        | RightOnly (:? ApiCall) ) -> true
                    | _ ->
                        false
                ) === true

            ()

        [<Test>]
        member x.``Sqlite Dapper test`` () =
            let dbApi =
                Path.Combine(testDataDir(), "test_dssystem.sqlite3")
                |> createSqliteDbApi
            dbApi.With(fun (conn, tr) ->
                conn.Execute($"""INSERT INTO {Tn.TypeTest}
                                       (optionGuid,  nullableGuid, optionInt,   nullableInt,  jsonb,  dateTime)
                                VALUES (@OptionGuid, @NullableGuid, @OptionInt, @NullableInt, @Jsonb, @DateTime)""", [testRowFull; testRowEmpty], tr) )
            |> ignore
