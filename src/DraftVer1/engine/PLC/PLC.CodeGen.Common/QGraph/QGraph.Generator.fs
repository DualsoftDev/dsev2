namespace Dual.Core.QGraph

open System.Collections.Generic
open FSharpPlus
open Dual.Common
open Dual.Common.Graph.QuickGraph
open Dual.Core
open Dual.Core.Types
open RelayMarkerPath


[<AutoOpen>]
module RelayOptimzier =
    let private groupMarkers wb (ms:RelayMarker list) =
        /// 동일 set/reset 을 갖는 markers 중에서 resetExtendables 의 교집합
        let masterResettables =
            ms
            |> List.map (fun m -> m.ResetExtendables)
            |> List.intersectMany

        match masterResettables, ms with
        // ms 중에서 reset 확장의 교집합이 존재하지 않으면 포기
        | [], _ ->
            ()
        // 교집합 및 ms 모두 non-empty 인 경우
        | (_::_), (h::ts) ->
            /// reset extendable 을 공유하는 marker 중에서 임의로 하나를 master id 로 정하고, 나머지를 다 여기로 merge 한다.
            let masterId = getIdOfTag h.DuId
            // ms 의 head 를 master 로 지정
            h.ResetExtendables <- masterResettables

            // ms 의 tails 에 대해서 head 에 정보 merge
            for m in ts do
                m.ResetExtendables <- masterResettables

                // marker m 이 master 에 merge 됨을 표시
                m.MergedToId <- Some masterId
                h.MergedIds.Add(m.Id) |> ignore

            /// 이번에 처리된 marker 들의 id
            let ids = ms |> List.map (fun m -> m.Id) |> HashSet

            /// ms 이외에, 다른 영역에 marking 된 marker 중에서 ms 와 동일한 id 를 갖는 marker
            let processed =
                getAllUnprocessedMarkers wb
                |> List.filter(fun m -> ids.Contains(m.Id))
                |> List.differencePipe ms

            /// processed 및 ms 중에서 master 이외의 marker 들에 대해서 처리 됨으로 표시
            wb.ProcessedMarkers.AddRange (ts@processed)
        | _ ->
            failwith "ERROR"

    /// Relay markers ([ms]) 를 받아서, 각 marker 의 relay 의 Set/Reset 조건에 따라서 grouping 해서 반환
    /// 반환 type : [ RelayOnOffCOndition * RelayOnOffCOndition * [RelayMarker] ]
    let mergeMarkers wb (ms:RelayMarker seq) =
        /// (marker, set, reset) 의 triples
        let ``m&s&r's`` =
            let ms = ms |> getUnprocessedMarkers wb
            [
                for m in ms do
                    for r in m.ResetExtendables do
                        // 아직 처리되지 않은 marker 들에 대해서 reset extendable list 를 분할해서 marker, set, reset 정보로 배출
                        yield m, m.Set, r
            ]

        /// [ (set, reset, [markers]) ]
        let ``s&r&ms`` =
            ``m&s&r's``
            // set * reset 조건으로 grouping : [ (set * reset) -> ([markers] * set * reset) ]
            |> List.groupBy (fun (m,s,r) -> s, r)     // [ (RelayOnOffCondition * RelayOnOffCondition) * [RelayMarker * RelayOnOffCondition * RelayOnOffCondition] ]
            // [ (set * reset) -> ([markers] * set * reset) ] ==> [ (set * reset) -> [markers] ]
            |> List.map2nd (List.map Tuple.tuple1st)  // [ (RelayOnOffCondition * RelayOnOffCondition) * [RelayMarker] ]
            |> List.map (fun ((s,r), ms) -> s, r, ms) // [ RelayOnOffCondition * RelayOnOffCondition * * [RelayMarker] ]
            // set * reset 이 동일하면서, marker 갯수가 가장 많은 순으로 정렬
            |> List.sortByDescending(fun (s, r, ms) -> ms.Length)

        for (s, r, ms') in ``s&r&ms`` do
            /// Set/Reset 이 동일한 marker 중에서 처리 안된 것들
            let ms = ms' |> getUnprocessedMarkers wb
            groupMarkers wb ms

        ``s&r&ms``

    /// 주어진 markers [ms] 의 개별 marker 에 대해서 relay 를 생성하고
    /// marker 의 모든 id (원래 id + merged ids) 에 대해서 해당 relay 를 key - value 로 갖는
    /// Dictionary<int,Relay> 반환
    let generateRelays opt (wb:RelayMarkerWorkbook) (ms:RelayMarker seq) =
        let relgen = opt.RelayGenerator

        let ms = ms |> Seq.sortBy(fun m -> m.Id) |> Seq.distinctBy(fun m -> m.Id)
        [
            for m in ms do
                // revert relay 로 생성
                let relay =
                    if opt.RevertRelays then
                        { Name=relgen(); Set=m.Reset; Reset=m.Set; RelayMarker=m; Comments=[] }
                    else
                        { Name=relgen(); Set=m.Set; Reset=m.Reset; RelayMarker=m; Comments=[] }
                for id in m.Id::(m.MergedIds |> List.ofSeq) do  // 원래 id + merged ids
                    yield id, relay
        ] |> dict |> Dictionary

    /// marker 를 최적화해서 Dictionary<int,Relay> 반환
    let optimizeRelays opt wb =
        let allMarkers =
            getAllMarkers wb |> List.distinct |> List.sortBy(fun m -> m.Id)

        allMarkers
        |> traceMarkers "------------------------\nBefore optimize\n\t" "\n\t"

        /// [ vertex -> [marker] ] 에서 marker 의 갯수가 많은 순으로 정렬
        let ```v&ms`` =
            wb.Vertex2MarkersMap.EnumerateKeyAndGroupValue()
            |> Seq.sortByDescending (snd >> (fun hash -> hash.Count))
            |> List.ofSeq

        // [marker] 갯수가 많은 vertex 위치 부터 [marker] 를 merge
        for (v, ms) in ```v&ms`` do
            mergeMarkers wb ms |> ignore

        if opt.OptimizeNodeStartAndNodeFinish then
            // NodeIncomingCondition 과 NodeRawFinishCondition 합성 조건 check
            mergeMarkers wb allMarkers |> ignore


        let remainings = getAllUnprocessedMarkers wb
        remainings |> traceMarkers "------------------------\nRemaining relays\n\t" "\n\t"

        remainings |> generateRelays opt wb

[<AutoOpen>]
module ExpressionGenerator =
    /// relay 는 반전으로 사용
    let relayCoil (opt:CodeGenerationOption) (wb:RelayMarkerWorkbook) (rel:Relay) e =
        let isFakeEdge = wb.Model.DAG.ContainsEdge(e) |> not
        let isTerminalRelay = wb.TerminalRelays.Contains(rel)
        toCoil(rel.Name) |> if opt.RevertRelays (*|| (isFakeEdge && isTerminalRelay)*) then mkNeg else id

    /// Synonym for getVertexRawExpression
    /// graph 상에 vertex v 에 대한 expression 을 생성.
    let getVertexSensorExpression (opt:CodeGenerationOption) (v:IVertex) =
        match v.SensorPort.GetTag(), opt.SensorTagGenerator with
        | Expression.Zero, Some(f) -> f v |> toCoil
        | Expression.Zero, None -> v.ToText() |> toCoil
        | _ as expr, _ -> expr

    let getVertexOutputExpression (opt:CodeGenerationOption) (v:IVertex) =
        match v.StartPort.GetTag(), opt.SensorTagGenerator with
        | Expression.Zero, Some(f) -> f v |> toCoil
        | Expression.Zero, None -> v.ToText() |> toCoil
        | _ as expr, _ -> expr

    /// vertex v의 Going State에 해당하는 Expr 생성
    let getVertexGoingExpression (opt:CodeGenerationOption) (v:IVertex) =
        match opt.GoingStateNameGenerator with
        | Some(f) -> f v
        | _ -> v.ToText()
        |> toCoil

    //let getSourcePortExpression (source:IVertex) (v:IVertex) =
    //    monad{
    //        let! sourcePort = v.StartPort.ConnectedPorts |> Seq.tryFind(fun p -> p.Parent = source)
    //        let! plctag = sourcePort.PLCTag

    //        plctag |> mkTerminal
    //    }
    //    //// source target 관계에 port연결을 못찾은경우
    //    |> Option.defaultValue Expression.Zero

    /// edge에 해당하는 Moved Relay 추출
    let getMovedRelayExpression opt (wb:RelayMarkerWorkbook) (id2RelayDic:Dictionary<int, Relay>) e =
        /// e로 moved relay 추출 id
        let ids =
            wb.Edge2IdsMap.[e]
            |> List.ofSeq
            |> List.where(fun id -> isMoved id)
            |> List.map(fun id -> getIdOfTag id)

        /// id로 Relay 추출
        let result =
            ids
            |> List.map(fun id -> relayCoil opt wb id2RelayDic.[id] e)
            |> List.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero

        result
    /// Relay Area First Vertex에 해당하는 Moved Relay를 가져오기위함
    /// First Vertex에 의해서 Set되는 Moved Relay를 가져옴
    /// Moved Relay는 Block First Vertex를 조건 Vertex로 가지고 있기 때문에
    /// First Vertex로 Relay Markder를 찾을수있다.
    let getMovedRelayExpressionForVertex opt (wb:RelayMarkerWorkbook) (id2RelayDic:Dictionary<int, Relay>) v =
        monad{
            let! marker =
                match wb.Vertex2MarkersMap.ContainsKey(v) with
                | true ->
                    wb.Vertex2MarkersMap.[v]
                    |> List.ofSeq
                    |> List.where(fun rm -> isMoved rm.DuId)
                    |> List.tryHead
                | false -> None

            getMovedRelayExpression opt wb id2RelayDic marker.Edge |> mkNeg
        }
        |> Option.defaultValue Expression.Zero


    /// Relay Area Last Relay에 해당하는 Moved Relay를 가져오기위함
    /// Moved Relay는 블럭의 마지막과 다음 블럭사이의 Edge를 가지고 있기 때문에
    /// [A; B; C;] [D;] 블럭이있을경우 C->D 엣지로 Moved 릴레이를 찾을수있다.
    let getMovedRelayExpressionForRelay opt (wb:RelayMarkerWorkbook) (id2RelayDic:Dictionary<int, Relay>) v =
        let marker =
            wb.Vertex2MarkersMap.[v]
            |> List.ofSeq
            |> List.where(fun rm -> isMoved rm.DuId |> not)
            |> List.tryHead

        match marker with
        | Some(m) -> getMovedRelayExpression opt wb id2RelayDic m.Edge
        | None -> Expression.Zero

    /// Prev Moved Relay를 가져오기위함
    let getPrevMovedRelayExpressionForRelay opt (wb:RelayMarkerWorkbook) (id2RelayDic:Dictionary<int, Relay>) v =
        let incomingEdges = wb.Model.DAG.GetIncomingEdges v

        match incomingEdges.any() with
        | true ->
            let incoming = incomingEdges |> Seq.map(getMovedRelayExpression opt wb id2RelayDic) |> List.ofSeq

            incoming |> List.reduce mkAnd
            ///// if else 경우 or
            //if wb.Model.DCG.GetIncomingVertices(v) |> Seq.exists(fun v -> v.IsIfVertex() |> not) then
            //    incoming |> List.reduce mkAnd
            //else
            //    incoming |> List.reduce mkOr
        | false -> Expression.Zero

    let getBlockFirstFromLast (wb:RelayMarkerWorkbook) v =
        monad{
            let! emarker =
                wb.Vertex2MarkersMap.[v]
                |> List.ofSeq
                |> List.where(fun rm -> isMoved rm.DuId |> not)
                |> List.tryHead

            let! ids =
                wb.Edge2IdsMap.[emarker.Edge]
                |> List.ofSeq
                |> List.where(fun id -> isMoved id)
                |> List.map(fun id -> getIdOfTag id)
                |> List.tryHead

            let! first = wb.Vertex2MarkersMap.EnumerateKeyAndValue() |> Seq.where(fun (_, v) -> v.Id = ids) |> Seq.map(fun (_, v) -> v.Set) |> Seq.tryHead

            let! firstv =
                match first with
                | ResetBlockCondition(v) -> Some v
                | _ -> None

            firstv
        }

    let getRelayCauseExpression (wb:RelayMarkerWorkbook) (source:IVertex)=
        monad{
            let! marker =
                wb.Vertex2MarkersMap.[source]
                |> List.ofSeq
                |> List.where(fun rm -> isMoved rm.DuId |> not)
                |> List.tryHead
            let target = marker.Edge.Target

            let! port = target.StartPort.ConnectedPorts.where(fun p -> p.Parent = source) |> Seq.tryHead

            port.GetTag()
        }
        |> Option.defaultValue Expression.Zero


    /// incoming edge 에 대한 expression 반환
    let generateEdgeExpression (opt:CodeGenerationOption) wb (id2RelayDic:Dictionary<int, Relay>) (isCondExpr:bool) e  =
        /// 하나의 edge 는 복수개의 relay id 를 가질 수 있다.
        let ids = wb.Edge2IdsMap.[e] |> List.ofSeq
        let isFirstVertex = wb.Model.DAG.GetInitialVertices().Contains(e.Target)
        let sourcePort = e.Target.StartPort.ConnectedPorts |> Seq.find(fun p -> p.Parent = e.Source)
        let srcCoil =
            if isFirstVertex then Expression.Zero
            else sourcePort.GetTag()
        let relayCoil (rel:Relay) =
            let re = relayCoil opt wb rel e
            if isFirstVertex && isCondExpr then mkNeg re else re

        match ids with
        | Moved(id)::[] -> srcCoil
        | [] -> srcCoil
        | Unique(id)::[] ->
            relayCoil id2RelayDic.[id] //? |> mkAnd srcCoil
        | Sustainable(id)::[] ->
            relayCoil id2RelayDic.[id]
        | Unique(uid)::Sustainable(sid)::[] ->
            ///assert(false)
            relayCoil id2RelayDic.[sid]
        | Moved(mid)::Unique(uid)::[] ->
            relayCoil id2RelayDic.[uid]
        | Moved(mid)::Sustainable(sid)::[] ->
            relayCoil id2RelayDic.[sid]
        | _ ->
            failwith "ERROR"


    /// vertex 에 대한 expression 반환 : vertex v 로 들어오는 모든 edge 의 condition 구한 후
    /// incoming이 if else면 OR 아니면 AND
    /// id2RelayDic : relay 생성 id 별 relay
    let generateVertexIncomingEdgesExpression (opt:CodeGenerationOption) wb id2RelayDic v =
        // 마지막 릴레이를 조건으로 사용하고 싶으면 DCG를 사용
        // 아니면 DAG
        let incoming =
            wb.Model.DCG.GetIncomingEdges(v)
            |> List.ofSeq
            |> List.map (generateEdgeExpression opt wb id2RelayDic true)

        incoming |> List.tryReduce mkAnd
        |> Option.defaultValue Expression.Zero
        ///// if else 경우 or
        //if wb.Model.DCG.GetIncomingVertices(v) |> Seq.exists(fun v -> v.IsIfVertex() |> not) then
        //    incoming |> List.tryReduce mkAnd
        //else
        //    incoming |> List.tryReduce mkOr
        //// v 가 sensor 인 경우, incoming edge 가 존재하지 않는다.
        //|> Option.defaultValue Expression.Zero


    /// vertex 출력에 대한 reset interlock 생성
    ///
    let generateOutputInterlocks opt (v:IVertex) =
        if v.isSelfReset() then v.SensorPort.GetTag()
        else
            v.ResetPort.ConnectedPorts
            |> List.ofSeq
            |> List.map (fun p -> p.GetTag())
            |> List.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero
        <&&> v.ResetPort.ConnectedExpression

    /// A+ 출력에 대해서 A+ 센서 비접을 interlock 으로 생성
    let generateOutputResetByFinish opt (v:IVertex) =
        getVertexSensorExpression opt v |> mkNeg



    /// 리셋 대상이 현재 task보다 선행된 경우,현재 task를 실행시키기 위한 릴레이의 set 조건으로 활용될 수 있다.
    let generatePrecedeResetTargetExpression (wb:RelayMarkerWorkbook) source =
        monad{
            let! marker =
                match wb.Vertex2MarkersMap.ContainsKey(source) with
                | true ->
                    wb.Vertex2MarkersMap.[source]
                    |> List.ofSeq
                    |> List.where(fun rm -> isMoved rm.DuId |> not)
                    |> List.tryHead
                | false -> None

            let resetSource = marker.Edge.Target
            let vs = wb.Model.DAG.Vertices
            let resetTargets = vs |> Seq.where(fun v -> v.getResetVertices() |> Seq.contains(resetSource))
            let precede = resetTargets |> Seq.where(fun v -> v.Address < resetSource.Address)

            let! result =
                precede
                |> Seq.map(fun v -> v.SensorPort.GetTag())
                |> Seq.tryReduce mkAnd

            result
        }
        |> Option.defaultValue Expression.Zero




    let isTerminalRelay (model:QgModel) (relay:Relay) =
        model.DAG.GetTerminalVertices().Contains(relay.RelayMarker.Location)


    /// expr 을 그대로 반환하되, comments array 에 logging 항목을 추가함
    let teeExpressionComment prefix (comments:ResizeArray<string>) (expr:Expression) : Expression =
        if expr.IsNonZero then
            prefix + expr.ToText() |> comments.Add
        expr

    /// 모델에 대한 모든 expressions 반환 : [Expression * Expression]
    /// id2RelayDic : 미리 구한 relay 정보
    ///
    /// return : LadderInfo
    let generateLadderInfo (modelComments:string seq) (opt:CodeGenerationOption) wb id2RelayDic =
        /// 모든 vertex 에 대한 expressions (relay 는 해당하지 않음)
        let vertexRungInfos =
            let sensors = wb.Model.Sensors |> HashSet
            let heads = wb.Model.DAG.GetInitialVertices()
            wb.Model.DCG.Vertices
            |> Seq.filter (fun v -> not <| sensors.Contains(v.Name))
            |> Seq.groupBy (fun v -> v.Name)        // 이름 순서로 정렬
            |> List.ofSeq
            |> List.map (fun (name, vs) ->
                let comments = ResizeArray<string>()
                let v = Seq.head vs
                let parent = v.Parent
                let start =
                    match parent, wb.Model.Start, heads.Contains(v) with
                    | None, Some(s), _ -> toCoil s
                    | Some(p), _, _ -> getVertexGoingExpression opt p
                    | _ -> Expression.Zero

                let manual =
                    let 원점 =
                        match v.InitialStatus with
                        | VertexStatus.Finish -> "원점" |> toCoil
                        | _ -> Expression.Zero
                    let manualTag = v.ManualTag |> Option.bind(fun tag -> tag |> mkTerminal |> Some) |> Option.defaultValue Expression.Zero
                    (*원점 <||>*)
                    manualTag
                /// 자신의 Start(출력)
                let selfHold =
                    if v.UseSelfHold then
                        v.StartPort.GetTag()
                    else
                        Expression.Zero

                let condition =
                    vs
                    |> Seq.map (fun v ->
                        /// 블럭 첫 job이 다시 돌아가지 못하게 하기 위함
                        let forBlockFirst = getMovedRelayExpressionForVertex opt wb id2RelayDic v
                        generateVertexIncomingEdgesExpression opt wb id2RelayDic v <&&> forBlockFirst <&&> v.StartPort.ConnectedExpression
                        )
                    |> Seq.reduce mkOr
                    |> teeExpressionComment "Condition:" comments

                let interlock =
                    [
                        if v.UseOutputInterlock then
                            yield v.ResetPort.GetTag() |> mkNeg // 출력 interlock

                        if v.UseOutputResetByWorkFinish then
                            yield generateOutputResetByFinish opt v   // 출력 완료 비접
                    ]
                    |> List.tryReduce mkAnd
                    |> Option.defaultValue Expression.Zero
                    |> teeExpressionComment "Interlock:" comments

                let coilOrigin = v.StartPort.GetCoil() |> Seq.head |> mkTerminal |> CoilOriginTypeExt.toCoilOrigin

                {
                    defaultExpressionInfo with
                        Start      = start
                        Set        = condition
                        Manual     = manual
                        Interlock  = interlock
                        CoilOrigin = coilOrigin
                        Selfhold   = selfHold
                        Comments   = comments |> List.ofSeq
                }
            )

        /// 모든 relay 에 대한 expressions
        let relayRungInfos =
            let relayOnOffConditionToExpression onOffCond =
                match onOffCond with
                | NodeIncomingCondition(v)     -> generateVertexIncomingEdgesExpression opt wb id2RelayDic v
                | NodeRawFinishCondition(v)    -> getVertexSensorExpression opt v
                | TrustedNodeStartCondition(v) ->
                    /// for block to block
                    let forLastMoved = getMovedRelayExpressionForRelay opt wb id2RelayDic v
                    /// 블럭 첫번쨰에 대한 릴레이가 다시 살지 못하게하는 moved릴레이 조건
                    let forFirstMoved =
                        if forLastMoved = Expression.Zero then getMovedRelayExpressionForVertex opt wb id2RelayDic v
                        else Expression.Zero

                    let incoming = generateVertexIncomingEdgesExpression opt wb id2RelayDic v |> remove (mkNeg forLastMoved)
                    let cause = getRelayCauseExpression wb v
                    /// cause : A+ B+ C+ A- B- C- D+ D- MODEL
                    let blockFirst =
                        getBlockFirstFromLast wb v
                        //|> Option.bind(fun v -> Some (v.getResetVertices() |> Seq.map(getVertexSensorExpression opt) |> Seq.reduce mkAnd))
                        |> Option.bind(fun v ->
                            if v.SensorPort.GetTag() = cause then
                                Expression.Zero
                            else
                                v.SensorPort.GetTag() |> mkNeg
                            |> Some)
                        |> Option.defaultValue Expression.Zero

                    let precedeResetTerget = generatePrecedeResetTargetExpression wb v
                    blockFirst <&&> incoming <&&> cause <&&> forLastMoved <&&> forFirstMoved <&&> precedeResetTerget
                | TrustedEdgeCondition(e)      ->
                    let incoming = generateVertexIncomingEdgesExpression opt wb id2RelayDic e.Target
                    let edge = generateEdgeExpression opt wb id2RelayDic false e
                    let sensor =
                        let s = getVertexSensorExpression opt (e.Source)
                        if e.Source.isSelfReset() then Expression.Zero
                        else if incoming = s then Expression.Zero
                        else s
                    edge <&&> sensor
                | NodeResetCondition(expr)     -> expr
                | ResetBlockCondition(v)       ->
                    match v.isSelfReset() with
                    | true ->
                        let output = getVertexOutputExpression opt v
                        let incoming = generateVertexIncomingEdgesExpression opt wb id2RelayDic v
                        let moved = getPrevMovedRelayExpressionForRelay opt wb id2RelayDic v
                        output <&&> incoming <&&> moved
                    | false ->
                        let sensor = getVertexSensorExpression opt v
                        let moved = getPrevMovedRelayExpressionForRelay opt wb id2RelayDic v
                        sensor <&&> moved


            let allRelays = id2RelayDic.Values |> List.ofSeq
            let nonTerminalRelays = allRelays |> List.except wb.TerminalRelays

            /// 모델 전체의 reset 명령
            let modelResetExpression =
                let v = wb.Model.DAG.Vertices |> Seq.head
                //let resets = v.Parent |> Option.bind(fun parent -> generateOutputInterlocks opt parent |> Some) |> Option.defaultValue Expression.Zero
                let resets = v.Parent |> Option.bind(fun parent -> parent.HomingPort.GetTag() |> Some) |> Option.defaultValue Expression.Zero

                resets

            id2RelayDic.Values
            |> List.ofSeq
            |> List.distinct
            |> List.map (fun r ->
                let comments = ResizeArray<string>()
                //sprintf "Location: %s" (r.RelayMarker.Location.ToText()) |> comments.Add
                sprintf "%s" (r.RelayMarker.ToTextHelper()) |> comments.Add

                let y = toCoil(r.Name)
                let isTerminal = wb.TerminalRelays.Contains r
                let sets   = [ relayOnOffConditionToExpression r.Set ] |> ResizeArray
                let resets = [ relayOnOffConditionToExpression r.Reset]|> ResizeArray
                //if isTerminal then
                //    /// Start X (DAG 내 모든 device 원위치 상태) X (모든 relay off) 를 terminal relay 의 Set 의 OR 조건으로 추가
                //    // 모든 relay 는 최초에 off 상태이므로, 첫 시작을 할 수 없다.
                //    // 시작 조건이 되면 맨 마지막 릴레이가 존재할 때 이를 살려서 최초 시작 node 의 task 가 실행되도록 한다.
                //    nonTerminalRelays
                //    |> List.map (fun r -> ~~ toCoil(r.Name))
                //    |> List.tryReduce mkAnd
                //    |> Option.defaultValue Expression.Zero
                //    |> teeExpressionComment "Terminal relay with nonterminals: " comments
                //    |> sets.Add

                //    modelResetExpression |> sets.Add
                //else
                modelResetExpression |> resets.Add

                let set   = sets |> Seq.reduce mkOr
                let reset = resets |> Seq.reduce mkOr
                { defaultExpressionInfo with Set = set; Reset = reset; Selfhold = y; CoilOrigin = Relay r; Comments = comments |> List.ofSeq }
            )

        let usedRelayRungInfos =
            /// 주어진 expression 의 terminals 모으기
            let getTerminals exprs = exprs |> Seq.collect (collectTerminals) |> Seq.map (fun t -> t.ToText())
            let relayNames = relayRungInfos |> Seq.map (fun rungInfo -> rungInfo.GetCoilName())
            let relayNamesUsed =
                let relaysUsedInVertex =
                    vertexRungInfos
                    |> Seq.map(fun info -> info.Set)
                    |> getTerminals
                    |> Seq.intersect relayNames

                let relaysUsedInRelays =
                    relayRungInfos
                    |> Seq.filter (fun rungInfo -> relaysUsedInVertex.Contains(rungInfo.GetCoilName()) )
                    |> Seq.map(fun info -> info.Set)
                    |> getTerminals
                    |> Seq.filter (fun r -> relayNames.Contains r)

                relaysUsedInVertex @@ relaysUsedInRelays
                |> Seq.distinct
                |> HashSet

            relayRungInfos
            |> List.filter (fun rungInfo -> relayNamesUsed.Contains(rungInfo.GetCoilName()))


        let modelComments = modelComments |> List.ofSeq
        let rungs = vertexRungInfos @ usedRelayRungInfos

        { PrologComments = modelComments; Rungs = rungs }


