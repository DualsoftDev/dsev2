namespace T


open NUnit.Framework

open Ev2.Core.FS
open System.IO


[<AutoOpen>]
module HelloDSTestModule =
    let testDataDir() = Path.Combine(__SOURCE_DIRECTORY__, @"..\test-data")
    [<Test>]
    let ``create hello ds`` () =
        let prj = createHelloDS()
        let json = prj.ToJson(Path.Combine(testDataDir(), "helloDS.json"))
        ()

