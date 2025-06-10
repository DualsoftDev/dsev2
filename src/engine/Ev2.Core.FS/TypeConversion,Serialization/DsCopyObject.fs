namespace Ev2.Core.FS

open System
open System.Collections.Generic
open Dual.Common.Core.FS
open Dual.Common.Base

/// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
module internal rec DsObjectCopyImpl =
    /// Work <-> Flow, Arrow <-> Call, Arrow <-> Work 간의 참조를 찾기 위한 bag
    type ReplicateBag() =
        /// NewGuid -> New object
        member val Newbies = Guid2UniqDic()

    let uniqReplicateWithBag (bag:ReplicateBag) (src:#Unique) (dst:#Unique) : #Unique =
        dst
        |> uniqReplicate src
        |> tee(fun z -> bag.Newbies.TryAdd(src.Guid, z))

    type RtProject with // replicate
        /// Project 복제.  PrototypeSystems 은 공용이므로, 참조 공유 (shallow copy) 방식으로 복제됨.
        member x.replicate(bag:ReplicateBag) =
            let actives    = x.ActiveSystems    |-> _.replicate(bag) |> toArray
            let passives   = x.PassiveSystems   |-> _.replicate(bag) |> toArray

            RtProject.Create()
            |> tee(fun z ->
                (actives @ passives) |> iter (fun (s:RtSystem) -> setParentI z s)
                x.RawPrototypeSystems |> z.RawPrototypeSystems.AddRange // 참조 공유 (shallow copy) 방식으로 복제됨.
                actives    |> z.RawActiveSystems   .AddRange
                passives   |> z.RawPassiveSystems  .AddRange)
            |> uniqReplicateWithBag bag x
            |> validateRuntime


    type RtSystem with // replicate
        member x.replicate(bag:ReplicateBag) =
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
            |> uniqReplicateWithBag bag x
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid    <- Some x.Guid                                       // 최근 원본 지향 버젼
                s.IRI           <- x.IRI
                s.Author        <- x.Author
                s.EngineVersion <- x.EngineVersion
                s.LangVersion   <- x.LangVersion
                s.Description   <- x.Description )


    type RtWork with // replicate
        member x.replicate(bag:ReplicateBag) =
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
            |> uniqReplicateWithBag bag x
            |> tee(fun w ->
                w.Status4    <- x.Status4
                w.Motion     <- x.Motion
                w.Script     <- x.Script
                w.IsFinished <- x.IsFinished
                w.NumRepeat  <- x.NumRepeat
                w.Period     <- x.Period
                w.Delay      <- x.Delay )


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type RtFlow with // replicate
        member x.replicate(bag:ReplicateBag) =
            let buttons    = x.Buttons    |-> _.replicate(bag) |> toArray
            let lamps      = x.Lamps      |-> _.replicate(bag) |> toArray
            let conditions = x.Conditions |-> _.replicate(bag) |> toArray
            let actions    = x.Actions    |-> _.replicate(bag) |> toArray

            RtFlow(buttons, lamps, conditions, actions)
            |> uniqReplicateWithBag bag x


    type RtButton with // replicate
        member x.replicate(bag:ReplicateBag) = RtButton()    |> uniqReplicateWithBag bag x


    type RtLamp with // replicate
        member x.replicate(bag:ReplicateBag) = RtLamp()      |> uniqReplicateWithBag bag x


    type RtCondition with // replicate
        member x.replicate(bag:ReplicateBag) = RtCondition() |> uniqReplicateWithBag bag x


    type RtAction with // replicate
        member x.replicate(bag:ReplicateBag) = RtAction()    |> uniqReplicateWithBag bag x


    type RtCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            RtCall(x.CallType, x.ApiCallGuids, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout)
            |> uniqReplicateWithBag bag x
            |> tee(fun c -> c.Status4 <- x.Status4 )

    type RtApiCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            RtApiCall(x.ApiDefGuid, x.InAddress, x.OutAddress,
                      x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> uniqReplicateWithBag bag x

    type RtApiDef with // replicate
        member x.replicate(bag:ReplicateBag) =
            RtApiDef(x.IsPush)
            |> uniqReplicateWithBag bag x


    type RtArrowBetweenWorks with // replicate
        member x.replicate(bag:ReplicateBag) =
            let source = bag.Newbies[x.Source.Guid] :?> RtWork
            let target = bag.Newbies[x.Target.Guid] :?> RtWork
            RtArrowBetweenWorks(source, target, x.Type)
            |> uniqReplicateWithBag bag x

    type RtArrowBetweenCalls with // replicate
        member x.replicate(bag:ReplicateBag) =
            let source = bag.Newbies[x.Source.Guid] :?> RtCall
            let target = bag.Newbies[x.Target.Guid] :?> RtCall
            RtArrowBetweenCalls(source, target, x.Type)
            |> uniqReplicateWithBag bag x

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type RtSystem with // Replicate, Duplicate
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate(ReplicateBag())

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate() =
            let oldies = x.EnumerateRtObjects().ToDictionary( _.Guid, id)
            let current = now()
            let replicaSys =
                x.Replicate()
                |> uniqGuid (newGuid()) |> uniqDateTime current |> uniqId None
                |> validateRuntime
            let replicas = replicaSys.EnumerateRtObjects()
            let newGuids = replicas.ToDictionary( _.Guid, (fun _ -> newGuid()))

            replicaSys.OriginGuid <- Some x.Guid
            replicaSys.IRI <- null     // IRI 는 항시 고유해야 하므로, 복제시 null 로 초기화

            replicas |> iter (fun repl ->
                repl.Id <- None
                repl.Guid <- newGuids[repl.Guid])

            // [ApiCall 에서 APiDef Guid 참조] 부분, 신규 생성 객체의 Guid 로 교체
            for ac in replicaSys.ApiCalls do
                let newGuid = newGuids[ac.ApiDefGuid]
                ac.ApiDefGuid <- newGuid

            for c in replicaSys.Works >>= _.Calls do

                // [Call 에서 APiCall Guid 참조] 부분, 신규 생성 객체의 Guid 로 교체
                let newGuids =
                    c.ApiCallGuids
                    |-> (fun g -> newGuids[g])
                    |> toList

                c.ApiCallGuids.Clear()
                c.ApiCallGuids.AddRange newGuids


            replicaSys |> validateRuntime |> ignore

            // 삭제 요망: debug only
            // flow 할당된 works 에 대해서 새로 duplicate 된 flow 를 할당되었나 확인
            replicaSys.Works
            |> filter _.Flow.IsSome
            |> iter (fun w ->
                replicaSys.Flows
                |> exists (fun f -> f.Guid = w.Flow.Value.Guid)
                |> verify)

            replicaSys


    type RtProject with // Replicate, Duplicate
        /// RtProject 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =  // RtProject
            x.EnumerateRtObjects()
            |> iter (fun z ->
                z.RtObject <- None
                z.NjObject <- None
                z.ORMObject <- None
                z.DDic.Clear())

            x.replicate(ReplicateBag())
            |> validateRuntime

        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(newName:string) =  // RtProject
            let actives  = x.ActiveSystems    |-> _.Duplicate()
            let passives = x.PassiveSystems   |-> _.Duplicate()
            RtProject.Create()
            |> uniqReplicate x |> uniqName newName |> uniqGuid (newGuid()) |> uniqDateTime (now())  |> uniqId None
            |> tee (fun z ->
                actives  |> z.RawActiveSystems.AddRange
                passives |> z.RawPassiveSystems.AddRange
                x.PrototypeSystems   |> z.RawPrototypeSystems.AddRange

                actives @ passives |> iter (fun s -> s.RawParent <- Some z)

                z.Version     <- x.Version
                z.Author      <- x.Author
                z.Description <- x.Description
                z.Database    <- x.Database )


    /// fwdDuplicate <- duplicateUnique
    let internal duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? RtSystem  as rs -> rs.Duplicate()
        | :? RtProject as rp -> rp.Duplicate($"CC_{rp.Name}")
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())