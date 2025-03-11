namespace T


open System.IO
open System
open System.Text.RegularExpressions

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open Dual.Plc2DS

module BruteForceMatch =
    type B() =
        [<Test>]
        member _.``Minimal`` () =
            //let sm = Semantic()
            //sm.Flows   <- WordSet(["STN01"], ic)
            //sm.Devices <- WordSet(["LAMP"; "CYL"], ic)
            //sm.Actions <- WordSet(["CLAMP"], ic)

            let rs = StringSearch.FindFDA("STN01_B_CYL_CLAMP1", [|"STN01"|], [|"LAMP"; "CYL"|], [|"CLAMP"|])
            noop()
            let xxx = rs
            noop()



