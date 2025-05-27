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
        let connStr =
            Path.Combine(testDataDir(), "test_dssystem.sqlite3")
            |> path2ConnectionString

        let dsProject = RtProject.FromSqlite3(ById 1, connStr)
        let xxx = dsProject
        use conn = dbApi.CreateConnection()
        ()


