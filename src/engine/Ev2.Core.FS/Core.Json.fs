namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base.CS
open Dual.Common.Core.FS

(*
 * Graph<'V, 'E> 의 JSON serialize 가 복잡하므로, GraphDTO 형태를 경유해서 serialize/deserialize 수행한다.
 *)

[<AutoOpen>]
module CoreJson =
    type IDsObject with
        member x.DefaultSerialize(): string =
            let settings = JsonSerializerSettings(ReferenceLoopHandling = ReferenceLoopHandling.Ignore)
            JsonConvert.SerializeObject(x, Formatting.Indented, settings);

    type DsSystem with
        member x.Serialize(): string =
            x.PrepareSerialize()
            x.DefaultSerialize()
        static member Deserialize(json:string): DsSystem =
            let system = JsonConvert.DeserializeObject<DsSystem>(json)
            system.PrepareDeserialize()
            system

    type DsSystem with
        member internal x.PrepareSerialize() = x.Flows.Iter(_.PrepareSerialize())
        member internal x.PrepareDeserialize() = x.Flows.Iter(_.PrepareDeserialize(x))

    type DsFlow with
        /// Graph -> Json DTO
        member internal x.PrepareSerialize() =
            x.Works.Iter(_.PrepareSerialize())
            x.GraphDTO <- GraphDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize(system:DsSystem) =
            x.System <- system
            let g = x.Graph
            x.Works.Iter(_.PrepareDeserialize(x))

            let vs =
                let coins = x.Coins |> Seq.cast<Vertex>
                let works = x.Works |> Seq.cast<Vertex>
                (coins @ works).ToArray()
            g.AddVertices vs |> ignore

            if !! x.GraphDTO.Vertices.SetEqual(g.Vertices.Map(_.Name)) then
                failwith "ERROR: mismatch"
            
            x.GraphDTO.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType)|> ignore)

    type DsWork with
        /// Graph -> Json DTO
        member internal x.PrepareSerialize() = x.GraphDTO <- GraphDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize(flow:DsFlow) =
            x.Container <- Flow flow
            x.Flow <- flow
            x.Coins.Iter(fun c -> c.Container <- Work x)
            let g = x.Graph
            x.Coins |> Seq.cast<Vertex> |> g.AddVertices |> ignore
            if !! x.GraphDTO.Vertices.SetEqual(g.Vertices.Map(_.Name)) then
                failwith "ERROR: mismatch"
            x.GraphDTO.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType) |> ignore)
