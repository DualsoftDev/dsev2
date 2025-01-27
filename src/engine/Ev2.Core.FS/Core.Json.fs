namespace rec Dual.Ev2

open Newtonsoft.Json

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
            x.Edges <- EdgeDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize(system:DsSystem) =
            x.System <- system
            let g = x.Graph
            x.Works.Iter(_.PrepareDeserialize(x))

            x.Vertices.Map(_.AsVertex()) |> g.AddVertices |> ignore            
            x.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType)|> ignore)

    type DsWork with
        /// Graph -> Json DTO
        member internal x.PrepareSerialize() = x.Edges <- EdgeDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize(parentFlow:DsFlow) =
            x.Container <- VCFlow parentFlow
            let vs = x.Vertices.Map(_.AsVertex())
            let g = x.Graph
            vs.Iter(fun c -> c.Container <- VCWork x)
            vs |> g.AddVertices |> ignore
            x.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType) |> ignore)
