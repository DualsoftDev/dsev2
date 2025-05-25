namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base

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
        member x.AddActiveSystem (sys:EdSystem) =
            x.UpdateDateTimeUpward()
            sys.RawParent <- Some x
            activeSystems.Add(sys)
        member x.AddPassiveSystem(sys:EdSystem) =
            x.UpdateDateTimeUpward()
            sys.RawParent <- Some x
            passiveSystems.Add(sys)

        member x.RemoveActiveSystem (sys:EdSystem) =
            x.UpdateDateTimeUpward()
            sys.RawParent <- None
            activeSystems.Remove(sys)
        member x.RemovePassiveSystem(sys:EdSystem) =
            x.UpdateDateTimeUpward()
            sys.RawParent <- None
            passiveSystems.Remove(sys)


        static member Create(name:string, ?activeSystems:EdSystem seq, ?passiveSystems:EdSystem seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let activeSystems = activeSystems |? Seq.empty |> ResizeArray
            let passiveSystems = passiveSystems |? Seq.empty |> ResizeArray
            EdProject(name, activeSystems, passiveSystems, guid, dateTime)



    type EdSystem private (name:string, project:EdProject option, flows:ResizeArray<EdFlow>, works:ResizeArray<EdWork>, arrows:ResizeArray<EdArrowBetweenWorks>, guid:Guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id, ?parent=(project >>= tryCast<Unique>))
        interface IEdSystem


        member x.Flows = flows |> toArray
        member x.Works = works |> toArray
        member x.Arrows = arrows

        member x.AddFlows(fs:EdFlow seq) =
            x.UpdateDateTimeUpward()
            flows.AddRange(fs)
            fs |> iter (fun f -> f.RawParent <- Some x)
        member x.AddWorks(ws:EdWork seq) =
            x.UpdateDateTimeUpward()
            works.AddRange(ws)
            ws |> iter (fun w -> w.RawParent <- Some x)
        member x.AddArrows(arrs:EdArrowBetweenWorks seq) =
            x.UpdateDateTimeUpward()
            arrows.AddRange(arrs)
            arrs |> iter (fun c -> c.RawParent <- Some x)


        member x.RemoveFlows(fs:EdFlow seq) =
            x.UpdateDateTimeUpward()
            for f in fs do
                flows.Remove f |> ignore
                f.RawParent <- None
        member x.RemoveWorks(ws:EdWork seq) =
            x.UpdateDateTimeUpward()
            for w in works do
                works.Remove w |> ignore
                w.RawParent <- None
        member x.RemoveArrows(arrs:EdArrowBetweenWorks seq) =
            x.UpdateDateTimeUpward()
            for a in arrows do
                arrows.Remove a |> ignore
                a.RawParent <- None


        static member Create(name:string, ?project:EdProject, ?flows:EdFlow seq, ?works:EdWork seq, ?arrows:EdArrowBetweenWorks seq, ?id:Id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let flows = flows |? Seq.empty |> ResizeArray
            let works = works |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdSystem(name, project, flows, works, arrows, guid, dateTime, ?id=id)

    type EdFlow private (name:string, guid:Guid, dateTime:DateTime, ?system:EdSystem, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?parent=(system >>= tryCast<Unique>), ?id=id)
        interface IEdFlow
        static member Create(name, ?arrows:EdArrowBetweenWorks seq, ?id, ?guid, ?dateTime, ?system:EdSystem) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdFlow(name, guid, dateTime, ?system=system, ?id=id)
            |> tee(fun f -> system |> Option.iter (fun sys -> sys.AddFlows [f]))

        member x.Works = //x.OptParent |> map _.Works //|> choose id
            match x.RawParent with
            | Some (:? EdSystem as p) -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
            | _ -> failwith "Parent is not set. Cannot get works from flow."

        member x.AddWorks(ws:EdWork seq) =
            x.UpdateDateTimeUpward()
            ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
        member x.RemoveWorks(ws:EdWork seq) =
            x.UpdateDateTimeUpward()
            ws |> iter (fun w -> w.OptOwnerFlow <- None)


    type EdWork private(name:string, guid:Guid, dateTime:DateTime, calls:ResizeArray<EdCall>, arrows:ResizeArray<EdArrowBetweenCalls>, ?parent:EdSystem, ?ownerFlow:EdFlow, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
        interface IEdWork
        member val OptOwnerFlow = ownerFlow with get, set
        member x.Calls = calls |> toArray
        member x.Arrows = arrows |> toArray

        member x.AddCalls(cs:EdCall seq) =
            x.UpdateDateTimeUpward()
            calls.AddRange(cs)
            cs |> iter (fun c -> c.RawParent <- Some x)

        member x.AddArrows(arrs:EdArrowBetweenCalls seq) =
            x.UpdateDateTimeUpward()
            arrows.AddRange(arrs)
            arrs |> iter (fun c -> c.RawParent <- Some x)

        static member Create(name:string, system:EdSystem, ?calls:EdCall seq, ?ownerFlow:EdFlow, ?arrows:EdArrowBetweenCalls seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
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


    type EdArrowBetweenCalls(source:EdCall, target:EdCall, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit Arrow<EdCall>(source, target, dateTime, guid, ?id=id)

    type EdArrowBetweenWorks(source:EdWork, target:EdWork, dateTime:DateTime, guid:Guid, ?id:Id) =
        inherit Arrow<EdWork>(source, target, dateTime, guid, ?id=id)

