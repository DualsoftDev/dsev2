namespace Dual.Core.QGraph

open System.Collections.Generic
open System.Diagnostics
open FSharpPlus
open QuickGraph
open Dual.Common
open Dual.Common.Graph.QuickGraph
open Dual.Core
open Dual.Core.Types
open QuickGraph


[<AutoOpen>]
module TrustProcessing =
    // tag:#processing

    /// Dag 의 신뢰 처리 결과
    type ProcessResultInfo = {
        /// Process로 생성된 Expression 정보
        LadderInfo: LadderInfo

        Workbook: RelayMarkerWorkbook

        Relays: Relay array
    }


    /// 인과가 주어질 때, 인에 의한 uniq relay 를 reset 할 지점을 찾는다.
    /// 과가 변하기 전까지 reset 가능하다.
    /// effectDurations = [[과, 과'], [과, 과'], ...] 의 {edge path}s
    let getResetExntendableNodes g (effectDurations:EPaths) =

        let untilNextEffects =
            effectDurations
            |> List.mapiInner (fun i edge ->
                [
                    if i = 0 then
                        yield TrustedEdgeCondition(edge)
                    yield NodeIncomingCondition(edge.Target)
                ])
            |> List.map List.flatten
            |> List.flatten
            |> List.distinct

        assert(untilNextEffects |> List.any)
        untilNextEffects




    /// edge 의 인이 전체 graph 상에 한번만 나타난다고 가정하고 처리 (인' 은 나타날 수 있음)
    ///
    /// 실제 전체 graph 상에서 인이 여러번 나타나는 경우, local 처리와 유일 처리를 병행
    let processEdge (model:QgModel) idGenerator (edge:IEdge) =
        let g = model.DCG
        let cause = edge.Source
        let effect = edge.Target
        /// [인 --> 인'].  drs : Durations
        let drsCauseFull = getVDurations g (QgVertexObj cause)
        /// [인 --> 인')
        let drsCause = drsCauseFull |> List.map List.initv

        /// [과 --> 과'] 의 edge durations
        let drsEdgeEffectFull = getEDurations g (QgVertexObj effect)
        /// 인'
        let cause2 = drsCauseFull |> List.map List.last |> List.distinct |> List.tryExactlyOne

        /// [인 -> 인') 경로에 과 포함여부.  유지 check
        let cContainsE = drsCause.Contains(effect)

        /// 인 이 graph 상에서 유일한가? (globally)
        let cUniqGlobal = g.FindNamedVertices(cause.Name) |> Seq.length = 1

        /// [과 -> 과') 경로에 인' 포함여부.  유일 check
        /// 인' <> 과 인 경우만 포함된다고 봐야한다.
        /// e.g 다이아몬드 인과에서 A+ -> A- 의 예.  [과 -> 과') = {A-, B-} 이고, 인' = A- 이지만, 인' 이 [과->과') 에 포함된다고 볼 수 없다.
        let eContainsC2 =
            /// [과 --> 과')
            let drsEffect2 = drsEdgeEffectFull |> eps2vps |> List.map List.initv
            cause2
            |> Option.map(fun c -> c <> effect && drsEffect2.Contains(c))
            |> Option.defaultValue false

        /// `인이 외부에서 들어와 인 -> `인이 존재하지 않지만
        /// `인의 위치가 확실하여 인의 초기상태가 정해진경우
        let isTrustOutSideReset = IVertexExt.ContainOutsideReset cause g(* && cause.InitialStatus <> VertexStatus.Undefined*)

        let resetExtendables =
            if model.DAG.GetInitialVertices() |> Seq.contains(effect) && model.DAG.GetTerminalVertices() |> Seq.contains(cause) then
                [NodeResetCondition(Expression.Zero)]
            else
                if eContainsC2 && drsEdgeEffectFull.any() then
                    getResetExntendableNodes g drsEdgeEffectFull
                else
                    let edgesNext = g.GetOutgoingEdges(effect)
                    [
                        yield! edgesNext |> Seq.map(fun en -> TrustedEdgeCondition(en))
                        yield! edgesNext |> Seq.map(fun en -> NodeIncomingCondition(en.Target))
                    ]
        let createURMs markables =
            let id = idGenerator()
            markables |> List.map (fun v ->
                URM(id, v, edge, TrustedNodeStartCondition(v), resetExtendables) :> RelayMarker)

        //! 유지 check, 유일 check
        match cUniqGlobal, cContainsE, eContainsC2, isTrustOutSideReset with
        //| true, _, _, true
        | true, true, true, false ->         // happy case
            []
        | false, _, _, _ ->              // 인이 graph 전체에서 복수
            createURMs [cause]
        | true, true, false, false ->        // 유일 flag.  (인prev ... 인 -> 과) 에서 (인prev.. 인] 의 구간에 유일 flag 설정
            createURMs [cause]
        | true, false, false, _ ->           // 유지 relay
            [ SRM(idGenerator(), cause, edge, TrustedNodeStartCondition(cause), resetExtendables) ]
        | true, false, true, _ ->        // 있을 수 없는 상황
            failwith "ERROR: Absurd case"

    /// V -> `V에 해당하는 Path들이 겹치면 Merge하여 하나의 Block으로 만든다.
    let getResetBlock (model:QgModel) (vertices:seq<IVertex>) =
        let g = model.DAG
        let getPath (vertex:IVertex) =
            let resets = vertex.getResetVertices()
            /// [V --> V'] 버텍스 -> 리셋 경로
            let drsCause =
                resets
                /// V -> V` or V <- V` path
                |> Seq.collect(fun reset ->
                    let fpath = g.GetAllPaths (vertex, reset)
                    /// self Reset
                    if vertex.isSelfReset() then [vertex; reset]
                    /// outside reset
                    else if g.ContainsVertex(reset) |> not then [vertex; vertex]
                    else
                        if fpath.IsEmpty then g.GetAllPaths (reset, vertex) else fpath
                        |> List.flatten
                    )

            /// 분기를 가지고 있으면 분기까지 포함시킨다.
            let outvertices = g.GetOutgoingVertices(vertex)
            let paths =
                if outvertices |> Seq.length > 1 then
                    drsCause |> Seq.append (outvertices |> Seq.where(fun v -> v.isSelfReset() |> not)) else drsCause
                |> List.ofSeq

            vertex, paths

        /// Vertex별 V -> `V Path
        let vpath = vertices |> Seq.map(getPath) |> List.ofSeq

        /// path에 속하는 Vertex의 Path를 Merge한다.
        let rec vPathGroup (vnpath:(IVertex * IVertex list)list) =
            let result =
                vnpath
                |> List.map(fun (v, spath) ->
                    /// v경로의 모든 버텍스가 포함된 path들 취합 및 중복 버텍스제거
                    /// path에 동일 버텍스가 2번 나오는 경우가 없어야함
                    let mpath =
                        spath
                        |> List.collect(fun v ->
                            vnpath
                            |> List.where(fun (_, tpath) -> tpath.Contains(v) )
                            |> List.collect snd)
                        |> List.distinct
                    v, mpath)
                |> List.distinctBy snd

            /// 경로가 더이상 머지안될때까지
            if vnpath = result then result else vPathGroup result

        let mergePath = vPathGroup(vpath)

        /// vertex path를 edge로 변환
        let ePathGroup =
            mergePath
            |> List.map(fun (v,path) ->
                if path.Length = 1 then
                    [QgEdge(v, v) :> IEdge]
                else if path |> Seq.isEmpty && IVertexExt.ContainOutsideReset v g then [QgEdge(v, v) :> IEdge]
                else
                    path
                    |> List.collect(fun s ->
                        path
                        |> List.where(fun t ->
                            g.Edges
                            |> Seq.exists(fun e -> e.Source = s && e.Target = t) )
                        |> List.map(fun t -> g.GetEdgeExactlyOne(s, t))
                    )
            )

        /// 블럭이 비었는가
        mergePath |> Seq.iter(fun (v, path) -> if path |> Seq.isEmpty &&  IVertexExt.ContainOutsideReset v g |> not then failwith (sprintf "%A block empty" v))
        ePathGroup |> Seq.where(fun es -> es.IsEmpty |> not) |> Seq.map(fun es -> es.ToAdjacencyGraph())


    /// Job Sequence 처리 : 각 job 의 출력 조건을 relay 를 사용하여 생성.  해당 relay 조건 포함
    let processModel (model:QgModel) (Default (createAbstractCodeGenerationOption()) opt) =
        ModelVaidation.validateModel model |> ignore

        let idGenerator = counterGenerator 0

        /// edge 별로 생성한 markers
        ///
        /// [QgEdge * [RelayMarker]]
        let ``e&ms`` =
            model.DCG.Edges
            |> List.ofSeq
            |> List.map (fun e ->
                let markers = e |> processEdge model idGenerator
                e, markers
            )

        /// Reset Block 별로 생성한 markers
        let ``rb&ms`` =
            /// v -> v`의 path를 구하여 해당 path에 존재하는 vertex들의 path를 취합하여 graph로 변형
            /// pairwisewinding하여 앞 뒤 block끼리 묶어줌
            /// 앞 last vertex -> 뒤 first vertex Edge를 만들어 marker의 대상 edge로 사용
            /// marker의 cause는 앞 first vertex
            let blocks = getResetBlock model model.DAG.Vertices |> List.ofSeq

            let result =
                blocks
                |> List.collect(fun block ->
                    let g = model.DCG
                    let fstTerminals =
                        if block.VertexCount = 1 then
                            seq{block.Vertices |> Seq.head}
                        else block.GetTerminalVertices()

                    let cause =
                        if block.VertexCount = 1 then
                            block.Vertices
                        else block.GetInitialVertices()
                        |> Seq.head

                    let edges =
                        fstTerminals
                        |> Seq.collect(fun s ->
                            blocks
                            |> Seq.collect(fun b ->
                                let sndInitials =
                                    if b.VertexCount = 1 then
                                        seq{b.Vertices |> Seq.head}
                                    else b.GetInitialVertices()
                                sndInitials
                            )
                            |> Seq.where(fun t -> g.Edges |> Seq.exists(fun e -> e.Source = s && e.Target = t))
                            |> Seq.map(fun t -> g.GetEdgeExactlyOne(s, t)))
                        |> List.ofSeq

                    edges |> List.map(fun edge -> MRM(idGenerator(), cause, edge, ResetBlockCondition(cause), [NodeResetCondition(Expression.Zero)])) |> List.cast<RelayMarker>
                )
            result

        /// edge 별로 생성한 marker 의 id 를 갖는 multimap : edge -> [id]
        ///
        /// Multimap<QgEdge, RMType<int>>
        let ``e->id multimaps`` =
            let erbms =
                ``rb&ms``
                |> List.map(fun rm -> rm.Edge, [rm])

            ``e&ms``
            |> List.append  erbms
            |> List.groupBy fst
            |> List.map (fun (key, values) -> (key, values |> List.collect snd))
            |> List.map2nd (Seq.map (fun (rm:RelayMarker) -> rm.DuId))
            |> MultiMap.CreateDeep

        /// marker 의 location (vertex) 별로 marker 를 갖는 multimap : v -> [markers]
        ///
        /// Multimap<QgVertex, RelayMarker>
        let ``v->m multimap`` =
            ``e&ms``
            |> List.map snd
            |> List.flatten
            |> List.append ``rb&ms``
            |> List.map (fun m -> m.Location, m)
            |> MultiMap.CreateFlat

        /// id 별로 어떤 edge 에 속하는지의 map.  debugging purpose only
        ///
        /// Dictionary<int, QgEdge>
        let ``id->e map`` =
            ``e->id multimaps``.EnumerateKeyAndValue()
            |> Seq.map Tuple.swap
            |> Seq.map1st getIdOfTag
            |> dict |> Dictionary

        let workbook =
            {
                Model             = model
                Edge2IdsMap       = ``e->id multimaps``
                Vertex2MarkersMap = ``v->m multimap``
                Id2EdgeMap        = ``id->e map``
                ProcessedMarkers  = HashSet<RelayMarker>()
                TerminalRelays    = ResizeArray<Relay>()
                Id2RelayDic       = Dictionary<int, Relay>()
            }

        /// id 별 Relay map
        ///
        /// Dictionary<int, Relay>
        let id2RelayDic =
            if opt.Optimize then
                optimizeRelays opt workbook
            else
                let allMarkers = ``v->m multimap``.FlatValues |> Seq.distinct
                allMarkers |> traceMarkers "------------------------\nGenerated markers\n\t" "\n\t"
                generateRelays opt workbook allMarkers

        // terminal relay 를 workbook 에 marking
        id2RelayDic.Values
        |> Seq.filter (isTerminalRelay model)
        |> workbook.TerminalRelays.AddRange

        let ladderInfos =
            let modelComments =
                [
                    model.Title |> Option.map (sprintf "Title: %s")
                    if (model.Sensors.any()) then
                        model.Sensors |> String.concat ", " |> sprintf "Sensors: %s" |> Some
                    model.Start |> Option.map (sprintf "Start: %s")
                    model.Reset |> Option.map (sprintf "Reset: %s")
                ]
                |> List.choose id
            generateLadderInfo modelComments opt workbook id2RelayDic

        tracefn "================================"
        id2RelayDic.Values
        |> Seq.iter(fun r -> tracefn "%s = %A" r.Name r.RelayMarker)
        tracefn "================================"

        tracefn "Generated Expressions..."
        ladderInfos.Rungs |> List.iter (fun ri ->
            let x = (ri.Set <||> ri.Selfhold) <&&> (~~ ri.Reset)
            tracefn "%s = %s" (ri.GetCoilName()) (x.ToText())
        )


        /// Result
        let result = {
            LadderInfo = ladderInfos
            Workbook = {workbook with Id2RelayDic = id2RelayDic}
            Relays = id2RelayDic.Values |> Array.ofSeq
        }
        result

    let rungInfoToExpr info = ((((info.Start <&&> info.Set) <||> info.Selfhold) <||> info.Manual) <&&> info.Interlock <&&> ~~ info.Reset)
