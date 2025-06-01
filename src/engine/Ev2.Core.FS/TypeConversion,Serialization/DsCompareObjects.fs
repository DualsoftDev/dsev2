namespace Ev2.Core.FS

open System
open Dual.Common.Core.FS
open Dual.Common.Base
open System.ComponentModel

[<AutoOpen>]
module rec DsCompareObjects =
    type IUnique with
        member x.GetGuid():Guid =
            match x with
            | :? Unique as u    -> u.Guid
            | :? NjUnique as u  -> u.Guid
            | :? ORMUnique as u -> s2guid u.Guid
            | _ -> failwith "ERROR"

        member x.GetName() =
            match x with
            | :? Unique as u    -> u.Name
            | :? NjUnique as u  -> u.Name
            | :? ORMUnique as u -> u.Name
            | _ -> failwith "ERROR"

        member x.TryGetId():Id option =
            match x with
            | :? Unique as u    -> u.Id
            | :? NjUnique as u  -> n2o u.Id
            | :? ORMUnique as u -> n2o u.Id
            | _ -> failwith "ERROR"

        member x.GetDateTime() =
            match x with
            | :? Unique as u    -> u.DateTime
            | :? NjUnique as u  -> u.DateTime
            | :? ORMUnique as u -> u.DateTime
            | _ -> failwith "ERROR"

        member x.TryGetRawParent():IUnique option =
            match x with
            | :? Unique as u    -> u.RawParent >>= tryCast<IUnique>
            | :? NjUnique as u  -> u.RawParent >>= tryCast<IUnique>
            | :? ORMUnique as u -> u.RawParent >>= tryCast<IUnique>
            | _ -> failwith "ERROR"

    type UniqueCompareCriteria(?id:bool, ?guid:bool, ?dateTime:bool, ?parentGuid) =
        let id       = id       |? true
        let guid     = guid     |? true
        let dateTime = dateTime |? true
        let parentGuid = parentGuid |? true
        member val Id       = id       with get, set
        member val Guid     = guid     with get, set
        member val DateTime = dateTime with get, set
        member val ParentGuid = parentGuid with get, set

        (* Project/System 속성 *)
        member val Author = true with get, set
        member val Version = true with get, set
        member val Description = true with get, set
        member val LastConnectionString = true with get, set
        member val EngineVersion = true with get, set
        member val LangVersion   = true with get, set

    /// abberviation
    type internal Ucc = UniqueCompareCriteria

    type IUnique with
        member internal x.IsEqualIUnique(y:IUnique, criteria:Ucc) =
            let c = criteria

            let e1 = x.GetName() =y.GetName()
            let e3 = c.Id       && x.TryGetId()    =y.TryGetId()
            let e4 = c.Guid     && x.GetGuid()     =y.GetGuid()
            let e5 = c.DateTime && x.GetDateTime() =y.GetDateTime()
            let e2 = c.ParentGuid && ( (x.TryGetRawParent() |-> _.GetGuid()) =(y.TryGetRawParent() |-> _.GetGuid()) )
            e1 && e2 && e3 && e4 && e5

    let private sortByGuid (xs:#IUnique list): #IUnique list = xs |> List.sortBy (fun x -> x.GetGuid())


    //let private isEqualSystems (xs:RtSystem list) (ys:RtSystem list) (criteria:Ucc) =
    //    if xs.Length <> ys.Length then
    //        false
    //    else
    //        let xs = xs |> sortByGuid
    //        let ys = ys |> sortByGuid
    //        xs |> zip ys |> forall (fun (x, y) -> x.IsEqual(y, criteria=criteria))

    let private isEqualList
        (xs: 'T list)
        (ys: 'T list)
        (criteria: Ucc option) =

        let xs = xs |> sortByGuid
        let ys = ys |> sortByGuid

        //xs.Length = ys.Length &&
        //(List.zip xs ys |> List.forall (fun (a, b) -> a.IsEqual(b, ?criteria=criteria)))

        let r =
            xs.Length = ys.Length &&
            (List.zip xs ys |> List.forall (fun (a, b) -> a.IsEqual(b, ?criteria=criteria)))
        if not r then
            noop()
        r



    type RtProject with
        member x.IsEqual(y:RtProject, ?criteria:Ucc) =
            let c = criteria |? Ucc()
            if not <| x.IsEqualIUnique(y, criteria=c) then
                false
            else
                (* System 들 비교*)
                let e1 = (x.PrototypeSystems, y.PrototypeSystems,  Some c) |||> isEqualList
                let e2 = (x.ActiveSystems,    y.ActiveSystems,     Some c) |||> isEqualList
                let e3 = (x.PassiveSystems,   y.PassiveSystems,    Some c) |||> isEqualList

                (* 기타 속성 비교 *)
                let e4 = c.Author && x.Author = y.Author

                e1 && e2 && e3 && e4

    type RtSystem with
        member x.IsEqual(y:RtSystem, ?criteria:Ucc) =
            let e1 = (x.Flows, y.Flows, criteria) |||> isEqualList
            let e2 = (x.Works, y.Works, criteria) |||> isEqualList
            let e3 = (x.Arrows, y.Arrows, criteria) |||> isEqualList
            let e4 = (x.ApiDefs, y.ApiDefs, criteria) |||> isEqualList
            let e5 = (x.ApiCalls, y.ApiCalls, criteria) |||> isEqualList

            let e10 = x.PrototypeSystemGuid = y.PrototypeSystemGuid
            let e11 = x.Author        = y.Author
            let e12 = x.EngineVersion = y.EngineVersion
            let e13 = x.LangVersion   = y.LangVersion
            let e14 = x.Description   = y.Description

            e1 && e2 && e3 && e4 && e5
               && e10 && e11 && e12 && e13 && e14


    type RtFlow with
        member x.IsEqual(y:RtFlow, ?criteria:Ucc) =
            let e1 = x.System.Guid = y.System.Guid
            let e2 = (x.Works, y.Works, criteria) |||> isEqualList
            e1 && e2

    type RtWork with
        member x.IsEqual(y:RtWork, ?criteria:Ucc) =
            let e1 = x.System.Guid = y.System.Guid
            let e2 = (x.OptFlow |-> _.Guid) = (y.OptFlow |-> _.Guid)
            let e3 = (x.Calls, y.Calls, criteria) |||> isEqualList
            let e4 = (x.Arrows, y.Arrows, criteria) |||> isEqualList
            e1 && e2 && e3 && e4

    type RtCall with
        member x.IsEqual(y:RtCall, ?criteria:Ucc) =
            let e1 = x.Work.Guid  = y.Work.Guid
            let e2 = x.CallType   = y.CallType
            let e3 = x.AutoPre    = y.AutoPre
            let e4 = x.Safety     = y.Safety
            let e5 = x.IsDisabled = y.IsDisabled
            let e6 = x.Timeout    = y.Timeout
            let e7 = (x.ApiCallGuids, y.ApiCallGuids) ||> setEqual
            e1 && e2 && e3 && e4 && e5 && e6 && e7

    type RtApiDef with
        member x.IsEqual(y:RtApiDef, ?criteria:Ucc) =
            let e1 = x.IsPush  = y.IsPush
            e1

    type RtApiCall with
        member x.IsEqual(y:RtApiCall, ?criteria:Ucc) =
            let e1 = x.ApiDefGuid = y.ApiDefGuid
            let e2 = x.InAddress  = y.InAddress
            let e3 = x.OutAddress = y.OutAddress
            let e4 = x.InSymbol   = y.InSymbol
            let e5 = x.OutSymbol  = y.OutSymbol
            let e6 = x.ValueType  = y.ValueType
            let e7 = x.Value      = y.Value
            e1 && e2 && e3 && e4 && e5 && e6 && e7

    type RtArrowBetweenWorks with
        member x.IsEqual(y:RtArrowBetweenWorks, ?criteria:Ucc) =
            let e1 = x.Source.Guid = y.Source.Guid
            let e2 = x.Target.Guid = y.Target.Guid
            let e3 = x.Type = y.Type
            e1 && e2 && e3

    type RtArrowBetweenCalls with
        member x.IsEqual(y:RtArrowBetweenCalls, ?criteria:Ucc) =
            let e1 = x.Source.Guid = y.Source.Guid
            let e2 = x.Target.Guid = y.Target.Guid
            let e3 = x.Type = y.Type
            e1 && e2 && e3

    type IUnique with
        member internal x.IsEqual(y:IUnique, ?criteria:Ucc) =
            let c = criteria |? Ucc()
            if not <| x.IsEqualIUnique(y, criteria=c) then
                false
            else
                match x, y with
                | (:? RtProject as u), (:? RtProject as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtSystem  as u), (:? RtSystem  as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtFlow    as u), (:? RtFlow    as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtWork    as u), (:? RtWork    as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtCall    as u), (:? RtCall    as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtApiDef  as u), (:? RtApiDef  as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtApiCall as u), (:? RtApiCall as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtArrowBetweenWorks as u), (:? RtArrowBetweenWorks as v)  -> u.IsEqual(v, criteria=c)
                | (:? RtArrowBetweenCalls as u), (:? RtArrowBetweenCalls as v)  -> u.IsEqual(v, criteria=c)

                (* Ed/Nj/ORM 은 불필요 할 듯.. *)

                //| (:? EdProject as u), (:? EdProject as v) -> u.IsEqual(v, criteria=c)
                //| (:? EdSystem as u),  (:? EdSystem as v)  -> u.IsEqual(v, criteria=c)
                //| (:? EdFlow as u),    (:? EdFlow as v)    -> u.IsEqual(v, criteria=c)
                //| (:? EdWork as u),    (:? EdWork as v)    -> u.IsEqual(v, criteria=c)
                //| (:? EdCall as u),    (:? EdCall as v)    -> u.IsEqual(v, criteria=c)
                //| (:? EdApiDef as u),  (:? EdApiDef as v)  -> u.IsEqual(v, criteria=c)
                //| (:? EdApiCall as u), (:? EdApiCall as v) -> u.IsEqual(v, criteria=c)


                //| (:? NjProject as u), (:? NjProject as v) -> u.IsEqual(v, criteria=c)
                //| (:? NjSystem as u),  (:? NjSystem as v)  -> u.IsEqual(v, criteria=c)
                //| (:? NjFlow as u),    (:? NjFlow as v)    -> u.IsEqual(v, criteria=c)
                //| (:? NjWork as u),    (:? NjWork as v)    -> u.IsEqual(v, criteria=c)
                //| (:? NjCall as u),    (:? NjCall as v)    -> u.IsEqual(v, criteria=c)
                //| (:? NjApiDef as u),  (:? NjApiDef as v)  -> u.IsEqual(v, criteria=c)
                //| (:? NjApiCall as u), (:? NjApiCall as v) -> u.IsEqual(v, criteria=c)

                | _ -> failwith "ERROR"

