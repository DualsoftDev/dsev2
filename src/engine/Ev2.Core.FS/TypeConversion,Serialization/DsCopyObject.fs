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
        member x.Add(old:Unique) =
            old.Guid |> tee (fun guid -> x.Oldies.Add(guid, old))

    let internal nn (oldName:string) =
#if DEBUG
        match oldName with
        | null | "" -> null
        | _ -> $"Copy of {oldName}"
#else
        oldName
#endif

    type EdProject with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let actives  = x.ActiveSystems  |-> _.replicate(bag) |> toArray
            let passives = x.PassiveSystems |-> _.replicate(bag) |> toArray

            EdProject()
            |> tee(fun z ->
                (actives @ passives) |> iter (fun (s:EdSystem) -> s.RawParent <- Some z)
                actives  |> z.ActiveSystems.AddRange
                passives |> z.PassiveSystems.AddRange )
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)
            |> validateEditable


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type EdFlow with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            EdFlow()
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type EdSystem with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let apiDefs  = x.ApiDefs  |-> _.replicate(bag)  |> toArray
            let apiCalls = x.ApiCalls |-> _.replicate(bag)  |> toArray
            let flows    = x.Flows    |-> _.replicate(bag)  |> toArray
            let works    = x.Works    |-> _.replicate(bag)  |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows   = x.Arrows   |-> _.replicate(bag)  |> toArray

            arrows
            |> iter (fun (a:EdArrowBetweenWorks) ->
                works |> contains a.Source |> verify
                works |> contains a.Target |> verify)

            EdSystem.Create(x.PrototypeSystemGuid, flows, works, arrows, apiDefs, apiCalls)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid <- Some x.Guid                                       // 최근 원본 지향 버젼
            ) |> tee(fun z -> bag.Newbies[guid] <- z)


    type EdWork with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            let calls =
                x.Calls |> Seq.map(fun z -> z.replicate bag) |> List.ofSeq

            let arrows:EdArrowBetweenCalls list =
                x.Arrows |> List.ofSeq |-> _.replicate(bag)

            arrows
            |> iter (fun (a:EdArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            let flow =
                x.OptFlow
                |-> (fun f -> bag.Newbies[f.Guid] :?> EdFlow)

            EdWork.Create(calls, arrows, flow)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type EdCall with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            EdCall(CallType=x.CallType, AutoPre=x.AutoPre, Safety=x.Safety, Timeout=x.Timeout)
            |> tee(fun z ->
                z.ApiCallGuids.Clear()
                x.ApiCallGuids |> z.ApiCallGuids.AddRange )
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type EdApiCall with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            EdApiCall(x.ApiDefGuid, InAddress=x.InAddress, OutAddress=x.OutAddress,
                InSymbol=x.InSymbol, OutSymbol=x.OutSymbol, ValueType=x.ValueType, Value=x.Value)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type EdApiDef with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            EdApiDef(IsPush=x.IsPush)
            |> uniqINGD_fromObj x |> uniqGuid guid
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type EdArrowBetweenWorks with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> EdWork
            let target = bag.Newbies[x.Target.Guid] :?> EdWork
            EdArrowBetweenWorks(source, target, x.Type)
            |> uniqGD guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type EdArrowBetweenCalls with
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> EdCall
            let target = bag.Newbies[x.Target.Guid] :?> EdCall
            EdArrowBetweenCalls(source, target, x.Type)
            |> uniqGD guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type EdSystem with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate() =
            let replica = x.Replicate() |> validateEditable
            let objs = replica.EnumerateEdObjects()
            let guidDic = objs.ToDictionary( _.Guid, (fun _ -> newGuid()))
            let current = now()

            replica.OriginGuid <- Some x.Guid
            objs |> iter (fun obj ->
                obj.Id <- None
                obj.Guid <- guidDic[obj.Guid]
                obj.DateTime <- current)

            // [ApiCall 에서 APiDef Guid 참조] 부분, 신규 생성 객체의 Guid 로 교체
            for ac in replica.ApiCalls do
                let newGuid = guidDic[ac.ApiDefGuid]
                ac.ApiDefGuid <- newGuid

            for c in replica.Works >>= _.Calls do

                // [Call 에서 APiCall Guid 참조] 부분, 신규 생성 객체의 Guid 로 교체
                let newGuids =
                    c.ApiCallGuids
                    |-> (fun g -> guidDic[g])
                    |> toList

                c.ApiCallGuids.Clear()
                c.ApiCallGuids.AddRange newGuids


            replica |> validateEditable |> ignore

            // 삭제 요망: debug only
            // flow 할당된 works 에 대해서 새로 duplicate 된 flow 를 할당되었나 확인
            replica.Works
            |> filter _.OptFlow.IsSome
            |> iter (fun w ->
                replica.Flows
                |> exists (fun f -> f.Guid = w.OptFlow.Value.Guid)
                |> verify)

            replica


    type EdProject with
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(?additionalActiveSystems:EdSystem seq, ?additionalPassiveSystems:EdSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toList
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toList
            let actives  = (x.ActiveSystems  @ plusActiveSystems)  |-> _.Duplicate() |> toArray
            let passives = (x.PassiveSystems @ plusPassiveSystems) |-> _.Duplicate() |> toArray

            EdProject()
            |> tee(fun z ->
                (actives @ passives) |> iter (fun s -> s.RawParent <- Some z)
                actives  |> z.ActiveSystems.AddRange
                passives |> z.PassiveSystems.AddRange )
            |> uniqName (nn x.Name)
            |> tee(fun p -> (actives @ passives) |> iter (fun s -> s.RawParent <- Some p))


    type RtProject with
        /// RtProject 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =
            x.ToEditableProject()   |> validateEditable
            |> _.Replicate()        |> validateEditable
            |> _.ToRuntimeProject() |> validateRuntime


        /// RtSystem 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(?additionalActiveSystems:RtSystem seq, ?additionalPassiveSystems:RtSystem seq) =
            let plusActiveSystems  = additionalActiveSystems  |? Seq.empty |> toList
            let plusPassiveSystems = additionalPassiveSystems |? Seq.empty |> toList
            let actives  = (x.ActiveSystems  @ plusActiveSystems)  |-> _.ToEditableSystem().Duplicate() |> toArray
            let passives = (x.PassiveSystems @ plusPassiveSystems) |-> _.ToEditableSystem().Duplicate() |> toArray

            x.ToEditableProject() |> validateEditable
            |> tee (fun ep -> (actives @ passives) |> iter (fun s -> s.RawParent <- Some ep))
            |> _.Duplicate(actives, passives)  |> validateEditable
            |> _.ToRuntimeProject() |> validateRuntime



    type RtSystem with
        /// RtSystem 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =
            x.ToEditableSystem() |> validateEditable
            |> _.Replicate()     |> validateEditable
            |> _.ToRuntimeSystem(Ed2RtBag()) |> validateRuntime

        /// RtSystem 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate() =
            x.ToEditableSystem() |> validateEditable
            |> _.Duplicate()     |> validateEditable
            |> _.ToRuntimeSystem(Ed2RtBag()) |> validateRuntime

    let duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? EdSystem  as es -> es.Duplicate()
        | :? EdProject as ep -> ep.Duplicate()
        | :? RtSystem  as rs -> rs.Duplicate()
        | :? RtProject as rp -> rp.Duplicate()
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())