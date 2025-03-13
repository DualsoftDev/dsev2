namespace T


open System.IO

open NUnit.Framework

open Dual.Common.UnitTest.FS

open Dual.Plc2DS
open Dual.Common.Core.FS
open Dual.Common.Base.FS
open System.Text.RegularExpressions

module GroupingTest =

    type G() =
        [<Test>]
        member _.``Minimal`` () =
            ()