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

                | _ -> failwith "ERROR"



            )
