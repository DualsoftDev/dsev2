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

        member x.ClearAllCaches() =
            x.WorkCache.Reset() |> ignore
            x.CallCache.Reset() |> ignore

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
