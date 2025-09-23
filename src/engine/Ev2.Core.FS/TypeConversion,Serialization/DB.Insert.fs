namespace Ev2.Core.FS

open System
open System.Linq
open System.Diagnostics
open Dapper
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS


[<AutoOpen>]
module internal DbInsertModule =

    type Project with // InsertSystemMapToDB
        // project 하부에 연결된 passive system 을 DB 에 저장
        member x.InsertSystemMapToDB(dbApi:DbApi) =

            dbApi.With(fun (conn, tr) ->


                let insertSystemMap (isActiveSystem:bool) (s:DsSystem) =
                    // s.PrototypeSystemGuid 에 따라 다른 처리???
                    //assert(not s.PrototypeSystemGuid.Is)

                    // update projectSystemMap table
                    let projId = x.Id.Value
                    let sysId = s.Id.Value


                    match conn.TryQuerySingle<ORMMapProjectSystem>(
                                    $"""SELECT * FROM {Tn.MapProject2System}
                                        WHERE projectId = @ProjectId AND systemId = @SystemId""", {|ProjectId = projId; SystemId=sysId|}, tr) with
                    | Some row ->
                            conn.Execute($"""UPDATE {Tn.MapProject2System}
                                             SET loadedName = @LoadedName, isActiveSystem=@IsActiveSystem
                                             WHERE id = @Id""", {|LoadedName=s.Name; Id=row.Id; IsActiveSystem=isActiveSystem|}, tr) |> ignore
                    | None ->
                        let affectedRows = conn.Execute(
                                $"""INSERT INTO {Tn.MapProject2System}
                                            (projectId, systemId, loadedName, isActiveSystem, guid)
                                     VALUES (@ProjectId, @SystemId, @LoadedName, @IsActiveSystem, @Guid)""",
                                {|  ProjectId = projId; SystemId = sysId; LoadedName=s.Name; IsActiveSystem=isActiveSystem;
                                    Guid=Guid.NewGuid() |}, tr)
                        ()

                x.ActiveSystems  |> iter (insertSystemMap true)
                x.PassiveSystems |> iter (insertSystemMap false)
            )


    type IRtUnique with // InsertToDB
        member x.InsertToDB(dbApi:AppDbApi) =
            let bag = dbApi.DDic.TryGet<DuplicateBag>() |?? (fun () -> DuplicateBag() |> tee dbApi.DDic.Set)
            let guidDicDebug = bag.OldGuid2NewObjectMap
            dbApi.With(fun (conn, tr) ->

                match box x with
                | :? Project as rt ->
                    let orm = rt.ToORM(dbApi)
                    assert (guidDicDebug.Any())

                    let projId =
                        conn.Insert($"""INSERT INTO {Tn.Project}
                                    (guid,   parameter,                     staticOption,  dynamicOption,  dateTime,  name,  author,  version,  description)
                            VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @DateTime, @Name, @Author, @Version, @Description);""", orm, tr)

                    rt.Id <- Some projId

                    (rt.PassiveSystems @ rt.ActiveSystems) |> iter _.InsertToDB(dbApi)  // 시스템 하부에 연결된 시스템들을 삽입 (재귀적 호출, prototype 은 제외됨)
                    //proj.Id <- Some projId
                    orm.Id <- Some projId
                    rt.Database <- dbApi.DbProvider

                    rt.InsertSystemMapToDB(dbApi)
                    rt.OnSaved(conn, tr)

                | :? DsSystem as rt ->
                    let ormSystem = rt.ToORM<ORMSystem>(dbApi)

                    let xxx = conn.Query<ORMSystem>($"SELECT * FROM {Tn.System}").ToArray()

                    let sysId = conn.Insert($"""INSERT INTO {Tn.System}
                                            (guid, parameter,                     staticOption, dynamicOption, dateTime,  name,  iri, author,     langVersion, engineVersion, description,  ownerProjectId)
                                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @DateTime, @Name, @IRI, @Author, @LangVersion, @EngineVersion, @Description, @OwnerProjectId);""",
                                    ormSystem, tr)

                    rt.Id <- Some sysId
                    guidDicDebug[rt.Guid].Id <- Some sysId

                    // flows 삽입 (먼저 삽입하여 Id 획득)
                    rt.Flows |> iter _.InsertToDB(dbApi)

                    //// Button, Lamp, Condition, Action 삽입 (DsSystem에 속하며 Flow.Id 참조)
                    //rt.Buttons    |> iter _.InsertToDB(dbApi)
                    //rt.Lamps      |> iter _.InsertToDB(dbApi)
                    //rt.Conditions |> iter _.InsertToDB(dbApi)
                    //rt.Actions    |> iter _.InsertToDB(dbApi)

                    // system 의 apiCalls 를 삽입
                    rt.ApiCalls |> iter _.InsertToDB(dbApi)

                    // works 및 하부의 calls 삽입
                    rt.Works |> iter _.InsertToDB(dbApi)

                    // system 의 arrows 를 삽입 (works 간 연결)
                    rt.Arrows |> iter _.InsertToDB(dbApi)

                    // polymorphic system entities 저장
                    let serializedEntities = rt.PolymorphicJsonEntities.JsonizeArray()

                    for (typeName, entityJsonText) in serializedEntities do
                        conn.Execute(
                            $"INSERT INTO {Tn.SystemEntity} (systemId, type, json) VALUES (@SystemId, @Type, @Json)",
                            {| SystemId = sysId; Type = typeName; Json = entityJsonText |}, tr)
                        |> ignore

                    // system 의 apiDefs 를 삽입
                    rt.ApiDefs |> iter _.InsertToDB(dbApi)


                | :? ApiDef as rt ->
                    let orm = rt.ToORM<ORMApiDef>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id
                    orm.TxId <- rt.TX.Id
                    orm.RxId <- rt.RX.Id

                    let apiDefId =
                        conn.Insert($"""INSERT INTO {Tn.ApiDef}
                                               (guid, parameter,                      staticOption, dynamicOption, name, isPush, txId, rxId, systemId)
                                        VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name, @IsPush, @TxId, @RxId, @SystemId);""", orm, tr)  // @TopicIndex, @IsTopicOrigin,

                    rt.Id <- Some apiDefId
                    orm.Id <- Some apiDefId
                    assert(guidDicDebug[rt.Guid] = orm)

                | :? ApiCall as rt ->
                    let orm = rt.ToORM<ORMApiCall>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let apiCallId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ApiCall}
                                       (guid,   parameter,                     staticOption,  dynamicOption,  name, systemId,  apiDefId,  inAddress,   outAddress, inSymbol,   outSymbol, valueSpec,                      ioTagsJson, valueSpecHint)
                                VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name, @SystemId, @ApiDefId, @InAddress, @OutAddress, @InSymbol, @OutSymbol, @ValueSpec{dbApi.DapperJsonB}, @IOTagsJson, @ValueSpecHint);"""
                            , orm, tr)

                    rt.Id <- Some apiCallId
                    orm.Id <- Some apiCallId
                    assert(guidDicDebug[rt.Guid] = orm)

                | :? Flow as rt ->
                    let orm = rt.ToORM<ORMFlow>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let flowId = conn.Insert($"""INSERT INTO {Tn.Flow}
                                            (guid, parameter, staticOption, dynamicOption, name, systemId)
                                     VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name, @SystemId);""", orm, tr)

                    rt.Id <- Some flowId
                    orm.Id <- Some flowId
                    assert (guidDicDebug[rt.Guid] = orm)

                | :? Work as rt ->
                    let orm = rt.ToORM<ORMWork>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let workId = conn.Insert($"""INSERT INTO {Tn.Work}
                                        (guid, parameter,                      staticOption, dynamicOption, name,  systemId,  flowId,  status4Id,  motion,  script,  externalStart,  isFinished,  numRepeat,  period,  delay)
                                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name, @SystemId, @FlowId, @Status4Id, @Motion, @Script, @ExternalStart, @IsFinished, @NumRepeat, @Period, @Delay);""", orm, tr)

                    rt.Id <- Some workId
                    orm.Id <- Some workId
                    assert(guidDicDebug[rt.Guid] = orm)

                    rt.Calls |> iter _.InsertToDB(dbApi)

                    // work 의 arrows 를 삽입 (calls 간 연결)
                    rt.Arrows |> iter _.InsertToDB(dbApi)


                | :? Call as rt ->
                    let orm = rt.ToORM<ORMCall>(dbApi)
                    orm.WorkId <- rt.RawParent >>= _.Id

                    let callId =
                        conn.Insert($"""INSERT INTO {Tn.Call}
                                    (guid,  parameter,                     staticOption, dynamicOption, name, workId,   status4Id,  callTypeId,  autoConditions, commonConditions, isDisabled, timeout)
                             VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name, @WorkId, @Status4Id, @CallTypeId, @AutoConditions, @CommonConditions, @IsDisabled, @Timeout);""", orm, tr)

                    rt.Id <- Some callId
                    orm.Id <- Some callId
                    assert(guidDicDebug[rt.Guid] = orm)

                    // call - apiCall 에 대한 mapping 정보 삽입
                    for apiCall in rt.ApiCalls do
                        let apiCallId = apiCall.ORMObject >>= tryCast<ORMUnique> >>= _.Id |?? (fun () -> failwith "ERROR")

                        let m = conn.TryQuerySingle<ORMMapCall2ApiCall>(
                                    $"""SELECT * FROM {Tn.MapCall2ApiCall}
                                        WHERE callId = @CallId AND apiCallId = @ApiCallId""", {|CallId=rt.Id; ApiCallId=apiCallId|}, tr)
                        match m with
                        | Some row ->
                            noop()
                            //conn.Execute($"UPDATE {Tn.MapCall2ApiCall} SET active = @IsActive WHERE id = @Id", {|IsActive=isActive; Id=row.Id|}
                            //            tr) |> ignore
                        | None ->
                            let guid = newGuid()
                            let affectedRows = conn.Execute(
                                    $"INSERT INTO {Tn.MapCall2ApiCall} (callId, apiCallId,   guid)
                                                                VALUES (@CallId, @ApiCallId, @Guid)",
                                    {| CallId = rt.Id.Value; ApiCallId = apiCallId ; Guid=guid |}, tr)

                            noop()
                        ()


                | :? ArrowBetweenCalls as rt ->
                    let ormArrow = rt.ToORM<ORMArrowCall>(dbApi)
                    ormArrow.WorkId <- rt.RawParent >>= _.Id |?? (fun () -> failwith "ERROR: RtArrowBetweenCalls must have a WorkId set before inserting to DB.")

                    let arrowCallId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ArrowCall}
                                       ( source, target,   typeId, workId,   guid, parameter,                     staticOption, dynamicOption, name)
                                VALUES (@Source, @Target, @TypeId, @WorkId, @Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name);"""
                            , ormArrow, tr)

                    rt.Id <- Some arrowCallId
                    ormArrow.Id <- Some arrowCallId
                    assert(guidDicDebug[rt.Guid] = ormArrow)

                | :? ArrowBetweenWorks as rt ->
                    let orm = rt.ToORM<ORMArrowWork>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id |?? (fun () -> failwith "ERROR: RtArrowBetweenWorks must have a SystemId set before inserting to DB.")

                    let arrowWorkId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ArrowWork}
                                       (source,   target,  typeId,  systemId,  guid,  parameter,                    staticOption, dynamicOption, name)
                                VALUES (@Source, @Target, @TypeId, @SystemId, @Guid, @Parameter{dbApi.DapperJsonB}, @StaticOption, @DynamicOption, @Name);"""
                            , orm, tr)

                    rt.Id <- Some arrowWorkId
                    orm.Id <- Some arrowWorkId
                    assert(guidDicDebug[rt.Guid] = orm)
                    ()


                | _ -> failwith "ERROR"


                getTypeFactory() |> iter (fun factory -> factory.HandleAfterInsert(x, conn, tr))
            )



    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? Unique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"


    let rTryCommitSystemToDBHelper (dbApi:AppDbApi) (s:DsSystem): DbCommitResult =
        match s.Id with
        | Some id ->             // 이미 DB 에 저장된 system 이므로 update
            rTryCheckoutSystemFromDB id dbApi
            |-> (fun dbSystem ->
                let criteria = Cc(parentGuid=false)
                let mutable diffs = dbSystem.ComputeDiff(s, criteria) |> toArray

                // 확장 속성 diff도 추가
                getTypeFactory()
                |> iter (fun factory ->
                    let extensionDiffs =
                        factory.ComputeExtensionDiff(dbSystem, s)
                        |> Seq.cast<CompareResult>
                        |> toArray
                    if not (extensionDiffs.IsEmpty()) then
                        diffs <- Array.append diffs extensionDiffs)

                match diffs with
                | [||] ->   // DB 에 저장된 system 과 동일하므로 변경 없음
                    NoChange
                | _ ->   // DB 에 저장된 system 과 다르므로 update
                    // 확장 처리 훅만 호출 (실제 업데이트는 DB.Update.fs에서 처리)

                    // 확장 처리 훅
                    getTypeFactory() |> iter (fun factory ->
                        dbApi.With(fun (conn, tr) ->
                            factory.HandleAfterUpdate(s, conn, tr)))

                    Updated diffs
                )
        | None ->   // DB 에 저장되지 않은 system 이므로 insert
            rTryDo(fun () -> s.InsertToDB dbApi; Inserted)
            |> Result.mapError _.Message



    /// DsProject 을 database (sqlite or pgsql) 에 저장
    let rTryInsertProjectToDB (proj:Project) (dbApi:AppDbApi): DbCommitResult =
        try
            proj.InsertToDB dbApi
            Ok Inserted
        with ex ->
            proj.Id <- None
            Error <| sprintf "Failed to insert project to DB: %s" ex.Message


    let rTryCommitSystemToDB (x:DsSystem) (dbApi:AppDbApi): DbCommitResult =
        dbApi.With(fun (conn, tr) ->
            DuplicateBag() |> dbApi.DDic.Set
            let ormSystem = x.ToORM(dbApi)
            assert (dbApi.DDic.Get<DuplicateBag>().OldGuid2NewObjectMap.Any())

            rTryCommitSystemToDBHelper dbApi x)

