namespace Ev2.Core.FS


open Dual.Common.Core.FS
open System

[<AutoOpen>]
module DsObjectUtilsModule =
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
            | :? DsProject as prj -> ProjectParameter() :> IParameter
            | :? DsSystem as sys -> SystemParameter()
            | _ -> failwith $"GetParameter not implemented for {x.GetType()}"


    type IArrow with
        member x.GetSource(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Source
            | :? ArrowBetweenWorks as a -> a.Source
            | _ -> failwith "ERROR"
        member x.GetTarget(): Unique =
            match x with
            | :? ArrowBetweenCalls as a -> a.Target
            | :? ArrowBetweenWorks as a -> a.Target
            | _ -> failwith "ERROR"

    type Unique with
        member x.EnumerateDsObjects(?includeMe): Unique list =
            seq {
                let includeMe = includeMe |? true
                if includeMe then
                    yield x
                match x with
                | :? DsProject as prj ->
                    yield! prj.Systems >>= _.EnumerateDsObjects()
                | :? DsSystem as sys ->
                    yield! sys.Works   >>= _.EnumerateDsObjects()
                    yield! sys.Flows   >>= _.EnumerateDsObjects()
                    yield! sys.Arrows  >>= _.EnumerateDsObjects()
                | :? DsWork as work ->
                    yield! work.Calls  >>= _.EnumerateDsObjects()
                    yield! work.Arrows >>= _.EnumerateDsObjects()
                | :? DsCall as call ->
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

        member x.UpdateDateTimeUpward(?dateTime:DateTime) =
            let dateTime = dateTime |?? now
            x.EnumerateDsObjects() |> iter (fun z -> z.DateTime <- dateTime)


        member x.Validate() =
            verify (x.Guid <> emptyGuid)
            verify (x.DateTime <> minDate)
            match x with
            | :? DsProject | :? DsSystem | :? DsFlow  | :? DsWork  | :? DsCall -> verify (x.Name.NonNullAny())
            | _ -> ()

            match x with
            | :? DsProject as prj ->
                prj.Systems |> iter _.Validate()
                for s in prj.Systems do
                    verify (s.RawParent.Value.Guid = prj.Guid)
            | :? DsSystem as sys ->
                sys.Works |> iter _.Validate()
                for w in sys.Works  do
                    verify (w.RawParent.Value.Guid = sys.Guid)

                sys.Arrows |> iter _.Validate()
                for a in sys.Arrows do
                    verify (a.RawParent.Value.Guid = sys.Guid)
                    sys.Works |> contains a.Source |> verify
                    sys.Works |> contains a.Target |> verify

            | :? DsFlow as flow ->
                let works = flow.Works
                works |> iter _.Validate()
                for w in works  do
                    verify (w.OptFlow = Some flow)


            | :? DsWork as work ->
                work.Calls |> iter _.Validate()
                for c in work.Calls do
                    verify (c.RawParent.Value.Guid = work.Guid)

                work.Arrows |> iter _.Validate()
                for a in work.Arrows do
                    verify (a.RawParent.Value.Guid = work.Guid)
                    work.Calls |> contains a.Source |> verify
                    work.Calls |> contains a.Target |> verify


            | :? DsCall as call ->
                ()
            | _ ->
                tracefn $"Skipping {(x.GetType())} in EnumerateDsObjects"
                ()



