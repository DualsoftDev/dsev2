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

        let dsProject = RtProject.FromSqlite3(ById 1, connStr)
        let xxx = dsProject
        use conn = dbApi.CreateConnection()
        ()


