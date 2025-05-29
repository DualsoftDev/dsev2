namespace Ev2.Core.FS

open System
open System.Data
open System.Linq
open System.Collections.Generic
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open System.Diagnostics

type DbObjectIdentifier =
    | ByGuid of Guid
    | ById of int
    | ByName of string

module internal Ds2SqliteImpl =
    /// src ORM 객체의 unique 속성(Id, Name, Guid, DateTime) 들을 dst 에 복사
    let fromOrmUniqINGD (src:#ORMUnique) (dst:#Unique) = dst |> uniqINGD (n2o src.Id) src.Name (s2guid src.Guid) src.DateTime

    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? ORMUnique as a -> a.Id <- Nullable id
            | :? RtUnique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"

    let system2SqliteHelper (dbApi:DbApi) (conn:IDbConnection) (tr:IDbTransaction) (cache:Dictionary<Guid, ORMUnique>) (s:RtSystem) (optProject:RtProject option)  =
        let bag = dbApi.DDic.Get<Db2RtBag>()
        let ormSystem = s.ToORM<ORMSystem>(dbApi, cache)
        let sysId = conn.Insert($"""INSERT INTO {Tn.System}
                        (guid, dateTime, name, author, langVersion, engineVersion, description, originGuid, prototype)
                        VALUES (@Guid, @DateTime, @Name, @Author, @LangVersion, @EngineVersion, @Description, @OriginGuid, @Prototype);""", ormSystem, tr)
        s.Id <- Some sysId
        cache[s.Guid].Id <- sysId

        match optProject with
        | Some proj ->
            // update projectSystemMap table
            let isActive = proj.ActiveSystems |> Seq.contains s
            let isPassive = proj.PassiveSystems |> Seq.contains s
            let projId = proj.Id.Value
            assert(isActive <> isPassive)   // XOR
            match conn.TryQuerySingle<ORMMapProjectSystem>($"SELECT * FROM {Tn.MapProject2System} WHERE projectId = {projId} AND systemId = {sysId}") with
            | Some row when row.IsActive = isActive -> ()
            | Some row ->
                conn.Execute($"UPDATE {Tn.MapProject2System} SET active = {isActive}, dateTime = @DateTime WHERE id = {row.Id}",
                            {| DateTime = now() |}) |> ignore
            | None ->
                let affectedRows = conn.Execute(
                        $"INSERT INTO {Tn.MapProject2System} (projectId, systemId, isActive, guid, dateTime) VALUES (@ProjectId, @SystemId, @IsActive, @Guid, @DateTime)",
                        {| ProjectId = projId; SystemId = sysId; IsActive = isActive; Guid=Guid.NewGuid(); DateTime=now() |}, tr)
                ()
        | None -> ()

        // flows 삽입
        for f in s.Flows do
            let ormFlow = f.ToORM<ORMFlow>(dbApi, cache)
            ormFlow.SystemId <- Nullable sysId
            let flowId = conn.Insert($"INSERT INTO {Tn.Flow} (guid, dateTime, name, systemId) VALUES (@Guid, @DateTime, @Name, @SystemId);", ormFlow, tr)
            f.Id <- Some flowId
            ormFlow.Id <- flowId
            assert (cache[f.Guid] = ormFlow)

            // TODO : arrow 처리.  System, flow, work 공히...
            //f.Arrows

        // system 의 apiDefs 를 삽입
        for rtAd in s.ApiDefs do
            let ormApiDef = rtAd.ToORM<ORMApiDef>(dbApi, cache)
            ormApiDef.SystemId <- sysId
            let r = conn.Upsert(Tn.ApiDef, ormApiDef, ["Id"; "Guid"; "DateTime"; "Name"; "IsPush"; "SystemId"], onInserted=idUpdator [ormApiDef; rtAd;])
            match r with
            | Some newId, affectedRows ->
                tracefn $"Inserted API Def: {rtAd.Name} with Id {newId}, systemId={ormApiDef.SystemId}"
            | None, 0 -> ()     // no change
            | None, affectedRows -> // update
                tracefn $"Updated API Def: {rtAd.Name} with Id {ormApiDef.Id.Value}, systemId={ormApiDef.SystemId}"
            ()
        // system 의 apiCalls 를 삽입
        for rtAc in s.ApiCalls do
            let ormApiCall = rtAc.ToORM<ORMApiCall>(dbApi, cache)
            ormApiCall.SystemId <- sysId
            let r = conn.Upsert(Tn.ApiCall, ormApiCall,
                        [   "Id"; "Guid"; "DateTime"; "Name"
                            "SystemId"; "ApiDefId"; "InAddress"; "OutAddress"
                            "InSymbol"; "OutSymbol"; "ValueTypeId"; "Value"], onInserted=idUpdator [ormApiCall; rtAc;])
            let xxx = r
            noop()

        // works, calls 삽입
        for w in s.Works do
            let ormWork = w.ToORM<ORMWork>(dbApi, cache)
            ormWork.SystemId <- Nullable sysId

            let workId = conn.Insert($"INSERT INTO {Tn.Work} (guid, dateTime, name, systemId, flowId) VALUES (@Guid, @DateTime, @Name, @SystemId, @FlowId);", ormWork, tr)
            w.Id <- Some workId
            ormWork.Id <- workId
            assert(cache[w.Guid] = ormWork)

            for c in w.Calls do
                let ormCall = c.ToORM<ORMCall>(dbApi, cache)
                ormCall.WorkId <- Nullable workId
                let callId =
                    conn.Insert($"""INSERT INTO {Tn.Call} (guid, dateTime, name, workId, callTypeId, autoPre, safety, timeout)
                        VALUES (@Guid, @DateTime, @Name, @WorkId, @CallTypeId, @AutoPre, @Safety, @Timeout);""", ormCall, tr)
                c.Id <- Some callId
                ormCall.Id <- callId
                assert(cache[c.Guid] = ormCall)

                // call - apiCall 에 대한 mapping 정보 삽입
                for apiCall in c.ApiCalls do
                    let apiCallId = cache[apiCall.Guid].Id.Value
                    //let ormMapping = ORMMapCall2ApiCall(callId, apiCallId)
                    //let r = conn.Upsert(Tn.MapCall2ApiCall, ormMapping, [ "CallId"; "ApiCallId"; ])

                    match conn.TryQuerySingle<ORMMapCall2ApiCall>($"SELECT * FROM {Tn.MapCall2ApiCall} WHERE callId = {c.Id.Value} AND apiCallId = {apiCallId}") with
                    | Some row ->
                        noop()
                        //conn.Execute($"UPDATE {Tn.MapCall2ApiCall} SET active = {isActive}, dateTime = @DateTime WHERE id = {row.Id}",
                        //            {| DateTime = now() |}) |> ignore
                    | None ->
                        let guid = newGuid()
                        let affectedRows = conn.Execute(
                                $"INSERT INTO {Tn.MapCall2ApiCall} (callId, apiCallId, guid, dateTime) VALUES (@CallId, @ApiCallId, @Guid, @DateTime)",
                                {| CallId = c.Id.Value; ApiCallId = apiCallId ; Guid=guid; DateTime=now() |}, tr)

                        noop()
                    ()










            // work 의 arrows 를 삽입 (calls 간 연결)
            for a in w.Arrows do
                let ormArrow = a.ToORM<ORMArrowCall>(dbApi, cache)
                ormArrow.WorkId <- workId

                let r = conn.Upsert(Tn.ArrowCall, ormArrow, ["Source"; "Target"; "TypeId"; "WorkId"; "Guid"; "DateTime"], onInserted=idUpdator [ormArrow; a;])
                ()

        // system 의 arrows 를 삽입 (works 간 연결)
        for a in s.Arrows do
            let ormArrow = a.ToORM<ORMArrowWork>(dbApi, cache)
            ormArrow.SystemId <- sysId
            let r = conn.Upsert(Tn.ArrowWork, ormArrow, ["Source"; "Target"; "TypeId"; "SystemId"; "Guid"; "DateTime"], onInserted=idUpdator [ormArrow; a;])
            ()




    /// DsProject 을 sqlite database 에 저장
    let project2Sqlite (proj:RtProject) (dbApi:DbApi) (removeExistingData:bool option) =
        let bag = dbApi.DDic.Get<Db2RtBag>()
        let rtObjs = proj.EnumerateRtObjects() |> List.cast<RtUnique> |> tee(fun zs -> zs |> iter bag.Add)
        let grDic = rtObjs |> groupByToDictionary _.GetType()
        let systems = grDic.[typeof<RtSystem>] |> Seq.cast<RtSystem> |> List.ofSeq

        let onError (ex:Exception) = logError $"project2Sqlite failed: {ex.Message}"; raise ex
        checkHandlers()
        dbApi.With(fun (conn, tr) ->
            match removeExistingData, proj.Id with
            | Some true, Some id ->
                //conn.TruncateAllTables()
                conn.Execute($"DELETE FROM {Tn.Project} WHERE id = {id}", tr) |> ignore
                //conn.Execute($"DELETE FROM {Tn.ProjectSystemMap} WHERE projectId = {id}", tr) |> ignore
            | _ -> ()

            let guidDic, ormProject = proj.ToORM(dbApi)
            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project} (guid, dateTime, name, author, version, description)
                    VALUES (@Guid, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)
            proj.Id <- Some projId
            ormProject.Id <- projId

            for s in systems do
                system2SqliteHelper dbApi conn tr guidDic s (Some proj)

            proj.LastConnectionString <- dbApi.ConnectionString
        , onError)

    let system2Sqlite (x:RtSystem) (dbApi:DbApi) (removeExistingData:bool option) =
        let onError (ex:Exception) = logError $"system2Sqlite failed: {ex.Message}"; raise ex
        checkHandlers()
        dbApi.With(fun (conn, tr) ->
            let cache, ormSystem = x.ToORM(dbApi)
            if removeExistingData = Some true then
                //conn.TruncateAllTables()
                conn.Execute($"DELETE FROM {Tn.System} WHERE guid = @Guid", ormSystem, tr) |> ignore

            system2SqliteHelper dbApi conn tr cache x None
        , onError)


module internal Sqlite2DsImpl =
    open Ds2SqliteImpl

    type Db2EdBag() =
        member val DbDic = Dictionary<string, ORMUnique>()  // string Guid
        member val EdDic = Dictionary<Guid, Unique>()


    let deleteFromDatabase(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
        ()

    let deleteFromDatabaseWithConnectionString(identifier:DbObjectIdentifier) (connStr:string) =
        DbApi(connStr).With(fun (conn, tr) ->
            deleteFromDatabase identifier conn tr
        )

    let fromSqlite3(identifier:DbObjectIdentifier) (dbApi:DbApi) =
        let bag = Db2EdBag()
        Trace.WriteLine($"--------------------------------------- fromSqlite3: {identifier}")
        noop()
        dbApi.With(fun (conn, tr) ->
            let ormProject =
                let sqlBase = $"SELECT * FROM {Tn.Project} WHERE "
                let sqlTail, param =
                    match identifier with
                    | ByGuid guid -> "guid = @Guid", {| Guid = guid |} |> box
                    | ById   id   -> "id = @Id",     {| Id = id |}
                    | ByName name -> "name = @Name", {| Name = name |}
                let sql = sqlBase + sqlTail
                conn.QuerySingle<ORMProject>(sql, param, tr)
                |> tee (fun z -> bag.DbDic.Add(z.Guid, z) )

            let projSysMaps =
                conn.Query<ORMMapProjectSystem>(
                    $"SELECT * FROM {Tn.MapProject2System} WHERE projectId = @ProjectId",
                    {| ProjectId = ormProject.Id |}, tr)
                |> tee (fun zs -> zs |> iter (fun z -> bag.DbDic.Add(z.Guid, z)) )
                |> toArray

            let ormSystems =
                let systemIds = projSysMaps |-> _.SystemId
                conn.Query<ORMSystem>($"SELECT * FROM {Tn.System} WHERE id IN @SystemIds",
                    {| SystemIds = systemIds |}, tr)
                |> tee (fun zs -> zs |> iter (fun z -> bag.DbDic.Add(z.Guid, z)) )
                |> toArray

            let edProj = EdProject() |> fromOrmUniqINGD ormProject |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
            let edSystems =
                ormSystems
                |-> fun s -> EdSystem() |> fromOrmUniqINGD s |> uniqParent (Some edProj) |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )

            let actives, passives =
                edSystems
                |> partition (fun s ->
                    projSysMaps
                    |> tryFind(fun m -> m.SystemId = s.Id.Value)
                    |-> _.IsActive |? false)

            actives  |> edProj.ActiveSystems.AddRange
            passives |> edProj.PassiveSystems.AddRange

            for s in edSystems do
                let edFlows = [
                    for orm in conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @Id", s, tr) do
                        bag.DbDic.Add(orm.Guid, orm) |> ignore
                        EdFlow(RawParent = Some s) |> fromOrmUniqINGD orm |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                ]
                edFlows |> s.Flows.AddRange

                let edApiDefs = [
                    for orm in conn.Query<ORMApiDef>($"SELECT * FROM {Tn.ApiDef} WHERE systemId = @Id", s, tr) do
                        bag.DbDic.Add(orm.Guid, orm) |> ignore
                        EdApiDef(RawParent = Some s) |> fromOrmUniqINGD orm |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                ]
                s.ApiDefs.AddRange(edApiDefs)

                let edApiCalls = [
                    for orm in conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE systemId = {s.Id.Value}", tr) do
                        bag.DbDic.Add(orm.Guid, orm) |> ignore

                        (* orm.ApiDefId -> EdApiDef : DB 에 저장된 key 로 bag 을 뒤져서 EdApiDef 객체를 찾는다. *)
                        let apiDefGuid =
                            bag.DbDic.Values.OfType<ORMApiDef>()
                            |> find(fun z -> z.Id = Nullable orm.ApiDefId)
                            |> _.Guid

                        EdApiCall(s2guid apiDefGuid) |> fromOrmUniqINGD orm |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                ]
                edApiCalls |> s.ApiCalls.AddRange



                let edWorks = [
                    for orm in conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @Id", s, tr) do
                        EdWork(RawParent = Some s) |> fromOrmUniqINGD orm
                        |> tee(fun w ->
                            if orm.FlowId.HasValue then
                                let flow = edFlows |> find(fun f -> f.Id.Value = orm.FlowId.Value)
                                w.OptOwnerFlow <- Some flow )
                        |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                ]
                edWorks |> s.Works.AddRange
                noop()
                for w in edWorks do
                    let edCalls = [
                        for orm in conn.Query<ORMCall>($"SELECT * FROM {Tn.Call} WHERE workId = @WorkId", {| WorkId = w.Id.Value |}, tr) do

                            bag.DbDic.Add(orm.Guid, orm) |> ignore

                            EdCall(RawParent = Some w) |> fromOrmUniqINGD orm
                            |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                            |> tee(fun c -> edApiCalls |> iter (fun apiCall -> apiCall |> uniqParent (Some c) |> ignore))
                    ]
                    edCalls |> w.Calls.AddRange


                    // work 내의 call 간 연결
                    let edArrows = [
                        for orm in conn.Query<ORMArrowCall>($"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId", {| WorkId = w.Id.Value |}, tr) do
                            bag.DbDic.Add(orm.Guid, orm) |> ignore
                            let src = edCalls |> find(fun c -> c.Id.Value = orm.Source)
                            let tgt = edCalls |> find(fun c -> c.Id.Value = orm.Target)
                            let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get
                            EdArrowBetweenCalls(src, tgt, arrowType) |> fromOrmUniqINGD orm |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                    ]
                    edArrows |> w.Arrows.AddRange

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
                        bag.DbDic.Add(orm.Guid, orm) |> ignore
                        let src = edWorks |> find(fun w -> w.Id.Value = orm.Source)
                        let tgt = edWorks |> find(fun w -> w.Id.Value = orm.Target)
                        let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get
                        EdArrowBetweenWorks(src, tgt, arrowType) |> fromOrmUniqINGD orm |> tee (fun z -> bag.EdDic.Add(z.Guid, z) )
                ]
                edArrows |> s.Arrows.AddRange
                assert(setEqual s.Arrows edArrows)

                ()


            edProj
            //|> _.ToDsProject
        )

[<AutoOpen>]
module Ds2SqliteModule =

    open Ds2SqliteImpl
    open Sqlite2DsImpl

    let private createDbApi (connStr:string) =
        let ddic = DynamicDictionary()
        ddic.Set<Db2RtBag>(Db2RtBag())
        DbApi(connStr, DDic=ddic)

    type RtProject with
        member x.ToSqlite3(connStr:string, ?removeExistingData:bool) =
            let dbApi = createDbApi connStr
            project2Sqlite x dbApi removeExistingData

        static member FromSqlite3(identifier:DbObjectIdentifier, connStr:string) =
            let dbApi = createDbApi connStr
            fromSqlite3 identifier dbApi

    type RtSystem with
        member x.ToSqlite3(connStr:string, ?removeExistingData:bool) =
            let dbApi = createDbApi connStr
            system2Sqlite x dbApi removeExistingData

        static member FromSqlite3(identifier:DbObjectIdentifier, connStr:string) =
            let dbApi = createDbApi connStr
            ()
