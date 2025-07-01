namespace Ev2.Core.FS

open System
open System.Linq
open System.Data
open System.IO
open Dapper

open Dual.Common.Db.FS
open Dual.Common.Core.FS
open Dual.Common.Base
open System.Collections.Generic


[<AutoOpen>]
module DbApiModule =
    /// 공용 캐시 초기화 함수
    let private createCache<'T> (venderDb:DcDbBase, tableName: string) =
        ResettableLazy<'T[]>(fun () ->
            use conn = venderDb.CreateConnection()
            conn.Query<'T>($"SELECT * FROM {tableName}") |> toArray)


    let specDir = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec")

    /// schema 생성 확인 완료된 connection strings
    let checkedConnections = HashSet<string>()


    /// Database API
    type AppDbApi(dbProvider:DbProvider) =
        inherit DbApi(dbProvider)

        let venderDb = base.VendorDB
        let conn() =
            venderDb.CreateConnection()
            |> tee (fun conn ->
                if conn.State <> ConnectionState.Open then
                    conn.Open()

                let connStr = conn.ConnectionString
                if not <| checkedConnections.Contains(connStr) then
                    logInfo $"Database Version: {dbProvider.VendorName} {conn.GetVersion()}..."
                    checkedConnections.Add connStr |> ignore
                    //initialized <- true
                    DcLogger.EnableTrace <- true        // TODO: 삭제 필요
                    let createDb() =
                        let withTrigger = false
                        let schema = getSqlCreateSchema dbProvider withTrigger
                        logInfo $"Creating database schema on {connStr}..."
                        logInfo $"CreateSchema:\r\n{schema}"
#if DEBUG
                        let sqlSpecFile = Path.Combine(specDir, "sqlite-schema.sql")
                        let header = $"""
--
-- Auto-generated DS schema for {dbProvider.VendorName}.
-- Date: {DateTime.Now}
-- Do *NOT* Edit.
--
"""
                        File.WriteAllText(sqlSpecFile, header + schema)
#endif
                        use tr = conn.BeginTransaction()
                        try
                            conn.Execute(schema, null, tr) |> ignore
                            insertEnumValues<DbStatus4>   conn tr Tn.Enum
                            insertEnumValues<DbCallType>  conn tr Tn.Enum
                            insertEnumValues<DbArrowType> conn tr Tn.Enum
                            conn.Execute($"INSERT INTO {Tn.Meta} (key, val) VALUES ('database vendor name', '{dbProvider.VendorName}')", null, tr) |> ignore
                            conn.Execute($"INSERT INTO {Tn.Meta} (key, val) VALUES ('database version',     '{conn.GetVersion()}')",     null, tr) |> ignore
                            conn.Execute($"INSERT INTO {Tn.Meta} (key, val) VALUES ('engine version',       '{Version(0, 9, 99)}')",     null, tr) |> ignore
                            tr.Commit()
                        with ex ->
                            logError $"Failed to create database schema: {ex.Message}"
                            tr.Rollback()
                            raise ex
                    try
                        let dic = conn.ParseConnectionString()
                        let schemaName = dic.TryGet("Search Path")// |? "tia"
                        if not <| conn.IsTableExists(Tn.TableDescription, ?schemaName=schemaName) then
                            createDb()
                    with exn ->
                        createDb()
            )
            //:?> SQLiteConnection
        do
            // 강제 초기화 실행
            conn() |> dispose

        /// DB 의 ORMWork[] 에 대한 cache
        member val WorkCache = createCache<ORMWork>(venderDb, Tn.Work)

        /// DB 의 ORMCall[] 에 대한 cache
        member val CallCache = createCache<ORMCall>(venderDb, Tn.Call)

        /// DB 의 ORMEnum[] 에 대한 cache
        member val EnumCache = createCache<ORMEnum>(venderDb, Tn.Enum)


        member x.ClearAllCaches() =
            x.WorkCache.Reset() |> ignore
            x.CallCache.Reset() |> ignore
            x.EnumCache.Reset() |> ignore

        member x.CreateConnection() = conn()

        member private x.EnumerateRows<'T>(tableName:string, criteriaName:string, criteriaIds:int[]) =
            use conn = x.CreateConnection()
            conn.EnumerateRows<'T>(tableName, criteriaName, criteriaIds, tr=null) |> toArray

        member x.EnumerateWorks       (?systemIds:int[]) = x.EnumerateRows<ORMWork>(Tn.Work, "systemId", systemIds |? [||])
        member x.EnumerateWorksOfFlows(?flowIds:int[])   = x.EnumerateRows<ORMWork>(Tn.Work, "flowId",   flowIds   |? [||])
        member x.EnumerateCalls       (?workIds:int[])   = x.EnumerateRows<ORMCall>(Tn.Call, "systemId", workIds   |? [||])

        // UI 에 의해서 변경되는 DB 항목을 windows service 구동되는 tiaApp 에서 감지하기 위한 용도.
        // UI 내에서는 변경감지를 하지 않고 refresh 를 통해서 DB 를 갱신한다.
        member x.CheckDatabaseChange() =
            x.With(fun (conn, tr) ->
                // 변경 내역 없는 경우, transaction 없이 return
                if conn.QuerySingle<int>($"SELECT COUNT (*) FROM {Tn.TableHistory}") > 0 then
                    let sql = $"SELECT * FROM {Tn.TableHistory}"
                    let rows = conn.Query<ORMTableHistory>(sql, tr) |> toArray
                    for kv in rows |> groupByToDictionary (fun row -> row.Name) do
                        let name, rows = kv.Key, kv.Value
                        tracefn $"Updating database change: {name}, numChangedRows={rows.Length}"
                        match name with
                        | Tn.Work -> x.WorkCache.Reset() |> ignore
                        | Tn.Call -> x.CallCache.Reset() |> ignore
                        | _ -> ()
                    conn.Execute($"DELETE FROM {Tn.TableHistory}", null, tr) |> ignore
                    conn.ResetSequence(Tn.TableHistory, tr) |> ignore  // auto increment id 초기화
            , optOnError = fun ex -> logError $"CheckDatabaseChange failed: {ex.Message}")



[<AutoOpen>]
module ORMTypeConversionModule =
    // see insertEnumValues also.  e.g let callTypeId = dbApi.TryFindEnumValueId<DbCallType>(DbCallType.Call)
    type AppDbApi with
        /// DB 에서 enum value 의 id 를 찾는다.  e.g. DbCallType.Call -> 1
        member dbApi.TryFindEnumValueId<'TEnum when 'TEnum : enum<int>> (enumValue: 'TEnum) : Id option =
            let category = typeof<'TEnum>.Name
            let name = enumValue.ToString()
            use conn = dbApi.CreateConnection()
            conn.TryQuerySingle<ORMEnum>(
                $"SELECT * FROM {Tn.Enum} WHERE category = @Category AND name = @Name",
                {| Category = category; Name = name |}
            ) >>= _.Id

        /// DB 의 enum id 에 해당하는 enum value 를 찾는다.  e.g. 1 -> DbCallType.Call
        member dbApi.TryFindEnumValue<'TEnum
                when 'TEnum : struct
                and 'TEnum : enum<int>
                and 'TEnum : (new : unit -> 'TEnum)
                and 'TEnum :> ValueType>
            (enumId: Id) : 'TEnum option =

            use conn = dbApi.CreateConnection()
            conn.TryQuerySingle<ORMEnum>($"SELECT * FROM {Tn.Enum} WHERE id = {enumId}")
            >>= (fun z -> Enum.TryParse<'TEnum>(z.Name) |> tryParseToOption)

    type ORMCall with   // Create
        static member Create(dbApi:AppDbApi, workId:Id, status4:DbStatus4 option, dbCallType:DbCallType,
            autoConditions:string seq, commonConditions:string seq, isDisabled:bool, timeout:int option
        ): ORMUnique =
            let callTypeId = dbApi.TryFindEnumValueId<DbCallType>(dbCallType)
            let status4Id = status4 >>= dbApi.TryFindEnumValueId<DbStatus4>
            ORMCall(workId, status4Id, callTypeId, autoConditions, commonConditions, isDisabled, timeout)

    let internal rt2Orm (dbApi:AppDbApi) (x:IDsObject): ORMUnique =
        /// Unique 객체의 속성정보 (Id, Name, Guid, DateTime)를 ORMUnique 객체에 저장
        let ormReplicateProperties (src:Unique) (dst:ORMUnique): ORMUnique =
            dst
            |> replicateProperties src
            |> tee(fun dst -> dst.ParentId <- src.RawParent >>= _.Id)

        match x |> tryCast<Unique> with
        | Some uniq ->
            let id = uniq.Id |? -1
            let pid = (uniq.RawParent >>= _.Id) |? -1
            let guid = uniq.Guid

            match uniq with
            | :? RtProject as z ->
                ORMProject(z.Author, z.Version, z.Description, z.DateTime)
                |> ormReplicateProperties z

            | :? RtSystem as rt ->
                // Runtime system 의 prototype system Guid 에 해당하는 DB 의 ORMSystem 의 PK id 를 찾는다.
                let prototypeId:Id option = rt.Prototype >>= _.Id

                (* System 소유주 project 지정.  1. system 이 project 에 active system 으로 사용된 경우.  또는 2. project 에 prototype 으로 등록된 경우. *)
                let ownerProjectId =
                    rt.Project
                    >>= (fun p ->
                        match rt.Project with
                        | Some proj ->
                            match rt.Prototype with
                            | _ when rt.IsPrototype || proj.ActiveSystems.Contains(rt) ->
                                proj.Id
                                |> Option.orElseWith(fun () ->
                                    dbApi.With(fun (conn, tr) ->
                                        conn.TryExecuteScalar<Id>($"SELECT id FROM {Tn.Project} where guid = @Guid", {|Guid = proj.Guid|})))
                            | _ -> None)

                if ownerProjectId.IsNone then
                    noop()

                ORMSystem(ownerProjectId, prototypeId, rt.IsPrototype, rt.OriginGuid, rt.IRI, rt.Author, rt.LangVersion, rt.EngineVersion, rt.Description, rt.DateTime)
                |> ormReplicateProperties rt

            | :? RtFlow as rt ->
                ORMFlow()
                |> ormReplicateProperties rt

            | :? RtWork as rt ->
                let flowId = (rt.Flow >>= _.Id)
                let status4Id = rt.Status4 >>= dbApi.TryFindEnumValueId<DbStatus4>
                ORMWork  (pid, status4Id, flowId)
                |> ormReplicateProperties rt

            | :? RtCall as rt ->
                ORMCall.Create(dbApi, pid, rt.Status4, rt.CallType, rt.AutoConditions, rt.CommonConditions, rt.IsDisabled, rt.Timeout)
                |> ormReplicateProperties rt

            | :? RtArrowBetweenWorks as rt ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n rt.Id, rt.Source.Id.Value, rt.Target.Id.Value
                let parentId = (rt.RawParent >>= _.Id).Value
                let arrowTypeId =
                    dbApi.TryFindEnumValueId<DbArrowType>(rt.Type)
                    |? int DbArrowType.None

                ORMArrowWork(src, tgt, parentId, arrowTypeId)
                |> ormReplicateProperties rt

            | :? RtArrowBetweenCalls as rt ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n rt.Id, rt.Source.Id.Value, rt.Target.Id.Value
                let parentId = (rt.RawParent >>= _.Id).Value
                let arrowTypeId =
                    dbApi.TryFindEnumValueId<DbArrowType>(rt.Type)
                    |? int DbArrowType.None

                ORMArrowCall(src, tgt, parentId, arrowTypeId)
                |> ormReplicateProperties rt

            | :? RtApiDef as rt ->
                ORMApiDef(pid)
                |> ormReplicateProperties rt


            | :? RtButton    as rt -> ORMButton(pid)    |> ormReplicateProperties rt
            | :? RtLamp      as rt -> ORMLamp(pid)      |> ormReplicateProperties rt
            | :? RtCondition as rt -> ORMCondition(pid) |> ormReplicateProperties rt
            | :? RtAction    as rt -> ORMAction(pid)    |> ormReplicateProperties rt


            | :? RtApiCall as rt ->
                let apiDefId = rt.ApiDef.ORMObject >>= tryCast<ORMUnique> >>= _.Id |?? (fun () -> failwith "ERROR")
                let valueParam = rt.ValueSpec |-> _.Jsonize() |? null
                ORMApiCall (pid, apiDefId, rt.InAddress, rt.OutAddress, rt.InSymbol, rt.OutSymbol, valueParam)
                |> ormReplicateProperties rt

            | _ -> failwith $"Not yet for conversion into ORM.{x.GetType()}={x}"

            // 새로 생성된 ORMUnique 객체에 대한 신규 Guid 정보를 dic 에 기록
            |> tee (fun ormUniq ->
                let guidDicDebug = dbApi.DDic.Get<Guid2UniqDic>()
                guidDicDebug[guid] <- ormUniq )

        | _ -> failwithf "Cannot convert to ORM. %A" x



    type IDsObject with // ToORM
        /// Rt object 를 DB 에 기록하기 위한 ORM object 로 변환.  e.g RtProject -> ORMProject
        member x.ToORM<'T when 'T :> ORMUnique>(dbApi:AppDbApi) =
            rt2Orm dbApi x :?> 'T

    type RtProject with // ToORM
        /// RtProject 를 DB 에 기록하기 위한 ORMProject 로 변환.
        member x.ToORM(dbApi:AppDbApi): ORMProject =
            rt2Orm dbApi x :?> ORMProject

    type RtSystem with // ToORM
        /// RtSystem 를 DB 에 기록하기 위한 ORMSystem 로 변환.
        member x.ToORM(dbApi:AppDbApi): ORMSystem =
            rt2Orm dbApi x :?> ORMSystem

