namespace T

open System.IO
open NUnit.Framework

open Ev2.Core.FS


[<AutoOpen>]
module SystemImportExportModule =
    [<Test>]
    let exportTest() =
        let jsonPath = Path.Combine(testDataDir(), "dssystem-export.system.json")
        edSystem.ExportToJson(jsonPath) |> ignore

        let system = RtSystem.ImportFromJson(File.ReadAllText jsonPath)
        ()
