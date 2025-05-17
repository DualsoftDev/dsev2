namespace Dual.Core.QGraph

open System.Collections.Generic
open FSharpPlus
open Dual.Common
open Dual.Common.Graph.QuickGraph
open Dual.Core
open Dual.Core.Types
open RelayMarkerPath
open QuickGraph
open Dual.Core.Types.Command

[<AutoOpen>]
module QgUtilModule2 =
    /// Memory Reset 관계.  dstName 으로 주어진 이름을 갖는 모든 node 에 대해서 reset 조건을 설정
    ///
    /// Device 의 출력 reset 관계는 device 레벨에서 정해 지지만,
    /// DAG 상에서의 임의의 vertex 의 reset 관계는 복수개의 vertex 의 조건으로 설정 가능해야 한다.
    /// 따라서 device 출력(name) 에 의한 reset 관계를 DAG 상의 vertex 간 reset 관계로 변환한다.
    let markResetsNameByName (g:DAG) dstName srcName =
        /// reset destination vertices
        let ds = g.FindNamedVertices(dstName)
        /// reset source vertices
        let ss = g.FindNamedVertices(srcName) |> HashSet
        for d in ds do
            let s =
                // destination d 에서 sources (ss)로 가는 경로
                getAllVPaths g (QgVertexObj d) (makeSelectorCreator (fun v -> ss.Contains(v)))
                // 자신과 동일한 d 를 한번 더 거치는 경로는 제외 : Non major path filtering
                // e.g 알록달록 + 모자의모자 + 다이아몬드 예제에서 다이아몬드의 A+ 에서 A- 까지의 경로 중
                // A+; B+; B-; A+; B+; A- 까지의 경로를 filter out
                |> Seq.filter(fun path -> path |> Seq.count(fun v -> v.Name = dstName) = 1)
                // .. 경로의 맨끝 -> source
                |> Seq.map Seq.tryLast
                |> Seq.choose id
                |> Seq.distinct
                |> Seq.exactlyOne

            // destination 의 reset 조건을 source 의 vertex 시작 시점으로 설정
            (d :?> IResetMutable).Reset <- ns(s)

    /// vertex에 해당하는 process result info를 찾아 tuple로
    let generateVertexProcInfoDict (procinfos:ProcessResultInfo list) (vertices:IVertex list) =
        vertices
        |> List.map(fun v ->
            /// proc
            ///let proc = procinfos |>  List.tryFind(fun proc -> proc.Workbook.Model.DAG.Vertices |> Seq.contains(v))
            /// inner proc 정보 탐색
            let innerInfo =
                procinfos
                |>  List.where(fun proc ->
                    let fv = proc.Workbook.Model.DAG.Vertices |> Seq.head
                    let result =
                        monad{
                            let! parent = fv.Parent
                            parent = v
                        }
                        |> Option.defaultValue false
                    result
                )
            (v, innerInfo)
        )
        |> List.distinctBy(fun (v, tu) -> v.Name)
        |> dict

    /// Model Status 생성
    let generateModelStatus (modelVertex:IVertex) (innerInfos:ProcessResultInfo list) (opt:CodeGenerationOption) (resetlocks:seq<IVertex * (PortCategory * RungInfo)>) (asyncInfos:(IVertex * Expression) list) =
        /// 옵션으로 코일생성
        let generateNameByOption (generator:(IVertex -> string) option) v =
            match generator with
            | Some(f) -> (f v) + "_Status"
            | _ -> v.ToText()
            |> toCoil
        let getResetLock portcate =
            let vResetLock = resetlocks |> Seq.where(fun kv -> (fst kv) = modelVertex) |> Seq.map(snd)
            vResetLock
            |> Seq.where(fun kv -> (fst kv) = portcate)
            |> Seq.map(fun kv -> (snd kv).GetCoilName() |> toCoil)
            |> Seq.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero

        /// 릴레이 off 확인
        let relaysoff =
            innerInfos
            |> Seq.collect(fun inf -> inf.Relays |> Seq.map(fun r -> toCoil(r.Name) |> mkNeg))
            |> Seq.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero

        /// 하위 초기상태
        let inits =
            modelVertex.DAGs
            |> Seq.collect(fun dag -> dag.Vertices)
            |> Seq.map(fun v -> v.GetInitialValue())
            |> Seq.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero


        /// 신뢰릴레이가 있으면 마지막 릴레이를
        /// 없으면 마지막 Job과 마지막 전 Job, Moved릴레이를
        /// Children End로 본다.
        let jobEnd =
            let edgeEqual (edge1:IEdge seq) (edge2:IEdge seq) =
                let findEdges = edge1 |> Seq.map(fun e1 -> edge2 |> Seq.tryFind(fun e2 -> e1.Source = e2.Source && e1.Target = e2.Target)) |> Seq.choose id
                edge1.isEmpty() |> not && edge1.length() = findEdges.length()

            let dagTerminalExpressions =
                modelVertex.DAGs
                |> Seq.map(fun dag ->
                    //let z1 = innerInfos |> Seq.tryFind(fun inf -> dag.Edges |> Seq.equal inf.Workbook.Model.DAG.Edges)
                    //let z2 = innerInfos |> Seq.tryFind(fun inf -> edgeEqual dag.Edges inf.Workbook.Model.DAG.Edges)


                    match innerInfos |> Seq.tryFind(fun inf -> edgeEqual dag.Edges inf.Workbook.Model.DAG.Edges) with
                    | Some(inf) ->
                        match inf.Workbook.TerminalRelays.isEmpty() with
                        | true ->
                            let wb = inf.Workbook
                            let id2Relay = inf.Workbook.Id2RelayDic
                            inf.Workbook.Model.DAG.GetTerminalVertices()
                            |> Seq.map(fun v ->
                                let edges = inf.Workbook.Model.DCG.GetOutgoingEdges(v)
                                let moved = edges |> Seq.map(getMovedRelayExpression opt wb id2Relay) |> Seq.reduce mkAnd
                                getVertexSensorExpression opt v <&&> generateVertexIncomingEdgesExpression opt wb id2Relay v  <&&> moved)
                            |> Seq.reduce mkAnd
                        | false ->
                            inf.Workbook.TerminalRelays |> Seq.map(fun r ->  toCoil(r.Name)) |> Seq.reduce mkAnd
                    /// inner info가 없는 경우는 DAG에 edge가 없고 Vertex만 존재하는 경우
                    | None ->
                        dag.Vertices |> Seq.map(fun v -> v.SensorPort.GetTag()) |> Seq.reduce mkAnd
                )

            match modelVertex with
            | :? QgSelect ->
                dagTerminalExpressions |> Seq.tryReduce mkOr
            | _ ->
                dagTerminalExpressions |> Seq.tryReduce mkAnd
            |> Option.defaultValue Expression.Zero

        /// dag parent condition
        let start = getVertexOutputExpression opt modelVertex
        let reset = generateOutputInterlocks opt modelVertex

        let forceFinish = generateNameByOption opt.ForceFinishNameGenerator modelVertex

        /// Ready Tag
        let t_r = generateNameByOption opt.StandbyStateNameGenerator modelVertex
        /// Going Tag
        let t_g = generateNameByOption opt.GoingStateNameGenerator modelVertex
        /// Finish Tag
        let t_f = generateNameByOption opt.SensorTagGenerator modelVertex
        /// Homing Tag
        let t_h = generateNameByOption opt.HomingStateNameGenerator modelVertex
        /// origin tag
        let t_o =
            if inits = Expression.Zero
            then Expression.Zero
            else
                generateNameByOption opt.OriginStateNameGenerator modelVertex

        let t_roff =
            if inits = Expression.Zero
            then Expression.Zero
            else
                sprintf "%A_RelayOff" modelVertex |> toCoil

        /// Runginfo
        let ready =
            {
                defaultExpressionInfo with
                    Set        = [t_h; t_roff; t_o] |> Seq.reduce mkAnd
                    Interlock  = mkAnd (mkNeg t_f) (mkNeg t_g)
                    CoilOrigin = t_r.toCoilOrigin()
                    Selfhold   = t_r
            }
        let going =
            {
                defaultExpressionInfo with
                    Set        = [(mkNeg reset); t_r; start] |> Seq.reduce mkAnd
                    Interlock  = mkNeg t_f
                    CoilOrigin = t_g.toCoilOrigin()
                    Selfhold   = t_g
            }
        let finish =
            let goinglock = mkOr (getResetLock PortCategory.Going) (getResetLock PortCategory.Start)
            {
                defaultExpressionInfo with
                    Set        = [(mkNeg goinglock); t_g; jobEnd] |> List.reduce mkAnd |> mkOr forceFinish
                    Interlock  = mkNeg t_h
                    CoilOrigin = t_f.toCoilOrigin()
                    Selfhold   = t_f
            }
        let homing =
            let finishlock = getResetLock PortCategory.Finish
            let asynclock =
                let vAsyncInfos = asyncInfos |> Seq.where(fun kv -> (fst kv) = modelVertex) |> Seq.map(snd)
                vAsyncInfos
                |> Seq.tryReduce mkAnd
                |> Option.defaultValue Expression.Zero
            let start =
                match modelVertex.Parent with
                | Some(_) -> start
                | None -> Expression.Zero
            {
                defaultExpressionInfo with
                    Set        = ([(mkNeg start); (mkNeg finishlock); (mkNeg asynclock); t_f; modelVertex.ResetPort.GetTag()] |> List.reduce mkAnd) |> mkOr (mkAnd (mkNeg t_f) (mkNeg t_g))
                    Interlock  = mkNeg t_r
                    CoilOrigin = t_h.toCoilOrigin()
                    Selfhold   = t_h
            }

        let statusToPort (status:Expression) (port:IPort) =
            port.GetCoil()
            |> Seq.map(fun c ->
                {
                    defaultExpressionInfo with
                        Set        = status
                        CoilOrigin = c |> mkTerminal |> RelayMarkerPath.CoilOriginTypeExt.toCoilOrigin
                }
            )


        let finishToSensor =
            modelVertex.SensorPort.GetCoil()
            |> Seq.map(fun c ->
                {
                    defaultExpressionInfo with
                        Set        = modelVertex.FinishPort.GetTag()
                        CoilOrigin = c |> mkTerminal |> RelayMarkerPath.CoilOriginTypeExt.toCoilOrigin
                }
            )


        let result =
            [
                yield homing
                yield finish
                yield going
                yield ready
                yield! statusToPort t_h modelVertex.HomingPort
                yield! statusToPort t_f modelVertex.FinishPort
                yield! statusToPort t_g modelVertex.GoingPort
                yield! statusToPort t_r modelVertex.ReadyPort
                yield! finishToSensor

                if t_o <> Expression.Zero then yield { defaultExpressionInfo with Set = inits; CoilOrigin = t_o.toCoilOrigin() }
                if t_roff <> Expression.Zero then yield { defaultExpressionInfo with Set = relaysoff; CoilOrigin = t_roff.toCoilOrigin() }
            ]

        result


    let processStatus procInfos opt resetlocks asyncInfos vs =
        let vProcDict = generateVertexProcInfoDict procInfos vs
        let status =
            let sinfo = vProcDict |> Seq.map(fun kv -> generateModelStatus kv.Key kv.Value opt resetlocks asyncInfos)
            sinfo |> Seq.flatten |> List.ofSeq //(sinfo |> Seq.collect(snd)) |> (@@) (sinfo |> Seq.collect(fst)) |> List.ofSeq

        status


    /// RelayLock에 대한 정보 생성
    let generateResetLockRelayRungInfo (opt:CodeGenerationOption) (vertices:IVertex seq) v =
        /// v를 reset으로 가지고있는 vertex들을 filtering
        let targets = vertices |> Seq.where(fun t -> t <> v && t.ResetPort.ConnectedPorts |> Seq.exists(fun p -> p.Parent = v))

        targets
        |> Seq.mapi(fun i t ->
            match opt.ResetLockRelayNameGenerator with
            | Some(f) ->
                monad{
                    let! setPort =  t.ResetPort.ConnectedPorts |> Seq.tryFind(fun p -> p.Parent = v)
                    let resetPort = t.HomingPort
                    let set = setPort.GetTag() |> getTerminal |> Option.bind(fun t -> t |> toPulse |> Some) |> Option.defaultValue Expression.Zero
                    let reset = resetPort.GetTag()
                    let goinglock = f v i |> toCoil

                    let rung = { defaultExpressionInfo with Set = set; Reset = reset; Selfhold = goinglock; CoilOrigin = goinglock.toCoilOrigin(); Comments = lempty }
                    let! kvPort = v.Ports |> Seq.tryFind(fun kv -> kv.Value = setPort)
                    let key = kvPort.Key

                    v, (key, rung)
                }
            | _ -> None
        )
        |> Seq.choose id

    /// Async Edge에 대한 릴레이와 Target RungInfo 생성
    /// Status생성에 비동기 릴레이가 필요하기 떄문에
    /// e를 key로하고 relay와 runginfo를 pair로 하는 결과를 만든다.
    let generateAsyncRungInfo (opt:CodeGenerationOption) (procinfos:ProcessResultInfo list) (segments:seq<ISegment>) (edges:IEdge seq) =
        let group = edges |> Seq.groupBy(fun e -> e.Target) |> Seq.map(fun (g, es) -> g, es(* |> Seq.map(fun e -> e.Source)*))

        group
        |> Seq.map(fun (target, edges) ->
            let asyncEdges = edges |> Seq.where(fun e -> e.EdgeType <> EdgeType.Global)
            let globalEdges = edges |> Seq.where(fun e -> e.EdgeType = EdgeType.Global)
            let asyncRelayInfo =
                asyncEdges
                |> Seq.map(fun e -> e.Source)
                |> Seq.map(fun source ->
                    let wb = procinfos |> Seq.tryFind(fun proc -> proc.Workbook.Model.DAG.Vertices |> Seq.contains(source))
                    let relay = sprintf "AR_%s_%s" (source.ToText()) (target.ToText()) |> toCoil
                    let relayinfo =
                        let set =
                            match wb with
                            | Some(wb) -> generateVertexIncomingEdgesExpression opt wb.Workbook wb.Workbook.Id2RelayDic source
                            | None -> Expression.Zero
                            |> mkAnd (source.SensorPort.GetTag())
                        let reset =
                            match target |> IVertexExt.GetSegmentType segments  with
                            | External -> target.StartPort.GetTag()
                            | Internal -> target.GoingPort.GetTag()

                        let fRisingFunc = CoilOutput.PulseCoilMode(Prelude.Coil(source.SensorPort.GetTag().ToText()+"_AsyncTemp")) :> IFunctionCommand
                        let gRisingFunc = CoilOutput.PulseCoilMode(Prelude.Coil(reset.ToText()+"_AsyncTemp"))  :> IFunctionCommand

                        /// Async Rungs
                        [
                            { defaultExpressionInfo with Set = set; CoilOrigin = fRisingFunc.fromPLCFunction(); Comments = lempty }
                            { defaultExpressionInfo with Set = reset; CoilOrigin = gRisingFunc.fromPLCFunction(); Comments = lempty }
                            { defaultExpressionInfo with Set = fRisingFunc.TerminalEndTag |> mkTerminal; Reset = gRisingFunc.TerminalEndTag |> mkTerminal; Selfhold = relay; CoilOrigin = relay.toCoilOrigin(); Comments = lempty }
                        ]

                    (source, relay), relayinfo
                )

            let relayinfo = asyncRelayInfo |> Seq.map(fst)
            let runginfo = asyncRelayInfo |> Seq.collect(snd)

            let targetinfo =
                let externals =
                    globalEdges |> Seq.map(fun e -> e.Source)
                    |> Seq.map(fun v -> v.SensorPort.GetTag())
                let coil = target.StartPort.GetCoil() |> Seq.head |> mkTerminal
                let self =
                    if target.UseSelfHold then coil else Expression.Zero
                let interlock =
                    [
                        if target.UseOutputInterlock then
                            yield target.ResetPort.GetTag() |> mkNeg // 출력 interlock

                        if target.UseOutputResetByWorkFinish then
                            yield generateOutputResetByFinish opt target   // 출력 완료 비접
                    ]
                    |> List.tryReduce mkAnd
                    |> Option.defaultValue Expression.Zero
                let set = (relayinfo |> Seq.map(snd)) @@ externals |> Seq.reduce mkAnd <&&> target.StartPort.ConnectedExpression

                /// Async relay - > Tag
                { defaultExpressionInfo with Set = set; Selfhold = self; Interlock = interlock; CoilOrigin = coil.toCoilOrigin(); Comments = lempty }

            relayinfo, runginfo @@ [targetinfo]
        )



    /// 포트 Tag와 function간의 Rung 생성
    /// IN Port : Dummy - (Func)  or  Dummy - (Tag)
    ///           Func.F - (Tag)
    ///
    /// Out Port : Tag - (Func)  or  Tag - (Dummy)
    ///            Func.F - (Dummy)
    let generatePortFunctionRungInfo (modelInfo:ProcessResultInfo list) (opt:CodeGenerationOption) (v:IVertex)  =
        let generateFunctionRungs (p:IPort) =
            match p.PLCFunctions.any() with
            | true ->
                let procInfo = modelInfo |> Seq.tryFind(fun pi -> pi.Workbook.Model.DAG.Vertices.Contains(v))
                let set =
                    let cause = p.GetCoil() |> Seq.map(mkTerminal) |> Seq.reduce mkAnd
                    match procInfo with
                    | Some(pi) ->
                        let incoming = generateVertexIncomingEdgesExpression opt pi.Workbook pi.Workbook.Id2RelayDic v
                        incoming <&&> cause
                    | None -> cause

                /// function 실행 rung
                let funcRungs =
                    p.PLCFunctions |> Seq.map(fun func ->
                        let isCompare (func:IFunctionCommand) =
                            match func with
                            | :? FunctionPure as pureFunc ->
                                match pureFunc with
                                | CompareGT(_) | CompareEQ(_) | CompareGE(_) | CompareLE(_) | CompareLT(_) | CompareNE(_) -> true
                                | _ -> false
                            | _ -> false
                        match isCompare func with
                        | true ->
                                { defaultExpressionInfo with Set = Expression.Zero; CoilOrigin = func.fromPLCFunction(); Comments = lempty }
                        | false ->
                                { defaultExpressionInfo with Set = set; CoilOrigin = func.fromPLCFunction(); Comments = lempty }
                    )

                /// function 결과를 조건으로 port tag를 살리는 rung
                let terminalRung =
                    let funcTerminals = p.PLCFunctions |> Seq.map(fun f -> f.TerminalEndTag |> mkTerminal) |> Seq.reduce mkAnd
                    let tagTerminal = p.GetTag()
                    { defaultExpressionInfo with Set = funcTerminals (*<||> tagTerminal*) <&&> set; CoilOrigin = tagTerminal.toCoilOrigin(); Comments = lempty }

                funcRungs @@ [terminalRung]
                |> List.ofSeq
            | false -> []
                //[
                //     { defaultExpressionInfo with Set = p.GetCoil |> mkTerminal; CoilOrigin = p.GetTerminal.toCoilOrigin(); Comments = lempty }
                //]

        v.Ports |> Seq.collect(fun kv -> generateFunctionRungs kv.Value)

