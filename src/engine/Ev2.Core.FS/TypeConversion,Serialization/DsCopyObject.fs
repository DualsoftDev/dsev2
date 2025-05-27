namespace Ev2.Core.FS

open System
open System.Collections.Generic
open Dual.Common.Core.FS
open Dual.Common.Base

/// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
module internal rec DsObjectCopyImpl =
    type ReplicateBag() =
        /// OldGuid -> Old object
        member val Oldies = Dictionary<Guid, Unique>()
        /// NewGuid -> New object
        member val Newbies = Dictionary<Guid, Unique>()
    with
        member x.Add(old:Unique) = old.Guid |> tee (fun guid -> x.Oldies.Add(guid, old))

    let internal nn (oldName:string) =
#if DEBUG
        $"Copy of {oldName}"
#else
        oldName
#endif

    type DsProject with
        member x.replicate(bag:ReplicateBag, additionalActiveSystems:DsSystem[], additionalPassiveSystems:DsSystem[]) =
            let guid = bag.Add(x)
            let activeSystems  = x.ActiveSystems  |-> _.replicate(bag)
            let passiveSystems = x.PassiveSystems |-> _.replicate(bag)
            let actives  = activeSystems  @ additionalActiveSystems  |> toArray
            let passives = passiveSystems @ additionalPassiveSystems |> toArray
            DsProject(nn x.Name, guid, actives, passives, x.DateTime)
            |> tee(fun p ->
                actives  |> iter (fun z -> z.RawParent <- Some p)
                passives |> iter (fun z -> z.RawParent <- Some p) )
            |> tee(fun z -> bag.Newbies[guid] <- z)


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type DsFlow with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            DsFlow(nn x.Name, guid, x.DateTime)
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type DsSystem with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let flows  = x.Flows  |-> _.replicate(bag)  |> toArray
            let works  = x.Works  |-> _.replicate(bag)  |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows = x.Arrows |-> _.replicate(bag)  |> toArray

            arrows
            |> iter (fun (a:ArrowBetweenWorks) ->
                works |> contains a.Source |> verify
                works |> contains a.Target |> verify)

            DsSystem.Create(nn x.Name, guid, flows, works, arrows, x.DateTime)
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid <- Some x.Guid                                       // 최근 원본 지향 버젼
            ) |> tee(fun z -> bag.Newbies[guid] <- z)


    type DsWork with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let calls  = x.Calls  |-> _.replicate(bag)
            let arrows:ArrowBetweenCalls list = x.Arrows |-> _.replicate(bag)

            arrows
            |> iter (fun (a:ArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            let flow = x.OptFlow |-> (fun f -> bag.Newbies[f.Guid] :?> DsFlow)
            DsWork.Create(nn x.Name, guid, calls, arrows, flow, x.DateTime)
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type DsCall with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let apiCalls = x.ApiCalls |-> _.replicate(bag)
            DsCall(nn x.Name, guid, x.CallType, apiCalls, x.DateTime)
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type DsApiCall with
        member x.replicate(bag:ReplicateBag) =
            failwith "ERROR"


    type ArrowBetweenWorks with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> DsWork
            let target = bag.Newbies[x.Target.Guid] :?> DsWork
            ArrowBetweenWorks(guid, source, target, x.DateTime, Name = nn null)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type ArrowBetweenCalls with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> DsCall
            let target = bag.Newbies[x.Target.Guid] :?> DsCall
            ArrowBetweenCalls(guid, source, target, x.DateTime, Name= nn null)
            |> tee(fun z -> bag.Newbies[guid] <- z)

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type DsSystem with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())

        /// Guid 및 DateTime 은 새로이 생성
        member x.Duplicate() =
            let replica = x.Replicate()
            let objs = replica.EnumerateDsObjects()
            let guidDic = objs.ToDictionary( (fun obj -> obj.Guid), (fun _ -> newGuid()))
            let current = now()

            replica.OriginGuid <- Some x.Guid
            objs |> iter (fun obj ->
                obj.Guid <- guidDic[obj.Guid]
                obj.DateTime <- current)

            // 삭제 요망: debug only
            // flow 할당된 works 에 대해서 새로 duplicate 된 flow 를 할당되었나 확인
            replica.Works
            |> filter _.OptFlow.IsSome
            |> iter (fun w -> replica.Flows |> exists (fun f -> f.Guid = w.OptFlow.Value.Guid) |> verify)



            replica


    type DsProject with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate(?additionalActiveSystems:DsSystem seq, ?additionalPassiveSystems:DsSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toArray
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toArray
            plusActiveSystems @ plusPassiveSystems |> iter (fun s -> s.RawParent <- Some x)
            x.replicate(ReplicateBag(), plusActiveSystems, plusPassiveSystems)

        /// Guid 및 DateTime 은 새로이 생성
        member x.Duplicate(?additionalActiveSystems:DsSystem seq, ?additionalPassiveSystems:DsSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toList
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toList
            let actives  = (x.ActiveSystems  @ plusActiveSystems)  |-> _.Duplicate() |> toArray
            let passives = (x.PassiveSystems @ plusPassiveSystems) |-> _.Duplicate() |> toArray
            DsProject(nn x.Name, newGuid(), actives, passives, now())
            |> tee(fun p -> (actives @ passives) |> iter (fun s -> s.RawParent <- Some p))

