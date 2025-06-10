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


[<AutoOpen>]
module internal Ds2DbImpl =
    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? Unique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"


    let insertSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem) (optProject:RtProject option)  =
        let conn, tr = dbApi.ActiveConnection, dbApi.ActiveTransaction

        let ormSystem = s.ToORM<ORMSystem>(dbApi)

        let sysId = conn.Insert($"""INSERT INTO {Tn.System}
                                (guid, parameter,                     dateTime,  name,  iri, author,     langVersion, engineVersion, description, originGuid, prototypeId)
                        VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @IRI, @Author, @LangVersion, @EngineVersion, @Description, @OriginGuid, @PrototypeId);""",
                        ormSystem, tr)

        s.Id <- Some sysId
        let guidDicDebug = dbApi.DDic.Get<Guid2UniqDic>()
        guidDicDebug[s.Guid].Id <- Some sysId

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
                                WHERE projectId = {projId} AND systemId = {sysId}""", transaction=tr) with
            | Some row ->
                if row.IsActive = isActive then
                    ()
                else
                    conn.Execute($"""UPDATE {Tn.MapProject2System}
                                     SET active = {isActive}
                                     WHERE id = {row.Id}""", transaction=tr) |> ignore
            | None ->
                let loadedName = s.PrototypeSystemGuid |-> (fun _ -> s.Name) |? null     // RtSystem.Name 은 loaded name 을 의미한다.
                let affectedRows = conn.Execute(
                        $"""INSERT INTO {Tn.MapProject2System}
                                    (projectId, systemId, loadedName, isActive, guid)
                             VALUES (@ProjectId, @SystemId, @LoadedName, @IsActive, @Guid)""",
                        {|  ProjectId = projId; SystemId = sysId; LoadedName=loadedName; IsActive = isActive;
                            Guid=Guid.NewGuid() |}, tr)
                ()
        | None ->
            // project 와 무관한 system 을 DB 에 저장
            failwith "Not yet implemented: insertSystemToDBHelper without project context"
            ()


        let jsonbColumns = if dbApi.IsPostgres() then ["Parameter"] else []

        // flows 삽입
        for f in s.Flows do
            let ormFlow = f.ToORM<ORMFlow>(dbApi)
            ormFlow.SystemId <- Some sysId

            let flowId = conn.Insert($"""INSERT INTO {Tn.Flow}
                                    (guid, parameter, name, systemId)
                             VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId);""", ormFlow, tr)

            f.Id <- Some flowId
            ormFlow.Id <- Some flowId
            assert (guidDicDebug[f.Guid] = ormFlow)


            let insertFlowElement (tableName:string) (rtX:#RtFlowEntity, ormX:#ORMFlowEntity) =
                    ormX.FlowId <- Some flowId
                    let xId =
                        conn.Insert($"""INSERT INTO {tableName}
                                        (guid, parameter, name, flowId)
                                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @FlowId);""", ormX, tr)
                    rtX.Id <- Some xId
                    ormX.Id <- Some xId
                    assert (guidDicDebug[rtX.Guid] = ormX)
                    ()


            (* Button, Lamps, Conditions, Action 등이 복잡해 질 경우 이렇게.. *)
            //for x in f.Buttons do
            //    let ormX = x.ToORM<ORMButton>(dbApi, cache)
            //    ormX.FlowId <- Some flowId
            //    let buttonId =
            //        conn.Insert($"""INSERT INTO {Tn.Button}
            //                        (guid, parameter, name, flowId)
            //                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @FlowId);""", ormX, tr)
            //    x.Id <- Some buttonId
            //    ormX.Id <- Some buttonId
            //    assert (cache[x.Guid] = ormX)

            (* 간략 format .. *)
            f.Buttons    |-> (fun z -> z, z.ToORM<ORMButton>    dbApi) |> iter (insertFlowElement Tn.Button)
            f.Lamps      |-> (fun z -> z, z.ToORM<ORMLamp>      dbApi) |> iter (insertFlowElement Tn.Lamp)
            f.Conditions |-> (fun z -> z, z.ToORM<ORMCondition> dbApi) |> iter (insertFlowElement Tn.Condition)
            f.Actions    |-> (fun z -> z, z.ToORM<ORMAction>    dbApi) |> iter (insertFlowElement Tn.Action)


        // system 의 apiDefs 를 삽입
        for rtAd in s.ApiDefs do
            let ormApiDef = rtAd.ToORM<ORMApiDef>(dbApi)
            ormApiDef.SystemId <- Some sysId

            let r = conn.Upsert(Tn.ApiDef, ormApiDef,
                    ["Guid"; "Parameter"; "Name"; "IsPush"; "SystemId"],     // PK 는 자동으로 채우져야 해서 "Id" 는 생략해야 함
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
            let ormApiCall = rtAc.ToORM<ORMApiCall>(dbApi)
            ormApiCall.SystemId <- Some sysId

            let r = conn.Upsert(Tn.ApiCall, ormApiCall,
                        [   "Guid"; "Parameter"; "Name"
                            "SystemId"; "ApiDefId"; "InAddress"; "OutAddress"
                            "InSymbol"; "OutSymbol"; "ValueSpec"; "ValueSpecHint"],
                        jsonbColumns=["Parameter"; "ValueSpec"],
                        onInserted=idUpdator [ormApiCall; rtAc;])
            let xxx = r
            noop()

        // works, calls 삽입
        for w in s.Works do
            let ormWork = w.ToORM<ORMWork>(dbApi)
            ormWork.SystemId <- Some sysId

            let workId = conn.Insert($"""INSERT INTO {Tn.Work}
                                (guid, parameter,                      name,  systemId,  flowId,  status4Id,  motion,  script,  isFinished,  numRepeat,  period,  delay)
                         VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId, @FlowId, @Status4Id, @Motion, @Script, @IsFinished, @NumRepeat, @Period, @Delay);""", ormWork, tr)

            w.Id <- Some workId
            ormWork.Id <- Some workId
            assert(guidDicDebug[w.Guid] = ormWork)

            for c in w.Calls do
                let ormCall = c.ToORM<ORMCall>(dbApi)
                ormCall.WorkId <- Some workId

                let callId =
                    conn.Insert($"""INSERT INTO {Tn.Call}
                                (guid,  parameter,                     name, workId,   status4Id,  callTypeId,  autoConditions, commonConditions,   isDisabled, timeout)
                         VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @WorkId, @Status4Id, @CallTypeId, @AutoConditions, @CommonConditions, @IsDisabled, @Timeout);""", ormCall, tr)

                c.Id <- Some callId
                ormCall.Id <- Some callId
                assert(guidDicDebug[c.Guid] = ormCall)

                // call - apiCall 에 대한 mapping 정보 삽입
                for apiCall in c.ApiCalls do
                    let apiCallId = apiCall.ORMObject >>= tryCast<ORMUnique> >>= _.Id |?? (fun () -> failwith "ERROR")

                    let m = conn.TryQuerySingle<ORMMapCall2ApiCall>(
                                $"""SELECT * FROM {Tn.MapCall2ApiCall}
                                    WHERE callId = {c.Id.Value} AND apiCallId = {apiCallId}""", transaction=tr)
                    match m with
                    | Some row ->
                        noop()
                        //conn.Execute($"UPDATE {Tn.MapCall2ApiCall} SET active = {isActive} WHERE id = {row.Id}",
                        //            transaction=tr) |> ignore
                    | None ->
                        let guid = newGuid()
                        let affectedRows = conn.Execute(
                                $"INSERT INTO {Tn.MapCall2ApiCall} (callId, apiCallId,   guid)
                                                            VALUES (@CallId, @ApiCallId, @Guid)",
                                {| CallId = c.Id.Value; ApiCallId = apiCallId ; Guid=guid |}, tr)

                        noop()
                    ()

            // work 의 arrows 를 삽입 (calls 간 연결)
            for a in w.Arrows do
                let ormArrow = a.ToORM<ORMArrowCall>(dbApi)
                ormArrow.WorkId <- workId

                let r = conn.Upsert(Tn.ArrowCall, ormArrow,
                    [ "Source"; "Target"; "TypeId"; "WorkId"; "Guid"; "Parameter" ],
                    jsonbColumns=jsonbColumns,
                    onInserted=idUpdator [ormArrow; a;])
                ()

        // system 의 arrows 를 삽입 (works 간 연결)
        for a in s.Arrows do
            let ormArrow = a.ToORM<ORMArrowWork>(dbApi)
            ormArrow.SystemId <- sysId

            let r = conn.Upsert(Tn.ArrowWork, ormArrow,
                    [ "Source"; "Target"; "TypeId"; "SystemId"; "Guid"; "Parameter" ],
                    jsonbColumns=jsonbColumns,
                    onInserted=idUpdator [ormArrow; a;])
            ()

    let commitSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem) (optProject:RtProject option) =
        match s.Id with
        | Some id ->             // 이미 DB 에 저장된 system 이므로 update
            failwith "ERROR: 구현"
        | None ->   // DB 에 저장되지 않은 system 이므로 insert
            insertSystemToDBHelper dbApi s optProject



    /// DsProject 을 database (sqlite or pgsql) 에 저장
    let insertProjectToDB (proj:RtProject) (dbApi:AppDbApi) (removeExistingData:bool option) =
        let rtObjs =
            proj.EnumerateRtObjects()
            |> List.cast<RtUnique>

        let grDic = rtObjs |> groupByToDictionary _.GetType()

        let systems =
            grDic.[typeof<RtSystem>]
            |> Seq.cast<RtSystem> |> List.ofSeq

        let onError (ex:Exception) = logError $"insertProjectToDB failed: {ex.Message}"; raise ex

        checkHandlers()

        dbApi.With(fun (conn, tr) ->
            match removeExistingData, proj.Id with
            | Some true, Some id ->
                //dbApi.DbProvider.TruncateAllTables(conn) |> ignore
                conn.Execute($"DELETE FROM {Tn.Project} WHERE id = {id}", tr) |> ignore
                //conn.Execute($"DELETE FROM {Tn.ProjectSystemMap} WHERE projectId = {id}", tr) |> ignore
            | _ -> ()

            Guid2UniqDic() |> dbApi.DDic.Set
            let ormProject = proj.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project}
                           (guid,   parameter,                     dateTime,  name,  author,  version,  description)
                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)

            proj.Id <- Some projId
            ormProject.Id <- Some projId

            for s in systems do
                commitSystemToDBHelper dbApi s (Some proj)

            proj.Database <- dbApi.DbProvider
        , onError)


    let commitSystemToDB (x:RtSystem) (dbApi:AppDbApi) =
        let onError (ex:Exception) = logError $"insertSystemToDB failed: {ex.Message}"; raise ex

        dbApi.With(fun (conn, tr) ->
            Guid2UniqDic() |> dbApi.DDic.Set
            let ormSystem = x.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            commitSystemToDBHelper dbApi x None
        , onError)



[<AutoOpen>]
module internal DbUpdateImpl =
    [<Obsolete("구현 필요")>]
    let updateProjectToDB (proj:RtProject) (dbApi:AppDbApi) =
        ()

[<AutoOpen>]
module internal Db2DsImpl =

    //let deleteFromDatabase(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
    //    ()

    //let deleteFromDatabaseWithConnectionString(identifier:DbObjectIdentifier) (connStr:string) =
    //    DbApi(connStr).With(fun (conn, tr) ->
    //        deleteFromDatabase identifier conn tr
    //    )

    // 사전 조건: ormSystem.RtObject 에 RtSystem 이 생성되어 있어야 한다.
    let private checkoutSystemFromDBHelper(ormSystem:ORMSystem) (dbApi:AppDbApi) :RtSystem =
        let conn, tr = dbApi.ActiveConnection, dbApi.ActiveTransaction
        let rtSystem = ormSystem.RtObject >>= tryCast<RtSystem> |?? (fun () -> failwith "ERROR")
        let s = rtSystem
        let rtFlows = [
            let orms = conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @Id", s, tr)

            for ormFlow in orms do
                let f = {| FlowId = ormFlow.Id |}
                let ormButtons    = conn.Query<ORMButton>   ($"SELECT * FROM {Tn.Button}    WHERE flowId = @FlowId", f,  tr)
                let ormLamps      = conn.Query<ORMLamp>     ($"SELECT * FROM {Tn.Lamp}      WHERE flowId = @FlowId", f,  tr)
                let ormConditions = conn.Query<ORMCondition>($"SELECT * FROM {Tn.Condition} WHERE flowId = @FlowId", f,  tr)
                let ormActions    = conn.Query<ORMAction>   ($"SELECT * FROM {Tn.Action}    WHERE flowId = @FlowId", f,  tr)

                let buttons    = ormButtons    |-> (fun z -> RtButton    ()) |> toArray
                let lamps      = ormLamps      |-> (fun z -> RtLamp      ()) |> toArray
                let conditions = ormConditions |-> (fun z -> RtCondition ()) |> toArray
                let actions    = ormActions    |-> (fun z -> RtAction    ()) |> toArray

                RtFlow(buttons, lamps, conditions, actions, RawParent = Some s)
                |> uniqReplicate ormFlow
        ]
        rtFlows |> s.AddFlows

        let rtApiDefs = [
            let orms =  conn.Query<ORMApiDef>($"SELECT * FROM {Tn.ApiDef} WHERE systemId = @Id", s, tr)

            for orm in orms do
                RtApiDef(orm.IsPush, RawParent = Some s)
                |> uniqReplicate orm
        ]
        rtApiDefs |> s.AddApiDefs

        let rtApiCalls = [
            let orms = conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE systemId = {s.Id.Value}", tr)

            /// orm.ApiDefId -> RtApiDef : rtSystem 하부의 RtApiDef 타입 객체들
            let rtApiDefs = rtSystem.EnumerateRtObjects().OfType<RtApiDef>().ToArray()
            for orm in orms do

                // orm.ApiDefId -> rtApiDef -> _.Guid
                let apiDefGuid = rtApiDefs.First(fun z -> z.Id = Some orm.ApiDefId).Guid

                let valueParam = IValueSpec.TryDeserialize orm.ValueSpec
                RtApiCall(apiDefGuid, orm.InAddress, orm.OutAddress,
                            orm.InSymbol, orm.OutSymbol, valueParam)
                |> uniqReplicate orm
        ]
        rtApiCalls |> s.AddApiCalls



        let rtWorks = [
            let orms = conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @Id", s, tr)

            for orm in orms do
                RtWork.Create()
                |> setParent s
                |> uniqReplicate orm
                |> tee(fun w ->
                    if orm.FlowId.HasValue then
                        let flow = rtFlows |> find(fun f -> f.Id.Value = orm.FlowId.Value)
                        //w.Status4 <- orm.Status4Id
                        w.Flow <- Some flow
                    w.Status4 <- n2o orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                    w.Motion     <- orm.Motion
                    w.Script     <- orm.Script
                    w.IsFinished <- orm.IsFinished
                    w.NumRepeat  <- orm.NumRepeat
                    w.Period     <- orm.Period
                    w.Delay      <- orm.Delay )
        ]
        rtWorks |> s.AddWorks

        for w in rtWorks do
            let rtCalls = [
                let orms = conn.Query<ORMCall>(
                        $"SELECT * FROM {Tn.Call} WHERE workId = @WorkId",
                        {| WorkId = w.Id.Value |}, tr)

                for orm in orms do

                    let callType = orm.CallTypeId.Value |> dbApi.TryFindEnumValue |> Option.get
                    let apiCallGuids =
                        conn.Query<Guid>(
                        $"""SELECT ac.guid
                            FROM {Tn.MapCall2ApiCall} m
                            JOIN {Tn.ApiCall} ac ON ac.id = m.apiCallId
                            WHERE m.callId = @CallId"""
                        , {| CallId = orm.Id.Value |}, tr)

                    let acs = orm.AutoConditions |> jsonDeserializeStrings
                    let ccs = orm.CommonConditions |> jsonDeserializeStrings
                    RtCall(callType, apiCallGuids, acs, ccs, orm.IsDisabled, n2o orm.Timeout)
                    |> setParent w
                    |> uniqReplicate orm
                    |> tee(fun c ->
                        c.Status4 <- n2o orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4>
                        rtApiCalls
                        |> iter (fun apiCall ->
                            apiCall
                            |> uniqParent (Some c)
                            |> ignore))
            ]
            w.AddCalls rtCalls


            // work 내의 call 간 연결
            let rtArrows = [
                let orms = conn.Query<ORMArrowCall>(
                        $"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId",
                        {| WorkId = w.Id.Value |}, tr)

                for orm in orms do
                    let src = rtCalls |> find(fun c -> c.Id.Value = orm.Source)
                    let tgt = rtCalls |> find(fun c -> c.Id.Value = orm.Target)
                    let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get

                    RtArrowBetweenCalls(src, tgt, arrowType)
                    |> uniqReplicate orm
            ]
            w.AddArrows rtArrows

            // call 이하는 더 이상 읽어 들일 구조가 없다.
            for c in rtCalls do
                ()


        // system 내의 work 간 연결
        let rtArrows = [
            let orms = conn.Query<ORMArrowWork>(
                    $"SELECT * FROM {Tn.ArrowWork} WHERE systemId = @SystemId"
                    , {| SystemId = s.Id.Value |}, tr)

            for orm in orms do
                let src = rtWorks |> find(fun w -> w.Id.Value = orm.Source)
                let tgt = rtWorks |> find(fun w -> w.Id.Value = orm.Target)
                let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get

                RtArrowBetweenWorks(src, tgt, arrowType)
                |> uniqReplicate orm
        ]
        rtArrows |> s.AddArrows
        assert(setEqual s.Arrows rtArrows)

        rtSystem



    let private checkoutProjectFromDBHelper(ormProject:ORMProject) (dbApi:AppDbApi):RtProject =
        let conn, tr = dbApi.ActiveConnection, dbApi.ActiveTransaction
        let projSysMaps =
            conn.Query<ORMMapProjectSystem>(
                $"SELECT * FROM {Tn.MapProject2System} WHERE projectId = @ProjectId",
                {| ProjectId = ormProject.Id |}, tr)
            |> toArray

        let rtProj =
            RtProject.Create()
            |> uniqReplicate ormProject

        let ormSystems =
            let systemIds = projSysMaps |-> _.SystemId

            conn.Query<ORMSystem>($"SELECT * FROM {Tn.System} WHERE id IN @SystemIds",
                {| SystemIds = systemIds |}, tr)
            |> tees (fun os ->
                    RtSystem.Create()
                    |> uniqReplicate os
                    |> uniqParent (Some rtProj))
            |> toArray

        let rtSystems =
            ormSystems |-> (fun os -> os.RtObject >>= tryCast<RtSystem> |?? (fun () -> failwith "ERROR"))

        let actives, passives =
            rtSystems
            |> partition (fun s ->
                projSysMaps
                |> tryFind(fun m -> m.SystemId = s.Id.Value)
                |-> _.IsActive |? false)

        actives  |> rtProj.RawActiveSystems.AddRange
        passives |> rtProj.RawPassiveSystems.AddRange

        ormSystems |> iter (fun os -> checkoutSystemFromDBHelper os dbApi |> ignore)

        rtProj

    let private tryGetORMRowWithId<'T> (conn:IDbConnection) (tr:IDbTransaction) (tableName:string) (id:Id) =
        conn.TryQuerySingle<'T>($"SELECT * FROM {tableName} WHERE id=@Id", {|Id = id|}, tr)


    let rTryCheckoutProjectFromDB(id:Id) (dbApi:AppDbApi):Result<RtProject, ErrorMessage> =
        Trace.WriteLine($"--------------------------------------- checkoutProjectFromDB: {id}")
        dbApi.With(fun (conn, tr) ->
            match tryGetORMRowWithId<ORMProject> conn tr Tn.Project id with
            | None ->
                Error <| sprintf "Project not found: %A" id
            | Some ormProject ->
                Ok <| checkoutProjectFromDBHelper ormProject dbApi)

    let rTryCheckoutSystemFromDB(id:Id) (dbApi:AppDbApi):Result<RtSystem, ErrorMessage> =
        Trace.WriteLine($"--------------------------------------- checkoutSystemFromDB: {id}")

        dbApi.With(fun (conn, tr) ->
            match tryGetORMRowWithId<ORMSystem> conn tr Tn.System id with
            | None ->
                Error <| sprintf "System not found: %A" id
            | Some ormSystem ->
                ormSystem.RtObject <-
                    let rtSystem =
                        RtSystem.Create()
                        |> uniqReplicate ormSystem
                    Some rtSystem

                Ok <| checkoutSystemFromDBHelper ormSystem dbApi )


[<AutoOpen>]
module Ds2SqliteModule =

    open Ds2DbImpl
    open Db2DsImpl

    type RtProject with // CommitToDB, CheckoutFromDB
        member x.CommitToDB(dbApi:AppDbApi, ?removeExistingData:bool) =
            match dbApi.WithConn _.TryQuerySingle($"SELECT * FROM {Tn.Project} WHERE guid = @Guid", {| Guid = x.Guid |}) with
            | Some _ ->
                // 이미 존재하는 프로젝트는 업데이트
                updateProjectToDB x dbApi
            | None ->
                // 신규 project 삽입
                insertProjectToDB x dbApi removeExistingData

        member x.RemoveFromDB(dbApi:AppDbApi) =
            ()

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi):Result<RtProject, ErrorMessage> =
            rTryCheckoutProjectFromDB id dbApi

        static member RTryCheckoutFromDB(projectName:string, dbApi:AppDbApi):Result<RtProject, ErrorMessage> =
            let id = dbApi.WithConn (fun conn ->
                match conn.TryQuerySingle<int>($"SELECT id FROM {Tn.Project} WHERE name = @Name", {| Name = projectName |}) with
                | Some id -> id
                | None -> failwithf "Project not found: %s" projectName)
            RtProject.RTryCheckoutFromDB(id, dbApi)

        static member CheckoutFromDB(projectName:string, dbApi:AppDbApi): RtProject = RtProject.RTryCheckoutFromDB(projectName, dbApi) |> Result.toObj
        static member CheckoutFromDB(id:Id, dbApi:AppDbApi): RtProject = RtProject.RTryCheckoutFromDB(id, dbApi) |> Result.toObj


    type RtSystem with  // CommitToDB, CheckoutFromDB
        member x.CommitToDB(dbApi:AppDbApi) =
            commitSystemToDB x dbApi

        static member RTryCheckoutFromDB(id:Id, dbApi:AppDbApi):Result<RtSystem, ErrorMessage> =
            rTryCheckoutSystemFromDB id dbApi
