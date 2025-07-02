namespace T.Core

open Dual.Ev2
open Dual.Ev2.Aas
open Dual.Ev2.Aas.CoreToJsonViaCode
open NUnit.Framework
open Dual.Common.UnitTest.FS
open Dual.Common.Base

open Dual.Common.Base
open Dual.Common.UnitTest.FS
open Dual.Common.Db.FS
open Dual.Common.Core.FS

open Ev2.Core.FS
open Newtonsoft.Json
open System.Reactive.Concurrency
open System.IO
open T

[<AutoOpen>]
module TestSetup =
    [<SetUpFixture>]
    type GlobalTestSetup() =
        [<OneTimeSetUp>]
        member _.GlobalSetup() =
            Ev2.Core.FS.ModuleInitializer.Initialize(null)
            DcLogger.EnableTrace <- true
            AppSettings.TheAppSettings <- AppSettings(UseUtcTime = false)

            Directory.CreateDirectory(testDataDir()) |> ignore
            createEditableProject()


        [<OneTimeTearDown>]
        member _.Cleanup() =
            //File.Delete(dbFilePath)
            ()





