namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module rec Ed2DsModule =

    type EdFlow with
        member x.ToDsFlow() =
            let works = x.Works |-> _.ToDsWork() |> toArray
            DsFlow(x.Name, x.Guid, x.OptParent.Value.Guid, works, ?id=x.Id, ?dateTime=x.DateTime)

    type EdWork with
        member x.ToDsWork() =
            let callDic = x.Calls.ToDictionary(id, fun c -> c.ToDsCall())
            let arrows = x.Arrows |-> (fun a -> Arrow<DsCall>(callDic[a.Source], callDic[a.Target])) |> Seq.toArray
            let optOwnerFlowGuid = x.OptOwnerFlow |-> _.Guid
            let calls = callDic.Values |> toArray
            DsWork(x.Name, x.Guid, x.OptParent.Value.Guid, calls, arrows, ?flowGuid=optOwnerFlowGuid, ?id=x.Id, ?dateTime=x.DateTime)


    type EdCall with
        member x.ToDsCall() =
            let xxx = x
            DsCall(x.Name, x.Guid, x.OptParent.Value.Guid, ?id=x.Id, ?dateTime=x.DateTime)

    type EdSystem with
        member x.ToDsSystem() =
            let flows = x.Flows |> Seq.map (fun f -> f.ToDsFlow()) |> Seq.toArray
            let workDic = x.Works.ToDictionary(id, fun w -> w.ToDsWork())
            let arrows = x.Arrows |-> (fun w -> Arrow<DsWork>(workDic[w.Source], workDic[w.Target])) |> toArray
            DsSystem(x.Name, x.Guid, flows, workDic.Values.ToArray(), arrows, ?id=x.Id, ?dateTime=x.DateTime)
