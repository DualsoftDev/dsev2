namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System


[<AutoOpen>]
module Core =
    /// DS system
    type DsSystem(name:string) =
        inherit DsItem(name)
        interface ISystem
        [<JsonProperty(Order = 2)>] member val Flows = ResizeArray<DsFlow>() with get, set

    type DsItemWithGraph(name:string, ?container:DsItem) =
        inherit DsItem(name, ?container=container)
        interface IGraph
        //[<Obsolete("삭제대상")>] [<JsonIgnore>] member val Graph = DsGraphObsolete()
        [<JsonIgnore>] member val Graph = TGraph<GuidVertex, GuidEdge>()

        [<JsonIgnore>] member x.Vertices = x.Graph.Vertices
        [<JsonIgnore>] member x.Edges = x.Graph.Edges

        [<JsonProperty(Order = 3)>] member val VertexDTOs:VertexDTO[] = [||] with get, set
        [<JsonProperty(Order = 4)>] member val EdgeDTOs:EdgeDTO[] = [||] with get, set

    /// DS flow
    type DsFlow(system:DsSystem, name:string) =
        inherit DsItemWithGraph(name, container=system)
        interface IFlow

        [<JsonIgnore>]
        member x.System
            with get() = x.Container :?> DsSystem
            and set (v:DsSystem) = x.Container <- v
        member val Works = ResizeArray<DsWork>() with get, set

    /// DS work
    type DsWork(flow:DsFlow, name:string) =
        inherit DsItemWithGraph(name, container=flow)
        interface IWork
        //new(name) = DsWork(getNull<DsFlow>(), name)
        //new() = DsWork(getNull<DsFlow>(), null)

        [<JsonIgnore>]
        member x.Flow
            with get() = x.Container :?> DsFlow
            and set (v:DsFlow) = x.Container <- v
        member val Actions = ResizeArray<DsAction>() with get, set


    /// DS coin.  base class for Ds{Action, AutoPre, Safety, Command, Operator}
    [<AbstractClass>]
    type DsCoin(name:string, ?work:DsWork) =
        inherit DsItem(name, ?container=work.Cast<DsItem>())

    /// DS action.  외부 device 호출
    type DsAction(name:string, ?work:DsWork) =
        inherit DsCoin(name, ?work=work)
        new(name) = DsAction(name, getNull<DsWork>())   // for C#
        new() = DsAction(null, getNull<DsWork>())   // for JSON
        member val IsDisabled = false with get, set
        member val IsPush = false with get, set
        [<JsonIgnore>] member x.Work = x.Container :?> DsWork

    /// DS auto-pre.  자동 운전시에만 참조하는 조건
    type DsAutoPre(name:string) =
        inherit DsCoin(name)

    /// DS safety.  안전 인과 조건
    type DsSafety(name:string, safeties:string []) =
        inherit DsCoin(name)
        new(name) = DsSafety(name, [||])
        member val Safeties = safeties with get, set

    /// DS command
    type DsCommand(name:string) =
        inherit DsCoin(name)

    /// DS operator
    type DsOperator(name:string) =
        inherit DsCoin(name)








// 동일 module 내에 있어야 확장을 C# 에서 볼 수 있음.
//[<AutoOpen>]
//module CoreCreate =



    type DsSystem with
        static member Create(name:string) = new DsSystem(name)
        member x.CreateFlow(flowName:string) =
            if x.Flows.Exists(fun f -> (f :> INamed).Name = flowName) then
                getNull<DsFlow>();
            else
                DsFlow(x, flowName).Tee(fun f -> x.Flows.Add f)

    type DsFlow with    // AddWork, AddVertex, CreateEdge

        member x.AddWork(workName:string, ?returnNullOnError:bool): DsWork * GuidVertex =
            if x.Works.Exists(fun w -> w.Name = workName) then
                let returnNullOnError = returnNullOnError |? false
                if returnNullOnError then
                    getNull<DsWork>(), getNull<GuidVertex>();
                else
                    failwith $"Duplicated work name [{workName}]"
            else
                let w = DsWork(x, workName)
                x.Works.Add w
                let v = x.AddVertex(w)
                w, v

        member x.AddVertex(work:DsWork): GuidVertex = x.AddVertexBase(work)

    type DsItemWithGraph with

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
            | (:? DsFlow, :? DsWork)
            | (:? DsWork, :? DsAction) ->
                x.Graph.AddVertex vertex
            | _ ->
                failwith "ERROR"

        member internal x.AddVertexBase(dsItem:DsItem): GuidVertex =
            match x, dsItem with
            | (:? DsFlow, :? DsWork)
            | (:? DsWork, :? DsAction) ->
                GuidVertex(dsItem)
                |> tee( x.AddVertexBase >> ignore)
            | _ ->
                failwith "ERROR"

        member x.AddVertices(vertices:GuidVertex seq) =
            let allOk = vertices.ForAll(fun v ->
                match x, v.Content with
                | (:? DsFlow, :? DsWork)
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

    type DsWork with
        member x.AddAction(actionName:string, ?returnNullOnError:bool): DsAction =
            if x.Actions.Exists(fun w -> w.Name = actionName) then
                let returnNullOnError = returnNullOnError |? false
                if returnNullOnError then
                    getNull<DsAction>();
                else
                    failwith $"Duplicated action name [{actionName}]"
            else
                DsAction(actionName, x) |> tee(fun w -> x.Actions.Add w)

        member x.AddVertex(action:DsAction): GuidVertex =
            x.AddVertexBase(action)
            |> tee (fun _gv -> x.Actions.Add action)

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

            | :? DsFlow
            | :? DsWork
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


        /// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        ///
        /// e.g : system1.TryFindLqdnObj(["flow1"; "work1"; "call1"]) === call1
        [<Extension>]
        static member TryFindLqdnObj(fqdnObj:GuidObject, lqdn:string seq) =
            match tryHeadAndTail lqdn with
            | Some (h, t) ->
                match fqdnObj with
                | :? DsSystem   as s -> s.Flows.TryFind(fun f -> f.Name = h).Bind(_.TryFindLqdnObj(t))
                | :? DsFlow     as f -> f.Graph.Vertices.Map(_.Content).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
                | :? DsWork     as w -> w.Graph.Vertices.Map(_.Content).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
                | _ -> failwith "ERROR"
            | None ->
                Some fqdnObj

        /// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        ///
        /// e.g : system1.TryFindLqdnObj("flow1.work1.call1") === call1
        [<Extension>] static member TryFindLqdnObj(fqdnObj:GuidObject, lqdn:string) = fqdnObj.TryFindLqdnObj(lqdn.Split([|'.'|]))








