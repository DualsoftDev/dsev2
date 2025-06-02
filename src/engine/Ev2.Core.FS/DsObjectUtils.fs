namespace Ev2.Core.FS

open Dual.Common.Core.FS
open System
open System.Collections.Generic

[<AutoOpen>]
module DsObjectUtilsModule =
    type RtSystem with
        static member Create(protoGuid:Guid option, flows:RtFlow[], works:RtWork[],
            arrows:RtArrowBetweenWorks[], apiDefs:RtApiDef[], apiCalls:RtApiCall[]
        ) =
            RtSystem(protoGuid, flows, works, arrows, apiDefs, apiCalls)
            |> tee (fun z ->
                flows    |> iter (fun y -> y.RawParent <- Some z)
                works    |> iter (fun y -> y.RawParent <- Some z)
                arrows   |> iter (fun y -> y.RawParent <- Some z)
                apiDefs  |> iter (fun y -> y.RawParent <- Some z)
                apiCalls |> iter (fun y -> y.RawParent <- Some z) )
        static member Create() = RtSystem(None, [||], [||], [||], [||], [||])

    type RtWork with
        static member Create(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, optFlow:RtFlow option) =
            let calls = calls |> toList
            let arrows = arrows |> toList
            RtWork(calls, arrows, optFlow)
            |> tee (fun z ->
                calls   |> iter (fun y -> y.RawParent <- Some z)
                arrows  |> iter (fun y -> y.RawParent <- Some z)
                optFlow |> iter (fun y -> y.RawParent <- Some z) )
        static member Create() = RtWork([], [], None)

    type RtCall with
        static member Create(callType:DbCallType, apiCalls:RtApiCall seq,
            autoPre:string, safety:string, isDisabled:bool, timeout:int option
        ) =
            let apiCallGuids = apiCalls |-> _.Guid
            RtCall(callType, apiCallGuids, autoPre, safety, isDisabled, timeout)
            |> tee (fun z ->
                apiCalls |> iter (fun y -> y.RawParent <- Some z) )

        static member Create() = RtCall(DbCallType.Normal, [], nullString, nullString, false, None)


    [<AbstractClass>]
    type ParameterBase() =
        interface IParameter

    type ProjectParameter() =
        // name, langVersion, engineVersion, description, author, dateTime, activeSystems, passiveSystems
        inherit ParameterBase()

    type SystemParameter() =
        inherit ParameterBase()

    type IParameterContainer with
        member x.GetParameter(): IParameter =
            match x with
            | :? RtProject as prj -> ProjectParameter() :> IParameter
            | :? RtSystem as sys -> SystemParameter()
            | _ -> failwith $"GetParameter not implemented for {x.GetType()}"


    type IArrow with
        member x.GetSource(): Unique =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Source
            | :? RtArrowBetweenWorks as a -> a.Source
            | _ -> failwith "ERROR"
        member x.GetTarget(): Unique =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Target
            | :? RtArrowBetweenWorks as a -> a.Target
            | _ -> failwith "ERROR"
        member x.GetArrowType(): DbArrowType =
            match x with
            | :? RtArrowBetweenCalls as a -> a.Type
            | :? RtArrowBetweenWorks as a -> a.Type
            | _ -> failwith "ERROR"

    type Unique with
        member x.EnumerateAncestors(?includeMe): Unique list = [
            let includeMe = includeMe |? true
            if includeMe then
                yield x
            match x.RawParent with
            | Some parent ->
                yield! parent.EnumerateAncestors()
            | None -> ()
        ]

        member x.GetFQDN(): string =
            x.EnumerateAncestors()
            |> reverse
            |-> fun z -> if z.Name.IsNullOrEmpty() then $"[{z.GetType().Name}]" else z.Name
            |> String.concat "/"


    type RtUnique with
        ///// DS object 의 모든 상위 DS object 의 DateTime 을 갱신.  (tree 구조를 따라가면서 갱신)
        //member x.UpdateDateTime(?dateTime:DateTime) =
        //    let dateTime = dateTime |?? now
        //    x.EnumerateRtObjects() |> iter (fun z -> z.DateTime <- dateTime)

        //(* see also EdUnique.EnumerateRtObjects *)
        //member x.EnumerateRtObjects(?includeMe): RtUnique list =
        //    seq {
        //        let includeMe = includeMe |? true
        //        if includeMe then
        //            yield x
        //        match x with
        //        | :? RtProject as prj ->
        //            yield! prj.PrototypeSystems >>= _.EnumerateRtObjects()
        //            yield! prj.Systems   >>= _.EnumerateRtObjects()
        //        | :? RtSystem as sys ->
        //            yield! sys.Works     >>= _.EnumerateRtObjects()
        //            yield! sys.Flows     >>= _.EnumerateRtObjects()
        //            yield! sys.Arrows    >>= _.EnumerateRtObjects()
        //            yield! sys.ApiDefs   >>= _.EnumerateRtObjects()
        //            yield! sys.ApiCalls  >>= _.EnumerateRtObjects()
        //        | :? RtWork as work ->
        //            yield! work.Calls    >>= _.EnumerateRtObjects()
        //            yield! work.Arrows   >>= _.EnumerateRtObjects()
        //        | :? RtCall as call ->
        //            //yield! call.ApiCalls >>= _.EnumerateRtObjects()
        //            ()
        //        | _ ->
        //            tracefn $"Skipping {(x.GetType())} in EnumerateRtObjects"
        //            ()
        //    } |> List.ofSeq

        member x.Validate(guidDic:Dictionary<Guid, RtUnique>) =
            verify (x.Guid <> emptyGuid)
            verify (x.DateTime <> minDate)
            match x with
            | :? RtProject | :? RtSystem | :? RtFlow  | :? RtWork  | :? RtCall -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? RtProject as prj ->
                prj.Systems |> iter _.Validate(guidDic)
                for s in prj.Systems do
                    verify (s.RawParent.Value.Guid = prj.Guid)
            | :? RtSystem as sys ->
                sys.Works |> iter _.Validate(guidDic)
                for w in sys.Works  do
                    verify (w.RawParent.Value.Guid = sys.Guid)
                    for c in w.Calls do
                        c.ApiCalls |-> _.Guid |> forall(guidDic.ContainsKey) |> verify
                        for ac in c.ApiCalls do
                            ac.ApiDef.Guid = ac.ApiDefGuid |> verify

                sys.Arrows |> iter _.Validate(guidDic)
                for a in sys.Arrows do
                    verify (a.RawParent.Value.Guid = sys.Guid)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

                sys.ApiDefs |> iter _.Validate(guidDic)
                for w in sys.ApiDefs  do
                    verify (w.RawParent.Value.Guid = sys.Guid)

                sys.ApiCalls |> iter _.Validate(guidDic)
                for ac in sys.ApiCalls  do
                    verify (ac.RawParent.Value.Guid = sys.Guid)

            | :? RtFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate(guidDic)
                for w in works  do
                    verify (w.OptFlow = Some flow)


            | :? RtWork as work ->
                work.Calls |> iter _.Validate(guidDic)
                for c in work.Calls do
                    verify (c.RawParent.Value.Guid = work.Guid)

                work.Arrows |> iter _.Validate(guidDic)
                for a in work.Arrows do
                    verify (a.RawParent.Value.Guid = work.Guid)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? RtCall as call ->
                ()
            | _ ->
                tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                ()



