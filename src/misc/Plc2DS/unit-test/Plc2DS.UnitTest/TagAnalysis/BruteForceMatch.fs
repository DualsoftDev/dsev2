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

            do
                let rs = StringSearch.MatchRawFDA("STN01_B_CYL_CLAMP1", [|"STN01"|], [|"LAMP"; "CYL"|], [|"CLAMP"|])
                rs[0].Matches === [|
                    { Text = "STN01"; Start =  0; Category = Flow}
                    { Text = "CYL"  ; Start =  8; Category = Device}
                    { Text = "CLAMP"; Start = 12; Category = Action}
                |]

            do
                let rs = StringSearch.MatchRawFDA("CYL_CLAMP1_STN01_B", [|"STN01"|], [|"CYL"; "LAMP"|], [|"CLAMP"|])    // CYL <-> STN, [CYL <-> LAMP] 위치 변경
                rs[0].Matches === [|
                    { Text = "STN01"; Start = 11; Category = Flow}
                    { Text = "CYL"  ; Start =  0; Category = Device}
                    { Text = "CLAMP"; Start =  4; Category = Action}
                |]

            do
                let rs = StringSearch.MatchRawFDA("STN01_CLOCK_LOCK", [|"STN01"|], [|"LOCK"; "CLOCK"|], [|"LOCK"|])
                rs[0].Matches === [|
                    { Text = "STN01"; Start =  0; Category = Flow}
                    { Text = "CLOCK"; Start =  6; Category = Device}    // CLOCK, LOCK 가능하나, CLOCK 이 더 길어서 선택됨
                    { Text = "LOCK" ; Start = 12; Category = Action}
                |]


            noop()



