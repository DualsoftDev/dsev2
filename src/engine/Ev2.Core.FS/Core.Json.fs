namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base.FS

(*
 * 코드 없이(최소한의 코드로) Newtonsoft.Json 을 이용해서 serialize/deserialize 하는 것이 목적.
 * 하부의 다른 class 가 추가되더라도 수정 최소화 할 수 있게 설계.
 *
 * Graph<'V, 'E> 의 JSON serialize 가 복잡하므로, GraphDTO 형태를 경유해서 serialize/deserialize 수행한다.
 *
 * AAS 관련 serialize/deserialize 는 형태의 변형이 많이 필요하므로 별도로 구현.  see Ev2.Aas.FS project
 *)

[<AutoOpen>]
module CoreJson =
    type DsSystem with
        member x.ToJson(): string =
            x.PrepareToJson()
            EmJson.ToJson(x)

        static member FromJson(json:string): DsSystem =
            let system = JsonConvert.DeserializeObject<DsSystem>(json)
            system.PrepareFromJson()
            system


    type DsSystem with
        member internal x.PrepareToJson() = x.Flows.Iter(_.PrepareToJson())
        member internal x.PrepareFromJson() = x.Flows.Iter(_.PrepareFromJson(x))

    type DsFlow with
        /// Graph -> Json DTO
        member (*internal*) x.PrepareToJson() =
            x.Works.Iter(_.PrepareToJson())
            x.Edges <- EdgeDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareFromJson(system:DsSystem) =
            x.System <- system
            let g = x.Graph
            x.Works.Iter(_.PrepareFromJson(x))
            x.Vertices |> x.AddVertices |> ignore
            x.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType)|> ignore)

    type DsWork with
        /// Graph -> Json DTO
        member (*internal*) x.PrepareToJson() = x.Edges <- EdgeDTO.FromGraph(x.Graph)

        /// Json DTO -> Graph
        member internal x.PrepareFromJson(parentFlow:DsFlow) =
            x.Container <- parentFlow
            let contents = x.Vertices.Map(_.Content)
            let g = x.Graph
            contents.Iter(fun c -> c.Container <- x)
            x.Vertices |> g.AddVertices |> ignore
            x.Edges.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType) |> ignore)
