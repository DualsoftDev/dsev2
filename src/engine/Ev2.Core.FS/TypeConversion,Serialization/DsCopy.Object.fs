namespace Ev2.Core.FS

open Dual.Common.Core.FS
open Dual.Common.Base
open System

/// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
module internal rec DsObjectCopyImpl =
    let uniqReplicateWithBag (bag:ReplicateBag) (src:#Unique) (dst:#Unique) : #Unique =
        dst
        |> replicateProperties src
        |> tee(fun z -> bag.OldGuid2NewObjectMap.TryAdd(src.Guid, z))

    type Project with // replicate
        /// Project 복제.
        member x.replicate(): Project =
            Project.Create([], [])
            |> tee(fun newProject ->
                let passives   = x.PassiveSystems |-> _.replicate() |> toArray
                let actives    = x.ActiveSystems  |-> _.replicate() |> toArray

                newProject
                |> replicateProperties x
                |> tee(fun z ->
                    (actives @ passives) |> iter (fun (s:DsSystem) -> setParentI z s)
                    actives    |> z.RawActiveSystems   .AddRange
                    passives   |> z.RawPassiveSystems  .AddRange)
                |> validateRuntime)

    type DsSystem with // replicate
        /// DsSystem 복제. 지정된 newSystem 객체에 현재 시스템의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            DsSystem.Create([], [], [], [], [])
            |> tee(fun newSystem ->
                // flow, work 상호 참조때문에 일단 flow 만 shallow copy
                let apiDefs  = x.ApiDefs  |-> _.replicate()  |> toArray
                let apiCalls = x.ApiCalls |-> _.replicate()  |> toArray
                let flows    = x.Flows    |-> _.replicate()  |> toArray
                let works    = x.Works    |-> _.replicate()  |> toArray // work 에서 shallow  copy 된 flow 참조 가능해짐.
                let arrows   = x.Arrows   |-> _.replicate()  |> toArray

                // 복제된 데이터를 newSystem에 설정
                flows    |> newSystem.RawFlows   .AddRange
                works    |> newSystem.RawWorks   .AddRange
                arrows   |> newSystem.RawArrows  .AddRange
                apiDefs  |> newSystem.RawApiDefs .AddRange
                apiCalls |> newSystem.RawApiCalls.AddRange

                // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
                newSystem |> replicateProperties x |> ignore

                // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
                flows    |> iter (setParentI newSystem)
                works    |> iter (setParentI newSystem)
                arrows   |> iter (setParentI newSystem)
                apiDefs  |> iter (setParentI newSystem)
                apiCalls |> iter (setParentI newSystem)

                // 검증
                arrows
                |> iter (fun (a:ArrowBetweenWorks) ->
                    works |> contains a.Source |> verify
                    works |> contains a.Target |> verify))



    type Work with // replicate
        /// Work 복제. 지정된 newWork 객체에 현재 작업의 내용을 복사
        member x.replicate() =
            Work.Create([], [], None)
            |> tee(fun newWork ->
                let calls = x.Calls |-> _.replicate() |> List.ofSeq

                let arrows:ArrowBetweenCalls list =
                    x.Arrows |-> _.replicate() |> List.ofSeq

                // 복제된 데이터를 newWork에 설정
                calls |> newWork.RawCalls.AddRange
                arrows |> newWork.RawArrows.AddRange
                newWork.FlowGuid <- x.FlowGuid

                // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
                newWork |> replicateProperties x |> ignore

                // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
                calls |> iter (setParentI newWork)
                arrows |> iter (setParentI newWork)

                // 검증
                arrows
                |> iter (fun (a:ArrowBetweenCalls) ->
                    calls |> contains a.Source |> verify
                    calls |> contains a.Target |> verify)

                )


    /// flow 와 work 는 상관관계로 복사할 때 서로를 참조해야 하므로, shallow copy 우선 한 후, works 생성 한 후 나머지 정보 채우기 수행
    type Flow with // replicate
        /// Flow 복제. 지정된 newFlow 객체에 현재 플로우의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            Flow.Create([], [], [], [])
            |> tee(fun newFlow ->
                let buttons    = x.Buttons    |-> _.replicate() |> toArray
                let lamps      = x.Lamps      |-> _.replicate() |> toArray
                let conditions = x.Conditions |-> _.replicate() |> toArray
                let actions    = x.Actions    |-> _.replicate() |> toArray

                // 복제된 데이터를 newFlow에 설정
                buttons    |> newFlow.RawButtons.AddRange
                lamps      |> newFlow.RawLamps.AddRange
                conditions |> newFlow.RawConditions.AddRange
                actions    |> newFlow.RawActions.AddRange

                // 먼저 bag에 등록하고 속성 복사 (GUID 포함)
                newFlow |> replicateProperties x |> ignore

                // 그 다음 parent 설정 - GUID가 확정된 후에 설정해야 함
                buttons    |> iter (fun z -> z.RawParent <- Some newFlow)
                lamps      |> iter (fun z -> z.RawParent <- Some newFlow)
                conditions |> iter (fun z -> z.RawParent <- Some newFlow)
                actions    |> iter (fun z -> z.RawParent <- Some newFlow) )


    type DsButton with // replicate
        member x.replicate() = DsButton.Create() |> replicateProperties x


    type Lamp with // replicate
        member x.replicate() = Lamp.Create() |> replicateProperties x


    type DsCondition with // replicate
        member x.replicate() = DsCondition.Create() |> replicateProperties x


    type DsAction with // replicate
        member x.replicate() = DsAction.Create() |> replicateProperties x


    type Call with // replicate
        /// Call 복제. 지정된 newCall 객체에 현재 호출의 내용을 복사
        member x.replicate() =
            // 원본 객체와 동일한 타입으로 복제 (확장 속성 유지)
            Call.Create(x.CallType, x.ApiCallGuids |> toList, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout)
            |> tee(fun newCall ->
                // ApiCall들은 시스템 레벨에서 복제되므로 그대로 유지
                let apiCallGuids = x.ApiCallGuids |> toList

                // 복제된 데이터를 newCall에 설정
                newCall.CallType <- x.CallType
                newCall.IsDisabled <- x.IsDisabled
                newCall.Timeout <- x.Timeout
                newCall.AutoConditions.Clear()
                newCall.CommonConditions.Clear()
                newCall.ApiCallGuids.Clear()
                newCall.AutoConditions.AddRange(x.AutoConditions)
                newCall.CommonConditions.AddRange(x.CommonConditions)
                newCall.ApiCallGuids.AddRange(apiCallGuids)

                newCall
                |> replicateProperties x
                |> tee(fun c ->
                    c.Status4 <- x.Status4))


    type ApiCall with // replicate
        member x.replicate() =
            ApiCall.Create(x.ApiDefGuid, x.InAddress, x.OutAddress, x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> replicateProperties x

    type ApiDef with // replicate
        member x.replicate() =

            ApiDef.Create(IsPush=x.IsPush, TxGuid=x.TxGuid, RxGuid=x.RxGuid)
            |> replicateProperties x
            //|> tee(fun z ->
            //    newApiDef.TxGuid <- bag.Newbies.GetNewGuid(x.TxGuid)
            //    newApiDef.RxGuid <- bag.Newbies.GetNewGuid(x.RxGuid))

    type ArrowBetweenWorks with // replicate

        member x.replicate() =
            ArrowBetweenWorks.Create(x.SourceGuid, x.TargetGuid, x.Type)
            |> replicateProperties x

    type ArrowBetweenCalls with // replicate
        member x.replicate() =
            ArrowBetweenCalls.Create(x.SourceGuid, x.TargetGuid, x.Type)
            |> replicateProperties x

[<AutoOpen>]
module DsObjectCopyAPIModule =
    open DsObjectCopyImpl

    type DsSystem with // Duplicate, Replicate
        /// Exact copy version: Guid, DateTime, Id 모두 동일하게 복제
        member x.Replicate() = x.replicate()

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

            //// [ApiCall 에서 APiDef Guid 참조] 부분, 신규 생성 객체의 Guid 로 교체
            //for ac in replicaSys.ApiCalls do
            //    let newGuid = newGuids[ac.ApiDefGuid]
            //    ac.ApiDefGuid <- newGuid

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


    type Project with // Duplicate, Replicate
        /// RtProject 객체 완전히 동일하게 복사 생성.  (Id, Guid 및 DateTime 포함 모두 동일하게 복사)
        member x.Replicate() =  // RtProject
            x.EnumerateRtObjects()
            |> iter (fun z ->
                z.RtObject <- None
                z.NjObject <- None
                z.ORMObject <- None
                z.DDic.Clear())

            x.replicate()
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

