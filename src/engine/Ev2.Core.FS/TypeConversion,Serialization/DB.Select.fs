namespace Ev2.Core.FS

open System
open System.Data
open System.Linq
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Runtime.CompilerServices

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

type DbCommitResultExtension =
    [<Extension>]
    static member GetDiffFields(r:DbCommitResult) =
        match r with
        | Ok (Updated diffs) -> diffs |> toList |-> _.GetPropertiesDiffFields()
        | _ -> []
    [<Extension>]
    static member IsNoChangeOrOnlyDiffFields(r:DbCommitResult, fields:string seq) =
        match r with
        | Ok NoChange -> true
        | Ok (Updated diffs) ->
            diffs |> filter (fun d -> not <| d.IsPropertiesDiffOnly(fields)) |> iter (fun d -> tracefn $"Diff: {d.GetPropertiesDiffFields()}")
            diffs |> Seq.forall (fun d -> d.IsPropertiesDiffOnly(fields))
        | _ -> false

type DbCheckoutResult<'T> = Result<'T, ErrorMessage>

[<CLIMutable>]
type private SystemEntityRow =
    { Id: Id
      Guid: Guid
      Type: string
      Json: string }

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

            let rtSystem = ormSystem.RtObject >>= tryCast<DsSystem> |?? (fun () -> fail())
            verify(rtSystem.Guid = ormSystem.Guid)
            let s = rtSystem

            s.PropertiesJson <- ormSystem.PropertiesJson

            let sys = {| SystemId = ormSystem.Id.Value |}

            // Load polymorphic system entities
            let ormEntities = conn.Query<SystemEntityRow>($"SELECT * FROM {Tn.SystemEntity} WHERE systemId = @SystemId", sys, tr).ToArray()

            if ormEntities.Any() then
                let serialized = JArray()
                for row in ormEntities do
                    let jobj = JObject.Parse(row.Json)
                    if isNull (jobj.Property("$type")) then
                        jobj.AddFirst(JProperty("$type", JValue(row.Type)))
                    if isNull (jobj.Property("Guid")) then
                        jobj.Add(JProperty("Guid", JValue(row.Guid)))
                    serialized.Add(jobj)
                s.PolymorphicJsonEntities.SerializedItems <- serialized
                s.PolymorphicJsonEntities.SyncToValues()

                let runtimeEntities = s.Entities |> Seq.toArray
                verify(runtimeEntities.Length = ormEntities.Length)
                for idx = 0 to runtimeEntities.Length - 1 do
                    runtimeEntities[idx].Guid <- ormEntities[idx].Guid
                    runtimeEntities[idx].Id <- Some ormEntities[idx].Id
                    runtimeEntities[idx].RawParent <- Some s

            // Load Flows
            let rtFlows = [
                let orms = conn.Query<ORMFlow>($"SELECT * FROM {Tn.Flow} WHERE systemId = @Id", s, tr)

                for ormFlow in orms do
                    let flow = Flow.Create()
                    setParentI s flow
                    flow
                    |> replicateProperties ormFlow
                    |> tee (fun f -> f.PropertiesJson <- ormFlow.PropertiesJson)
                    |> tee handleAfterSelect
            ]

            s.addFlows(rtFlows, false)

            let rtApiCalls = [
                let orms = conn.Query<ORMApiCall>($"SELECT * FROM {Tn.ApiCall} WHERE systemId = {s.Id.Value}", tr)

                /// orm.ApiDefId -> RtApiDef : rtSystem 하부의 RtApiDef 타입 객체들
                let container = rtSystem.Project >>= tryCast<RtUnique> |? (rtSystem :> RtUnique)
                let rtApiDefs = container.EnumerateRtObjects().OfType<ApiDef>().ToArray()
                for orm in orms do

                    // orm.ApiDefId -> rtApiDef -> _.Guid
                    let apiDefGuid =
                        let ormProperties = orm.PropertiesJson |> JsonPolymorphic.FromJson<ApiCallProperties>
                        rtApiDefs.First(fun z -> z.Guid = ormProperties.ApiDefGuid).Guid
                    let valueParam = IValueSpec.TryDeserialize orm.ValueSpec
                    ApiCall.Create(ApiCallProperties.Create(), valueParam)
                    |> replicateProperties orm
                    |> tee (fun ac -> ac.Properties.ApiDefGuid <- apiDefGuid )
                    |> tee handleAfterSelect
            ]
            s.addApiCalls(rtApiCalls, false)



            let rtWorks = [
                let orms = conn.Query<ORMWork>($"SELECT * FROM {Tn.Work} WHERE systemId = @Id", s, tr).ToArray()

                for orm in orms do
                    Work.Create()
                    |> setParent s
                    |> replicateProperties orm
                    |> tee (fun w -> w.PropertiesJson <- orm.PropertiesJson)
                    |> tee handleAfterSelect
                    |> tee(fun w ->
                        w.FlowGuid <- rtFlows |> tryFind(fun f -> f.Id = orm.FlowId) |-> _.Guid
                        w.FlowId <- orm.FlowId
                        w.Status4  <- orm.Status4Id >>= DbApi.TryGetEnumValue<DbStatus4> )

            ]
            s.addWorks(rtWorks, false)

            for w in rtWorks do
                let rtCalls = [
                    let orms = conn.Query<ORMCall>(
                            $"SELECT * FROM {Tn.Call} WHERE workId = @WorkId",
                            {| WorkId = w.Id.Value |}, tr)

                    for orm in orms do

                        let apiCallGuids =
                            conn.Query<Guid>(
                            $"""SELECT ac.guid
                                FROM {Tn.MapCall2ApiCall} m
                                JOIN {Tn.ApiCall} ac ON ac.id = m.apiCallId
                                WHERE m.callId = @CallId"""
                            , {| CallId = orm.Id.Value |}, tr)

                        // JSON 문자열을 ApiCallValueSpecs로 역직렬화
                        let acs =
                            if orm.AutoConditions.IsNullOrEmpty() then
                                ApiCallValueSpecs()
                            else
                                ApiCallValueSpecs.FromJson(orm.AutoConditions)
                        let ccs =
                            if orm.CommonConditions.IsNullOrEmpty() then
                                ApiCallValueSpecs()
                            else
                                ApiCallValueSpecs.FromJson(orm.CommonConditions)
                        let properties =
                            orm.PropertiesJson
                            |> String.toOption
                            |-> JsonPolymorphic.FromJson<CallProperties>
                            |?? CallProperties.Create
                            |> tee(fun p ->
                                p.ApiCallGuids.Clear()
                                p.ApiCallGuids.AddRange(apiCallGuids))

                        Call.Create(acs, ccs, properties)
                        |> replicateProperties orm
                        |> tee (fun c -> c.PropertiesJson <- orm.PropertiesJson)
                        |> tee handleAfterSelect
                        |> setParent w
                        |> tee(fun c -> c.Status4 <- orm.Status4Id >>= DbApi.TryGetEnumValue<DbStatus4> )
                ]
                w.addCalls(rtCalls, false)


                // work 내의 call 간 연결
                let rtArrows = [
                    let orms = conn.Query<ORMArrowCall>(
                            $"SELECT * FROM {Tn.ArrowCall} WHERE workId = @WorkId",
                            {| WorkId = w.Id.Value |}, tr)

                    for orm in orms do
                        let arrowType = DbApi.TryGetEnumValue<DbArrowType> orm.TypeId |> Option.get
                        let src = rtCalls |> find(fun c -> c.Id.Value = orm.Source)
                        let tgt = rtCalls |> find(fun c -> c.Id.Value = orm.Target)
                        orm.XSourceGuid <- src.Guid
                        orm.XTargetGuid <- tgt.Guid
                        orm.XType <- arrowType

                        noop()
                        ArrowBetweenCalls.Create(src.Guid, tgt.Guid, arrowType)
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
                    let arrowType = DbApi.TryGetEnumValue<DbArrowType> orm.TypeId |> Option.get
                    let src = rtWorks |> find(fun w -> w.Id.Value = orm.Source)
                    let tgt = rtWorks |> find(fun w -> w.Id.Value = orm.Target)
                    orm.XSourceGuid <- src.Guid
                    orm.XTargetGuid <- tgt.Guid
                    orm.XType <- arrowType

                    ArrowBetweenWorks.Create(src.Guid, tgt.Guid, arrowType)
                    |> replicateProperties orm
                    |> tee handleAfterSelect
            ]
            s.addArrows(rtArrows, false)
            assert(setEqual s.Arrows rtArrows)

            let rtApiDefs = [
                let orms =  conn.Query<ORMApiDef>($"SELECT * FROM {Tn.ApiDef} WHERE systemId = @Id", s, tr)

                let sys = ormSystem.RtObject >>= tryCast<DsSystem> |> Option.get
                for orm in orms do


                    ApiDef.Create()
                    |> replicateProperties orm
                    |> tee (fun ad -> ad.PropertiesJson <- orm.PropertiesJson)
                    |> tee handleAfterSelect

            ]
            s.addApiDefs(rtApiDefs, false)



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

            let rtActives = ormActiveSystems   |-> (fun os -> os.RtObject >>= tryCast<DsSystem> |?? (fun () -> fail()))
            let rtPassives = ormPassiveSystems |-> (fun os -> os.RtObject >>= tryCast<DsSystem> |?? (fun () -> fail()))

            rtActives  |> rtProj.RawActiveSystems.AddRange
            rtPassives |> rtProj.RawPassiveSystems.AddRange

            ormSystems
            |-> (fun os -> rTryCheckoutSystemFromDBHelper os dbApi)
            |> iter (function Error err -> failwith $"Failed to check out system: {err}" | _ -> ())

            // 확장 복원 훅
            getTypeFactory() |> iter (fun factory -> factory.HandleAfterSelect(rtProj, conn, tr))
            rtProj.OnLoaded(conn, tr)
            rtProj.OnLoaded()

            rtProj

        try
            Ok (dbApi.With helper)
        with ex ->
            Error <| sprintf "Failed to checkout project from DB: %s" ex.Message

    let private tryGetORMRowWithId<'T> (conn:IDbConnection) (tr:IDbTransaction) (tableName:string) (id:Id) =
        let sql =
            if typeof<'T>.GetProperty("PropertiesJson") |> isNull |> not then
                $"SELECT *, properties AS PropertiesJson FROM {tableName} WHERE id=@Id"
            else
                $"SELECT * FROM {tableName} WHERE id=@Id"

        conn.TryQuerySingle<'T>(sql, {|Id = id|}, tr)


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
