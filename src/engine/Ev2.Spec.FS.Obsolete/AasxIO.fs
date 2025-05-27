namespace Dual.EV2.AasxIO

open System.IO
open Dual.EV2.Core

module AASX =

    let private ensureDir (path:string) =
        let dir = Path.GetDirectoryName(path)
        if not (Directory.Exists(dir)) then Directory.CreateDirectory(dir) |> ignore

    let exportSystemAASX (sys: System) (path: string) =
        ensureDir path
        let content = $"{{\"idShort\": \"{sys.Name}\", \"id\": \"{sys.Guid}\"}}"
        File.WriteAllText(path, content)

    let exportAllAASX (project: Project) (dir: string) =
        for sys in project.Systems do
            let path = Path.Combine(dir, $"{sys.Name}.aasx")
            exportSystemAASX sys path
