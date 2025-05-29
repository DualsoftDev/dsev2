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
        match oldName with
        | null | "" -> null
        | _ -> $"Copy of {oldName}"
#else
        oldName
#endif

    type RtProject with
        member x.replicate(bag:ReplicateBag, additionalActiveSystems:RtSystem[], additionalPassiveSystems:RtSystem[]) =
            let guid = bag.Add(x)
            let activeSystems  = x.ActiveSystems  |-> _.replicate(bag)
            let passiveSystems = x.PassiveSystems |-> _.replicate(bag)
            let actives  = activeSystems  @ additionalActiveSystems  |> toArray
            let passives = passiveSystems @ additionalPassiveSystems |> toArray
            RtProject(actives, passives) |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type RtFlow with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtFlow() |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtSystem with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let apiDefs  = x.ApiDefs  |-> _.replicate(bag)  |> toArray
            let apiCalls = x.ApiCalls |-> _.replicate(bag)  |> toArray
            let flows    = x.Flows    |-> _.replicate(bag)  |> toArray
            let works    = x.Works    |-> _.replicate(bag)  |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows   = x.Arrows   |-> _.replicate(bag)  |> toArray

            arrows
            |> iter (fun (a:RtArrowBetweenWorks) ->
                works |> contains a.Source |> verify
                works |> contains a.Target |> verify)

            RtSystem.Create(x.IsPrototype, flows, works, arrows, apiDefs, apiCalls)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid <- Some x.Guid                                       // 최근 원본 지향 버젼
            ) |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtWork with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let calls  = x.Calls  |-> _.replicate(bag)
            let arrows:RtArrowBetweenCalls list = x.Arrows |-> _.replicate(bag)

            arrows
            |> iter (fun (a:RtArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            let flow = x.OptFlow |-> (fun f -> bag.Newbies[f.Guid] :?> RtFlow)
            RtWork.Create(calls, arrows, flow) |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtCall with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtCall(x.CallType, x.ApiCalls, x.AutoPre, x.Safety, x.Timeout) |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtApiCall with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtApiCall(x.ApiDef, x.InAddress, x.OutAddress, x.InSymbol, x.OutSymbol, x.ValueType, x.Value) |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtApiDef with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtApiDef(x.IsPush) |> uniqINGD_fromObj x |> uniqGuid guid
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenWorks with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtWork
            let target = bag.Newbies[x.Target.Guid] :?> RtWork
            RtArrowBetweenWorks(source, target, x.Type) |> uniqGD guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenCalls with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtCall
            let target = bag.Newbies[x.Target.Guid] :?> RtCall
            RtArrowBetweenCalls(source, target, x.Type) |> uniqGD guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type RtSystem with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())


        // TODO:::
        // 모든 replicate 를 Ed object 기반으로 변경 필요... replicate 된 객체의 Guid 새로 mapping 해서 assign 할 필요 있음.

        /// Id, Guid 및 DateTime 은 새로이 생성
        member x.Duplicate() =
            let replica = x.Replicate()
            let objs = replica.EnumerateDsObjects()
            let guidDic = objs.ToDictionary( (fun obj -> obj.Guid), (fun _ -> newGuid()))
            let current = now()

            replica.OriginGuid <- Some x.Guid
            objs |> iter (fun obj ->
                obj.Id <- None
                obj.Guid <- guidDic[obj.Guid]
                obj.DateTime <- current)

            //for c in replica.Works >>= _.Calls do
            //    let newGuids = c.ApiCalls |-> _.Guid |-> (fun g -> guidDic[g])
            //    let newApiCalls = newGuids |-> (fun g -> objs[g] :?> RtApiCall)
            //    c.ApiCalls <- newApiCalls |> toList
            //    ()

            // 삭제 요망: debug only
            // flow 할당된 works 에 대해서 새로 duplicate 된 flow 를 할당되었나 확인
            replica.Works
            |> filter _.OptFlow.IsSome
            |> iter (fun w -> replica.Flows |> exists (fun f -> f.Guid = w.OptFlow.Value.Guid) |> verify)



            replica


    type RtProject with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate(?additionalActiveSystems:RtSystem seq, ?additionalPassiveSystems:RtSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toArray
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toArray
            plusActiveSystems @ plusPassiveSystems |> iter (fun s -> s.RawParent <- Some x)
            x.replicate(ReplicateBag(), plusActiveSystems, plusPassiveSystems)

        /// Guid 및 DateTime 은 새로이 생성
        member x.Duplicate(?additionalActiveSystems:RtSystem seq, ?additionalPassiveSystems:RtSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toList
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toList
            let actives  = (x.ActiveSystems  @ plusActiveSystems)  |-> _.Duplicate() |> toArray
            let passives = (x.PassiveSystems @ plusPassiveSystems) |-> _.Duplicate() |> toArray
            RtProject(actives, passives) |> uniqName (nn x.Name)
            |> tee(fun p -> (actives @ passives) |> iter (fun s -> s.RawParent <- Some p))

