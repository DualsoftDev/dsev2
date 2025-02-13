namespace rec Dual.Ev2

open Newtonsoft.Json
open System.Linq

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
            x.WriteJsonProlog()
            EmJson.ToJson(x)

        static member FromJson(json:string): DsSystem =
            let system = EmJson.FromJson<DsSystem>(json)
            system.ReadJsonEpilog()
            system


    type DsSystem with
        /// Graph -> Json DTO
        member (*internal*) x.WriteJsonProlog() =
            x.BaseWriteJsonProlog()
            x.Works.Iter(_.WriteJsonProlog())
            //x.Flows.Iter(_.WriteJsonProlog(x))

        member (*internal*) x.ReadJsonEpilog() =

            let g = x.Graph
            x.Flows.Iter(_.ReadJsonEpilog(x))
            x.Works.Iter(_.ReadJsonEpilog(x))
            for v in x.VertexDTOs do
                match x.Works.TryFind(fun a -> a.Guid = v.ContentGuid) with
                | Some w ->
                    let gv = GuidVertex(w, v.Guid)
                    x.Graph.Vertices.Add gv |> ignore
                | None -> failwith $"Work not found for VertexDTO: {v}"

            x.BaseReadJsonEpilog()

            //x.Flows.Iter(_.ReadJsonEpilog(x))

    type DsFlow with
        /// Json DTO -> Graph
        member internal x.ReadJsonEpilog(system:DsSystem) =
            x.System <- system

    type DsWork with
        /// Graph -> Json DTO
        member (*internal*) x.WriteJsonProlog() = x.BaseWriteJsonProlog()

        /// Json DTO -> Graph
        member internal x.ReadJsonEpilog(system:DsSystem) =
            x.Container <- system

            /// 메모리가 두개 생기는 것을 방지하기 위해서
            /// System 의 Flows 목록은 JSON 에서 읽어서 생성
            /// Work 의 Flow 는 읽어들인 Guid 를 key 로 해서, 이미 만들어진 System.Flows 에서 찾아서 할당
            x.Flow <- system.Flows.First(fun f -> f.Guid = x.Flow.Guid)

            x.Actions.Iter (fun a -> a.Container <- x)
            for v in x.VertexDTOs do
                match x.Actions.TryFind(fun a -> a.Guid = v.ContentGuid) with
                | Some a ->
                    let gv = GuidVertex(a, v.Guid)
                    x.Graph.Vertices.Add gv |> ignore
                | None -> failwith $"Action not found for VertexDTO: {v}"


            x.BaseReadJsonEpilog()

            ()
