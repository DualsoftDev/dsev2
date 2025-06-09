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
        /// Project 복제.  PrototypeSystems 은 공용이므로, 참조 공유 (shallow copy) 방식으로 복제됨.
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let actives    = x.ActiveSystems    |-> _.replicate(bag) |> toArray
            let passives   = x.PassiveSystems   |-> _.replicate(bag) |> toArray

            RtProject.Create()
            |> tee(fun z ->
                (actives @ passives) |> iter (fun (s:RtSystem) -> setParentI z s)
                x.RawPrototypeSystems |> z.RawPrototypeSystems.AddRange // 참조 공유 (shallow copy) 방식으로 복제됨.
                actives    |> z.RawActiveSystems   .AddRange
                passives   |> z.RawPassiveSystems  .AddRange)
            |> uniqNGDA (nn x.Name) guid x.DateTime x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)
            |> validateRuntime


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
            |> uniqNGDA (nn x.Name) guid x.DateTime x.Parameter
            |> tee(fun s ->
                //s.OriginGuid <- x.OriginGuid |> Option.orElse (Some x.Guid)     // 최초 원본 지향 버젼
                s.OriginGuid    <- Some x.Guid                                       // 최근 원본 지향 버젼
                s.IRI           <- x.IRI
                s.Author        <- x.Author
                s.EngineVersion <- x.EngineVersion
                s.LangVersion   <- x.LangVersion
                s.Description   <- x.Description )
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


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
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> bag.Newbies[guid] <- z)
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
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
            let guid = bag.Add(x)

            let buttons    = x.Buttons    |-> _.replicate(bag) |> toArray
            let lamps      = x.Lamps      |-> _.replicate(bag) |> toArray
            let conditions = x.Conditions |-> _.replicate(bag) |> toArray
            let actions    = x.Actions    |-> _.replicate(bag) |> toArray

            RtFlow(buttons, lamps, conditions, actions)
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtButton with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtButton()
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtLamp with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtLamp()
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtCondition with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtCondition()
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtAction with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtAction()
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)










    type RtCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            RtCall(x.CallType, x.ApiCallGuids, x.AutoConditions, x.CommonConditions, x.IsDisabled, x.Timeout)
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> bag.Newbies[guid] <- z)
            |> tee(fun c -> c.Status4 <- x.Status4 )

    type RtApiCall with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)

            RtApiCall(x.ApiDefGuid, x.InAddress, x.OutAddress,
                      x.InSymbol, x.OutSymbol, x.ValueSpec)
            |> uniqNGA (nn x.Name) guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)

    type RtApiDef with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            RtApiDef(x.IsPush)
            |> uniqReplicate x |> uniqGuid guid
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenWorks with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtWork
            let target = bag.Newbies[x.Target.Guid] :?> RtWork
            RtArrowBetweenWorks(source, target, x.Type)
            |> uniqINGA x.Id x.Name guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)


    type RtArrowBetweenCalls with // replicate
        member x.replicate(bag:ReplicateBag) =
            let guid = bag.Add(x)
            let source = bag.Newbies[x.Source.Guid] :?> RtCall
            let target = bag.Newbies[x.Target.Guid] :?> RtCall
            RtArrowBetweenCalls(source, target, x.Type)
            |> uniqINGA x.Id x.Name guid x.Parameter
            |> tee(fun z -> x.RtObject <- Some z; z.RtObject <- Some x)
            |> tee(fun z -> bag.Newbies[guid] <- z)

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
            let replicaSys = x.Replicate() |> tee(fun z -> z.DateTime <- current) |> validateRuntime
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



        ///// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        //member x.DuplicateXXX() =  // RtProject
        //    RtProject.Create()
        //    |> tee(fun z ->
        //        let actives  = x.ActiveSystems    |-> _.Duplicate()
        //        let passives = x.PassiveSystems   |-> _.Duplicate()
        //        let protos   = x.PrototypeSystems |-> _.Duplicate()
        //        (actives @ passives) |> iter (setParentI z)
        //        actives  |> z.RawActiveSystems.AddRange
        //        passives |> z.RawPassiveSystems.AddRange
        //        protos   |> z.RawPrototypeSystems.AddRange

        //        z.Name        <- x.Name
        //        z.Parameter   <- x.Parameter
        //        z.Version     <- x.Version
        //        z.Author      <- x.Author
        //        z.Description <- x.Description
        //        z.Database    <- x.Database )
        //    |> uniqName (nn x.Name)


        /// 객체 복사 생성.  Id, Guid 및 DateTime 은 새로운 값으로 치환
        member x.Duplicate(newName:string) =  // RtProject
            let actives  = x.ActiveSystems    |-> _.Duplicate()
            let passives = x.PassiveSystems   |-> _.Duplicate()
            RtProject.Create(Name=newName)
            |> tee (fun z ->
                actives  |> z.RawActiveSystems.AddRange
                passives |> z.RawPassiveSystems.AddRange
                x.PrototypeSystems   |> z.RawPrototypeSystems.AddRange

                actives @ passives |> iter (fun s -> s.RawParent <- Some z)

                z.Name        <- x.Name
                z.Parameter   <- x.Parameter
                z.Version     <- x.Version
                z.Author      <- x.Author
                z.Description <- x.Description
                z.Database    <- x.Database )

            //x.Replicate()
            //|> tee(fun replicaPrj ->
            //    replicaPrj.Guid <- newGuid()

            //    replicaPrj.ActiveSystems @ replicaPrj.PassiveSystems
            //    |> iter(fun z ->
            //        z.IRI <- null // IRI 는 항시 고유해야 하므로, 복제시 null 로 초기화
            //        z.Guid <- newGuid()))



    /// fwdDuplicate <- duplicateUnique
    let internal duplicateUnique (source:IUnique): IUnique =
        match source with
        | :? RtSystem  as rs -> rs.Duplicate()
        | :? RtProject as rp -> rp.Duplicate($"CC_{rp.Name}")
        | _ -> failwithf "Unsupported type for duplication: %A" (source.GetType())