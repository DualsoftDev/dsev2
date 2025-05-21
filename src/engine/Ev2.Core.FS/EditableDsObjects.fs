namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.Serialization

/// 편집 가능한 버젼
[<AutoOpen>]
module rec EditableDsObjects =

    type IEdObject = interface end
    type IEdSystem = inherit IEdObject inherit IDsSystem
    type IEdFlow   = inherit IEdObject inherit IDsFlow
    type IEdWork   = inherit IEdObject inherit IDsWork
    type IEdCall   = inherit IEdObject inherit IDsCall

    type EdSystem private (name:string, flows:ResizeArray<EdFlow>, works:ResizeArray<EdWork>, arrows:Arrow<EdWork> seq,  guid:Guid, dateTime:DateTime, ?id) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
        interface IEdSystem
        member x.Flows = flows |> toArray
        member x.Works = works |> toArray
        member x.Arrows = arrows
        member x.AddFlows(fs:EdFlow seq) =
            flows.AddRange(fs)
            fs |> iter (fun f -> f.OptParent <- Some x)
        member x.AddWorks(ws:EdWork seq) =
            works.AddRange(ws)
            ws |> iter (fun w -> w.OptParent <- Some x)

        static member Create(name:string, ?flows:EdFlow seq, ?works:EdWork seq, ?arrows:Arrow<EdWork> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let flows = flows |? Seq.empty |> ResizeArray
            let works = works |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdSystem(name, flows, works, arrows, guid, dateTime)

    type EdFlow private (name:string, guid:Guid, dateTime:DateTime, ?id, ?parent:EdSystem) =
        inherit Unique(name, guid=guid, dateTime=dateTime, ?id=id)
        interface IEdFlow
        member val OptParent = parent with get, set
        static member Create(name, ?id, ?guid, ?dateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            EdFlow(name, guid, dateTime, ?id=id)

        member x.AddWorks(ws:EdWork seq) =
            ws |> iter (fun w -> w.OptOwnerFlow <- Some x)
        member x.Works = //x.OptParent |> map _.Works //|> choose id
            match x.OptParent with
            | Some p -> p.Works |> filter (fun w -> w.OptOwnerFlow = Some x) |> toArray
            | None -> failwith "Parent is not set. Cannot get works from flow."


    type EdWork private(name:string, guid:Guid, dateTime:DateTime, calls:ResizeArray<EdCall>, arrows:ResizeArray<Arrow<EdCall>>, ?parent:EdSystem, ?ownerFlow:EdFlow, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
        interface IEdWork
        member val OptOwnerFlow = ownerFlow with get, set
        member x.Calls = calls |> toArray
        member x.Arrows = arrows
        member val OptParent = parent with get, set

        member x.AddCalls(cs:EdCall seq) =
            calls.AddRange(cs)
            cs |> iter (fun c -> c.OptParent <- Some x)

        static member Create(name:string, ?parent:EdSystem, ?calls:EdCall seq, ?ownerFlow:EdFlow, ?arrows:Arrow<EdCall> seq, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            let calls = calls |? Seq.empty |> ResizeArray
            let arrows = arrows |? Seq.empty |> ResizeArray
            EdWork(name, guid, dateTime, calls, arrows, ?parent=parent, ?ownerFlow=ownerFlow, ?id=id)


    type EdCall private(name:string, guid:Guid, dateTime:DateTime, ?parent:EdWork, ?id) =
        inherit Unique(name, ?id=id, guid=guid, dateTime=dateTime)
        interface IEdCall
        member val OptParent = parent with get, set

        static member Create(name:string, ?parent:EdWork, ?id, ?guid:Guid, ?dateTime:DateTime) =
            let guid = guid |? Guid.NewGuid()
            let dateTime = dateTime |?? now
            EdCall(name, guid, dateTime, ?parent=parent, ?id=id)



