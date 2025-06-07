namespace T


open NUnit.Framework

open Ev2.Core.FS
open Dual.Common.UnitTest.FS


[<AutoOpen>]
module ValueSpecTestModule =
    [<Test>]
    let ``parse test`` () =
        let p = Multiple [1; 2; 3] :> IValueSpec
        let xxx = p.ToString()
        let yyy = xxx |> IValueSpec.Parse |> _.ToString()

        p.ToString() |> IValueSpec.Parse === p
        p.ToString() |> IValueSpec.Parse |> _.ToString() === p.ToString()
        ()

