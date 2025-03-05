namespace T

open System.IO

[<AutoOpen>]
module TestCommon =
    let getFile(file:string) =
        Path.Combine(__SOURCE_DIRECTORY__, "Samples", file)

