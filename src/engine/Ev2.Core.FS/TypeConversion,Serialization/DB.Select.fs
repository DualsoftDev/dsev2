namespace Ev2.Core.FS

open System
open System.Data
open System.Linq
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS

type DbObjectIdentifier = //
    | ByGuid of Guid
    | ById of int
    | ByName of string

/// DB commit success 응답.  예외난 경우 제외.
type DbCommitSuccessResponse = // Stringify
    /// commit 으로 인한 db 변경 없음
    | NoChange
    /// 신규 추가 (하부 포함)
    | Inserted
    /// 수정, 하부 추가/삭제
    | Updated of CompareResult[]
    /// 삭제 (하부 포함)
    | Deleted
    with
        member x.Stringify() =
            match x with
            | NoChange -> "NoChange"
            | Inserted -> "Inserted"
            | Updated diffs -> $"Updated ({diffs.Length} changes)"
            | Deleted -> "Deleted"

type DbCommitResult = Result<DbCommitSuccessResponse, ErrorMessage>

type DbCheckoutResult<'T> = Result<'T, ErrorMessage>

[<AutoOpen>]
module internal Db2DsImpl =

    //let deleteFromDatabase(identifier:DbObjectIdentifier) (conn:IDbConnection) (tr:IDbTransaction) =
    //    ()

    //let deleteFromDatabaseWithConnectionString(identifier:DbObjectIdentifier) (connStr:string) =
    //    DbApi(connStr).With(fun (conn, tr) ->
    //        deleteFromDatabase identifier conn tr
    //    )

    [<Obsolete("N+1 query 최적화 필요!.")>]
    // 사전 조건: ormSystem.RtObject 에 RtSystem 이 생성되어 있어야 한다.
    let private rTryCheckoutSystemFromDBHelper(ormSystem:ORMSystem) (dbApi:AppDbApi): DbCheckoutResult<DsSystem> =
        let helper(conn:IDbConnection, tr:IDbTransaction) =
            let handleAfterSelect (runtime:IRtUnique) =
                getTypeFactory() |> iter (fun factory -> factory.HandleAfterSelect(runtime, conn, tr))

            let rtSystem = ormSystem.RtObject >>= tryCast<DsSystem> |?? (fun () -> failwith "ERROR")
            verify(rtSystem.Guid = ormSystem.Guid)
            let s = rtSystem
            let rtFlows = [
                let orms = conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @Id", s, tr)

                for ormFlow in orms do
                    let f = {| FlowId = ormFlow.Id |}
                    let ormButtons    = conn.Query<ORMButton>   ($"SELECT * FROM {Tn.Button}    WHERE flowId = @FlowId", f,  tr)
                    let ormLamps      = conn.Query<ORMLamp>     ($"SELECT * FROM {Tn.Lamp}      WHERE flowId = @FlowId", f,  tr)
                    let ormConditions = conn.Query<ORMCondition>($"SELECT * FROM {Tn.Condition} WHERE flowId = @FlowId", f,  tr)
                    let ormActions    = conn.Query<ORMAction>   ($"SELECT * FROM {Tn.Action}    WHERE flowId = @FlowId", f,  tr)

                    let buttons    = ormButtons    |-> (fun z -> new DsButton() |> replicateProperties z) |> toArray
                    let lamps      = ormLamps      |-> (fun z -> new Lamp() |> replicateProperties z) |> toArray
                    let conditions = ormConditions |-> (fun z -> new DsCondition() |> replicateProperties z) |> toArray
                    let actions    = ormActions    |-> (fun z -> new DsAction() |> replicateProperties z) |> toArray

                    let flow = Flow.Create(buttons, lamps, conditions, actions)
                    setParentI s flow
                    flow
                    |> replicateProperties ormFlow
                    |> tee handleAfterSelect
            ]

            s.addFlows(rtFlows, false)

            let rtApiDefs = [
                let orms =  conn.Query<ORMApiDef>($"SELECT * FROM {Tn.ApiDef} WHERE systemId = @Id", s, tr)

                for orm in orms do
                    let apiDef = ApiDef.Create()
                    apiDef.IsPush <- orm.IsPush
                    apiDef
                    |> replicateProperties orm
                    |> tee handleAfterSelect

            ]
            s.addApiDefs(rtApiDefs, false)

            let rtApiCalls = [
                let orms = conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE systemId = {s.Id.Value}", tr)

                /// orm.ApiDefId -> RtApiDef : rtSystem 하부의 RtApiDef 타입 객체들
                let rtApiDefs = rtSystem.EnumerateRtObjects().OfType<ApiDef>().ToArray()
                for orm in orms do

                    // orm.ApiDefId -> rtApiDef -> _.Guid
                    let apiDefGuid = rtApiDefs.First(fun z -> z.Id = Some orm.ApiDefId).Guid

                    let valueParam = IValueSpec.TryDeserialize orm.ValueSpec
                    let apiCall = ApiCall.Create()
                    apiCall.ApiDefGuid <- apiDefGuid
                    apiCall.InAddress <- orm.InAddress
                    apiCall.OutAddress <- orm.OutAddress
                    apiCall.InSymbol <- orm.InSymbol
                    apiCall.OutSymbol <- orm.OutSymbol
                    apiCall.ValueSpec <- valueParam
                    apiCall
                    |> replicateProperties orm
                    |> tee handleAfterSelect
            ]
            s.addApiCalls(rtApiCalls, false)



            let rtWorks = [
                let orms = conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @Id", s, tr)

                for orm in orms do
                    Work.Create()
                    |> setParent s
                    |> replicateProperties orm
                    |> tee handleAfterSelect
                    |> tee(fun w ->
                        match orm.FlowId with
                        | Some flowId ->
                            let flow = rtFlows |> find(fun f -> f.Id.Value = flowId)
                            w.Flow <- Some flow
                        | None -> ()

                        w.Status4    <- orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4> )

            ]
            s.addWorks(rtWorks, false)

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
                        Call.Create(callType, apiCallGuids, acs, ccs, orm.IsDisabled, orm.Timeout)
                        |> replicateProperties orm
                        |> tee handleAfterSelect
                        |> setParent w
                        |> tee(fun c -> c.Status4 <- orm.Status4Id >>= dbApi.TryFindEnumValue<DbStatus4> )
                ]
                w.addCalls(rtCalls, false)


                // work 내의 call 간 연결
                let rtArrows = [
                    let orms = conn.Query<ORMArrowCall>(
                            $"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId",
                            {| WorkId = w.Id.Value |}, tr)

                    for orm in orms do
                        let src = rtCalls |> find(fun c -> c.Id.Value = orm.Source)
                        let tgt = rtCalls |> find(fun c -> c.Id.Value = orm.Target)
                        let arrowType = dbApi.TryFindEnumValue<DbArrowType> orm.TypeId |> Option.get

                        ArrowBetweenCalls.Create(src, tgt, arrowType)
                        |> replicateProperties orm
                        |> tee handleAfterSelect
                ]
                w.addArrows(rtArrows, false)

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

                    ArrowBetweenWorks.Create(src, tgt, arrowType)
                    |> replicateProperties orm
                    |> tee handleAfterSelect
            ]
            s.addArrows(rtArrows, false)
            assert(setEqual s.Arrows rtArrows)

            // 확장 복원 훅
            getTypeFactory() |> iter (fun factory -> factory.HandleAfterSelect(rtSystem, conn, tr))

            rtSystem

        try
            Ok (dbApi.With helper)
        with ex ->
            Error <| sprintf "Failed to checkout system from DB: %s" ex.Message



    let private rTryCheckoutProjectFromDBHelper(ormProject:ORMProject) (dbApi:AppDbApi): DbCheckoutResult<Project> =
        let helper(conn:IDbConnection, tr:IDbTransaction) =
            let projSysMaps =
                conn.Query<ORMMapProjectSystem>(
                    $"SELECT * FROM {Tn.MapProject2System} WHERE projectId = @ProjectId",
                    {| ProjectId = ormProject.Id |}, tr)
                |> toArray

            let rtProj =
                Project.Create()
                |> replicateProperties ormProject

            let ormSystems =
                let systemIds = projSysMaps |-> _.SystemId

                let sql =
                    let idCheck = if dbApi.IsPostgres() then "id = ANY(@SystemIds)" else "id IN @SystemIds"
                    $"SELECT * FROM {Tn.System} WHERE {idCheck}"

                conn.Query<ORMSystem>(sql, {| SystemIds = systemIds |}, tr)
                |> tees (fun os ->
                    let sys = createExtended<DsSystem>()
                    sys |> replicateProperties os |> ignore
                    sys |> uniqParent (Some rtProj) |> ignore
                    os.RtObject <- Some (sys :> IRtUnique))
                |> toArray

            let (ormActiveSystems, ormPassiveSystems) =
                let activeSystemIds = projSysMaps |> filter (fun m -> m.IsActiveSystem) |-> _.SystemId |> toArray
                ormSystems |> Seq.partition(fun s -> activeSystemIds.Contains s.Id.Value)

            let rtActives = ormActiveSystems   |-> (fun os -> os.RtObject >>= tryCast<DsSystem> |?? (fun () -> failwith "ERROR"))
            let rtPassives = ormPassiveSystems |-> (fun os -> os.RtObject >>= tryCast<DsSystem> |?? (fun () -> failwith "ERROR"))

            rtActives  |> rtProj.RawActiveSystems.AddRange
            rtPassives |> rtProj.RawPassiveSystems.AddRange

            ormSystems |> iter (fun os -> rTryCheckoutSystemFromDBHelper os dbApi |> ignore)

            // 확장 복원 훅
            getTypeFactory() |> iter (fun factory -> factory.HandleAfterSelect(rtProj, conn, tr))
            rtProj.OnAfterLoad(conn, tr)

            rtProj

        try
            Ok (dbApi.With helper)
        with ex ->
            Error <| sprintf "Failed to checkout project from DB: %s" ex.Message

    let private tryGetORMRowWithId<'T> (conn:IDbConnection) (tr:IDbTransaction) (tableName:string) (id:Id) =
        conn.TryQuerySingle<'T>($"SELECT * FROM {tableName} WHERE id=@Id", {|Id = id|}, tr)


    /// Project 의 PK id 로 DB 에서 Project 를 조회하고, 성공 시 DbCheckoutResult<Project> 객체를 반환
    let rTryCheckoutProjectFromDB(id:Id) (dbApi:AppDbApi):DbCheckoutResult<Project> =
        dbApi.With(fun (conn, tr) ->
            match tryGetORMRowWithId<ORMProject> conn tr Tn.Project id with
            | None ->
                Error <| sprintf "Project not found: %A" id
            | Some ormProject ->
                rTryCheckoutProjectFromDBHelper ormProject dbApi)

    /// System 의 PK id 로 DB 에서 DsSystem 를 조회하고, 성공 시 DbCheckoutResult<DsSystem> 객체를 반환
    let rTryCheckoutSystemFromDB(id:Id) (dbApi:AppDbApi):DbCheckoutResult<DsSystem> =
        dbApi.With(fun (conn, tr) ->
            match tryGetORMRowWithId<ORMSystem> conn tr Tn.System id with
            | None ->
                Error <| $"System not found with id: {id}"
            | Some ormSystem ->
                ormSystem.RtObject <-
                    let rtSystem =
                        DsSystem.Create()
                        |> replicateProperties ormSystem
                    Some (rtSystem :> IRtUnique)

                rTryCheckoutSystemFromDBHelper ormSystem dbApi
        )



