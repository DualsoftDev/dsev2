namespace Ev2.Core.FS

open System
open System.Data
open System.Linq
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS


[<AutoOpen>]
module internal DbInsertImpl =
    /// IUnique 를 상속하는 객체에 대한 db insert/update 시, 메모리 객체의 Id 를 db Id 로 업데이트
    let idUpdator (targets:IUnique seq) (id:int)=
        for t in targets do
            match t with
            | :? Unique  as a -> a.Id <- Some id
            | _ -> failwith $"Unknown type {t.GetType()} in idUpdator"


    let rTryInsertSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem) (optProject:RtProject option): DbCommitResult =
        let helper(conn:IDbConnection, tr:IDbTransaction) =
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


                let apiDefId =
                    conn.Insert($"""INSERT INTO {Tn.ApiDef}
                                           (guid, parameter, name, isPush, systemId)
                                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @IsPush, @SystemId);""", ormApiDef, tr)

                rtAd.Id <- Some apiDefId
                ormApiDef.Id <- Some apiDefId
                assert(guidDicDebug[rtAd.Guid] = ormApiDef)

            // system 의 apiCalls 를 삽입
            for rtAc in s.ApiCalls do
                let ormApiCall = rtAc.ToORM<ORMApiCall>(dbApi)
                ormApiCall.SystemId <- Some sysId

                let apiCallId =
                    conn.Insert(
                        $"""INSERT INTO {Tn.ApiCall}
                                   (guid,   parameter,                     name, systemId,  apiDefId,  inAddress,   outAddress, inSymbol,   outSymbol, valueSpec,                      valueSpecHint)
                            VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId, @ApiDefId, @InAddress, @OutAddress, @InSymbol, @OutSymbol, @ValueSpec{dbApi.DapperJsonB}, @ValueSpecHint);"""
                        , ormApiCall, tr)

                rtAc.Id <- Some apiCallId
                ormApiCall.Id <- Some apiCallId
                assert(guidDicDebug[rtAc.Guid] = ormApiCall)


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

                    let arrowCallId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ArrowCall}
                                       ( source, target,   typeId, workId,   guid, parameter,                     name)
                                VALUES (@Source, @Target, @TypeId, @WorkId, @Guid, @Parameter{dbApi.DapperJsonB}, @Name);"""
                            , ormArrow, tr)

                    a.Id <- Some arrowCallId
                    ormArrow.Id <- Some arrowCallId
                    assert(guidDicDebug[a.Guid] = ormArrow)

                    ()

            // system 의 arrows 를 삽입 (works 간 연결)
            for a in s.Arrows do
                let ormArrow = a.ToORM<ORMArrowWork>(dbApi)
                ormArrow.SystemId <- sysId

                let arrowWorkId =
                    conn.Insert(
                        $"""INSERT INTO {Tn.ArrowWork}
                                   (source,   target,  typeId,  systemId,  guid,  parameter,                    name)
                            VALUES (@Source, @Target, @TypeId, @SystemId, @Guid, @Parameter{dbApi.DapperJsonB}, @Name);"""
                        , ormArrow, tr)

                a.Id <- Some arrowWorkId
                ormArrow.Id <- Some arrowWorkId
                assert(guidDicDebug[a.Guid] = ormArrow)


                ()
            Inserted

        try
            Ok (dbApi.With helper)
        with ex ->
            Error <| sprintf "Failed to insert system to DB: %s" ex.Message




    let rTryCommitSystemToDBHelper (dbApi:AppDbApi) (s:RtSystem) (optProject:RtProject option): DbCommitResult =
        match s.Id with
        | Some id ->             // 이미 DB 에 저장된 system 이므로 update
            rTryCheckoutSystemFromDB id dbApi
            |-> (fun system ->
                let criteria = Cc(parentGuid=false)
                let diffs = system.ComputeDiff(s, criteria) |> toArray

                match diffs with
                | [||] ->   // DB 에 저장된 system 과 동일하므로 변경 없음
                    NoChange
                | _ ->   // DB 에 저장된 system 과 다르므로 update
                    Updated diffs
                )
        | None ->   // DB 에 저장되지 않은 system 이므로 insert
            rTryInsertSystemToDBHelper dbApi s optProject



    /// DsProject 을 database (sqlite or pgsql) 에 저장
    let rTryInsertProjectToDB (proj:RtProject) (dbApi:AppDbApi): DbCommitResult =
        let helper(conn:IDbConnection, tr:IDbTransaction): DbCommitResult =

            let rtObjs =
                proj.EnumerateRtObjects()
                |> List.cast<RtUnique>

            let grDic = rtObjs |> groupByToDictionary _.GetType()

            let systems =
                grDic.[typeof<RtSystem>]
                |> Seq.cast<RtSystem> |> List.ofSeq

            Guid2UniqDic() |> dbApi.DDic.Set
            let ormProject = proj.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            let projId =
                conn.Insert($"""INSERT INTO {Tn.Project}
                            (guid,   parameter,                     dateTime,  name,  author,  version,  description)
                    VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @DateTime, @Name, @Author, @Version, @Description);""", ormProject, tr)

            proj.Id <- Some projId

            let firstError =
                seq {
                    for s in systems do
                        rTryCommitSystemToDBHelper dbApi s (Some proj)
                } |> tryPick (function
                    | Error e -> Some e
                    | Ok _ -> None)

            match firstError with
            | Some e ->
                Error e
            | None ->
                //proj.Id <- Some projId
                ormProject.Id <- Some projId
                proj.Database <- dbApi.DbProvider

                Ok Inserted

        try
            dbApi.With helper
        with ex ->
            proj.Id <- None
            Error <| sprintf "Failed to insert project to DB: %s" ex.Message


    let rTryCommitSystemToDB (x:RtSystem) (dbApi:AppDbApi): DbCommitResult =
        dbApi.With(fun (conn, tr) ->
            Guid2UniqDic() |> dbApi.DDic.Set
            let ormSystem = x.ToORM(dbApi)
            assert (dbApi.DDic.Get<Guid2UniqDic>().Any())

            rTryCommitSystemToDBHelper dbApi x None)



