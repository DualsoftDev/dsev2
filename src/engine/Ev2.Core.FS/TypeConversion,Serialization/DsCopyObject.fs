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
            old.Guid |> tee (fun guid -> x.Oldies.TryAdd(guid, old))

    let internal nn (oldName:string) =
//#if DEBUG
//        match oldName with
//        | null | "" -> null
//        | _ -> $"Copy of {oldName}"
//#else
        oldName
//#endif

    type RtProject with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let prototypes = x.PrototypeSystems |-> _.replicate(bag) |> toArray
            let actives  = x.ActiveSystems  |-> _.replicate(bag) |> toArray
            let passives = x.PassiveSystems |-> _.replicate(bag) |> toArray

            RtProject.Create()
            |> tee(fun z ->
                (actives @ passives) |> iter (fun (s:RtSystem) -> setParentI z s)
                prototypes |> z.RawPrototypeSystems.AddRange
                actives  |> z.RawActiveSystems.AddRange
                passives |> z.RawPassiveSystems.AddRange )
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)
            |> validateRuntime


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type RtFlow with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            RtFlow()
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtSystem with // replicate
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

            RtSystem.Create(x.PrototypeSystemGuid, flows, works, arrows, apiDefs, apiCalls)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid <- Some x.Guid                                       // 최근 원본 지향 버젼
            ) |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtWork with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            let calls =
                x.Calls |> Seq.map(fun z -> z.replicate bag) |> List.ofSeq

            let arrows:RtArrowBetweenCalls list =
                x.Arrows |> List.ofSeq |-> _.replicate(bag)

            arrows
            |> iter (fun (a:RtArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            let flow =
                x.Flow
                |-> (fun f -> bag.Newbies[f.Guid] :?> RtFlow)

            RtWork.Create(calls, arrows, flow)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            RtCall(x.CallType, x.ApiCallGuids, x.AutoPre, x.Safety, x.IsDisabled, x.Timeout)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtApiCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            RtApiCall(x.ApiDefGuid, x.InAddress, x.OutAddress,
                      x.InSymbol, x.OutSymbol, x.ValueType, x.Value)
            |> uniqNGD (nn x.Name) guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtApiDef with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtApiDef(x.IsPush)
            |> uniqINGD_fromObj x |> uniqGuid guid
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenWorks with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtWork
            let target = bag.Newbies[x.Target.Guid] :?> RtWork
            RtArrowBetweenWorks(source, target, x.Type)
            |> uniqINGD x.Id x.Name guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenCalls with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtCall
            let target = bag.Newbies[x.Target.Guid] :?> RtCall
            RtArrowBetweenCalls(source, target, x.Type)
            |> uniqINGD x.Id x.Name guid x.DateTime
            |> tee(fun z -> bag.Newbies[guid] <- z)

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type RtSystem with // Replicate, Duplicate
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate() =
            let replica = x.Replicate() |> validateRuntime
            let objs = replica.EnumerateRtObjects()
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


            replica |> validateRuntime |> ignore

            // 삭제 요망: debug only
            // flow 할당된 works 에 대해서 새로 duplicate 된 flow 를 할당되었나 확인
            replica.Works
            |> filter _.Flow.IsSome
            |> iter (fun w ->
                replica.Flows
                |> exists (fun f -> f.Guid = w.Flow.Value.Guid)
                |> verify)

            replica


    type RtProject with // Replicate, Duplicate
        /// RtProject 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =  // RtProject
            x.EnumerateRtObjects() |> iter (fun z -> z.DDic.Clear())

            x.replicate(ReplicateBag())
            |> validateRuntime



        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate() =  // RtProject
            RtProject.Create()
            |> tee(fun z ->
                let actives  = x.ActiveSystems |-> _.Duplicate()
                let passives = x.PassiveSystems |-> _.Duplicate()
                let protos = x.PrototypeSystems |-> _.Duplicate()
                (actives @ passives) |> iter (setParentI z)
                actives |> z.RawActiveSystems.AddRange
                passives |> z.RawPassiveSystems.AddRange
                protos |> z.RawPrototypeSystems.AddRange

                z.Name <- x.Name
                z.Version <- x.Version
                z.Author <- x.Author
                z.Description <- x.Description
                z.LastConnectionString <- x.LastConnectionString )
            |> uniqName (nn x.Name)


    /// fwdDuplicate <- duplicateUnique
    let internal duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? RtSystem  as rs -> rs.Duplicate()
        | :? RtProject as rp -> rp.Duplicate()
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())