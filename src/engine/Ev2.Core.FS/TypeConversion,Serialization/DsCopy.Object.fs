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

            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let targetType = x.GetType()
            let newProject =
                if targetType = typeof<Project> then
                    // 기본 타입인 경우
                    new Project([], []) :> Project
                else
                    // 확장 타입인 경우 - 복제 전용 생성자 사용
                    try
                        System.Activator.CreateInstance(targetType, true) :?> Project
                    with
                    | _ -> new Project([], []) :> Project

            newProject
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

            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let targetType = x.GetType()
            let newSystem =
                if targetType = typeof<DsSystem> then
                    // 기본 타입인 경우
                    new DsSystem(flows, works, arrows, apiDefs, apiCalls) :> DsSystem
                else
                    // 확장 타입인 경우 - 복제 전용 생성자 사용
                    try
                        let instance = System.Activator.CreateInstance(targetType, true) :?> DsSystem
                        // 복제된 데이터 설정
                        flows |> instance.RawFlows.AddRange
                        works |> instance.RawWorks.AddRange
                        arrows |> instance.RawArrows.AddRange
                        apiDefs |> instance.RawApiDefs.AddRange
                        apiCalls |> instance.RawApiCalls.AddRange

                        instance
                    with
                    | _ -> new DsSystem(flows, works, arrows, apiDefs, apiCalls) :> DsSystem

            // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
            let replicatedSystem = newSystem |> uniqReplicateWithBag bag x

            // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
            flows |> iter (setParentI replicatedSystem)
            works |> iter (setParentI replicatedSystem)
            arrows |> iter (setParentI replicatedSystem)
            apiDefs |> iter (setParentI replicatedSystem)
            apiCalls |> iter (setParentI replicatedSystem)

            replicatedSystem


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

            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let targetType = x.GetType()
            let newWork =
                if targetType = typeof<Work> then
                    // 기본 타입인 경우
                    new Work(calls, arrows, flow) :> Work
                else
                    // 확장 타입인 경우 - 복제 전용 생성자 사용
                    try
                        let instance = System.Activator.CreateInstance(targetType, true) :?> Work
                        // 복제된 데이터 설정
                        calls |> instance.RawCalls.AddRange
                        arrows |> instance.RawArrows.AddRange
                        instance.Flow <- flow

                        instance
                    with
                    | _ -> new Work(calls, arrows, flow) :> Work

            // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
            let replicatedWork = newWork |> uniqReplicateWithBag bag x

            // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
            calls |> iter (setParentI replicatedWork)
            arrows |> iter (setParentI replicatedWork)

            replicatedWork


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type Flow with // replicate
        member x.replicate(bag:ReplicateBag) =
            let buttons    = x.Buttons    |-> _.replicate(bag) |> toArray
            let lamps      = x.Lamps      |-> _.replicate(bag) |> toArray
            let conditions = x.Conditions |-> _.replicate(bag) |> toArray
            let actions    = x.Actions    |-> _.replicate(bag) |> toArray

            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let targetType = x.GetType()
            let newFlow =
                if targetType = typeof<Flow> then
                    // 기본 타입인 경우
                    new Flow(buttons, lamps, conditions, actions) :> Flow
                else
                    // 확장 타입인 경우 - 복제 전용 생성자 사용
                    try
                        let instance = System.Activator.CreateInstance(targetType, true) :?> Flow
                        // 복제된 데이터 설정
                        buttons |> instance.RawButtons.AddRange
                        lamps |> instance.RawLamps.AddRange
                        conditions |> instance.RawConditions.AddRange
                        actions |> instance.RawActions.AddRange

                        instance
                    with
                    | _ -> new Flow(buttons, lamps, conditions, actions) :> Flow

            // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
            let replicatedFlow = newFlow |> uniqReplicateWithBag bag x

            // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
            buttons    |> iter (fun z -> z.RawParent <- Some replicatedFlow)
            lamps      |> iter (fun z -> z.RawParent <- Some replicatedFlow)
            conditions |> iter (fun z -> z.RawParent <- Some replicatedFlow)
            actions    |> iter (fun z -> z.RawParent <- Some replicatedFlow)

            replicatedFlow


    type DsButton with // replicate
        member x.replicate(bag:ReplicateBag) = new DsButton() |> uniqReplicateWithBag bag x


    type Lamp with // replicate
        member x.replicate(bag:ReplicateBag) = new Lamp() |> uniqReplicateWithBag bag x


    type DsCondition with // replicate
        member x.replicate(bag:ReplicateBag) = new DsCondition() |> uniqReplicateWithBag bag x


    type DsAction with // replicate
        member x.replicate(bag:ReplicateBag) = new DsAction() |> uniqReplicateWithBag bag x


    type Call with // replicate
        member x.replicate(bag:ReplicateBag) =
            // ApiCall들은 시스템 레벨에서 복제되므로 그대로 유지
            let apiCallGuids = x.ApiCallGuids |> toList

            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            let targetType = x.GetType()
            let newCall =
                if targetType = typeof<Call> then
                    // 기본 타입인 경우
                    new Call(x.CallType, apiCallGuids, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout) :> Call
                else
                    // 확장 타입인 경우 - 복제 전용 생성자 사용
                    try
                        let instance = System.Activator.CreateInstance(targetType, true) :?> Call
                        // 복제된 데이터 설정
                        instance.CallType <- x.CallType
                        instance.IsDisabled <- x.IsDisabled
                        instance.Timeout <- x.Timeout
                        instance.AutoConditions.Clear()
                        instance.CommonConditions.Clear()
                        instance.ApiCallGuids.Clear()
                        instance.AutoConditions.AddRange(x.AutoConditions)
                        instance.CommonConditions.AddRange(x.CommonConditions)
                        instance.ApiCallGuids.AddRange(apiCallGuids)
                        instance
                    with
                    | _ -> new Call(x.CallType, apiCallGuids, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout) :> Call

            newCall
            |> uniqReplicateWithBag bag x
            |> tee(fun c ->
                c.Status4 <- x.Status4
            )

    type ApiCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            new ApiCall(x.ApiDefGuid, x.InAddress, x.OutAddress, x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> uniqReplicateWithBag bag x

    type ApiDef with // replicate
        member x.replicate(bag:ReplicateBag) =
            new ApiDef(x.IsPush)
            |> uniqReplicateWithBag bag x
            |> tee(fun a ->
                a.TopicIndex <- x.TopicIndex
                a.IsTopicOrigin <- x.IsTopicOrigin
            )


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


            // 새로운 GUID 할당 후 validation 실행
            // parent 관계는 유지되어야 하므로 validation이 성공해야 함
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

