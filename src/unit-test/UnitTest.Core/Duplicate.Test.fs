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
open System.Collections.Generic


[<AutoOpen>]
module DuplicateTestModule =
    [<Test>]
    let ``duplicate test`` () =
        let dsProject = edProject.ToRtProject()
        let sys0 = dsProject.Systems[0].Duplicate() |> validateRuntime
        ()

