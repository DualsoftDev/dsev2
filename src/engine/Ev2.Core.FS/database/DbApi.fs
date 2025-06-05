namespace Ev2.Core.FS

open System
open System.Data
open System.IO
open System.Data.SQLite
open System.Linq
open Dapper

open Dual.Common.Db.FS
open Dual.Common.Core.FS
open Dual.Common.Base
open System.Collections.Generic


[<AutoOpen>]
module DbApiModule =
    type Db2RtBag() =
        member val DbDic = Dictionary<string, ORMUnique>()  // string Guid
        member val RtDic = Dictionary<Guid, RtUnique>()
        member x.Add(u:ORMUnique) = x.DbDic.TryAdd(u.Guid, u) |> ignore
        member x.Add(u:RtUnique)  = x.RtDic.TryAdd(u.Guid, u) |> ignore
        member x.Add2 (db:ORMUnique) (rt:RtUnique) = x.Add db; x.Add rt


    /// 공용 캐시 초기화 함수
    let private createCache<'T> (venderDb:DcDbBase, tableName: string) =
        ResettableLazy<'T[]>(fun () ->
            use conn = venderDb.CreateConnection()
            conn.Query<'T>($"SELECT * FROM {tableName}") |> toArray)


    let specDir = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec")

    let checkedConnections = HashSet<string>()


    /// Database API
    type DbApi(dbProvider:DbProvider) =
        let venderDb:DcDbBase =
            match dbProvider with
            | Sqlite   connStr -> DcSqlite(connStr, enableWAL=true, enableForeignKey=true)
            | Postgres connStr -> DcPgSql(connStr)

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
-- Auto-generated DS schema.  Do *NOT* Edit.
--
"""
                        File.WriteAllText(sqlSpecFile, header + schema)
#endif
                        conn.Execute(schema) |> ignore
                        insertEnumValues<DbStatus4> conn
                        insertEnumValues<DbCallType> conn
                        insertEnumValues<DbArrowType> conn
                        insertEnumValues<DbDataType> conn
                        insertEnumValues<DbRangeType> conn
                    try
                        if not <| conn.IsTableExists(Tn.EOT) then
                            createDb()
                    with exn ->
                        createDb() )
            :?> SQLiteConnection
        do
            // 강제 초기화 실행
            conn() |> dispose

        member val DDic = DynamicDictionary() with get, set

        member val ConnectionString = venderDb.ConnectionString

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

        static member GetDefaultConnectionString(dbName:string, ?busyTimeoutSec) =
            let busyTimeoutSec = busyTimeoutSec |? 20
            let dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{dbName}.sqlite3")
            $"Data Source={dbPath};Version=3;BusyTimeout={busyTimeoutSec}"

        /// DB connection 및 transaction wrapper 생성 및 관리하에서 주어진 action 수행.
        member x.With<'T>(action:IDbConnection * IDbTransaction -> 'T, ?optOnError:Exception->unit) =
            venderDb.With(action, ?optOnError=optOnError)



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
                    conn.Execute($"DELETE FROM {Tn.TableHistory}", tr) |> ignore
                    conn.Execute($"DELETE FROM sqlite_sequence WHERE name = '{Tn.TableHistory}'", tr) |> ignore     // auto increment id 초기화
            , optOnError = fun ex -> logError $"CheckDatabaseChange failed: {ex.Message}")



[<AutoOpen>]
module ORMTypeConversionModule =
    // see insertEnumValues also.  e.g let callTypeId = dbApi.TryFindEnumValueId<DbCallType>(DbCallType.Call)
    type DbApi with
        /// DB 에서 enum value 의 id 를 찾는다.  e.g. DbCallType.Call -> 1
        member dbApi.TryFindEnumValueId<'TEnum when 'TEnum : enum<int>> (enumValue: 'TEnum) : int option =
            let category = typeof<'TEnum>.Name
            let name = enumValue.ToString()
            use conn = dbApi.CreateConnection()
            conn.TryQuerySingle<ORMEnum>(
                $"SELECT * FROM {Tn.Enum} WHERE Category = @Category AND Name = @Name",
                {| Category = category; Name = name |}
            ) >>= (fun z -> n2o z.Id)

        /// DB 의 enum id 에 해당하는 enum value 를 찾는다.  e.g. 1 -> DbCallType.Call
        member dbApi.TryFindEnumValue<'TEnum
                when 'TEnum : struct
                and 'TEnum : enum<int>
                and 'TEnum : (new : unit -> 'TEnum)
                and 'TEnum :> ValueType>
            (enumId: int) : 'TEnum option =

            use conn = dbApi.CreateConnection()
            conn.TryQuerySingle<ORMEnum>($"SELECT * FROM {Tn.Enum} WHERE id = {enumId}")
            >>= (fun z -> Enum.TryParse<'TEnum>(z.Name) |> tryParseToOption)

    type ORMCall with   // Create
        static member Create(dbApi:DbApi, workId:Id, status4:DbStatus4 option, dbCallType:DbCallType,
            autoPre:string, safety:string, isDisabled:bool, timeout:Nullable<int>
        ): ORMUnique =
            let callTypeId = dbApi.TryFindEnumValueId<DbCallType>(dbCallType) |> Option.toNullable
            let status4Id = status4 >>= dbApi.TryFindEnumValueId<DbStatus4> |> Option.toNullable
            ORMCall(workId, status4Id, callTypeId, autoPre, safety, isDisabled, timeout)

    let internal ds2Orm (dbApi:DbApi) (guidDic:Dictionary<Guid, ORMUnique>) (x:IDsObject) =
        let ormUniqINGDP (src:#Unique) (dst:#ORMUnique): ORMUnique = toOrmUniqINGDP src dst :> ORMUnique
        let bag = dbApi.DDic.Get<Db2RtBag>()

        match x |> tryCast<Unique> with
        | Some uniq ->
            let id = uniq.Id |? -1
            let pid = (uniq.RawParent >>= _.Id) |? -1
            let guid = uniq.Guid

            match uniq with
            | :? RtProject as z ->
                ORMProject(z.Author, z.Version, z.Description)
                |> ormUniqINGDP z |> tee (fun y -> bag.Add2 y z)

            | :? RtSystem as z ->
                let originGuid = z.OriginGuid |> Option.toNullable

                // Runtime system 의 prototype system Guid 에 해당하는 DB 의 ORMSystem 의 PK id 를 찾는다.
                let prototypeId:Nullable<Id> =
                    z.PrototypeSystemGuid
                    |-> (fun protoGuid ->
                            bag.RtDic.Values
                                .OfType<RtSystem>()
                                .First(fun s -> s.Guid = protoGuid))        // 현재 RtSystem z 의 Prototype 이 지정되어 있으면, 이미 저장된 RtSystem 들 중에서 해당 prototype 을 갖는 객체를 찾는다.
                    >>= (fun (s:RtSystem) ->                // s : prototype 에 해당하는 RtSystem
                            s.DDic.TryGet("ORMObject")      // 이미 변환된 ORMSystem 객체가 있다면, 해당 객체의 Id 를 구한다.
                            >>= tryCast<ORMSystem>
                            >>= fun s -> n2o s.Id)
                    |> o2n

                ORMSystem(prototypeId, originGuid, z.Author, z.LangVersion, z.EngineVersion, z.Description)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtFlow as z ->
                ORMFlow()
                |> ormUniqINGDP z |> tee (fun y -> bag.Add2 y z)

            | :? RtWork as z ->
                let flowId = (z.Flow >>= _.Id) |> Option.toNullable
                let status4Id = z.Status4 >>= dbApi.TryFindEnumValueId<DbStatus4> |> Option.toNullable
                ORMWork  (pid, status4Id, flowId)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtCall as z ->
                ORMCall.Create(dbApi, pid, z.Status4, z.CallType, z.AutoPre, z.Safety, z.IsDisabled, o2n z.Timeout)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtArrowBetweenWorks as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                let parentId = (z.RawParent >>= _.Id).Value
                let arrowTypeId =
                    dbApi.TryFindEnumValueId<DbArrowType>(z.Type)
                    |? int DbArrowType.None

                ORMArrowWork(src, tgt, parentId, arrowTypeId)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtArrowBetweenCalls as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                let parentId = (z.RawParent >>= _.Id).Value
                let arrowTypeId =
                    dbApi.TryFindEnumValueId<DbArrowType>(z.Type)
                    |? int DbArrowType.None

                ORMArrowCall(src, tgt, parentId, arrowTypeId)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtApiDef as z ->
                ORMApiDef(pid)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | :? RtApiCall as z ->
                let valueTypeId = dbApi.TryFindEnumValueId<DbDataType>(z.ValueType).Value
                let rangeTypeId = dbApi.TryFindEnumValueId<DbRangeType>(z.RangeType).Value
                let apiDefId = guidDic[z.ApiDefGuid].Id.Value
                ORMApiCall (pid, apiDefId, z.InAddress, z.OutAddress, z.InSymbol, z.OutSymbol, valueTypeId, rangeTypeId, z.Value1, z.Value2)
                |> ormUniqINGDP z  |> tee (fun y -> bag.Add2 y z)

            | _ -> failwith $"Not yet for conversion into ORM.{x.GetType()}={x}"

            // 새로 생성된 ORMUnique 객체에 대한 신규 Guid 정보를 dic 에 기록
            |> tee (fun ormUniq -> guidDic[guid] <- ormUniq )

        | _ -> failwithf "Cannot convert to ORM. %A" x



    type IDsObject with // ToORM
        /// Rt object 를 DB 에 기록하기 위한 ORM object 로 변환.  e.g RtProject -> ORMProject
        member x.ToORM<'T when 'T :> ORMUnique>(dbApi:DbApi, guidDic:Dictionary<Guid, ORMUnique>) =
            ds2Orm dbApi guidDic x :?> 'T

    type RtProject with // ToORM
        /// RtProject 를 DB 에 기록하기 위한 ORMProject 로 변환.
        member x.ToORM(dbApi:DbApi): Dictionary<Guid, ORMUnique> * ORMProject =
            let guidDic = Dictionary<Guid, ORMUnique>()
            guidDic, ds2Orm dbApi guidDic x :?> ORMProject

    type RtSystem with // ToORM
        /// RtSystem 를 DB 에 기록하기 위한 ORMSystem 로 변환.
        member x.ToORM(dbApi:DbApi): Dictionary<Guid, ORMUnique> * ORMSystem =
            let guidDic = Dictionary<Guid, ORMUnique>()
            guidDic, ds2Orm dbApi guidDic x :?> ORMSystem

