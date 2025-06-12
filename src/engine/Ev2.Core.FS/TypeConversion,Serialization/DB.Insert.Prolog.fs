namespace Ev2.Core.FS

open System
open Dapper

open Dual.Common.Base
open Dual.Common.Core.FS
open Dual.Common.Db.FS

[<AutoOpen>]
module DBInsertProlog =

    type IRtUnique with
        member x.InsertToDB(dbApi:AppDbApi) =
            let guidDicDebug = dbApi.DDic.Get<Guid2UniqDic>()
            dbApi.With(fun (conn, tr) ->

                match box x with
                | :? RtApiDef as rt ->
                    let orm = rt.ToORM<ORMApiDef>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let apiDefId =
                        conn.Insert($"""INSERT INTO {Tn.ApiDef}
                                               (guid, parameter, name, isPush, systemId)
                                        VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @IsPush, @SystemId);""", orm, tr)

                    rt.Id <- Some apiDefId
                    orm.Id <- Some apiDefId
                    assert(guidDicDebug[rt.Guid] = orm)

                | :? RtApiCall as rt ->
                    let orm = rt.ToORM<ORMApiCall>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let apiCallId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ApiCall}
                                       (guid,   parameter,                     name, systemId,  apiDefId,  inAddress,   outAddress, inSymbol,   outSymbol, valueSpec,                      valueSpecHint)
                                VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId, @ApiDefId, @InAddress, @OutAddress, @InSymbol, @OutSymbol, @ValueSpec{dbApi.DapperJsonB}, @ValueSpecHint);"""
                            , orm, tr)

                    rt.Id <- Some apiCallId
                    orm.Id <- Some apiCallId
                    assert(guidDicDebug[rt.Guid] = orm)

                | :? RtFlow as rt ->
                    let orm = rt.ToORM<ORMFlow>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let flowId = conn.Insert($"""INSERT INTO {Tn.Flow}
                                            (guid, parameter, name, systemId)
                                     VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId);""", orm, tr)

                    rt.Id <- Some flowId
                    orm.Id <- Some flowId
                    assert (guidDicDebug[rt.Guid] = orm)

                    rt.Buttons    |> iter _.InsertToDB(dbApi)
                    rt.Lamps      |> iter _.InsertToDB(dbApi)
                    rt.Conditions |> iter _.InsertToDB(dbApi)
                    rt.Actions    |> iter _.InsertToDB(dbApi)

                | :? RtFlowEntity as fe ->
                    let flowId = fe.Flow |-> _.Id |??  (fun () -> failwith "ERROR: RtFlowEntity must have a FlowId set before inserting to DB.")
                    let rtX = x :?> RtFlowEntity

                    let insertFlowElement (tableName:string) (rtX:#RtFlowEntity, ormX:#ORMFlowEntity) =
                            let guidDicDebug = dbApi.DDic.Get<Guid2UniqDic>()
                            ormX.FlowId <- flowId
                            dbApi.With(fun (conn, tr) ->
                                let xId =
                                    conn.Insert($"""INSERT INTO {tableName}
                                                    (guid, parameter, name, flowId)
                                                VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @FlowId);""", ormX, tr)
                                rtX.Id <- Some xId
                                ormX.Id <- Some xId
                                assert (guidDicDebug[rtX.Guid] = ormX)
                            )


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

                    match box x with
                    | :? RtButton    as rt -> let orm = rt.ToORM<ORMButton>    dbApi in insertFlowElement Tn.Button    (rtX, orm)
                    | :? RtLamp      as rt -> let orm = rt.ToORM<ORMLamp>      dbApi in insertFlowElement Tn.Lamp      (rtX, orm)
                    | :? RtCondition as rt -> let orm = rt.ToORM<ORMCondition> dbApi in insertFlowElement Tn.Condition (rtX, orm)
                    | :? RtAction    as rt -> let orm = rt.ToORM<ORMAction>    dbApi in insertFlowElement Tn.Action    (rtX, orm)
                    | _ -> failwith "ERROR"


                | :? RtWork as rt ->
                    let orm = rt.ToORM<ORMWork>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id

                    let workId = conn.Insert($"""INSERT INTO {Tn.Work}
                                        (guid, parameter,                      name,  systemId,  flowId,  status4Id,  motion,  script,  isFinished,  numRepeat,  period,  delay)
                                 VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @SystemId, @FlowId, @Status4Id, @Motion, @Script, @IsFinished, @NumRepeat, @Period, @Delay);""", orm, tr)

                    rt.Id <- Some workId
                    orm.Id <- Some workId
                    assert(guidDicDebug[rt.Guid] = orm)

                    rt.Calls |> iter _.InsertToDB(dbApi)

                    // work 의 arrows 를 삽입 (calls 간 연결)
                    rt.Arrows |> iter _.InsertToDB(dbApi)


                | :? RtCall as rt ->
                    let orm = rt.ToORM<ORMCall>(dbApi)
                    orm.WorkId <- rt.RawParent >>= _.Id

                    let callId =
                        conn.Insert($"""INSERT INTO {Tn.Call}
                                    (guid,  parameter,                     name, workId,   status4Id,  callTypeId,  autoConditions, commonConditions,   isDisabled, timeout)
                             VALUES (@Guid, @Parameter{dbApi.DapperJsonB}, @Name, @WorkId, @Status4Id, @CallTypeId, @AutoConditions, @CommonConditions, @IsDisabled, @Timeout);""", orm, tr)

                    rt.Id <- Some callId
                    orm.Id <- Some callId
                    assert(guidDicDebug[rt.Guid] = orm)

                    // call - apiCall 에 대한 mapping 정보 삽입
                    for apiCall in rt.ApiCalls do
                        let apiCallId = apiCall.ORMObject >>= tryCast<ORMUnique> >>= _.Id |?? (fun () -> failwith "ERROR")

                        let m = conn.TryQuerySingle<ORMMapCall2ApiCall>(
                                    $"""SELECT * FROM {Tn.MapCall2ApiCall}
                                        WHERE callId = {rt.Id.Value} AND apiCallId = {apiCallId}""", transaction=tr)
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
                                    {| CallId = rt.Id.Value; ApiCallId = apiCallId ; Guid=guid |}, tr)

                            noop()
                        ()


                | :? RtArrowBetweenCalls as rt ->
                    let ormArrow = rt.ToORM<ORMArrowCall>(dbApi)
                    ormArrow.WorkId <- rt.RawParent >>= _.Id |?? (fun () -> failwith "ERROR: RtArrowBetweenCalls must have a WorkId set before inserting to DB.")

                    let arrowCallId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ArrowCall}
                                       ( source, target,   typeId, workId,   guid, parameter,                     name)
                                VALUES (@Source, @Target, @TypeId, @WorkId, @Guid, @Parameter{dbApi.DapperJsonB}, @Name);"""
                            , ormArrow, tr)

                    rt.Id <- Some arrowCallId
                    ormArrow.Id <- Some arrowCallId
                    assert(guidDicDebug[rt.Guid] = ormArrow)

                | :? RtArrowBetweenWorks as rt ->
                    let orm = rt.ToORM<ORMArrowWork>(dbApi)
                    orm.SystemId <- rt.System >>= _.Id |?? (fun () -> failwith "ERROR: RtArrowBetweenWorks must have a SystemId set before inserting to DB.")

                    let arrowWorkId =
                        conn.Insert(
                            $"""INSERT INTO {Tn.ArrowWork}
                                       (source,   target,  typeId,  systemId,  guid,  parameter,                    name)
                                VALUES (@Source, @Target, @TypeId, @SystemId, @Guid, @Parameter{dbApi.DapperJsonB}, @Name);"""
                            , orm, tr)

                    rt.Id <- Some arrowWorkId
                    orm.Id <- Some arrowWorkId
                    assert(guidDicDebug[rt.Guid] = orm)
                    ()


                | _ -> failwith "ERROR"



            )
