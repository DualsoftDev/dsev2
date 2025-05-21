namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.Serialization

/// 편집 가능한 버젼
[<AutoOpen>]
module rec EditableDsObjects =

    type IEdObject  = interface end
    type IEdProject = inherit IEdObject inherit IDsProject
    type IEdSystem  = inherit IEdObject inherit IDsSystem
    type IEdFlow    = inherit IEdObject inherit IDsFlow
    type IEdWork    = inherit IEdObject inherit IDsWork
    type IEdCall    = inherit IEdObject inherit IDsCall

    type EdProject private (name:string, activeSystems:ResizeArray<EdSystem>, passiveSystems:ResizeArray<EdSystem>, guid:Guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
        interface IEdProject
        member x.ActiveSystems = activeSystems |> toArray
        member x.PassiveSystems = passiveSystems |> toArray
        member x.AddActiveSystem(sys:EdSystem) =
            activeSystems.Add(sys)
            sys.RawParent <- Some x
        member x.AddPassiveSystem(sys:EdSystem) =
            passiveSystems.Add(sys)
            sys.RawParent <- Some x

        static member Create(name:string, ?activeSystems:EdSystem seq, ?passiveSystems:EdSystem seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let activeSystems = activeSystems |? Seq.empty |> ResizeArray
            let passiveSystems = passiveSystems |? Seq.empty |> ResizeArray
            EdProject(name, activeSystems, passiveSystems, guid, dateTime)



    type EdSystem private (name:string, project:EdProject, flows:ResizeArray<EdFlow>, works:ResizeArray<EdWork>, arrows:Arrow<EdWork> seq,  guid:Guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id, parent=project)
        interface IEdSystem
        member x.Flows = flows |> toArray
        member x.Works = works |> toArray
        member x.Arrows = arrows
        member x.AddFlows(fs:EdFlow seq) =
            flows.AddRange(fs)
            fs |> iter (fun f -> f.RawParent <- Some x)
        member x.AddWorks(ws:EdWork seq) =
            works.AddRange(ws)
            ws |> iter (fun w -> w.RawParent <- Some x)

        static member Create(name:string, project:EdProject, asActive:bool, ?flows:EdFlow seq, ?works:EdWork seq, ?arrows:Arrow<EdWork> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let flows = flows |? Seq.empty |> ResizeArray
            let works = works |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdSystem(name, project, flows, works, arrows, guid, dateTime)
            |> tee(fun s -> if asActive then project.AddActiveSystem s else project.AddPassiveSystem s )

    type EdFlow private (name:string, guid:Guid, dateTime:DateTime, system:EdSystem, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, parent=system, ?id=id)
        interface IEdFlow
        static member Create(name, system, ?id, ?guid, ?dateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            EdFlow(name, guid, dateTime, system, ?id=id)
            |> tee(fun f -> system.AddFlows [f])

        member x.AddWorks(ws:EdWork seq) =
            ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
        member x.Works = //x.OptParent |> map _.Works //|> choose id
            match x.RawParent with
            | Some (:? EdSystem as p) -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
            | _ -> failwith "Parent is not set. Cannot get works from flow."


    type EdWork private(name:string, guid:Guid, dateTime:DateTime, calls:ResizeArray<EdCall>, arrows:ResizeArray<Arrow<EdCall>>, ?parent:EdSystem, ?ownerFlow:EdFlow, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
        interface IEdWork
        member val OptOwnerFlow = ownerFlow with get, set
        member x.Calls = calls |> toArray
        member x.Arrows = arrows

        member x.AddCalls(cs:EdCall seq) =
            calls.AddRange(cs)
            cs |> iter (fun c -> c.RawParent <- Some x)

        static member Create(name:string, system:EdSystem, ?calls:EdCall seq, ?ownerFlow:EdFlow, ?arrows:Arrow<EdCall> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let calls = calls |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdWork(name, guid, dateTime, calls, arrows, parent=system, ?ownerFlow=ownerFlow, ?id=id)
            |> tee(fun w ->
                system.AddWorks [w]
                ownerFlow |> Option.iter(fun f -> f.AddWorks [w]))


    type EdCall private(name:string, guid:Guid, dateTime:DateTime, work:EdWork, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime, parent=work)
        interface IEdCall

        static member Create(name:string, work:EdWork, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            EdCall(name, guid, dateTime, work, ?id=id)
            |> tee(fun c -> work.AddCalls [c] )



