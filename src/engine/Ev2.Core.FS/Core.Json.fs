namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base.CS
open Dual.Common.Core.FS



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
        member internal x.PrepareDeserialize() = x.Flows.Iter(_.PrepareDeserialize())

    type DsFlow with
        /// Graph -> Json DTO
        member internal x.PrepareSerialize() =
            x.Works.Iter(_.PrepareSerialize())
            x.GraphDTO <- GraphDTO.FromGraph(x.GetGraph())

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize() =
            let g = x.GetGraph()
            x.Works.Iter(_.PrepareDeserialize())

            let vs =
                let coins = x.Coins |> Seq.cast<Vertex>
                let works = x.Works |> Seq.cast<Vertex>
                (coins @ works).ToArray()
            g.AddVertices vs |> ignore

            if !! x.GraphDTO.Vertices.SetEqual(g.Vertices.Map(_.Name)) then
                failwith "ERROR: mismatch"
            
            x.GraphDTO.Edges.Map(fun e -> x.CreateEdge(e.Source, e.Target, e.EdgeType)) |> g.AddEdges |> ignore

    type DsWork with
        /// Graph -> Json DTO
        member internal x.PrepareSerialize() = x.GraphDTO <- GraphDTO.FromGraph(x.GetGraph())

        /// Json DTO -> Graph
        member internal x.PrepareDeserialize() =
            let g = x.GetGraph()
            x.Coins |> Seq.cast<Vertex> |> g.AddVertices |> ignore
            if !! x.GraphDTO.Vertices.SetEqual(g.Vertices.Map(_.Name)) then
                failwith "ERROR: mismatch"
            x.GraphDTO.Edges.Map(fun e -> x.CreateEdge(e.Source, e.Target, e.EdgeType)) |> g.AddEdges |> ignore
