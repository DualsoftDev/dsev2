namespace Ev2.Core.FS

open System
open System.Data
open System.Collections.Generic
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS

type DbObjectIdentifier =
    | ByGuid of Guid
    | ById of int
    | ByName of string

module internal Ds2SqliteImpl =
    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? ORMArrowBase as a -> a.Id <- Nullable id
            | :? ArrowBetweenCalls as a -> a.Id <- Some id
            | :? ArrowBetweenWorks as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"

    let system2SqliteHelper (s:DsSystem) (optProject:DsProject option) (cache:Dictionary<Guid, ORMUniq>) (conn:IDbConnection) (tr:IDbTransaction) =
        let ormSystem = s.ToORM(cache) :?> ORMSystem
        let sysId = conn.Insert($"""INSERT INTO {Tn.System} (guid, dateTime, name, author, langVersion, engineVersion, description, originGuid)
                        VALUES (@Guid, @DateTime, @Name, @Author, @LangVersion, @EngineVersion, @Description, @OriginGuid);""", ormSystem, tr)
        s.Id <- Some sysId
        cache[s.Guid].Id <- sysId

        match optProject with
        | Some proj ->
            // update projectSystemMap table
            let isActive = proj.ActiveSystems |> Seq.contains s
            let isPassive = proj.PassiveSystems |> Seq.contains s
            let projId = proj.Id.Value
            assert(isActive <> isPassive)   // XOR
            match conn.TryQuerySingle<ORMProjectSystemMap>($"SELECT * FROM {Tn.ProjectSystemMap} WHERE projectId = {projId} AND systemId = {sysId}") with
            | Some row when row.IsActive = isActive -> ()
            | Some row ->
                conn.Execute($"UPDATE {Tn.ProjectSystemMap} SET active = {isActive}, dateTime = @DateTime WHERE id = {row.Id}",
                            {| DateTime = now() |}) |> ignore
            | None ->
                let affectedRows = conn.Execute(
                        $"INSERT INTO {Tn.ProjectSystemMap} (projectId, systemId, isActive, guid, dateTime) VALUES (@ProjectId, @SystemId, @IsActive, @Guid, @DateTime)",
                        {| ProjectId = projId; SystemId = sysId; IsActive = isActive; Guid=Guid.NewGuid(); DateTime=now() |}, tr)
                ()
        | None -> ()

        // flows 삽입
        for f in s.Flows do
            let ormFlow = f.ToORM(cache) :?> ORMFlow
            ormFlow.SystemId <- Nullable sysId
            let flowId = conn.Insert($"INSERT INTO {Tn.Flow} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormFlow, tr)
            f.Id <- Some flowId
            ormFlow.Id <- flowId
            assert (cache[f.Guid] = ormFlow)

            // TODO : arrow 처리.  System, flow, work 공히...
            //f.Arrows

        // works, calls 삽입
        for w in s.Works do
            let ormWork = w.ToORM(cache) :?> ORMWork
            ormWork.SystemId <- Nullable sysId

            let workId = conn.Insert($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId, flowId) VALUES (@Guid, @DateTime, @Name, @SystemId, @FlowId);", ormWork, tr)
            w.Id <- Some workId
            ormWork.Id <- workId
            assert(cache[w.Guid] = ormWork)

            for c in w.Calls do
                let ormCall = c.ToORM(cache) :?> ORMCall
                ormCall.WorkId <- Nullable workId
                let callId = conn.Insert($"INSERT INTO {Tn.Call} (guid, dateTime, name, workId) VALUES (@Guid, @DateTime, @Name, @WorkId);", ormCall, tr)
                c.Id <- Some callId
                ormCall.Id <- callId
                assert(cache[c.Guid] = ormCall)

            // work 의 arrows 를 삽입 (calls 간 연결)
            for a in w.Arrows do
                let ormArrow = a.ToORM(cache) :?> ORMArrowCall
                ormArrow.WorkId <- Nullable workId

                let r = conn.Upsert(Tn.ArrowCall, ormArrow, ["Source"; "Target"; "WorkId"; "Guid"; "DateTime"], onInserted=idUpdator [ormArrow; a;])
                ()

        // system 의 arrows 를 삽입 (works 간 연결)
        for a in s.Arrows do
            let ormArrow = a.ToORM(cache) :?> ORMArrowWork
            ormArrow.SystemId <- Nullable sysId


    /// DsProject 을 sqlite database 에 저장
    let project2Sqlite (proj:DsProject) (connStr:string) (removeExistingData:bool option) =
        let grDic = proj.EnumerateDsObjects() |> groupByToDictionary _.GetType()
        let systems = grDic.[typeof<DsSystem>] |> Seq.cast<DsSystem> |> List.ofSeq

        let dbApi = DbApi(connStr)
        checkHandlers()
        use conn = dbApi.CreateConnection()
        use tr = conn.BeginTransaction()
        try
            match removeExistingData, proj.Id with
            | Some true, Some id ->
                //conn.TruncateAllTables()
                conn.Execute($"DELETE FROM {Tn.Project} WHERE id = {id}", tr) |> ignore
                //conn.Execute($"DELETE FROM {Tn.ProjectSystemMap} WHERE projectId = {id}", tr) |> ignore
            | _ -> ()

            let guidDic, ormProject = proj.ToORM()
            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project} (guid, dateTime, name, author, version, description)
                    VALUES (@Guid, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)
            proj.Id <- Some projId
            ormProject.Id <- projId

            for s in systems do
                system2SqliteHelper s (Some proj) guidDic conn tr

            tr.Commit()
            proj.LastConnectionString <- connStr
        with ex ->
            tr.Rollback()
            logError $"project2Sqlite failed: {ex.Message}"
            raise ex

    let system2Sqlite (x:DsSystem) (connStr:string) (removeExistingData:bool option) =
        let dbApi = DbApi(connStr)
        checkHandlers()
        use conn = dbApi.CreateConnection()
        use tr = conn.BeginTransaction()
        try
            let cache, ormSystem = x.ToORM()
            if removeExistingData = Some true then
                //conn.TruncateAllTables()
                conn.Execute($"DELETE FROM {Tn.System} WHERE guid = @Guid", ormSystem, tr) |> ignore

            system2SqliteHelper x None cache conn tr
            tr.Commit()
        with ex ->
            tr.Rollback()
            logError $"project2Sqlite failed: {ex.Message}"
            raise ex


module internal Sqlite2DsImpl =


    let deleteFromDatabase(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
        ()

    let deleteFromDatabaseWithConnectionString(identifier:DbObjectIdentifier) (connStr:string) =
        DbApi(connStr).WithConnection(fun (conn, tr) ->
            deleteFromDatabase identifier conn tr
        )

    let fromSqlite3(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
        let ormProject =
            let sqlBase = $"SELECT * FROM {Tn.Project} WHERE "
            let sqlTail, param =
                match identifier with
                | ByGuid guid -> "guid = @Guid", {| Guid = guid |} |> box
                | ById   id   -> "id = @Id",     {| Id = id |}
                | ByName name -> "name = @Name", {| Name = name |}
            let sql = sqlBase + sqlTail
            conn.QuerySingle<ORMProject>(sql, param, tr)

        let projSysMaps =
            conn.Query<ORMProjectSystemMap>(
                $"SELECT * FROM {Tn.ProjectSystemMap} WHERE projectId = @ProjectId",
                {| ProjectId = ormProject.Id |}, tr)
            |> toArray

        let ormSystems =
            let systemIds = projSysMaps |-> _.SystemId
            conn.Query<ORMSystem>($"SELECT * FROM {Tn.System} WHERE id IN @SystemIds", {| SystemIds = systemIds |}, tr) |> toArray

        let edProj = EdProject.Create(ormProject.Name, id=ormProject.Id, guid=Guid.Parse(ormProject.Guid), dateTime=ormProject.DateTime)
        let edSystems =
            ormSystems
            |-> fun s -> EdSystem.Create(s.Name, edProj, ?id=n2o s.Id, guid=s2guid s.Guid, dateTime=s.DateTime)
        let actives, passives = edSystems |> partition (fun s -> projSysMaps |> tryFind(fun m -> m.SystemId = s.Id.Value) |-> _.IsActive |? false)
        actives  |> iter edProj.AddActiveSystem
        passives |> iter edProj.AddPassiveSystem

        for s in edSystems do
            let edFlows = [
                for orm in conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @SystemId", {| SystemId = s.Id.Value |}, tr) do
                    EdFlow.Create(orm.Name, ?id=n2o orm.Id, guid=s2guid orm.Guid, dateTime=orm.DateTime, ?system=Some s)
            ]
            // edFlows |> s.AddFlows      EdFlow 생성시  ?system 인자로 이미 추가되었음.

            assert( setEqual s.Flows edFlows )

            let edWorks = [
                for orm in conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @SystemId", {| SystemId = s.Id.Value |}, tr) do
                    EdWork.Create(orm.Name, s, ?id=n2o orm.Id, guid=s2guid orm.Guid, dateTime=orm.DateTime)
                    |> tee(fun w ->
                        if orm.FlowId.HasValue then
                            let flow = edFlows |> find(fun f -> f.Id.Value = orm.FlowId.Value)
                            w.OptOwnerFlow <- Some flow
                        noop()
                        )
            ]
            //edWorks |> s.AddWorks : 이미 위에서 active/passive 로 추가완료했음!
            noop()
            for w in edWorks do
                let edCalls = [
                    for orm in conn.Query<ORMCall>($"SELECT * FROM {Tn.Call} WHERE workId = @WorkId", {| WorkId = w.Id.Value |}, tr) do
                        EdCall.Create(orm.Name, w, ?id=n2o orm.Id, guid=s2guid orm.Guid, dateTime=orm.DateTime)
                ]
                //edCalls |> w.AddCalls : EdCall 생성시  w 인자로 이미 추가되었음.
                assert(setEqual w.Calls edCalls)

                // work 내의 call 간 연결
                let edArrows = [
                    for orm in conn.Query<ORMArrowCall>($"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId", {| WorkId = w.Id.Value |}, tr) do
                        let src = edCalls |> find(fun c -> c.Id.Value = orm.Source)
                        let tgt = edCalls |> find(fun c -> c.Id.Value = orm.Target)
                        EdArrowBetweenCalls(src, tgt, orm.DateTime, s2guid orm.Guid, ?id=n2o orm.Id)
                ]
                edArrows |> w.AddArrows
                assert(setEqual w.Arrows edArrows)

                // TODO: call 하부 구조
                for c in edCalls do
                    //let edApiCalls = [
                    //    for orm in conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE callId = @CallId", {| CallId = c.Id.Value |}, tr) do
                    //        EdApiCall.Create(orm.Name, c, ?id=n2o orm.Id, guid=s2guid orm.Guid, dateTime=orm.DateTime)
                    //]
                    ()


            // system 내의 work 간 연결
            let edArrows = [
                for orm in conn.Query<ORMArrowWork>($"SELECT * FROM {Tn.ArrowWork} WHERE systemId = @SystemId", {| SystemId = s.Id.Value |}, tr) do
                    let src = edWorks |> find(fun w -> w.Id.Value = orm.Source)
                    let tgt = edWorks |> find(fun w -> w.Id.Value = orm.Target)
                    EdArrowBetweenWorks(src, tgt, orm.DateTime, s2guid orm.Guid, ?id=n2o orm.Id)
            ]
            edArrows |> s.AddArrows
            assert(setEqual s.Arrows edArrows)

            ()


        edProj
        //|> _.ToDsProject

    let fromSqlite3WithConnectionString(identifier:DbObjectIdentifier) (connStr:string):EdProject =
        DbApi(connStr).WithConnection(fun (conn, tr) ->
            fromSqlite3 identifier conn tr
        )


[<AutoOpen>]
module Ds2SqliteModule =

    open Ds2SqliteImpl
    open Sqlite2DsImpl

    type DsProject with
        member x.ToSqlite3(connStr:string, ?removeExistingData:bool) = project2Sqlite x connStr removeExistingData
        static member FromSqlite3(identifier:DbObjectIdentifier, connStr:string) = fromSqlite3WithConnectionString identifier connStr
        static member FromSqlite3(identifier:DbObjectIdentifier, conn:IDbConnection, tr:IDbTransaction) = fromSqlite3 identifier conn tr

    type DsSystem with
        member x.ToSqlite3(connStr:string, ?removeExistingData:bool) = system2Sqlite x connStr removeExistingData
        static member FromSqlite3(identifier:DbObjectIdentifier, connStr:string) =
            ()

        static member FromSqlite3(identifier:DbObjectIdentifier, conn:IDbConnection, tr:IDbTransaction) =
            ()
