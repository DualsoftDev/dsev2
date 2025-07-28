namespace Ev2.Core.FS

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
        |> replicateProperties src
        |> tee(fun z -> bag.Newbies.TryAdd(src.Guid, z))

    type Project with // replicate
        /// Project 복제.  PrototypeSystems 은 공용이므로, 참조 공유 (shallow copy) 방식으로 복제됨.
        member x.replicate(bag:ReplicateBag): Project =
            let actives    = x.ActiveSystems    |-> _.replicate(bag) |> toArray
            let passives   = x.PassiveSystems   |-> _.replicate(bag) |> toArray

            // TODO: MyPrototypeSystems 및 ImportedPrototypeSystems 을 먼저 복사하고, 이들을 통해 instantiate 해야 한다.  또는 복사하고 관계를 맞춰주든지..
            // Project.Instantiate  bag 이용해서 관계 찾아 낼 것.

            Project.Create()
            |> uniqReplicateWithBag bag x
            |> tee(fun z ->
                (actives @ passives) |> iter (fun (s:DsSystem) -> setParentI z s)
                actives    |> z.RawActiveSystems   .AddRange
                passives   |> z.RawPassiveSystems  .AddRange)
            |> validateRuntime


    type DsSystem with // replicate
        member x.replicate(bag:ReplicateBag) =
            // flow, work 상호 참조때문에 일단 flow 만 shallow copy
            let apiDefs  = x.ApiDefs  |-> _.replicate(bag)  |> toArray
            let apiCalls = x.ApiCalls |-> _.replicate(bag)  |> toArray
            let flows    = x.Flows    |-> _.replicate(bag)  |> toArray
            let works    = x.Works    |-> _.replicate(bag)  |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
            let arrows   = x.Arrows   |-> _.replicate(bag)  |> toArray

            arrows
            |> iter (fun (a:ArrowBetweenWorks) ->
                works |> contains a.Source |> verify
                works |> contains a.Target |> verify)

            DsSystem.Create(flows, works, arrows, apiDefs, apiCalls)
            |> uniqReplicateWithBag bag x


    type Work with // replicate
        member x.replicate(bag:ReplicateBag) =
            let calls =
                x.Calls |> Seq.map(fun z -> z.replicate bag) |> List.ofSeq

            let arrows:ArrowBetweenCalls list =
                x.Arrows |> List.ofSeq |-> _.replicate(bag)

            arrows
            |> iter (fun (a:ArrowBetweenCalls) ->
                calls |> contains a.Source |> verify
                calls |> contains a.Target |> verify)

            let flow =
                x.Flow
                |-> (fun f -> bag.Newbies[f.Guid] :?> Flow)

            Work.Create(calls, arrows, flow)
            |> uniqReplicateWithBag bag x


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type Flow with // replicate
        member x.replicate(bag:ReplicateBag) =
            let buttons    = x.Buttons    |-> _.replicate(bag) |> toArray
            let lamps      = x.Lamps      |-> _.replicate(bag) |> toArray
            let conditions = x.Conditions |-> _.replicate(bag) |> toArray
            let actions    = x.Actions    |-> _.replicate(bag) |> toArray

            Flow(buttons, lamps, conditions, actions)
            |> uniqReplicateWithBag bag x


    type DsButton with // replicate
        member x.replicate(bag:ReplicateBag) = DsButton()    |> uniqReplicateWithBag bag x


    type Lamp with // replicate
        member x.replicate(bag:ReplicateBag) = Lamp()      |> uniqReplicateWithBag bag x


    type DsCondition with // replicate
        member x.replicate(bag:ReplicateBag) = DsCondition() |> uniqReplicateWithBag bag x


    type DsAction with // replicate
        member x.replicate(bag:ReplicateBag) = DsAction()    |> uniqReplicateWithBag bag x


    type Call with // replicate
        member x.replicate(bag:ReplicateBag) =
            Call(x.CallType, x.ApiCallGuids, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout)
            |> uniqReplicateWithBag bag x
            |> tee(fun c -> c.Status4 <- x.Status4 )

    type ApiCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            ApiCall(x.ApiDefGuid, x.InAddress, x.OutAddress,
                      x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> uniqReplicateWithBag bag x

    type ApiDef with // replicate
        member x.replicate(bag:ReplicateBag) =
            ApiDef(x.IsPush, x.TopicIndex)
            |> uniqReplicateWithBag bag x


    type ArrowBetweenWorks with // replicate
        member x.replicate(bag:ReplicateBag) =
            let source = bag.Newbies[x.Source.Guid] :?> Work
            let target = bag.Newbies[x.Target.Guid] :?> Work
            ArrowBetweenWorks(source, target, x.Type)
            |> uniqReplicateWithBag bag x

    type ArrowBetweenCalls with // replicate
        member x.replicate(bag:ReplicateBag) =
            let source = bag.Newbies[x.Source.Guid] :?> Call
            let target = bag.Newbies[x.Target.Guid] :?> Call
            ArrowBetweenCalls(source, target, x.Type)
            |> uniqReplicateWithBag bag x

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type DsSystem with // Replicate, Duplicate
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


    type Project with // Replicate, Duplicate
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
            Project.Create()
            |> replicateProperties x |> uniqName newName |> uniqGuid (newGuid()) |> uniqDateTime (now())  |> uniqId None
            |> tee (fun z ->
                actives  |> z.RawActiveSystems.AddRange
                passives |> z.RawPassiveSystems.AddRange
                actives @ passives |> iter (fun s -> s.RawParent <- Some z))

