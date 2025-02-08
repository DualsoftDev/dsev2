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
            x.BasePrepareToJson()

        /// Json DTO -> Graph
        member internal x.PrepareFromJson(system:DsSystem) =
            x.System <- system
            let g = x.Graph
            x.Works.Iter(_.PrepareFromJson(x))
            for v in x.VertexDTOs do
                match x.Works.TryFind(fun a -> a.Guid = v.ContentGuid) with
                | Some w ->
                    let gv = GuidVertex(v.Name, w, v.Guid)
                    x.Graph.Vertices.Add gv |> ignore
                | None -> failwith $"Work not found for VertexDTO: {v}"


            x.BasePrepareFromJson()

    type DsWork with
        /// Graph -> Json DTO
        member (*internal*) x.PrepareToJson() = x.BasePrepareToJson()

        /// Json DTO -> Graph
        member internal x.PrepareFromJson(parentFlow:DsFlow) =
            x.Container <- parentFlow
            x.Actions.Iter (fun a -> a.Container <- x)
            for v in x.VertexDTOs do
                match x.Actions.TryFind(fun a -> a.Guid = v.ContentGuid) with
                | Some a ->
                    let gv = GuidVertex(v.Name, a, v.Guid)
                    x.Graph.Vertices.Add gv |> ignore
                | None -> failwith $"Action not found for VertexDTO: {v}"


            x.BasePrepareFromJson()

            ()
