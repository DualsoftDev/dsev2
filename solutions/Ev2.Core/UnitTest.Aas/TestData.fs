module DsJson

open Ev2.Core.FS

let createProject () =
    MiniSample.create()

let projectJson () =
    createProject().ToJson()

let systemJson () =
    let project = createProject()
    match project.ActiveSystems |> Seq.tryHead with
    | Some system -> system.ExportToJson()
    | None -> failwith "MiniSample.create()로 생성한 프로젝트에 활성 시스템이 없습니다."

let dsProject () = projectJson()

let dsSystem () = systemJson()
