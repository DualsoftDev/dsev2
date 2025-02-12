namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System

// 동일 module 내에 있어야 확장을 C# 에서 볼 수 있음.
[<AutoOpen>]
module CoreCreate =

    type DsSystem with  // Create, CreateFlow,
        static member Create(name:string) = new DsSystem(name)
        member x.CreateFlow(flowName:string) =
            if x.Flows.Exists(fun f -> (f :> INamed).Name = flowName) then
                getNull<DsFlow>();
            else
                DsFlow(x, flowName).Tee(fun f -> x.Flows.Add f)

    // 편의상, DsFlow 의 확장으로 정의하긴 하지만, 실제 Work 가 추가되는 곳은 DsSystem.Works 이다.
    type DsFlow with  // AddWork, AddVertex
        member x.AddWork(workName:string): DsWork * GuidVertex =
            if x.System.Works.Exists(fun w -> w.Name = workName) then
                failwith $"Duplicated work name [{workName}]"
            else
                let w = DsWork(x.System, x, workName)
                let v = x.AddVertex(w)
                w, v

        member x.AddVertex(work:DsWork): GuidVertex =
            x.System.Works.Add work
            x.System.AddVertexBase work

    type DsItemWithGraph with   // BasePrepareFromJson, BasePrepareToJson, AddVertexBase, AddVertices, CreateEdge

        member x.BasePrepareFromJson() =
            let g = x.Graph
            //for v in x.VertexDTOs do
            //    let content = g.FindVertex(v.Guid.ToString())
            //    if isItNull(content) then
            //        assert(false)
            //        let vv = GuidVertex(v.Name, content, v.Guid)
            //        let rr = g.AddVertex vv
            //        let xxx = rr
            //        ()

            x.EdgeDTOs.Iter(fun e -> g.CreateEdge(e.Source, e.Target, e.EdgeType)|> ignore)

        member x.BasePrepareToJson() =
            x.EdgeDTOs <- EdgeDTO.FromGraph(x.Graph)
            x.VertexDTOs <- VertexDTO.FromGraph(x.Graph)


        member internal x.AddVertexBase(vertex:GuidVertex): bool =
            match x, vertex.Content with
            | (:? DsSystem, :? DsWork)          // System 에 Work 추가.
            | (:? DsWork, :? DsAction) ->       // Work 에 Action 추가.
                x.Graph.AddVertex vertex
            | _ ->
                failwith "ERROR"

        member internal x.AddVertexBase(dsItem:DsItem): GuidVertex =
            match x, dsItem with
            | (:? DsSystem, :? DsWork)
            | (:? DsWork, :? DsAction) ->
                GuidVertex(dsItem)
                |> tee( x.AddVertexBase >> ignore)
            | _ ->
                failwith "ERROR"

        member x.AddVertices(vertices:GuidVertex seq) =
            let allOk = vertices.ForAll(fun v ->
                match x, v.Content with
                | (:? DsSystem, :? DsWork)
                | (:? DsWork, :? DsAction) -> true
                | _ ->
                    false
            )
            if allOk then
                vertices |> iter (x.AddVertexBase >> ignore)
            else
                failwith "ERROR"


        /// Flow 내에서 edge 생성
        member x.CreateEdge(src:GuidVertex, dst:GuidVertex, edgeType:CausalEdgeType): GuidEdge = x.Graph.CreateEdge(src.Guid, dst.Guid, edgeType)
        /// Flow 내에서 edge 생성
        member x.CreateEdge(src:Guid, dst:Guid, edgeType:CausalEdgeType): GuidEdge = x.CreateEdge(src, dst, edgeType)

    type DsWork with    // AddAction, AddVertex
        member x.AddAction(actionName:string): DsAction * GuidVertex =
            if x.Actions.Exists(fun w -> w.Name = actionName) then
                failwith $"Duplicated action name [{actionName}]"

            let a = DsAction(actionName, x).Tee(fun w -> x.Actions.Add w)
            x.Actions.Add a
            let v = x.AddVertex(a)
            a, v

        member x.AddVertex(action:DsAction): GuidVertex =
            x.AddVertexBase(action)
            |> tee (fun _gv -> x.Actions.Add action)

        [<JsonIgnore>] member x.System = x.Container :?> Core.DsSystem

(*
 * Graph 구조의 Json serialize 는 직접 수행하지 않는다.
 * 내부 Graph 구조를 Vertex 와 Edge 로 나누어, 다음 속성을 경유해서 JSON serialize 수행한다.
    - Vertex 는 {Flow, Work}.Vertices 에 (VertexDetailObsolete type)
    - Edge 는 {Flow, Work}.Edges 에 (EdgeDTO type)
 *)
[<AutoOpen>]
module CoreGraph =

    type GraphExtension =
        /// Graph 상에 인과 edge 생성
        [<Extension>]
        static member CreateEdge(graph:DsItemWithGraph, src:GuidVertex, dst:GuidVertex, edgeType:CausalEdgeType): GuidEdge =
            GuidEdge(src, dst, edgeType)
            |> tee(fun e ->
                graph.Graph.AddEdge(e) |> verifyM $"Duplicated edge [{src.Name}{edgeType}{dst.Name}]" )

        /// Graph 상에 인과 edge 생성
        [<Extension>]
        static member CreateEdge(graph:DsGraph, src:Guid, dst:Guid, edgeType:CausalEdgeType): GuidEdge =
            let s, e = graph.FindVertex(src.ToString()), graph.FindVertex(dst.ToString())
            GuidEdge(s, e, edgeType) |> tee(fun e -> graph.AddEdge e |> ignore)
        [<Extension>]
        static member CreateEdge(graph:DsItemWithGraph, src:Guid, dst:Guid, edgeType:CausalEdgeType): GuidEdge = graph.Graph.CreateEdge(src, dst, edgeType)


        //[<Extension>] static member HasVertexWithName(graph:DsGraphObsolete, name:string) = graph.Vertices.Any(fun v -> v.Name = name)


        /// fqdnObj 기준 상위로 System 찾기
        [<Extension>]
        static member GetSystem(fqdnObj:DsItem):DsSystem =
            match fqdnObj with
            | :? DsSystem   as s -> s
            | :? DsWork   as w -> w.System

            | :? DsFlow
            | :? DsAction
            | :? DsCommand
            | :? DsOperator ->
                fqdnObj.Container.GetFlow().System

            | _ -> failwith "ERROR"

        /// fqdnObj 기준 상위로 Flow 찾기
        [<Extension>]
        static member GetFlow(dsItem:DsItem):DsFlow =
            match dsItem with
            | :? DsFlow     as f -> f
            | :? DsWork     as w -> w.Flow

            | :? DsAction
            | :? DsCommand
            | :? DsOperator -> (dsItem.Container :?> DsWork).Flow
            | _ -> failwith "ERROR"

        /// fqdnObj 기준 상위로 Work 찾기
        [<Extension>]
        static member GetWork(dsItem:DsItem):DsWork =
            match dsItem with
            | :? DsWork     as w -> w
            | :? DsAction   as a -> a.Work
            //| :? DsCommand  as c -> c.Container.OptWork
            //| :? DsOperator as o -> o.Container.OptWork
            | _ -> getNull<DsWork>()

        /// System 이름부터 시작하는 FQDN
        [<Extension>]
        static member Fqdn(fqdnObj:DsItem) =
            match fqdnObj with
            | :? DsSystem   as s -> s.Name
            | :? DsFlow     as f -> $"{f.System.Name}.{f.Name}"
            | :? DsWork     as w -> $"{w.Flow.System.Name}.{w.Flow.Name}.{w.Name}"
            | :? DsAction   as a -> $"{a.Container.Fqdn()}.{a.Name}"
            | :? DsCommand  as c -> $"{c.Container.Fqdn()}.{c.Name}"
            | :? DsOperator as o -> $"{o.Container.Fqdn()}.{o.Name}"
            | _ -> failwith "ERROR"


        //// todo : need fix

        ///// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        /////
        ///// e.g : system1.TryFindLqdnObj(["flow1"; "work1"; "call1"]) === call1
        //[<Extension>]
        //static member TryFindLqdnObj(fqdnObj:GuidObject, lqdn:string seq) =
        //    match tryHeadAndTail lqdn with
        //    | Some (h, t) ->
        //        match fqdnObj with
        //        | :? DsSystem   as s -> s.Flows.TryFind(fun f -> f.Name = h).Bind(_.TryFindLqdnObj(t))
        //        | :? DsFlow     as f -> f.Graph.Vertices.Map(_.Content).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
        //        | :? DsWork     as w -> w.Graph.Vertices.Map(_.Content).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
        //        | _ -> failwith "ERROR"
        //    | None ->
        //        Some fqdnObj




        ///// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        /////
        ///// e.g : system1.TryFindLqdnObj("flow1.work1.call1") === call1
        //[<Extension>] static member TryFindLqdnObj(fqdnObj:GuidObject, lqdn:string) = fqdnObj.TryFindLqdnObj(lqdn.Split([|'.'|]))








