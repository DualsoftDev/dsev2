namespace Ev2.Core.FS

open System

open Dual.Common.Core.FS
open Dual.Common.Base
open System.Data.SQLite
open Dapper
open Dual.Common.Db.FS
open System.Data
open System.Reactive.Disposables
open System.IO
open System.Collections.Generic
open Dual.Common.Db.FS


[<AutoOpen>]
module DbApiModule =
    /// 공용 캐시 초기화 함수
    let private createCache<'T> (connectionString: string, tableName: string) =
        ResettableLazy<'T[]>(fun () ->
            use conn = new SQLiteConnection(connectionString)
            conn.Open()
            conn.Query<'T>($"SELECT * FROM {tableName}") |> toArray)



    /// Database API
    type DbApi(connStr:string) =
        let mutable initialized = false
        let sqlite = DcSqlite(connStr, enableWAL=true, enableForeignKey=true)
        let conn() =
            sqlite.CreateConnection()
            |> tee (fun conn ->
                if not initialized then
                    initialized <- true
                    DcLogger.EnableTrace <- true        // TODO: 삭제 필요
                    let createDb() =
                        logInfo $"Creating database schema on {connStr}..."
                        tracefn $"CreateSchema:\r\n{getSqlCreateSchema()}"
                        conn.Execute(getSqlCreateSchema()) |> ignore
                    try
                        if not <| conn.IsTableExists(Tn.EOT) then
                            createDb()
                    with exn ->
                        createDb() )


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
            conn.EnumerateRows<'T>(tableName, criteriaName, criteriaIds) |> toArray

        member x.EnumerateWorks       (?systemIds:int[]) = x.EnumerateRows<ORMWork>(Tn.Work, "systemId", systemIds |? [||])
        member x.EnumerateWorksOfFlows(?flowIds:int[])   = x.EnumerateRows<ORMWork>(Tn.Work, "flowId", flowIds|? [||])
        member x.EnumerateCalls       (?workIds:int[])   = x.EnumerateRows<ORMCall>(Tn.Call, "systemId", workIds |? [||])



        static member GetDefaultConnectionString(dbName:string, ?busyTimeoutSec) =
            let busyTimeoutSec = busyTimeoutSec |? 20
            let dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{dbName}.sqlite3")
            $"Data Source={dbPath};Version=3;BusyTimeout={busyTimeoutSec}"

        /// DB connection 및 transaction wrapper 생성.
        /// - 기존 connection 이나 transaction 이 있으면 그걸 사용하고 dispose 시 아무것도 안함.
        /// - 없으면 새로 생성하고, dispose 시 clear 함.
        ///
        /// wrapping 된 tr.Commint() 이나 tr.Rollback() 을 수행해서는 안됨.
        /// trWrapper.NeedRolback 을 true 로 설정시 rollback 수행되고, 그렇지 않으면 자동으로 commit 되는 모델
        member x.CreateSQLiteWrapper(?conn, ?tr:IDbTransaction) =
            DbWrapper.CreateConnectionAndTransactionWrapper(conn, tr, (fun () -> x.CreateConnection()), (fun conn -> conn.BeginTransaction()))



        // UI 에 의해서 변경되는 DB 항목을 windows service 구동되는 tiaApp 에서 감지하기 위한 용도.
        // UI 내에서는 변경감지를 하지 않고 refresh 를 통해서 DB 를 갱신한다.
        member x.CheckDatabaseChange() =

            use conn = x.CreateConnection()
            // 변경 내역 없는 경우, transaction 없이 return
            if conn.QuerySingle<int>($"SELECT COUNT (*) FROM {Tn.TableHistory}") > 0 then
                use tr = conn.BeginTransaction()

                try
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
                    tr.Commit()
                with ex ->
                    tr.Rollback()
                    logError $"CheckDatabaseChange failed: {ex.Message}"