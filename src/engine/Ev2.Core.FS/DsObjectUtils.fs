namespace Ev2.Core.FS


open Dual.Common.Core.FS

[<AutoOpen>]
module DsObjectUtilsModule =
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



