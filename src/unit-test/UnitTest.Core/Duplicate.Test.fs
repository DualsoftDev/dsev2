namespace T


open NUnit.Framework

open Ev2.Core.FS


[<AutoOpen>]
module DuplicateTestModule =
    [<Test>]
    let ``duplicate test`` () =
        createEditableProject()
        let sys0 = edProject.Systems[0].Duplicate() |> validateRuntime
        ()

