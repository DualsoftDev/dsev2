namespace Ev2.Core.FS

open System
open System.Data
open System.IO
open System.Data.SQLite
open Dapper

open Dual.Common.Db.FS
open Dual.Common.Core.FS
open Dual.Common.Base
open System.Collections.Generic


[<AutoOpen>]
module DbApiModule =
    /// 공용 캐시 초기화 함수
    let private createCache<'T> (connectionString: string, tableName: string) =
        ResettableLazy<'T[]>(fun () ->
            use conn = new SQLiteConnection(connectionString)
            conn.Open()
            conn.Query<'T>($"SELECT * FROM {tableName}") |> toArray)



    let checkedConnections = HashSet<string>()
    /// Database API
    type DbApi(connStr:string) =
        //let mutable initialized = false
        let sqlite = DcSqlite(connStr, enableWAL=true, enableForeignKey=true)
        let conn() =
            sqlite.CreateConnection()
            |> tee (fun conn ->
                noop()
                if not <| checkedConnections.Contains(connStr) then
                    checkedConnections.Add connStr |> ignore
                    //initialized <- true
                    DcLogger.EnableTrace <- true        // TODO: 삭제 필요
                    let createDb() =
                        let schema = getSqlCreateSchema()
                        logInfo $"Creating database schema on {connStr}..."
                        logInfo $"CreateSchema:\r\n{schema}"
#if DEBUG
                        let sqlSpecFile = Path.Combine(__SOURCE_DIRECTORY__, @"..\..\..\..\docs\Spec\sqlite-schema.sql")
                        let header = $"""
--
-- Auto-generated DS schema.  Do *NOT* Edit.
--
"""
                        File.WriteAllText(sqlSpecFile, header + schema)
#endif
                        conn.Execute(schema) |> ignore
                        insertEnumValues<DbCallType> conn
                        insertEnumValues<DbDataType> conn
                        insertEnumValues<DbArrowType> conn
                    try
                        if not <| conn.IsTableExists(Tn.EOT) then
                            createDb()
                    with exn ->
                        createDb() )
        do
            conn() |> dispose

        member val ConnectionString = connStr

        /// DB 의 ORMWork[] 에 대한 cache
        member val WorkCache = createCache<ORMWork>(connStr, Tn.Work)

        /// DB 의 ORMCall[] 에 대한 cache
        member val CallCache = createCache<ORMCall>(connStr, Tn.Call)

        /// DB 의 ORMEnum[] 에 대한 cache
        member val EnumCache = createCache<ORMEnum>(connStr, Tn.Enum)

        member x.ClearAllCaches() =
            x.WorkCache.Reset() |> ignore
            x.CallCache.Reset() |> ignore
            x.EnumCache.Reset() |> ignore

        member x.CreateConnection() = conn()

        member private x.EnumerateRows<'T>(tableName:string, criteriaName:string, criteriaIds:int[]) =
            use conn = x.CreateConnection()
            conn.EnumerateRows<'T>(tableName, criteriaName, criteriaIds, tr=null) |> toArray

        member x.EnumerateWorks       (?systemIds:int[]) = x.EnumerateRows<ORMWork>(Tn.Work, "systemId", systemIds |? [||])
        member x.EnumerateWorksOfFlows(?flowIds:int[])   = x.EnumerateRows<ORMWork>(Tn.Work, "flowId", flowIds|? [||])
        member x.EnumerateCalls       (?workIds:int[])   = x.EnumerateRows<ORMCall>(Tn.Call, "systemId", workIds |? [||])

        static member GetDefaultConnectionString(dbName:string, ?busyTimeoutSec) =
            let busyTimeoutSec = busyTimeoutSec |? 20
            let dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{dbName}.sqlite3")
            $"Data Source={dbPath};Version=3;BusyTimeout={busyTimeoutSec}"

        member x.With<'T>(action:IDbConnection * IDbTransaction -> 'T, ?optOnError:Exception->unit) =
            sqlite.With(action, ?optOnError=optOnError)



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
            dbApi.EnumCache.Value
            |> tryFind(fun e -> e.Category = category && e.Name = name)
            >>= (fun e -> e.Id |> Option.ofNullable)

        /// DB 의 enum id 에 해당하는 enum value 를 찾는다.  e.g. 1 -> DbCallType.Call
        member dbApi.TryFindEnumValue<'TEnum
                when 'TEnum : struct
                and 'TEnum : enum<int>
                and 'TEnum : (new : unit -> 'TEnum)
                and 'TEnum :> ValueType>
            (enumId: int) : 'TEnum option =

            let category = typeof<'TEnum>.Name
            dbApi.EnumCache.Value
            |> tryFind(fun e -> e.Id = Nullable enumId)
            >>= (fun z -> Enum.TryParse<'TEnum>(z.Name) |> tryParseToOption)


    type ORMCall with
        static member Create(dbApi:DbApi, workId:Id, dbCallType:DbCallType): IORMUnique =
            let callTypeId = dbApi.TryFindEnumValueId<DbCallType>(dbCallType) |> Option.toNullable
            ORMCall(workId, callTypeId)


    let o2n = Option.toNullable
    let internal ds2Orm (dbApi:DbApi) (guidDic:Dictionary<Guid, IORMUnique>) (x:IDsObject) =
        let ormUniqINGDP (src:#Unique) (dst:#IORMUnique): IORMUnique = toOrmUniqINGDP src dst :> IORMUnique

        match x |> tryCast<Unique> with
        | Some uniq ->
            let id = uniq.Id |? -1
            let pid = (uniq.RawParent >>= _.Id) |? -1
            let guid, name = uniq.Guid, uniq.Name
            let pGuid, dateTime = uniq.PGuid, uniq.DateTime

            match uniq with
            | :? RtProject as z ->
                ORMProject(z.Author, z.Version, z.Description) |> ormUniqINGDP z
            | :? RtSystem as z ->
                let originGuid = z.OriginGuid |> Option.toNullable
                ORMSystem(originGuid, z.Author, z.LangVersion, z.EngineVersion, z.Description) |> ormUniqINGDP z
            | :? RtFlow   as z -> ORMFlow() |> ormUniqINGDP z
            | :? RtWork   as z ->
                let flowId = (z.OptFlow >>= _.Id) |> Option.toNullable
                ORMWork  (pid, flowId) |> ormUniqINGDP z
            | :? RtCall   as z -> ORMCall.Create (dbApi, pid, z.CallType) |> ormUniqINGDP z

            | :? RtArrowBetweenWorks as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                let parentId = (z.RawParent >>= _.Id).Value
                let arrowTypeId = dbApi.TryFindEnumValueId<DbArrowType>(z.Type) |? int DbArrowType.None

                ORMArrowWork (src, tgt, parentId, arrowTypeId) |> ormUniqINGDP z

            | :? RtArrowBetweenCalls as z ->  // arrow 삽입 전에 parent 및 양 끝점 node(call, work 등) 가 먼저 삽입되어 있어야 한다.
                let id, src, tgt = o2n z.Id, z.Source.Id.Value, z.Target.Id.Value
                let parentId = (z.RawParent >>= _.Id).Value
                let arrowTypeId = dbApi.TryFindEnumValueId<DbArrowType>(z.Type) |? int DbArrowType.None
                ORMArrowCall (src, tgt, parentId, arrowTypeId) |> ormUniqINGDP z

            | :? RtApiDef as z ->
                ORMApiDef (pid) |> ormUniqINGDP z

            | _ -> failwith $"Not yet for conversion into ORM.{x.GetType()}={x}"

            |> tee (fun ormUniq -> guidDic[guid] <- ormUniq )

        | _ -> failwithf "Cannot convert to ORM. %A" x



    type IDsObject with
        /// DS object 를 DB 에 기록하기 위한 ORM object 로 변환.  e.g DsProject -> ORMProject
        member x.ToORM<'T when 'T :> IORMUnique>(dbApi:DbApi, guidDic:Dictionary<Guid, IORMUnique>) = ds2Orm dbApi guidDic x :?> 'T

    type RtProject with
        /// DsProject 를 DB 에 기록하기 위한 ORMProject 로 변환.
        member x.ToORM(dbApi:DbApi): Dictionary<Guid, IORMUnique> * ORMProject =
            let guidDic = Dictionary<Guid, IORMUnique>()
            guidDic, ds2Orm dbApi guidDic x :?> ORMProject

    type RtSystem with
        /// DsSystem 를 DB 에 기록하기 위한 ORMSystem 로 변환.
        member x.ToORM(dbApi:DbApi): Dictionary<Guid, IORMUnique> * ORMSystem =
            let guidDic = Dictionary<Guid, IORMUnique>()
            guidDic, ds2Orm dbApi guidDic x :?> ORMSystem

