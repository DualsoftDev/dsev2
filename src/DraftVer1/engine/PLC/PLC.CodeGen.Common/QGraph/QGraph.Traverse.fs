namespace Dual.Core.QGraph

open System.Collections.Generic
open System.Diagnostics
open FSharpPlus
open Dual.Common
open Dual.Common.Graph.QuickGraph
open Dual.Core
open System.Runtime.CompilerServices

[<AutoOpen>]
module GraphTraverse =
    /// vertex 에 대한 선택 여부를 결정하는 함수
    type VertexSelector = IVertex -> bool
    /// vertex 에 대해 predicate 을 수행하는 함수를 생성하기 위한 type
    type VertexSelectorCreator = unit -> VertexSelector

    /// QgVertex selector 함수를 selector creator 함수로 변환해서 반환
    let makeSelectorCreator (selector:VertexSelector) = fun() -> selector

    /// DCG Graph dcg 에서 시작 edge estart 로부터 selelctorCreator 에 의해서 생성된 selector 의 조건을 만족할 때까지 경로를 찾는다.
    /// * dcg 의 원본 그래프 g :
    ///     - valid 한 model graph 이어야 한다. (cycle 이 없어야 하고, major path 규칙을 준수, isolated nodes 가 없음)
    /// * dcg : 원본 그래프 g 에 terminal nodes --> initial nodes 로 edges(FakeEdge type) 들을 연결한 graph
    /// * return type : [['E]]
    // --- 구현 아이디어 :
    // FakeEdge 를 두번 만나기 전에 원하는 결과를 못찾으면 검색 실패이다.
    //
    // see getAllPathsWindable2
    //
    let getAllEPathsFromStartEdge (dcg:DCG) (estart:IEdge) (selectorCreator:VertexSelectorCreator) : EPaths =
        /// history : se -> e path 이전에 이미 찾은 path.  즉 인자로 주어진 s 보다 이전에 이미 구한 path
        let rec getAllEPathsHelper (history:IEdge list) (se:IEdge) (targetSelector:VertexSelector) =
            [
                let t = se.Target
                if targetSelector(t) then
                    yield se::history
                else
                    let oes = dcg.OutEdges(t)
                    let oesBackEdge, oesNormal =
                        oes
                        |> Seq.partition (fun e ->
                            history
                            |> List.map (fun eg -> eg.Source)
                            |> List.contains(e.Source))

                    for oe in oesNormal do
                        let fakeEdge = oe :? FakeEdge
                        if fakeEdge && history |> Seq.ofType<FakeEdge> |> Seq.any then
                            // 현재가 fakeEdge 이면서 과거 이력에도 이미 fakeEdge 를 만났으면, 그래프를 한바퀴 돌아 온 상태.  결과 없음
                            ()
                        else
                            yield! getAllEPathsHelper (se::history) oe targetSelector
            ]
        assert(dcg.Edges.Contains(estart))
        getAllEPathsHelper [] estart (selectorCreator())
        |> List.map List.rev

    /// 역방향 그래프 탐색 모듈
    [<AutoOpen>]
    module GraphReverseTraverse =
        /// 주어진 edge 의 source 측에서 incoming 를 기준으로 역탐색
        let getAllEPathsReverseFromStartEdge (dcg:DCG) (estart:IEdge) (selectorCreator:VertexSelectorCreator) : EPaths =
            /// history : se -> e path 이전에 이미 찾은 path.  즉 인자로 주어진 s 보다 이전에 이미 구한 path
            let rec getAllEPathsHelper (history:IEdge list) (se:IEdge) (sourceSelector:VertexSelector) =
                [
                    let t = se.Source
                    if sourceSelector(t) then
                        yield se::history
                    else
                        let oes = dcg.GetIncomingEdges(t)
                        let oesBackEdge, oesNormal =
                            oes
                            |> Seq.partition (fun e ->
                                history
                                |> List.map (fun eg -> eg.Target)
                                |> List.contains(e.Target))

                        for oe in oesNormal do
                            let fakeEdge = oe :? FakeEdge
                            if fakeEdge && history |> Seq.ofType<FakeEdge> |> Seq.any then
                                // 현재가 fakeEdge 이면서 과거 이력에도 이미 fakeEdge 를 만났으면, 그래프를 한바퀴 돌아 온 상태.  결과 없음
                                ()
                            else
                                yield! getAllEPathsHelper (se::history) oe sourceSelector
                ]
            assert(dcg.Edges.Contains(estart))
            getAllEPathsHelper [] estart (selectorCreator())

        /// 시작 vertex (vstart)에서 selectorCreator 에 의해 생성된 selector 의 조건을 충족하는 path (edge 의 list)를 반환
        let getAllEPathsReverseFromStartVertex (dcg:DCG) vstart selectorCreator : EPaths =
            dcg.GetIncomingEdges(vstart)
            |> List.ofSeq
            |> List.collect(fun e -> getAllEPathsReverseFromStartEdge dcg e selectorCreator)

        /// 시작 위치(ostart)에서 selectorCreator 에 의해 생성된 selector 의 조건을 충족하는 path (edge 의 list)를 반환
        let getAllEPathsReverse dcg (ostart:QgObject) selectorCreator : EPaths =
            match ostart with
            | QgVertexObj(v) -> getAllEPathsReverseFromStartVertex dcg v selectorCreator
            | QgEdgeObj(e)   -> getAllEPathsReverseFromStartEdge dcg e selectorCreator


    /// 시작 vertex (vstart)에서 selectorCreator 에 의해 생성된 selector 의 조건을 충족하는 path (edge 의 list)를 반환
    let getAllEPathsFromStartVertex (dcg:DCG) vstart selectorCreator : EPaths =
        dcg.GetOutgoingEdges(vstart)
        |> List.ofSeq
        |> List.collect(fun e -> getAllEPathsFromStartEdge dcg e selectorCreator)

    /// 시작 위치(ostart)에서 selectorCreator 에 의해 생성된 selector 의 조건을 충족하는 path (edge 의 list)를 반환
    let getAllEPaths dcg (ostart:QgObject) selectorCreator : EPaths =
        match ostart with
        | QgVertexObj(v) -> getAllEPathsFromStartVertex dcg v selectorCreator
        | QgEdgeObj(e)   -> getAllEPathsFromStartEdge dcg e selectorCreator

    /// 시작 위치(ostart)에서 selectorCreator 에 의해 생성된 selector 의 조건을 충족하는 path (vertex 의 list)를 반환
    let getAllVPaths dcg (ostart:QgObject) selectorCreator : VPaths =
        getAllEPaths dcg ostart selectorCreator |> eps2vps

    /// dcg 상에서 src --> src' 까지의 모든 경로를 구해서 반환
    let getEDurations (dcg:DCG) (ostart:QgObject) =
        let startv = ostart.GetSource()
        let reset = startv.Reset |> enumerateNodes dcg.Vertices

        if startv.isSelfReset() then [[]]
        else
            getAllEPaths dcg ostart (makeSelectorCreator (fun n -> reset |> List.contains(n)))


    /// dcg 상에서 src --> src' 까지의 모든 경로를 구해서 반환
    let getVDurations (dcg:DCG) (ostart:QgObject) =
        let startv = ostart.GetSource()
        //let reset = startv.Reset |> enumerateNodes dcg.Vertices
        let reset = startv.getResetVertices()
        let slReset = dcg.Vertices |> Seq.where(fun v -> getAllVertices2 v |> Seq.exists(fun v -> reset |> Seq.contains(v))) |> List.ofSeq
        if startv.isSelfReset() then [[startv]]
        else
            getAllVPaths dcg ostart (makeSelectorCreator (fun n -> slReset |> List.contains(n)))

    /// s -> s' forward e paths
    let getEDurationsFG model ostart = getEDurations model.DCG ostart

    /// s -> s' forward v paths
    let getVDurationsFG model ostart =
        getEDurationsFG model ostart |> eps2vps

    [<Extension>] // type DurationsExt =
    type DurationsExt =
        /// 주어진 duration 내에 checkNode 가 존재하는지 검사
        [<Extension>]
        static member Contains(durations:IVertex list list, checkNode:IVertex) =
            durations
            |> List.collect id
            |> List.contains checkNode




