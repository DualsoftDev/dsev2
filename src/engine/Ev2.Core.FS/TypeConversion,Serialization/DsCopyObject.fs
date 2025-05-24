namespace Ev2.Core.FS

open System
open System.Collections.Generic
open Dual.Common.Core.FS

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
            x.Newbies.Add(newGuid, old)
            x.Old2NewMap.Add(old.Guid, newGuid)
            newGuid

    type DsProject with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let activeSystems  = x.ActiveSystems  |-> _.copy(bag) |> toArray
            let passiveSystems = x.PassiveSystems |-> _.copy(bag) |> toArray
            DsProject(x.Name, guid, activeSystems, passiveSystems, now())

    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type DsFlow with
        member x.copyShallow(bag:CopyBag) =
            let guid = bag.Add(x)
            let cc = DsFlow(x.Name, guid, [||], now())
            cc.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)
            cc

        member x.fillDetails(bag:CopyBag) =
            let oldGuid = bag.Old2NewMap |> find(fun (KeyValue(old, neo)) -> neo = x.Guid) |> _.Key //|>   Oldies[x.Guid] :?> DsFlow
            let old = bag.Oldies[oldGuid] :?> DsFlow
            old.WorksGuids
            |-> fun z -> bag.Old2NewMap[z]
            |-> fun z -> bag.Newbies[z] :?> DsWork
            |> x.forceSetWorks
            //cc.WorksGuids <- x.WorksGuids |-> fun z -> bag.Old2NewMap[z]
            ()

    type DsSystem with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let cc = DsSystem(x.Name, guid, [||], [||], [||], now())
            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let flows  = x.Flows  |-> _.copyShallow(bag)
            let works  = x.Works  |-> _.copy(bag)       // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows = x.Arrows |-> _.copy(bag)
            flows |> iter (fun f -> f.fillDetails bag)  // flow 에서 work 참조 가능해짐.
            cc.forceSet(flows, works, arrows)
            cc.RawParent <- Some bag.Oldies[x.PGuid.Value]
            cc.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)
            cc


    type DsWork with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let optFlowGuid = x.OptFlowGuid |-> fun z -> bag.Old2NewMap[z]
            let calls  = x.Calls  |-> _.copy(bag)
            let arrows = x.Arrows |-> _.copy(bag)

            DsWork(x.Name, guid, calls, arrows, optFlowGuid, now())
            |> tee(fun z ->
                z.RawParent <- Some bag.Oldies[x.PGuid.Value]
                z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))

    type DsCall with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            DsCall(x.Name, guid, now())
            |> tee(fun z ->
                z.RawParent <- Some bag.Oldies[x.PGuid.Value]
                z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))


    type ArrowBetweenWorks with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.Oldies[x.Source.Guid] :?> DsWork
            let target = bag.Oldies[x.Target.Guid] :?> DsWork
            ArrowBetweenWorks(guid, source, target, now())
            |> tee(fun z -> z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))


    type ArrowBetweenCalls with
        member x.copy(bag:CopyBag) =
            let guid = bag.Add(x)
            let source = bag.Oldies[x.Source.Guid] :?> DsCall
            let target = bag.Oldies[x.Target.Guid] :?> DsCall
            ArrowBetweenCalls(guid, source, target, now())
            |> tee(fun z -> z.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid))

[<AutoOpen>]
module DsObjectCopyModule =
    open DsObjectCopyImpl
    type DsProject with member x.Copy() = x.copy(CopyBag())
    type DsSystem with member x.Copy() = x.copy(CopyBag())


