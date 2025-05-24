namespace Ev2.Core.FS

open System
open System.Collections.Generic
open Dual.Common.Core.FS
open Dual.Common.Base

module internal rec DsObjectCopyImpl =
    type CopyBag() =
        /// OldGuid -> Old object
        member val Oldies = Dictionary<Guid, Unique>()
        /// NewGuid -> New object
        member val Newbies = Dictionary<Guid, Unique>()
        /// OldGuid -> NewGuid map
        member val Old2NewMap = Dictionary<Guid, Guid>()
    with
        member x.Add(old:Unique) =
            let newGuid = newGuid()
            x.Oldies.Add(old.Guid, old)
            //x.Newbies.Add(newGuid, old)
            x.Old2NewMap.Add(old.Guid, newGuid)
            newGuid
        member x.NewbieWithOldGuid(oldGuid:Guid) = x.Newbies[x.Old2NewMap[oldGuid]]

    let private nn (oldName:string) =
#if DEBUG
        $"Copy of {oldName}"
#else
        oldName
#endif

    type DsProject with
        member x.copy(bag:CopyBag, additionalActiveSystems:DsSystem[], additionalPassiveSystems:DsSystem[]) =
            let guid = bag.Add(x)
            let activeSystems  = x.ActiveSystems  |-> _.copy(bag)
            let passiveSystems = x.PassiveSystems |-> _.copy(bag)
            let actives  = activeSystems  @ additionalActiveSystems  |> toArray
            let passives = passiveSystems @ additionalPassiveSystems |> toArray
            DsProject(nn x.Name, guid, actives, passives, now())
            |> tee(fun p ->
                actives  |> iter (fun z -> z.RawParent <- Some p)
                passives |> iter (fun z -> z.RawParent <- Some p) )
            |> tee(fun z -> bag.Newbies[guid] <- z)


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type DsFlow with
        member x.copyShallow(bag:CopyBag) =
            let guid = bag.Add(x)
            DsFlow(nn x.Name, guid, [||], now())
            |> tee(fun z -> bag.Newbies[guid] <- z)

        member x.fillDetails(bag:CopyBag) =
            let oldGuid = bag.Old2NewMap |> find(fun (KeyValue(old, neo)) -> neo = x.Guid) |> _.Key
            let old = bag.Oldies[oldGuid] :?> DsFlow
            old.WorksGuids
            |-> fun z -> bag.Old2NewMap[z]
            |-> fun z -> bag.Newbies[z] :?> DsWork
            |> x.forceSetWorks
            ()

    type DsSystem with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)

            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let flows  = x.Flows  |-> _.copyShallow(bag)  |> toArray
            let works  = x.Works  |-> _.copy(bag)         |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows = x.Arrows |-> _.copy(bag)         |> toArray
            flows |> iter (fun f -> f.fillDetails bag)  // flow 에서 work 참조 가능해짐.

            arrows
            |> iter (fun (a:ArrowBetweenWorks) ->
                works |> contains a.Source |> verify
                works |> contains a.Target |> verify)

            DsSystem(nn x.Name, guid, flows, works, arrows, now())
            |> tee(fun s ->
                flows  |> iter (fun z -> z.RawParent <- Some s)
                works  |> iter (fun z -> z.RawParent <- Some s)
                arrows |> iter (fun z -> z.RawParent <- Some s)
                s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)
            ) |> tee(fun z -> bag.Newbies[guid] <- z)


    type DsWork with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let optFlowGuid = x.OptFlowGuid |-> fun z -> bag.Old2NewMap[z]
            let calls  = x.Calls  |-> _.copy(bag)
            let arrows:ArrowBetweenCalls list = x.Arrows |-> _.copy(bag)

            arrows
            |> iter (fun (a:ArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            DsWork(nn x.Name, guid, calls, arrows, optFlowGuid, now())
            |> tee(fun w ->
                calls  |> iter (fun z -> z.RawParent <- Some w)
                arrows |> iter (fun z -> z.RawParent <- Some w) )
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type DsCall with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            DsCall(nn x.Name, guid, now())
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type ArrowBetweenWorks with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.NewbieWithOldGuid x.Source.Guid :?> DsWork
            let target = bag.NewbieWithOldGuid x.Target.Guid :?> DsWork
            ArrowBetweenWorks(guid, source, target, now(), Name = nn null)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type ArrowBetweenCalls with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.NewbieWithOldGuid x.Source.Guid :?> DsCall
            let target = bag.NewbieWithOldGuid x.Target.Guid :?> DsCall
            ArrowBetweenCalls(guid, source, target, now(), Name= nn null)
            |> tee(fun z -> bag.Newbies[guid] <- z)

[<AutoOpen>]
module DsObjectCopyModule =
    open DsObjectCopyImpl
    type DsProject with
        member x.Copy(?additionalActiveSystems:DsSystem seq, ?additionalPassiveSystems:DsSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toArray
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toArray
            plusActiveSystems @ plusPassiveSystems |> iter (fun s -> s.RawParent <- Some x)
            x.copy(CopyBag(), plusActiveSystems, plusPassiveSystems)
    type DsSystem with member x.Copy() = x.copy(CopyBag())


