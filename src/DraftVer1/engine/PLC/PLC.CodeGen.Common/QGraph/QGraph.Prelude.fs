namespace Dual.Core.QGraph

open System.Runtime.CompilerServices
open System.Collections.Generic
open System.Diagnostics
open QuickGraph
open FSharpPlus
open Dual.Common
open Dual.Core
open Dual.Core.Types
open Dual.Core.Types.Command

[<AutoOpen>]
module Prelude =
    /// 수정 가능한 reset interface
    type IResetMutable =
        inherit IReset
        abstract member Reset:Expression with get, set


    type Coil(tag:string) =
        interface IExpressionTerminal with
            member x.ToText() = tag
        member x.ToText() = tag
    let toCoil tagName = Terminal(Coil(tagName))

    /// Relay를 위한 Tag
    type RelayTag(tag:string) =
        interface IExpressionTerminal with
            member x.ToText() = tag


    type DeviceId = int
    type OutputId = int
    /// 서로 다른 actuator 신호가 동일한 device 의 신호인지 구분하기 위한 사전 구조
    type OutputDeviceMap = Dictionary<string, DeviceId*OutputId>

    /// Relay 혹은 일반 memory(vertex) 의 On/Off condition.  see also MemoryOnOffSpec
    [<DebuggerDisplay("{ToText()}")>]
    type MemoryOnOffCondition =
        /// Node 의 시작 조건: node 의 모든 incoming edge 의 condition 을 AND.  유일의 인에 대한 ON 조건
        | NodeIncomingCondition of IVertex
        /// (edge.Source, 즉, 과 의 NodeRawFinishCondition) X (edge.Target, 즉, 과 다음 node 의 NodeIncomingCondition)
        ///
        /// 과의 reset 의 신뢰 조건으로 사용.  edge 는 과 와 다음 vertex 간의 edge
        | TrustedEdgeCondition of IEdge
        //? 제거 대상?
        /// Node v 의 순간 메모리 값
        | NodeRawFinishCondition of IVertex
        /// 신뢰 vertex : NodeIncomingCondition + NodeRawFinishCondition
        | TrustedNodeStartCondition of IVertex
        /// 상위 Task 리셋 컨디션
        | NodeResetCondition of Expression
        /// 리셋 블럭 컨디션 v의 메모리 값과 Prev Reset Block Relay
        | ResetBlockCondition of IVertex
        with
            member x.ToText() =
                match x with
                | NodeIncomingCondition(v) -> sprintf "NI(%A)" v
                | NodeRawFinishCondition(v) -> sprintf "NF(%A)" v
                | TrustedNodeStartCondition(v) -> sprintf "TNS(%A)" v
                | TrustedEdgeCondition(e) -> sprintf "TE(%A)" e
                | NodeResetCondition(expr) -> sprintf "NR(%A)" expr
                | ResetBlockCondition(v) -> sprintf "RBC(%A)" v

            member x.Name() =
                match x with
                | NodeIncomingCondition(v) -> v.Name
                | NodeRawFinishCondition(v) -> v.Name
                | TrustedNodeStartCondition(v) -> v.Name
                | TrustedEdgeCondition(e) -> sprintf "%A" e
                | NodeResetCondition(expr) -> sprintf "%A" expr
                | ResetBlockCondition(v) -> v.Name


            override x.ToString() = x.ToText()
            interface IExpressionTerminal with
                member x.ToText() = x.ToText()

    /// 신뢰 작업용 graph 의 vertex.  대략 Dual.Core.Circle 에 해당
    and
        [<DebuggerDisplay("{ToText()}")>]
        QgVertex(name, parent) =
            let name:string = name
            let mutable reset = Expression.Zero
            let preValues = Dictionary()
            let ports     = Dictionary()
            let userposts = ResizeArray()
            let conditions = ResizeArray()
            let interlocks = ResizeArray()
            let dags = ResizeArray()

            new(name) = QgVertex(name, None)

            member x.Name = name
            member x.ToText() = x.Name
            override x.ToString() = x.ToText()

            /// Memory reset 조건.  vertex 가 A+ 라면 A- 시작 시에 reset 된다.  NodeStartCondtion(A-)
            member x.Reset with get() = reset and set(v) = reset <- v
            member val PreProcessValues = preValues with get
            member val Ports            = ports :> IDictionary<PortCategory, IPort> with get, set
            member val UserPorts        = userposts with get, set
            member val DAGs             = dags with get, set
            member val Parent           = parent with get, set
            member val Condition        = conditions with get, set
            member val Interlock        = interlocks with get, set

            interface IVertex with
                member x.Parent           with get() = x.Parent and set(v) = x.Parent <- v
                member x.PreProcessValues = x.PreProcessValues :> IDictionary<string, obj>
                member x.Name             = name
                member x.ToText()         = x.ToText()
                member x.Ports            with get() = x.Ports      and set(v) = x.Ports        <- v
                member x.UserPorts        with get() = x.UserPorts  and set(v) = x.UserPorts    <- v
                member x.DAGs             with get() = x.DAGs   and set(v) = x.DAGs     <- v
                member x.Conditions        with get() = x.Condition  and set(v) = x.Condition    <- v
                member x.Interlocks        with get() = x.Interlock  and set(v) = x.Interlock    <- v
            interface IReset with
                member x.Reset = x.Reset
            interface IResetMutable with
                member x.Reset with get() = x.Reset and set(v) = x.Reset <- v


    and
        [<DebuggerDisplay("{ToText()}")>]
        QgEdge(src, tgt, originalEdge:IEdge option, ?edgetype0:EdgeType) =
            inherit Edge<IVertex>(src, tgt)

            let edgeType = defaultArg edgetype0 EdgeType.Sync

            new (src, tgt) = QgEdge(src, tgt, None)
            member x.OriginalEdge = originalEdge
            member x.ToText() = sprintf "%s -> %s" (src.ToText()) (tgt.ToText())
            member val EdgeType = edgeType with get ,set
            override x.Equals obj =
                match obj with
                | :? QgEdge as e -> e.Source = x.Source && e.Target = x.Target
                | _ -> false
            override x.GetHashCode() = x.Source.GetHashCode() ^^^ x.Target.GetHashCode()
            interface IEdge with
                member x.Source = x.Source
                member x.Target = x.Target
                member x.EdgeType = x.EdgeType
    and
        QgObject =
            | QgVertexObj of IVertex
            | QgEdgeObj of IEdge

    and
        [<DebuggerDisplay("{ToText()}")>]
        QgPort(parent, portType:PortCategory, tagType:TagType option, plcTag:PLCTag list, plcFuncs:IFunctionCommand ResizeArray) =
            new (parent, porttype) = QgPort(parent, porttype, None, [], ResizeArray())
            new (parent, porttype, tagType) = QgPort(parent, porttype, tagType, [], ResizeArray())
            member val ConnectedPorts = empty |> ResizeArray
            member val ConnectedExpression = Expression.Zero with get, set
            member val PortType = portType with get
            member val TagType = tagType with get, set
            member val PLCTags = plcTag |> ResizeArray with get, set
            member val Address = empty |> ResizeArray with get, set
            member val PLCFunctions = plcFuncs with get, set
            member val Parent = parent with get, set
            member val DummyTag = None with get, set
            member val EndTag = None with get, set


            interface IPort with
                member x.ConnectedPorts = x.ConnectedPorts
                member x.ConnectedExpression with get() = x.ConnectedExpression and set(e) = x.ConnectedExpression <- e
                member x.PortType = x.PortType
                member x.TagType  with get() = x.TagType  and set(t) = x.TagType  <- t
                member x.Address  with get() = x.Address  and set(a) = x.Address  <- a
                member x.PLCTags   with get() = x.PLCTags   and set(p) = x.PLCTags   <- p
                member x.DummyTag with get() = x.DummyTag and set(t) = x.DummyTag <- t
                member x.EndTag with get() = x.EndTag and set(t) = x.EndTag <- t
                member x.PLCFunctions with get() = x.PLCFunctions and set(v) = x.PLCFunctions <- v
                member x.Parent with get() = x.Parent and set(v) = x.Parent <- v

    and QgSystem(vertices:IVertex ResizeArray, edges:IEdge ResizeArray) =
        member val Vertices = vertices with get
        member val Edges = edges with get
        interface ISystem with
            member x.Vertices = x.Vertices
            member x.Edges = x.Edges

    and QgSegment(segmenttype:SegmentType, vertices:IVertex ResizeArray) =
        new(t) = QgSegment(t, empty |> ResizeArray)
        member val SegmentType = segmenttype with get, set
        member val Vertices = vertices with get
        interface ISegment with
            member x.SegmentType with get() = x.SegmentType and set(v) = x.SegmentType <- v
            member x.Vertices = x.Vertices

    and QgDAG(vertices:IVertex seq, edges:IEdge seq) =
        new(es) = QgDAG(es |> Seq.collect(fun (e:IEdge) -> [e.Source; e.Target;]) |> Seq.distinct, es)
        member val Vertices = vertices with get
        member val Edges = edges with get
        interface IDAG with
            member x.Vertices = x.Vertices
            member x.Edges = x.Edges

    and QgSelect(name, parent) =
        inherit QgVertex(name, parent)

        member val ConditionDAG = Dictionary() with get


        member x.AddConditionDAG(key:IFunctionCommand, dag:IDAG) =
            x.DAGs.Add(dag)
            x.ConditionDAG.Add(key, dag)

        interface ISelect with
            member x.ConditionDAG = x.ConditionDAG
            member x.AddConditionDAG(k, v) = x.AddConditionDAG(k, v)

    type FakeEdge (src, tgt) =
        inherit Edge<IVertex>(src, tgt)
        member val EdgeType = EdgeType.Sync with get, set
        interface IEdge with
            member x.Source = x.Source
            member x.Target = x.Target
            member x.EdgeType = x.EdgeType

    /// Graph 상의 Vertex 의 단일 경로
    type VPath = IVertex list
    /// Graph 상의 Vertex 의 다중 경로
    and VPaths = VPath list

    /// Graph 상의 edge 의 단일 경로
    and EPath = IEdge list
    /// Graph 상의 edge 의 다중 경로
    and EPaths = EPath list


    /// Directed Cycle Graph : (terminals -> initials) 간 fake edge 강제 연결
    type DCG = AdjacencyGraph<IVertex, IEdge>
    type DAG = AdjacencyGraph<IVertex, IEdge>

    type QgModel = {
        Title : string option

        /// Sensor vertex 들.  조건으로만 사용되고, 실제 출력을 생성하지 않는 vertex
        Sensors: string list

        /// Model reset 명령.  model reset 값이 살 때, 사용한 모든 relay 를 off 시켜서 전체 DAG 가 초기 상태로 설정.  (반전된 terminal relay 는 반대로)
        Reset: string option
        /// Model start 명령
        Start: string option

        /// 원본 모델
        DAG: DAG
        /// forward directed cyclic graph : DAG graph 에서 terminals 와 initials 를 fake edge 로 연결한 것
        DCG: DCG
    }

    /// default QgModel 을 생성
    let createDefaultModel() =
        let g = DAG()
        { DAG = g; DCG = g; Sensors=[]; Start = None; Reset = None; Title = None }

    /// 이름으로 주어진 vertex 의 On/Off condition.  see also MemoryOnOffCondition
    [<DebuggerDisplay("{ToText()}")>]
    type MemoryOnOffSpec =
        /// Node 의 시작 Spec
        | NodeIncomingSpec of string
        /// Node 를 빠져 나가는 Spec.
        | NodeOutgoingSpec of string
        /// Node v 의 순간 메모리 값
        | NodeRawFinishSpec of string
        with
            member x.ToText() =
                match x with
                | NodeIncomingSpec(v) -> sprintf "NI(%A)" v
                | NodeOutgoingSpec(v)  -> sprintf "NO(%A)" v
                | NodeRawFinishSpec(v) -> sprintf "NF(%A)" v

            override x.ToString() = x.ToText()
            interface IExpressionTerminal with
                member x.ToText() = x.ToText()

    [<Extension>] // type QgExt1 =
    type QgExt1 =
        /// edge e 의 시작과 끝 vertices 를 tuple 로 반환
        [<Extension>] static member ToVertexTuple(e:IEdge) = e.Source, e.Target

        /// edge e 의 반대 방향 edge 를 생성해서 반환
        [<Extension>]
        static member Revert (e:IEdge) (edgeCreator:EdgeCreator) =
            let src, dst = e.Source, e.Target
            match e with
            | :? FakeEdge -> FakeEdge(dst, src) :> IEdge
            | _ -> edgeCreator dst src

        [<Extension>]
        static member ToCoil(vertex:QgVertex) = toCoil vertex.Name

        /// 그래프 g 에서 주어진 name 을 가진 vertices 반환
        [<Extension>]
        static member FindNamedVertices(g:DAG, name:string) =
            g.Vertices |> Seq.filter(fun v -> v.Name = name)


        /// 그래프 객체(edge or vertex) 에서 source 반환
        [<Extension>]
        static member GetSource(qgObj:QgObject) =
            match qgObj with
            | QgVertexObj(v) -> v
            | QgEdgeObj(e)   -> e.Source

        /// 그래프 객체(edge or vertex) 에서 target 반환
        [<Extension>]
        static member GetTarget(qgObj:QgObject) =
            match qgObj with
            | QgVertexObj(v) -> v
            | QgEdgeObj(e)   -> e.Target
