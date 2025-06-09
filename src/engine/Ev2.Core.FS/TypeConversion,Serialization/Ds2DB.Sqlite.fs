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
    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? Unique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"


    let system2SqliteHelper (dbApi:AppDbApi) (conn:IDbConnection) (tr:IDbTransaction) (cache:Dictionary<Guid, ORMUnique>) (s:RtSystem) (optProject:RtProject option)  =
        let ormSystem = s.ToORM<ORMSystem>(dbApi, cache)

        let sysId = conn.Insert($"""INSERT INTO {Tn.System}
                                (guid, parameter,                     dateTime,  name,  iri, author,     langVersion, engineVersion, description, originGuid, prototypeId)
                        VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @IRI, @Author, @LangVersion, @EngineVersion, @Description, @OriginGuid, @PrototypeId);""", ormSystem, tr)

        s.Id <- Some sysId
        cache[s.Guid].Id <- Some sysId

        match optProject with
        | Some rtp ->
            // project 하부에 연결된 system 을 DB 에 저장

            // s.PrototypeSystemGuid 에 따라 다른 처리???
            //assert(not s.PrototypeSystemGuid.Is)

            // update projectSystemMap table
            let isActive = rtp.ActiveSystems |> Seq.contains s
            let isPassive = rtp.PassiveSystems |> Seq.contains s
            let projId = rtp.Id.Value

            // prototype 이 아니라면, active 나 passive 중 하나만 true 여야 한다.
            assert(isActive <> isPassive || s.PrototypeSystemGuid.IsNone)   // XOR

            match conn.TryQuerySingle<ORMMapProjectSystem>(
                            $"""SELECT * FROM {Tn.MapProject2System}
                                WHERE projectId = {projId} AND systemId = {sysId}""") with
            | Some row ->
                if row.IsActive = isActive then
                    ()
                else
                    conn.Execute($"""UPDATE {Tn.MapProject2System}
                                     SET active = {isActive}, dateTime = @DateTime
                                     WHERE id = {row.Id}""",
                                {| DateTime = now() |}) |> ignore
            | None ->
                let loadedName = s.PrototypeSystemGuid |-> (fun _ -> s.Name) |? null     // RtSystem.Name 은 loaded name 을 의미한다.
                let affectedRows = conn.Execute(
                        $"""INSERT INTO {Tn.MapProject2System}
                                    (projectId, systemId, loadedName, isActive, guid, dateTime)
                             VALUES (@ProjectId, @SystemId, @LoadedName, @IsActive, @Guid, @DateTime)""",
                        {|  ProjectId = projId; SystemId = sysId; LoadedName=loadedName; IsActive = isActive;
                            Guid=Guid.NewGuid(); DateTime=now() |}, tr)
                ()
        | None ->
            // project 와 무관한 system 을 DB 에 저장
            failwith "Not yet implemented: system2SqliteHelper without project context"
            ()


        let jsonbColumns = if dbApi.IsPostgres() then ["Parameter"] else []

        // flows 삽입
        for f in s.Flows do
            let ormFlow = f.ToORM<ORMFlow>(dbApi, cache)
            ormFlow.SystemId <- Some sysId

            let flowId = conn.Insert($"""INSERT INTO {Tn.Flow}
                                    (guid, parameter, dateTime, name, systemId)
                             VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @SystemId);""", ormFlow, tr)

            f.Id <- Some flowId
            ormFlow.Id <- Some flowId
            assert (cache[f.Guid] = ormFlow)


            let insertFlowElement (tableName:string) (rtX:#RtFlowEntity, ormX:#ORMFlowEntity) =
                    ormX.FlowId <- Some flowId
                    let xId =
                        conn.Insert($"""INSERT INTO {tableName}
                                        (guid, parameter, dateTime, name, flowId)
                                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @FlowId);""", ormX, tr)
                    rtX.Id <- Some xId
                    ormX.Id <- Some xId
                    assert (cache[rtX.Guid] = ormX)
                    ()

            // TODO : arrow 처리.  System, flow, work 공히...


            //for x in f.Buttons do
            //    let ormX = x.ToORM<ORMButton>(dbApi, cache)
            //    ormX.FlowId <- Some flowId
            //    let buttonId =
            //        conn.Insert($"""INSERT INTO {Tn.Button}
            //                        (guid, parameter, dateTime, name, flowId)
            //                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @FlowId);""", ormX, tr)
            //    x.Id <- Some buttonId
            //    ormX.Id <- Some buttonId
            //    assert (cache[x.Guid] = ormX)
            f.Buttons    |-> (fun z -> z, z.ToORM<ORMButton>   (dbApi, cache)) |> iter (insertFlowElement Tn.Button)
            f.Lamps      |-> (fun z -> z, z.ToORM<ORMLamp>     (dbApi, cache)) |> iter (insertFlowElement Tn.Lamp)
            f.Conditions |-> (fun z -> z, z.ToORM<ORMCondition>(dbApi, cache)) |> iter (insertFlowElement Tn.Condition)
            f.Actions    |-> (fun z -> z, z.ToORM<ORMAction>   (dbApi, cache)) |> iter (insertFlowElement Tn.Action)



            //f.Arrows

        // system 의 apiDefs 를 삽입
        for rtAd in s.ApiDefs do
            let ormApiDef = rtAd.ToORM<ORMApiDef>(dbApi, cache)
            ormApiDef.SystemId <- Some sysId

            let r = conn.Upsert(Tn.ApiDef, ormApiDef,
                    ["Guid"; "Parameter"; "DateTime"; "Name"; "IsPush"; "SystemId"],     // PK 는 자동으로 채우져야 해서 "Id" 는 생략해야 함
                    jsonbColumns=jsonbColumns,
                    onInserted=idUpdator [ormApiDef; rtAd;])

            match r with
            | Some newId ->
                tracefn $"Inserted API Def: {rtAd.Name} with Id {newId}, systemId={ormApiDef.SystemId}"
            | None -> // update or no change
                tracefn $"Updated/Or No change API Def: {rtAd.Name}, systemId={ormApiDef.SystemId}"
            ()

        // system 의 apiCalls 를 삽입
        for rtAc in s.ApiCalls do
            let ormApiCall = rtAc.ToORM<ORMApiCall>(dbApi, cache)
            ormApiCall.SystemId <- Some sysId

            let r = conn.Upsert(Tn.ApiCall, ormApiCall,
                        [   "Guid"; "Parameter"; "DateTime"; "Name"
                            "SystemId"; "ApiDefId"; "InAddress"; "OutAddress"
                            "InSymbol"; "OutSymbol"; "ValueSpec"; "ValueSpecHint"],
                        jsonbColumns=["Parameter"; "ValueSpec"],
                        onInserted=idUpdator [ormApiCall; rtAc;])
            let xxx = r
            noop()

        // works, calls 삽입
        for w in s.Works do
            let ormWork = w.ToORM<ORMWork>(dbApi, cache)
            ormWork.SystemId <- Some sysId

            let workId = conn.Insert($"""INSERT INTO {Tn.Work}
                                (guid, parameter,                      dateTime,  name,  systemId,  flowId,  status4Id,  motion,  script,  isFinished,  numRepeat,  period,  delay)
                         VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @SystemId, @FlowId, @Status4Id, @Motion, @Script, @IsFinished, @NumRepeat, @Period, @Delay);""", ormWork, tr)

            w.Id <- Some workId
            ormWork.Id <- Some workId
            assert(cache[w.Guid] = ormWork)

            for c in w.Calls do
                let ormCall = c.ToORM<ORMCall>(dbApi, cache)
                ormCall.WorkId <- Some workId

                let callId =
                    conn.Insert($"""INSERT INTO {Tn.Call}
                                (guid,  parameter,                     dateTime,   name, workId,   status4Id,  callTypeId,  autoConditions, commonConditions,   isDisabled, timeout)
                         VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @WorkId, @Status4Id, @CallTypeId, @AutoConditions, @CommonConditions, @IsDisabled, @Timeout);""", ormCall, tr)

                c.Id <- Some callId
                ormCall.Id <- Some callId
                assert(cache[c.Guid] = ormCall)

                // call - apiCall 에 대한 mapping 정보 삽입
                for apiCall in c.ApiCalls do
                    let apiCallId = cache[apiCall.Guid].Id.Value

                    let m = conn.TryQuerySingle<ORMMapCall2ApiCall>(
                                $"""SELECT * FROM {Tn.MapCall2ApiCall}
                                    WHERE callId = {c.Id.Value} AND apiCallId = {apiCallId}""")
                    match m with
                    | Some row ->
                        noop()
                        //conn.Execute($"UPDATE {Tn.MapCall2ApiCall} SET active = {isActive}, dateTime = @DateTime WHERE id = {row.Id}",
                        //            {| DateTime = now() |}) |> ignore
                    | None ->
                        let guid = newGuid()
                        let affectedRows = conn.Execute(
                                $"INSERT INTO {Tn.MapCall2ApiCall} (callId, apiCallId,   guid, dateTime)
                                                            VALUES (@CallId, @ApiCallId, @Guid, @DateTime)",
                                {| CallId = c.Id.Value; ApiCallId = apiCallId ; Guid=guid; DateTime=now() |}, tr)

                        noop()
                    ()

            // work 의 arrows 를 삽입 (calls 간 연결)
            for a in w.Arrows do
                let ormArrow = a.ToORM<ORMArrowCall>(dbApi, cache)
                ormArrow.WorkId <- workId

                let r = conn.Upsert(Tn.ArrowCall, ormArrow,
                    ["Source"; "Target"; "TypeId"; "WorkId"; "Guid"; "Parameter"; "DateTime"],
                    jsonbColumns=jsonbColumns,
                    onInserted=idUpdator [ormArrow; a;])
                ()

        // system 의 arrows 를 삽입 (works 간 연결)
        for a in s.Arrows do
            let ormArrow = a.ToORM<ORMArrowWork>(dbApi, cache)
            ormArrow.SystemId <- sysId

            let r = conn.Upsert(Tn.ArrowWork, ormArrow,
                    ["Source"; "Target"; "TypeId"; "SystemId"; "Guid"; "Parameter"; "DateTime"],
                    jsonbColumns=jsonbColumns,
                    onInserted=idUpdator [ormArrow; a;])
            ()




    /// DsProject 을 sqlite database 에 저장
    let project2Sqlite (proj:RtProject) (dbApi:AppDbApi) (removeExistingData:bool option) =
        let bag = dbApi.DDic.Get<Db2RtBag>()

        let rtObjs =
            proj.EnumerateRtObjects()
            |> List.cast<RtUnique>
            |> tee(fun zs ->
                zs |> iter bag.Add)

        let grDic = rtObjs |> groupByToDictionary _.GetType()

        let systems =
            grDic.[typeof<RtSystem>]
            |> Seq.cast<RtSystem> |> List.ofSeq

        let onError (ex:Exception) = logError $"project2Sqlite failed: {ex.Message}"; raise ex

        checkHandlers()

        dbApi.With(fun (conn, tr) ->
            match removeExistingData, proj.Id with
            | Some true, Some id ->
                //dbApi.DbProvider.TruncateAllTables(conn) |> ignore
                conn.Execute($"DELETE FROM {Tn.Project} WHERE id = {id}", tr) |> ignore
                //conn.Execute($"DELETE FROM {Tn.ProjectSystemMap} WHERE projectId = {id}", tr) |> ignore

                tr.Commit()
                failwith "ERROR"
            | _ -> ()

            let guidDic, ormProject = proj.ToORM(dbApi)

            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project}
                           (guid, parameter, dateTime, name, author, version, description)
                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)

            proj.Id <- Some projId
            ormProject.Id <- Some projId

            for s in systems do
                system2SqliteHelper dbApi conn tr guidDic s (Some proj)

            proj.Database <- dbApi.DbProvider
        , onError)


    let system2Sqlite (x:RtSystem) (dbApi:AppDbApi) (removeExistingData:bool option) =
        let onError (ex:Exception) = logError $"system2Sqlite failed: {ex.Message}"; raise ex

        checkHandlers()

        dbApi.With(fun (conn, tr) ->
            let cache, ormSystem = x.ToORM(dbApi)
            if removeExistingData = Some true then
                //dbApi.DbProvider.TruncateAllTables(conn) |> ignore
                conn.Execute($"DELETE FROM {Tn.System} WHERE guid = @Guid", ormSystem, tr) |> ignore

            system2SqliteHelper dbApi conn tr cache x None
        , onError)


module internal Sqlite2DsImpl =
    open Ds2SqliteImpl

    //let deleteFromDatabase(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
    //    ()

    //let deleteFromDatabaseWithConnectionString(identifier:DbObjectIdentifier) (connStr:string) =
    //    DbApi(connStr).With(fun (conn, tr) ->
    //        deleteFromDatabase identifier conn tr
    //    )

    let fromSqlite3(identifier:DbObjectIdentifier) (dbApi:AppDbApi) =
        let bag = Db2RtBag()
        Trace.WriteLine($"--------------------------------------- fromSqlite3: {identifier}")
        noop()
        dbApi.With(fun (conn, tr) ->
            (* DB 로부터 project, project-system map, systems, ... 등등의 정보를 읽어 들인후, EdXXXX 객체를 생성. *)

            let ormProject =
                let sqlBase = $"SELECT * FROM {Tn.Project} WHERE "

                let sqlTail, param =
                    match identifier with
                    | ByGuid guid -> "guid = @Guid", {| Guid = guid |} |> box
                    | ById   id   -> "id = @Id",     {| Id = id |}
                    | ByName name -> "name = @Name", {| Name = name |}

                let sql = sqlBase + sqlTail

                conn.QuerySingle<ORMProject>(sql, param, tr)
                |> tee (fun z -> bag.DbDic.Add(guid2str z.Guid, z) )

            let projSysMaps =
                conn.Query<ORMMapProjectSystem>(
                    $"SELECT * FROM {Tn.MapProject2System} WHERE projectId = @ProjectId",
                    {| ProjectId = ormProject.Id |}, tr)
                |> tee (fun zs ->
                    zs |> iter (fun z -> bag.DbDic.Add(guid2str z.Guid, z)) )
                |> toArray

            let ormSystems =
                let systemIds = projSysMaps |-> _.SystemId

                conn.Query<ORMSystem>($"SELECT * FROM {Tn.System} WHERE id IN @SystemIds",
                    {| SystemIds = systemIds |}, tr)
                |> tee (fun zs ->
                    zs |> iter (fun z -> bag.DbDic.Add(guid2str z.Guid, z)) )
                |> toArray

            let edProj =
                RtProject.Create()
                |> fromUniqINGD ormProject
                |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )

            let edSystems =
                ormSystems
                    |-> fun s ->
                        RtSystem.Create()
                        |> fromUniqINGD s
                        |> uniqParent (Some edProj)
                        |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )

            let actives, passives =
                edSystems
                |> partition (fun s ->
                    projSysMaps
                    |> tryFind(fun m -> m.SystemId = s.Id.Value)
                    |-> _.IsActive |? false)

            actives  |> edProj.RawActiveSystems.AddRange
            passives |> edProj.RawPassiveSystems.AddRange

            for s in edSystems do
                let edFlows = [
                    let orms = conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @Id", s, tr)

                    for ormFlow in orms do
                        bag.DbDic.Add(guid2str ormFlow.Guid, ormFlow) |> ignore

                        let f = {| FlowId = ormFlow.Id |}
                        let ormButtons    = conn.Query<ORMButton>   ($"SELECT * FROM {Tn.Button}    WHERE flowId = @FlowId", f,  tr)
                        let ormLamps      = conn.Query<ORMLamp>     ($"SELECT * FROM {Tn.Lamp}      WHERE flowId = @FlowId", f,  tr)
                        let ormConditions = conn.Query<ORMCondition>($"SELECT * FROM {Tn.Condition} WHERE flowId = @FlowId", f,  tr)
                        let ormActions    = conn.Query<ORMAction>   ($"SELECT * FROM {Tn.Action}    WHERE flowId = @FlowId", f,  tr)

                        let buttons    = ormButtons    |-> (fun z -> RtButton    ()) |> toArray    //.Create
                        let lamps      = ormLamps      |-> (fun z -> RtLamp      ()) |> toArray    //.Create
                        let conditions = ormConditions |-> (fun z -> RtCondition ()) |> toArray    //.Create
                        let actions    = ormActions    |-> (fun z -> RtAction    ()) |> toArray    //.Create

                        RtFlow(buttons, lamps, conditions, actions, RawParent = Some s)
                        |> fromUniqINGD ormFlow
                        |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                ]
                edFlows |> s.AddFlows

                let edApiDefs = [
                    let orms =  conn.Query<ORMApiDef>($"SELECT * FROM {Tn.ApiDef} WHERE systemId = @Id", s, tr)

                    for orm in orms do
                        bag.DbDic.Add(guid2str orm.Guid, orm) |> ignore

                        RtApiDef(orm.IsPush, RawParent = Some s)
                        |> fromUniqINGD orm
                        |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                ]
                edApiDefs |> s.AddApiDefs

                let edApiCalls = [
                    let orms = conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE systemId = {s.Id.Value}", tr)

                    for orm in orms do
                        bag.DbDic.Add(guid2str orm.Guid, orm) |> ignore

                        (* orm.ApiDefId -> EdApiDef : DB 에 저장된 key 로 bag 을 뒤져서 EdApiDef 객체를 찾는다. *)
                        let apiDefGuid =
                            bag.DbDic.Values.OfType<ORMApiDef>()
                            |> find(fun z -> z.Id = Some orm.ApiDefId)
                            |> _.Guid

                        let valueParam = IValueSpec.TryDeserialize orm.ValueSpec
                        RtApiCall(apiDefGuid, orm.InAddress, orm.OutAddress,
                                    orm.InSymbol, orm.OutSymbol, valueParam)
                        |> fromUniqINGD orm |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                ]
                edApiCalls |> s.AddApiCalls



                let edWorks = [
                    let orms = conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @Id", s, tr)

                    for orm in orms do
                        RtWork.Create()
                        |> setParent s
                        |> fromUniqINGD orm
                        |> tee(fun w ->
                            if orm.FlowId.HasValue then
                                let flow = edFlows |> find(fun f -> f.Id.Value = orm.FlowId.Value)
                                //w.Status4 <- orm.Status4Id
                                w.Flow <- Some flow
                            w.Status4 <- n2o orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                            w.Motion     <- orm.Motion
                            w.Script     <- orm.Script
                            w.IsFinished <- orm.IsFinished
                            w.NumRepeat  <- orm.NumRepeat
                            w.Period     <- orm.Period
                            w.Delay      <- orm.Delay )
                        |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                ]
                edWorks |> s.AddWorks

                for w in edWorks do
                    let edCalls = [
                        let orms = conn.Query<ORMCall>(
                                $"SELECT * FROM {Tn.Call} WHERE workId = @WorkId",
                                {| WorkId = w.Id.Value |}, tr)

                        for orm in orms do
                            bag.DbDic.Add(guid2str orm.Guid, orm) |> ignore

                            let callType = orm.CallTypeId.Value |> dbApi.TryFindEnumValue |> Option.get
                            let apiCallGuids =
                                conn.Query<Guid>($"""SELECT ac.guid
                                                        FROM {Tn.MapCall2ApiCall} m
                                                        JOIN {Tn.ApiCall} ac ON ac.id = m.apiCallId
                                                        WHERE m.callId = @CallId""",
                                {| CallId = orm.Id.Value |}, tr)

                            let acs = orm.AutoConditions |> jsonDeserializeStrings
                            let ccs = orm.CommonConditions |> jsonDeserializeStrings
                            RtCall(callType, apiCallGuids, acs, ccs, orm.IsDisabled, n2o orm.Timeout)
                            |> setParent w
                            |> fromUniqINGD orm
                            |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                            |> tee(fun c ->
                                c.Status4 <- n2o orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                                edApiCalls
                                |> iter (fun apiCall ->
                                    apiCall
                                    |> uniqParent (Some c)
                                    |> ignore))
                    ]
                    w.AddCalls edCalls


                    // work 내의 call 간 연결
                    let edArrows = [
                        let orms = conn.Query<ORMArrowCall>(
                                $"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId",
                                {| WorkId = w.Id.Value |}, tr)

                        for orm in orms do
                            bag.DbDic.Add(guid2str orm.Guid, orm) |> ignore
                            let src = edCalls |> find(fun c -> c.Id.Value = orm.Source)
                            let tgt = edCalls |> find(fun c -> c.Id.Value = orm.Target)
                            let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get

                            RtArrowBetweenCalls(src, tgt, arrowType)
                            |> fromUniqINGD orm
                            |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                    ]
                    w.AddArrows edArrows

                    // call 이하는 더 이상 읽어 들일 구조가 없다.
                    for c in edCalls do
                        ()


                // system 내의 work 간 연결
                let edArrows = [
                    let orms = conn.Query<ORMArrowWork>(
                            $"SELECT * FROM {Tn.ArrowWork} WHERE systemId = @SystemId",
                            {| SystemId = s.Id.Value |}, tr)

                    for orm in orms do
                        bag.DbDic.Add(guid2str orm.Guid, orm) |> ignore
                        let src = edWorks |> find(fun w -> w.Id.Value = orm.Source)
                        let tgt = edWorks |> find(fun w -> w.Id.Value = orm.Target)
                        let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get

                        RtArrowBetweenWorks(src, tgt, arrowType)
                        |> fromUniqINGD orm
                        |> tee (fun z -> bag.RtDic.Add(z.Guid, z) )
                ]
                edArrows |> s.AddArrows
                assert(setEqual s.Arrows edArrows)

                ()


            edProj
            //|> _.ToDsProject
        )

[<AutoOpen>]
module Ds2SqliteModule =

    open Ds2SqliteImpl
    open Sqlite2DsImpl

    let private initializeDDic (dbApi:AppDbApi) =
        let ddic = DynamicDictionary()
        ddic.Set<Db2RtBag>(Db2RtBag())
        dbApi.DDic <- ddic


    type RtProject with // ToSqlite3, FromSqlite3
        member x.CommitToDB(dbApi:AppDbApi, ?removeExistingData:bool) =
            initializeDDic dbApi
            project2Sqlite x dbApi removeExistingData

        static member CheckoutFromDB(identifier:DbObjectIdentifier, dbApi:AppDbApi) =
            initializeDDic dbApi
            fromSqlite3 identifier dbApi

    type RtSystem with  // ToSqlite3, FromSqlite3
        member x.CommitToDB(dbApi:AppDbApi, ?removeExistingData:bool) =
            initializeDDic dbApi
            system2Sqlite x dbApi removeExistingData

        static member CheckoutFromDB(identifier:DbObjectIdentifier, dbApi:AppDbApi) =
            initializeDDic dbApi
            ()
