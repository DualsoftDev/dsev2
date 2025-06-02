namespace T

open System
open System.IO
open System.Data.SQLite

open NUnit.Framework
open Dapper

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open System.Collections
open FsUnitTyped


[<AutoOpen>]
module ReadFromDBTestModule =

    // DB 가 생성되어 있고, "MainProject" 가 저장되어 있어야 함.   다른 test 수행 이후에 실행되면 OK
    [<Test>]
    let dbReadTest() =
        Ev2.Core.FS.ModuleInitializer.Initialize(null)
        DcLogger.EnableTrace <- true
        //DapperTypeHandler.AddHandlers()
        //checkHandlers()
        AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)
        Directory.CreateDirectory(testDataDir()) |> ignore



        let connStr =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> path2ConnectionString

        let dsProject = RtProject.CheckoutFromSqlite3(ByName "MainProject", connStr)
        let xxx = dsProject
        use conn = dbApi.CreateConnection()
        ()


