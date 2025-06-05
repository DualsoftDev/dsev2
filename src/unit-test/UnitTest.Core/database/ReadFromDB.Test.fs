namespace T

open System.IO
open NUnit.Framework

open Dual.Common.Base
open Ev2.Core.FS


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



        let dbApi = Path.Combine(testDataDir(), "test_dssystem.sqlite3") |> createSqliteDbApi
        let dsProject = RtProject.CheckoutFromDB(ByName "MainProject", dbApi)
        let xxx = dsProject
        use conn = dbApi.CreateConnection()
        ()


