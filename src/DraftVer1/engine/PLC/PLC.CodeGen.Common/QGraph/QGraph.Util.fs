namespace Dual.Core.QGraph

open QuickGraph
open FSharpPlus
open Dual.Common
open Dual.Common.Graph.QuickGraph
open Dual.Core.Types
open System.Collections.Generic
open Dual.Core.QGraph
open Dual.Core
open System.Runtime.CompilerServices
open Dual.Core.Types.Command


[<AutoOpen>]
module DcgModule =
    let private createFakeEdge (s:IVertex) (e:IVertex) =
        e.StartPort.ConnectedPorts.Add(s.SensorPort) |> ignore
        FakeEdge(s, e) :> IEdge

    /// DAG g 로부터 terminal vertices 와 initial vertices 간의 fake edge 로 연결한 DCG 생성
    ///
    /// initial vertices 가 sensors 로 주어진 이름 목록에 포함된 경우(e.g START),
    /// sensor vertex 대신 이것이 가리키는 다음 vertex 들을 intial vertices 로 간주한다.
    let makeDCG2 (g:DAG) (sensors:string seq) (fakeEdgeCreator:IVertex -> IVertex -> IEdge) =
        let sensors = sensors |> HashSet

        /// DAG 의 initial vertices
        let initials =
            /// g 에서 모든 (sensor --> v) edge 를 제외하고 만든 임시 graph
            let tmpg =
                let validEdges =
                    g.Edges
                    |> Seq.filter(fun e -> not (sensors.Contains(e.Source.Name)))
                validEdges.ToAdjacencyGraph()
            getInitialNodes tmpg

        let terminals = getTerminalNodes g
        let fakeEdges =
            [
                for i in initials do
                    for t in terminals do
                        fakeEdgeCreator t i
            ]
        g.Edges @@ fakeEdges
        |> GraphExtensions.ToAdjacencyGraph

    /// DAG g 로부터 terminal vertices 와 initial vertices 간의 fake edge 로 연결한 DCG 생성
    ///
    /// initial vertices 가 sensors 로 주어진 이름 목록에 포함된 경우(e.g START),
    /// sensor vertex 대신 이것이 가리키는 다음 vertex 들을 intial vertices 로 간주한다.
    let makeDCG (g:DAG) sensors =
        makeDCG2 g sensors createFakeEdge


    /// graph g 로부터 model 생성
    ///
    /// initial vertices 가 sensors 로 주어진 이름 목록에 포함된 경우(e.g START),
    /// sensor vertex 대신 이것이 가리키는 다음 vertex 들을 intial vertices 로 간주한다.
    let makeModel g sensors _heads _tails =
        let fdcg = makeDCG g sensors
        { createDefaultModel() with DAG = g; DCG = fdcg; Sensors=sensors; }

    let makeSimpleModel g =
        let dcg = makeDCG g []
        { createDefaultModel() with DAG = g; DCG = dcg; }

    let rec getAllVertices2 (v:IVertex) : seq<IVertex> =
        let child =
            v.DAGs
            |> Seq.collect(fun dag ->
                dag.Vertices |> Seq.collect getAllVertices2
            )
        [v] @@ child

    /// graph vertex들과 vertex child들까지 모두 가져온다.
    let getAllVertices (g:AdjacencyGraph<IVertex, IEdge>) =
        //let rec getchild (v:IVertex) =
        //    monad{
        //        let! child =
        //        v.Vertices
        //        |> Option.bind(fun vs -> vs |> Seq.map(fun v -> getchild v)
        //        |> Seq.choose id |> Seq.flatten |> Some)
        //        let! vertices = v.Vertices

        //        child |> Seq.append vertices
        //    }

        g.Vertices |> Seq.collect(fun v -> getAllVertices2 v)




    /// model에 해당하는 vertex를 가져온다.
    let getParentVertexOfModel (model:QgModel) =
        monad{
            let! v = model.DAG.Vertices |> Seq.tryHead
            let! parent = v.Parent
            parent
        }





[<AutoOpen>]
module QgUtilModule =
    /// vertex list 를 edge list 로 변환 : e.g [a; b; c] --> [ a->b; b->c; ]
    let vs2es (edgeCreator:EdgeCreator) (vs:IVertex seq) =
        vs
        |> Seq.pairwise
        |> Seq.map (fun (v1, v2) -> edgeCreator v1 v2)
        |> List.ofSeq

    /// edge list 를 vertex list 로 변환 : e.g [ a->b; b->c; ] --> [a; b; c]
    let es2vs (es:IEdge seq) =
        let es = es |> List.ofSeq
        match es with
        | [] -> []
        | h::t ->
            let tails = es |> List.map (fun e -> e.Target)
            h.Source::tails

    /// edge paths 를 vertex paths 로 변환: [['E]] -> [['V]]
    let eps2vps ess = ess |> List.map es2vs

    let toGraph edgeCreator (vs:IVertex seq) =
        vs
        |> vs2es edgeCreator
        |> GraphExtensions.ToAdjacencyGraph

    /// reset expression 을 구성하는 vertex 를 추출한다.
    ///
    /// e.g A+ 의 reset expression 이 (A- & A--) 로 주어졌다면, [A-; A--] 를 반환한다.
    let rec enumerateNodes (vs:IVertex seq) (exp:Expression) =
        [
            match exp with
                | Terminal(t) ->
                    match t with
                    | :? MemoryOnOffCondition as m ->
                        match m with
                        | NodeIncomingCondition(v)
                        | NodeRawFinishCondition(v)
                        | TrustedNodeStartCondition(v) ->
                            yield v
                        | TrustedEdgeCondition(e) ->
                            yield e.Source
                        | NodeResetCondition(r) -> ()
                        | ResetBlockCondition(v) -> yield v
                    | :? MemoryOnOffSpec as s ->
                        match s with
                        | NodeIncomingSpec(n)
                        | NodeRawFinishSpec(n) ->
                            yield! vs |> Seq.filter(fun v -> v.Name = n)
                        | _ ->
                            failwith "ERROR"
                    | _ ->
                        failwith "ERROR"
                | Binary(l, op, r) when op = Op.And ->
                    yield! enumerateNodes vs l
                    yield! enumerateNodes vs r
                | Zero ->   // sensor(e.g START) 가 인 일때, reset 조건이 존재하지 않음.
                    ()
                | _ ->
                    failwith "ERROR"
        ]
    /// vertex v 의 NodeIncomingCondition expression
    let ns(v) = Expression.Terminal(NodeIncomingCondition(v))

    let sp(v:IVertex) =
        if v.Ports.ContainsKey(PortCategory.Start) then v.StartPort
        else
            let p = QgPort(v, PortCategory.Start)
            v.StartPort <- p
            p :> IPort

    let rstp(v:IVertex) =
        if v.Ports.ContainsKey(PortCategory.Reset) then v.ResetPort
        else
            let p = QgPort(v, PortCategory.Reset)
            v.ResetPort <- p
            p :> IPort

    let fp(v:IVertex) =
        if v.Ports.ContainsKey(PortCategory.Sensor) then v.SensorPort
        else
            let p = QgPort(v, PortCategory.Sensor)
            v.SensorPort <- p
            p :> IPort

    let gp(v:IVertex) =
        if v.Ports.ContainsKey(PortCategory.Going) then v.GoingPort
        else
            let p = QgPort(v, PortCategory.Going)
            v.SensorPort <- p
            p :> IPort

    /// src Finish Port -> dst Start Port 연결
    let (>->) (src:IVertex) (dst:IVertex) =
        sp(dst).ConnectedPorts.Add(fp(src))
        dst

    /// Memory Reset 관계 설정.  dst 에 의해서 src 의 메모리가 reset 됨을 의미
    /// e.g A+ 의 reset expression 을 A- 의 시작 조건으로 설정하고 싶을 때에 사용한다.
    /// 이때, A+, A- 는 QgVertex 로, reset expression 정보를 설정할 때 MemoryOnOffCondition type 을 이용한다.
    let (<=<) (dst:IVertex) (src:IVertex) =
        (dst :?> IResetMutable).Reset <- ns(src)
        rstp(dst).ConnectedPorts.Add(sp(src))
        src

    /// 상호 reset 관계 설정
    let (<=>) (dst:IVertex) (src:IVertex) =
        dst <=< src <=< dst

    /// AND reset
    let (<&&<) (dst:IVertex) (srcs:IVertex list) =
        (dst :?> IResetMutable).Reset <- srcs |> List.map(ns) |> List.reduce mkAnd
        rstp(dst).ConnectedPorts.AddRange(srcs|>Seq.map(fun v -> gp(v)))

    /// OR reset
    let (<||<) (dst:IVertex) (srcs:IVertex list) =
        (dst :?> IResetMutable).Reset <- srcs |> List.map(ns) |> List.reduce mkOr

    let edgeIntoParent (p:IVertex) (edges:IEdge seq) =
        let dag = QgDAG(edges)
        p.DAGs.Add(dag)
        dag.Vertices |> Seq.iter(fun v -> v.Parent <- p |> Some)

    let vertexIntoParent (p:IVertex) (vs:IVertex seq) =
        let dag = QgDAG(vs, [])
        p.DAGs.Add(dag)
        dag.Vertices |> Seq.iter(fun v -> v.Parent <- p |> Some)

    let edgeIntoSelect (edges:IEdge seq) (func) (select:ISelect) =
        let dag = QgDAG(edges)
        select.AddConditionDAG(func, dag)
        dag.Vertices |> Seq.iter(fun v -> v.Parent <- select :> IVertex |> Some)

    let vertexIntoSelect (vs:IVertex seq) (func) (select:ISelect) =
        let dag = QgDAG(vs, [])
        select.AddConditionDAG(func, dag)
        dag.Vertices |> Seq.iter(fun v -> v.Parent <- select :> IVertex |> Some)


[<Extension>]
type IVertexExt =
    [<Extension>]
    static member getResetVertices (v:IVertex) =
        getTerminals v.Reset
        |> Seq.where(fun r -> r :? MemoryOnOffCondition)
        |> Seq.cast<MemoryOnOffCondition>
        |> Seq.map(fun r ->
            match r with
            | NodeIncomingCondition(v) -> Some v
            | _ -> None
        )
        |> Seq.choose id

    [<Extension>]
    static member isSelfReset (v:IVertex) =
        let reset = v.getResetVertices()
        reset.Contains(v)

    /// Vertex를 QgModel로 변환
    [<Extension>]
    static member generateVertexToModel (v:IVertex) =
        v.DAGs
        |> Seq.where(fun d -> d.Edges.isEmpty() |> not)
        |> Seq.map(fun dag ->
            { makeSimpleModel (dag.Edges.ToAdjacencyGraph()) with
                Start = v.StartPort.GetTag().ToText() |> Some;
                Reset = v.ResetPort.GetTag().ToText() |> Some;
                Title = v.Name |> Some
            }
        )


    [<Extension>]
    static member GetAllDepthAddress (v:IVertex) :int list  =
        let parentAddress =
            v.Parent
            |> Option.bind(fun (p:IVertex)  ->
                Some (p.GetAllDepthAddress())
            )

        match parentAddress with
        | Some(pa) -> (pa @@ [v.Address]) |> List.ofSeq
        | None -> [v.Address]

    static member ContainOutsideReset (v:IVertex) (g:DAG) :bool =
        let resets = v.getResetVertices()
        resets |> Seq.exists(fun r -> g.Vertices |> Seq.collect(getAllVertices2) |> Seq.contains(r) |> not)

    [<Extension>]
    static member GetGoingStatusExpression (v:IVertex) = v.GoingPort.GetTag()

    [<Extension>]
    static member GetSegmentType (segments:seq<ISegment>) (v:IVertex)  =
        segments
        |> Seq.tryFind(fun seg -> seg.Vertices.Contains(v))
        |> Option.bind(fun seg -> seg.SegmentType |> Some)
        |> Option.defaultValue SegmentType.Internal

    /// 초기 상태 값 추출
    /// 초기 상태가 Ready면 Sensor값에 Not
    /// Finish면 Sensor값으대로 이용
    [<Extension>]
    static member GetInitialValue (v:IVertex) =
        let sensor = v.SensorPort.GetTag()
        match v.InitialStatus with
        | VertexStatus.Ready -> sensor |> mkNeg
        | VertexStatus.Finish -> sensor
        | _ -> failwithlog (sprintf "%A Initial Status not Define" v)

[<AutoOpen>]
module IVertexM =
    let applyVertexCondition (v:IVertex) =
        v.Conditions
        |> Seq.iter(fun cond ->
            v.StartPort.PLCFunctions.Add(cond)
            //|> Seq.iter(fun p -> p.PLCFunctions.Add(cond))
        )

        v

[<AutoOpen>]
module ISelectM =
    let applySelectCondition (v:ISelect) =
        v.ConditionDAG
        |> Seq.collect(fun kv ->
            let dag = kv.Value
            let cond = kv.Key
            dag.Vertices |> Seq.iter(fun v -> v.StartPort.PLCFunctions.Add(cond)) |> ignore
            dag.Vertices
        )
        |> List.ofSeq

