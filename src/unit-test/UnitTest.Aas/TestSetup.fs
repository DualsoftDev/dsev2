namespace T.Core

open NUnit.Framework
open Dual.Common.Base


open Ev2.Core.FS
open System.IO
open T

[<AutoOpen>]
module TestSetup =
    [<SetUpFixture>]
    type GlobalTestSetup() = // Cleanup, GlobalSetup
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





