namespace Dual.Core.QGraph


open System.Collections.Generic
open QuickGraph
open FSharpPlus
open Dual.Common
open Dual.Common.Graph.QuickGraph

//[<AutoOpen>]
module ModelVaidation =
    type ModelValidationError<'V, 'E> =
        /// 동떨어진 vertex 하나만 존재하는 것들
        | IsolatedNodes of 'V list
        /// 자기 자신으로 가는 edge 가 존재하는 경우
        | SelfEdgedNodes of 'V list
        /// history('V list) 를 따라오면서, edge('E) 를 따라 갈 때에 cycle 발생함.
        | CyclicBackEdge of {|BackEdge:'E; History:'V list |}
        /// Major path 이외의 경로에서 device 상태 변경이 발생하는 경우
        /// e.g "a" device 관점에서 볼 때에 s -> <a0> -> b0 -> <a1> -> b1 -> <a2> -> e 와 같이 a 에 대한 major path 가 존재하고,
        /// b0 -> c -> <a3> -> e 등과 같이 a 에 대한 major path 가 아닌 곳에서 a 에 대한 변경이 발생하는 경우를 detect.
        /// NonMajors = [a3], NonMajorPath=[b0; c; a3; e], MajorPath=[s; a0; b0; a1; b1; a2; e]
        | NonMajorPathChange of {|NonMajors:'V list; NonMajorPath:'V list; MajorPath:'V list |}
        /// 모델 구성하는 vertex 의 일부에만 original vertex 가 존재하는 경우.  (모두 original vertex 가 있거나, 모두 없거나 해야 하는데, 일부에만 존재)
        | AllOrNoOriginalVertex of {|WithOriginalVertex:'V list; WithoutOriginalVertex:'V list|}
        | DuplicatedOutputDeviceMap of string
        /// Sensor vertex 는 DAG 상에서 incoming edge 가 없어야 한다.
        | SensorWithIncomingEdges of 'V list

    /// graph g 상에서 {모든 initial nodes} 에서 {모든 terminal nodes} 로의 경로를 반환
    let getAllInitialsToTerminalsPaths (g:AdjacencyGraph<'V, 'E>) =
        let initials = getInitialNodes g
        let terminals = getTerminalNodes g
        [
            for i in initials do
                for t in terminals do
                    yield! getAllPaths g i t
        ]

    /// g 상의 모든 device 에 대해서 major path 를 구하고, major path 이외의 path 에서 변경이 발생한 지점을 검색
    /// major path : 특정 device 의 상태를 바꾸는 path 중, 그 device 의 상태를 가장 많이 바꾸는 path
    /// - major path 는 유일하게 존재하여야 하고, non major path 에서 해당 device 의 상태를 바꿀 때,
    ///   그 상태는 major path 에 포함된 상태이어야 한다.
    let validateMajorPaths (g:AdjacencyGraph<'V, 'E>) (outputDeviceMap:OutputDeviceMap) =
        /// [path] : 그래프 상의 시작 -> 끝 의 모든 경로.  path = [ node * node 의 device id ]
        let allPaths : ('V * DeviceId) list list =
            getAllInitialsToTerminalsPaths g
            |> List.map (List.map (fun (n:'V) ->
                    //tracefn "Searching %A" n
                    let devId = fst outputDeviceMap.[n.ToString()]
                    n, devId))

        /// graph 상에 존재하는 모든 node 들이 속하는 device 의 id
        let allDevicesIds =
            let names = g.Vertices |> Seq.map (fun n -> n.ToString()) |> HashSet
            outputDeviceMap
            |> Seq.map Tuple.ofKeyValuePair
            |> Seq.filter (fst >> names.Contains)
            |> Seq.map (fun (k, (devId, outputId)) -> devId)
            |> Seq.distinct

        /// 각 device id 별 major path 의 dicitonary
        let majorPaths =
            allDevicesIds
            |> Seq.map (fun id ->
                /// major path : 모든 경로 중에서 id 를 가장 많이 포함하는 경로
                let mpath =
                    allPaths    // [['V * DeviceId]]
                    |> List.maxBy (
                        List.map snd    // [DeviceId]
                        >> List.filter ((=) id)
                        >> List.length)
                id, mpath)
            |> dict |> Dictionary

        // non major path 상에 major path 에 없는 device 상태가 존재하는 것들을 추려서 반환
        [
            for devId in allDevicesIds do
                let mpath = majorPaths.[devId]
                let otherPaths = allPaths |> List.filter (fun p -> p <> mpath)
                /// major path 상에 존재하는 major device 의 node 들만 선택
                let nodesOnMajorPath = mpath |> List.filter (snd >> ((=) devId)) |> List.map fst |> HashSet

                yield!
                    otherPaths
                    |> List.map (fun oPath ->
                        /// non major path 상에 존재하는 major device 의 state 중에 major path 에 존재하지 않는 node 들
                        let nonMajors =
                            oPath
                            |> List.filter(fun (n, id) -> id = devId && not <| nodesOnMajorPath.Contains(n))
                            |> List.map fst
                        oPath, nonMajors)
                    |> List.filter (snd >> Seq.any)
                    |> List.map (fun (otherPath, nonMajors) ->
                        NonMajorPathChange
                            {|
                                NonMajors = nonMajors
                                NonMajorPath = otherPath |> List.map fst
                                MajorPath = mpath |> List.map fst |})
        ]



    /// 모델 validation : graph g 는 원본 DAG 형태
    //let validateModelDAG (model:QgModel) outputDeviceMap =
    let validateModel (model:QgModel) =
        let g = model.DAG
        let validateModelDAGHelper () =
            let vs = g.Vertices |> List.ofSeq
            let es = g.Edges |> List.ofSeq
            seq {
                // 연결 없는 vertex 허용 안함
                let islands = vs |> List.filter(fun v -> g.InDegree v = 0 && g.OutDegree v = 0)
                if islands.any() then
                    yield IsolatedNodes(islands)

                // self edge 허용 안함
                let selfEdgedNodes = vs |> List.filter(fun v -> g.GetOutgoingVertices(v) |> Seq.contains(v))
                if selfEdgedNodes.any() then
                    yield SelfEdgedNodes(selfEdgedNodes)


                // cycle 존재 유무 검사
                let backedges = findBackEdges g |> Seq.map(fun (e, path) -> CyclicBackEdge{| BackEdge=e; History=path |}) |> Array.ofSeq
                yield! backedges

                // sensor vertex 는 incoming edge 가 없어야 한다.
                let sensors = model.Sensors |> HashSet
                let invalidSensors =
                    es
                    |> List.filter(fun e -> sensors.Contains(e.Target.Name))
                    |> List.map(fun e -> e.Target)
                if (invalidSensors.any()) then
                    yield SensorWithIncomingEdges(invalidSensors)

                // todo : major path 검사
                //yield! validateMajorPaths g outputDeviceMap
            }

        let firstError = validateModelDAGHelper () |> Seq.tryHead
        firstError |> Option.iter (logError "MODEL ERROR: %A")
        firstError

    /// terminal nodes --> initial nodes 로의 fake edges (FakeEdge) 를 추가한 상태에서의 모델 검증
    let validateModelDCG (dcg:AdjacencyGraph<'V, 'E>) (outputDeviceMap:OutputDeviceMap) =
        ()



