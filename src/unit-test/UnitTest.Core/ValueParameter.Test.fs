namespace T


open NUnit.Framework

open Ev2.Core.FS
open Dual.Common.UnitTest.FS


[<AutoOpen>]
module ValueParameterTestModule =
    [<Test>]
    let ``parse test`` () =
        let p = Multiple [1; 2; 3] :> IValueParameter
        let xxx = p.ToString()
        let yyy = xxx |> IValueParameter.Parse |> _.ToString()

        p.ToString() |> IValueParameter.Parse === p
        p.ToString() |> IValueParameter.Parse |> _.ToString() === p.ToString()
        ()

