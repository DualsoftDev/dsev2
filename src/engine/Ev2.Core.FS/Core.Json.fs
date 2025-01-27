namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Core.FS
open System.Xml.Serialization
open System.IO

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

        // XML Serialization
        member x.SerializeToXml(): string =
            // System.InvalidOperationException: 'Dual.Ev2.Core.DsSystem cannot be serialized because it does not have a parameterless constructor.'
            (* XML serialize 위해 추기 필요한 요소
              1. parameterless constructor 추가
             *)
            let serializer = XmlSerializer(typeof<DsSystem>)
            use stringWriter = new StringWriter()
            serializer.Serialize(stringWriter, x)
            stringWriter.ToString()

        static member DeserializeFromXml(xml: string): DsSystem =
            let serializer = XmlSerializer(typeof<DsSystem>)
            use stringReader = new StringReader(xml)
            let system = serializer.Deserialize(stringReader) :?> DsSystem
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
