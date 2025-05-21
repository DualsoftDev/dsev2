namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System

[<AutoOpen>]
module rec Ed2DsModule =

    type EdFlow with
        member x.ToDsFlow() =
            let workDic = x.Works.ToDictionary(id, fun z -> z.ToDsWork())
            let works = workDic.Values |> toArray
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenWorks(workDic[a.Source], workDic[a.Target], now())) |> Seq.toArray
            DsFlow(x.Name, x.Guid, x.RawParent.Value.Guid, works, arrows, ?id=x.Id, dateTime=x.DateTime)

    type EdWork with
        member x.ToDsWork() =
            let callDic = x.Calls.ToDictionary(id, fun z -> z.ToDsCall())
            let arrows = x.Arrows |-> (fun a -> ArrowBetweenCalls(callDic[a.Source], callDic[a.Target], now())) |> Seq.toArray
            let optOwnerFlowGuid = x.OptOwnerFlow |-> _.Guid
            let calls = callDic.Values |> toArray
            DsWork(x.Name, x.Guid, x.RawParent.Value.Guid, calls, arrows, optOwnerFlowGuid, ?id=x.Id, dateTime=x.DateTime)


    type EdCall with
        member x.ToDsCall() =
            let xxx = x
            DsCall(x.Name, x.Guid, x.RawParent.Value.Guid, ?id=x.Id, dateTime=x.DateTime)

    type EdSystem with
        member x.ToDsSystem() =
            let flows = x.Flows |> Seq.map (fun f -> f.ToDsFlow()) |> Seq.toArray
            let workDic = x.Works.ToDictionary(id, fun w -> w.ToDsWork())
            let works = workDic.Values |> toArray
            let arrows = x.Arrows |-> (fun w -> ArrowBetweenWorks(workDic[w.Source], workDic[w.Target], now())) |> toArray
            let system = DsSystem(x.Name, x.Guid, flows, workDic.Values.ToArray(), arrows, ?id=x.Id, dateTime=x.DateTime)

            // parent 객체 할당
            flows |> iter (fun z -> z.RawParent <- Some system)
            works |> iter (fun z -> z.RawParent <- Some system)
            for w in works do
                w.Calls |> iter (fun c -> c.RawParent <- Some w)
            system

    type EdProject with
        member x.ToDsProject() =
            let activeSystems  = x.ActiveSystems  |> Seq.map (fun f -> f.ToDsSystem()) |> Seq.toArray
            let passiveSystems = x.PassiveSystems |> Seq.map (fun f -> f.ToDsSystem()) |> Seq.toArray
            (activeSystems @ passiveSystems) |> iter (fun z -> z.RawParent <- Some z)
            let project = DsProject(x.Name, x.Guid, activeSystems, passiveSystems, ?id=x.Id, dateTime=x.DateTime)
            project
