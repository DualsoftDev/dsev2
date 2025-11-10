namespace T

open System.IO
open NUnit.Framework

open Ev2.Core.FS


[<AutoOpen>]
module SystemImportExportModule =
    [<Test>]
    let systemExportTest() =
        let jsonPath = Path.Combine(testDataDir(), "dssystem-export.system.json")
        rtSystem.ExportToJson(jsonPath) |> ignore

        let system = DsSystem.ImportFromJson(File.ReadAllText jsonPath)
        ()
