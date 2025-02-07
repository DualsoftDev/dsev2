namespace rec Dual.Ev2

open System.Linq
open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System.Xml.Serialization
open System


[<AutoOpen>]
module Core =
    /// DS system
    type DsSystem(name:string) =
        inherit NamedGuidObject(name)
        interface ISystem
        [<JsonProperty(Order = 2)>] member val Flows = ResizeArray<DsFlow>() with get, set

    /// DS flow
    type DsFlow(system:DsSystem, name:string) =
        inherit NamedGuidObject(name)
        interface IFlow

        [<JsonIgnore>] member val System = system with get, set
        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 3)>] member val Vertices = ResizeArray<VertexDetail>() with get, set
        [<JsonProperty(Order = 4)>] member val Edges:EdgeDTO[] = [||] with get, set

    /// DS work
    type DsWork(flow:DsFlow, name:string) =
        inherit Vertex(name, VCFlow flow)
        interface IWork

        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 2)>] member val Vertices = ResizeArray<VertexDetail>() with get, set
        [<JsonProperty(Order = 4)>] member val Edges:EdgeDTO[] = [||] with get, set


    /// DS coin.  base class for Ds{Action, AutoPre, Safety, Command, Operator}
    [<AbstractClass>]
    type DsCoin(name:string) =
        inherit Vertex(name)

    /// DS action.  외부 device 호출
    type DsAction(name:string) =
        inherit DsCoin(name)
        member val IsDisabled = false with get, set
        member val IsPush = false with get, set

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

    /// flow, work 의 graph 기능 공통 구현
    type internal IGraph with
        /// Flow 혹은 Work 의 Graph 반환
        member x.GetGraph(): DsGraph =
            match x with
            | :? DsFlow as f -> f.Graph
            | :? DsWork as w -> w.Graph
            | _ -> failwith "ERROR"

        /// Flow 혹은 Work 의 vertex 반환
        member x.GetVertexDetails(): ResizeArray<VertexDetail> =
            match x with
            | :? DsFlow as f -> f.Vertices
            | :? DsWork as w -> w.Vertices
            | _ -> failwith "ERROR"

        /// Flow 혹은 Work 의 edges 반환
        member x.GetEdgeDTOs(): EdgeDTO[] =
            match x with
            | :? DsFlow as f -> f.Edges
            | :? DsWork as w -> w.Edges
            | _ -> failwith "ERROR"

        /// Flow 혹은 Work 의 VertexContainer wrapper 반환
        member x.GetVertexContainer(): VertexContainer =
            match x with
            | :? DsFlow as f -> VCFlow f
            | :? DsWork as w -> VCWork w
            | _ -> VCNone

        /// Flow 혹은 Work 의 graph 에 vertex 추가
        member x.AddVertex<'V when 'V :> Vertex>(vertex:'V) =
            if vertex.Container <> VCNone then
                failwith "ERROR: Vertex already has parent container"

            if !! x.GetGraph().AddVertex(vertex) then
                failwith "ERROR: Failed to add.  duplicated?"

            VertexDetail.FromVertex(vertex) |> x.GetVertexDetails().Add
            vertex.Container <- x.GetVertexContainer()
            vertex


    type DsSystem with
        static member Create(name:string) = new DsSystem(name)
        member x.CreateFlow(flowName:string) =
            if x.Flows.Exists(fun f -> (f :> INamed).Name = flowName) then
                getNull<DsFlow>();
            else
                DsFlow(x, flowName).Tee(fun f -> x.Flows.Add f)

    type DsFlow with
        [<JsonIgnore>] member x.Works = x.Vertices.Map(_.AsVertex()).OfType<DsWork>().ToArray()

        member x.CreateWork(workName:string) =
            if x.Vertices.Exists(fun w -> w.AsVertex().Name = workName) then
                getNull<DsWork>();
            else
                let w = DsWork(x, workName)
                x.Vertices.Add (Work w)
                x.Graph.AddVertex w |> ignore
                w

        member x.AddVertex<'V when 'V :> Vertex>(vertex:'V) = (x :> IGraph).AddVertex vertex

        /// Flow 내에서 edge 생성
        member x.CreateEdge(src:Vertex, dst:Vertex, edgeType:CausalEdgeType): Edge = x.Graph.CreateEdge(src, dst, edgeType)
        /// Flow 내에서 edge 생성
        member x.CreateEdge(src:string, dst:string, edgeType:CausalEdgeType): Edge = x.Graph.CreateEdge(src, dst, edgeType)

    type DsWork with
        [<JsonIgnore>] member x.Flow = match x.Container with | VCFlow f -> f | _ -> getNull<DsFlow>()

        member x.AddVertex<'V when 'V :> Vertex>(vertex:'V) = (x :> IGraph).AddVertex vertex

        /// Work 내에서 edge 생성
        member x.CreateEdge(src:Vertex, dst:Vertex, edgeType:CausalEdgeType): Edge = x.Graph.CreateEdge(src, dst, edgeType)
        /// Work 내에서 edge 생성
        member x.CreateEdge(src:string, dst:string, edgeType:CausalEdgeType): Edge = x.Graph.CreateEdge(src, dst, edgeType)


module 새버젼=

    type FlowGraph = TGraph<VWork, VwEdge>

    /// Work(=> VWork) 간 연결 edge
    type VwEdge internal (source:VWork, target:VWork, edgeType:CausalEdgeType) =
        inherit EdgeBase<VWork>(source, target, edgeType)
        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"

    /// Work wrapper vertex
    type VWork(name: string, ?work:DsWork) =
        inherit GuidVertex(name)

        member val Work = work |? getNull<DsWork>() with get, set


    type WorkGraph = TGraph<VAction, VaEdge>

    /// Action(=> VAction) 간 연결 edge
    type VaEdge internal (source:VAction, target:VAction, edgeType:CausalEdgeType) =
        inherit EdgeBase<VAction>(source, target, edgeType)
        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"

    /// Work wrapper vertex
    type VAction(name: string, ?action:DsAction) =
        inherit GuidVertex(name)

        member val Action = action |? getNull<DsAction>() with get, set


(*
 * Graph 구조의 Json serialize 는 직접 수행하지 않는다.
 * 내부 Graph 구조를 Vertex 와 Edge 로 나누어, 다음 속성을 경유해서 JSON serialize 수행한다.
    - Vertex 는 {Flow, Work}.Vertices 에 (VertexDetail type)
    - Edge 는 {Flow, Work}.Edges 에 (EdgeDTO type)
 *)
[<AutoOpen>]
module CoreGraph =

    type DsGraph = TGraph<Vertex, Edge>


    type Edge internal (source:Vertex, target:Vertex, edgeType:CausalEdgeType) =
        inherit EdgeBase<Vertex>(source, target, edgeType)
        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"



    /// Edge 구조 serialization 용도.
    type EdgeDTO(source:string, target:string, edgeType:CausalEdgeType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val EdgeType = edgeType with get, set
        static member FromGraph(graph:DsGraph) =
            let es = graph.Edges.Map(fun e -> EdgeDTO(e.Source.Name, e.Target.Name, e.EdgeType)).ToArray()
            es

    /// Vertex 의 parent 구분용 type
    type VertexContainer =
        | VCNone
        | VCFlow of DsFlow
        | VCWork of DsWork
        with
            /// VertexContainer Union type 의 내부 알맹이 공통 구조인 DsNamedObject 를 반환
            member x.AsNamedObject():NamedGuidObject =
                match x with
                | VCFlow f -> f :> NamedGuidObject
                | VCWork w -> w :> NamedGuidObject
                | _ -> failwith "ERROR"

            /// VertexContainer 의 상위 System
            member x.System:DsSystem =
                match x with
                | VCFlow f -> f.System
                | VCWork w -> w.Flow.System
                | _ -> failwith "ERROR"

            /// VertexContainer 의 상위 Flow
            member x.Flow:DsFlow=
                match x with
                | VCFlow f -> f
                | VCWork w -> w.Flow
                | _ -> failwith "ERROR"

            /// VertexContainer 의 상위 Work
            member x.OptWork:DsWork option =
                match x with
                | VCWork w -> Some w
                | _ -> None


    /// Vertex 의 Polymorphic types.  Json serialize 시의 type 구분용으로도 사용된다. (e.g "Case": "Action", "Fields": [...])
    type VertexDetail =
        | Work     of DsWork
        | Action   of DsAction
        | AutoPre  of DsAutoPre
        | Safety   of DsSafety
        | Command  of DsCommand
        | Operator of DsOperator
        with
            /// VertexDetail Union type 의 내부 알맹이 공통 구조인 vertex 를 반환
            member x.AsVertex():Vertex =
                match x with
                | Work     y -> y :> Vertex
                | Action   y -> y :> Vertex
                | AutoPre  y -> y :> Vertex
                | Safety   y -> y :> Vertex
                | Command  y -> y :> Vertex
                | Operator y -> y :> Vertex

            /// vertex subclass 로부터 VertexDetail Union type 생성 반환
            static member FromVertex(v:Vertex) =
                match v with
                | :? DsWork     as y -> Work     y
                | :? DsAction   as y -> Action   y
                | :? DsAutoPre  as y -> AutoPre  y
                | :? DsSafety   as y -> Safety   y
                | :? DsCommand  as y -> Command  y
                | :? DsOperator as y -> Operator y
                | _ -> failwith "ERROR"

            member x.Case:string =
                match x with
                | Work     _ -> "Work"
                | Action   _ -> "Action"
                | AutoPre  _ -> "AutoPre"
                | Safety   _ -> "Safety"
                | Command  _ -> "Command"
                | Operator _ -> "Operator"


    /// INamedVertex를 구현한 Vertex 추상 클래스
    [<AbstractClass>]
    type Vertex(name: string, ?container:VertexContainer) =
        inherit NamedGuidObject(name)
        interface INamedVertex
        interface IVertexKey with
            member x.VertexKey with get() = x.Name and set(v) = x.Name <- v

        [<JsonIgnore>] member val Container = container |? VCNone with get, set


    type GraphExtension =
        /// Graph 상에 인과 edge 생성
        [<Extension>]
        static member CreateEdge(graph:DsGraph, src:Vertex, dst:Vertex, edgeType:CausalEdgeType): Edge =
            Edge(src, dst, edgeType)
            |> tee(fun e ->
                graph.AddEdge(e) |> verifyM $"Duplicated edge [{src.Name}{edgeType}{dst.Name}]" )

        /// Graph 상에 인과 edge 생성
        [<Extension>]
        static member CreateEdge(graph:DsGraph, src:string, dst:string, edgeType:CausalEdgeType): Edge =
            let s, e = graph.FindVertex(src), graph.FindVertex(dst)
            graph.CreateEdge(s, e, edgeType)


        [<Extension>] static member HasVertexWithName(graph:DsGraph, name:string) = graph.Vertices.Any(fun v -> v.Name = name)


        /// fqdnObj 기준 상위로 System 찾기
        [<Extension>]
        static member GetSystem(fqdnObj:NamedGuidObject):DsSystem =
            match fqdnObj with
            | :? DsSystem   as s -> s
            | :? DsFlow     as f -> f.System
            | :? DsWork     as w -> w.Flow.System
            | :? DsAction   as a -> a.Container.System
            | :? DsCommand  as c -> c.Container.System
            | :? DsOperator as o -> o.Container.System
            | _ -> failwith "ERROR"

        /// fqdnObj 기준 상위로 Flow 찾기
        [<Extension>]
        static member GetFlow(fqdnObj:NamedGuidObject):DsFlow =
            match fqdnObj with
            | :? DsFlow     as f -> f
            | :? DsWork     as w -> w.Flow
            | :? DsAction   as a -> a.Container.Flow
            | :? DsCommand  as c -> c.Container.Flow
            | :? DsOperator as o -> o.Container.Flow
            | _ -> failwith "ERROR"

        /// fqdnObj 기준 상위로 Work 찾기
        [<Extension>]
        static member TryGetWork(fqdnObj:NamedGuidObject):DsWork option =
            match fqdnObj with
            | :? DsWork     as w -> Some w
            | :? DsAction   as a -> a.Container.OptWork
            | :? DsCommand  as c -> c.Container.OptWork
            | :? DsOperator as o -> o.Container.OptWork
            | _ -> None

        /// System 이름부터 시작하는 FQDN
        [<Extension>]
        static member Fqdn(fqdnObj:NamedGuidObject) =
            match fqdnObj with
            | :? DsSystem   as s -> s.Name
            | :? DsFlow     as f -> $"{f.System.Name}.{f.Name}"
            | :? DsWork     as w -> $"{w.Flow.System.Name}.{w.Flow.Name}.{w.Name}"
            | :? DsAction   as a -> $"{a.Container.AsNamedObject().Fqdn()}.{a.Name}"
            | :? DsCommand  as c -> $"{c.Container.AsNamedObject().Fqdn()}.{c.Name}"
            | :? DsOperator as o -> $"{o.Container.AsNamedObject().Fqdn()}.{o.Name}"
            | _ -> failwith "ERROR"


        /// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        ///
        /// e.g : system1.TryFindLqdnObj(["flow1"; "work1"; "call1"]) === call1
        [<Extension>]
        static member TryFindLqdnObj(fqdnObj:NamedGuidObject, lqdn:string seq) =
            match tryHeadAndTail lqdn with
            | Some (h, t) ->
                match fqdnObj with
                | :? DsSystem   as s -> s.Flows.TryFind(fun f -> f.Name = h).Bind(_.TryFindLqdnObj(t))
                | :? DsFlow     as f -> f.Vertices.Map(_.AsVertex()).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
                | :? DsWork     as w -> w.Vertices.Map(_.AsVertex()).TryFind(fun v -> v.Name = h).Bind(_.TryFindLqdnObj(t))
                | _ -> failwith "ERROR"
            | None ->
                Some fqdnObj

        /// 자신의 child 이름부터 시작하는 LQDN(Locally Qualified Name) 을 갖는 object 반환
        ///
        /// e.g : system1.TryFindLqdnObj("flow1.work1.call1") === call1
        [<Extension>] static member TryFindLqdnObj(fqdnObj:NamedGuidObject, lqdn:string) = fqdnObj.TryFindLqdnObj(lqdn.Split([|'.'|]))








