namespace Ev2.Core.FS

open Dual.Common.Core.FS
open System

[<AutoOpen>]
module DsObjectUtilsModule =
    type RtSystem with
        static member Create(flows:RtFlow[], works:RtWork[], arrows:RtArrowBetweenWorks[]) =
            RtSystem(flows, works, arrows)
            |> tee (fun z ->
                flows  |> iter (fun y -> y.RawParent <- Some z)
                works  |> iter (fun y -> y.RawParent <- Some z)
                arrows |> iter (fun y -> y.RawParent <- Some z) )

    type RtWork with
        static member Create(calls:RtCall seq, arrows:RtArrowBetweenCalls seq, optFlow:RtFlow option) =
            let calls = calls |> toList
            let arrows = arrows |> toList
            RtWork(calls, arrows, optFlow)
            |> tee (fun z ->
                calls   |> iter (fun y -> y.RawParent <- Some z)
                arrows  |> iter (fun y -> y.RawParent <- Some z)
                optFlow |> iter (fun y -> y.RawParent <- Some z) )

    type RtCall with
        static member Create(callType:DbCallType, apiCalls:RtApiCall seq) =
            let apiCalls = apiCalls |> toList
            RtCall(callType, apiCalls)
            |> tee (fun z ->
                apiCalls |> iter (fun y -> y.RawParent <- Some z) )



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

    type Unique with
        member x.EnumerateDsObjects(?includeMe): Unique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? RtProject as prj ->
                    yield! prj.Systems >>= _.EnumerateDsObjects()
                | :? RtSystem as sys ->
                    yield! sys.Works   >>= _.EnumerateDsObjects()
                    yield! sys.Flows   >>= _.EnumerateDsObjects()
                    yield! sys.Arrows  >>= _.EnumerateDsObjects()
                | :? RtWork as work ->
                    yield! work.Calls  >>= _.EnumerateDsObjects()
                    yield! work.Arrows >>= _.EnumerateDsObjects()
                | :? RtCall as call ->
                    ()
                | _ ->
                    tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                    ()
            } |> List.ofSeq

        member x.EnumerateAncestors(?includeMe): Unique list = [
            let includeMe = includeMe |? true
            if includeMe then
                yield x
            match x.RawParent with
            | Some parent ->
                yield! parent.EnumerateAncestors()
            | None -> ()
        ]

        /// DS object 의 모든 상위 DS object 의 DateTime 을 갱신.  (tree 구조를 따라가면서 갱신)
        member x.UpdateDateTime(?dateTime:DateTime) =
            let dateTime = dateTime |?? now
            x.EnumerateDsObjects() |> iter (fun z -> z.DateTime <- dateTime)


        member x.Validate() =
            verify (x.Guid <> emptyGuid)
            verify (x.DateTime <> minDate)
            match x with
            | :? RtProject | :? RtSystem | :? RtFlow  | :? RtWork  | :? RtCall -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? RtProject as prj ->
                prj.Systems |> iter _.Validate()
                for s in prj.Systems do
                    verify (s.RawParent.Value.Guid = prj.Guid)
            | :? RtSystem as sys ->
                sys.Works |> iter _.Validate()
                for w in sys.Works  do
                    verify (w.RawParent.Value.Guid = sys.Guid)

                sys.Arrows |> iter _.Validate()
                for a in sys.Arrows do
                    verify (a.RawParent.Value.Guid = sys.Guid)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

            | :? RtFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate()
                for w in works  do
                    verify (w.OptFlow = Some flow)


            | :? RtWork as work ->
                work.Calls |> iter _.Validate()
                for c in work.Calls do
                    verify (c.RawParent.Value.Guid = work.Guid)

                work.Arrows |> iter _.Validate()
                for a in work.Arrows do
                    verify (a.RawParent.Value.Guid = work.Guid)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? RtCall as call ->
                ()
            | _ ->
                tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                ()



