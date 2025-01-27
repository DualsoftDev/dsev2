namespace rec Dual.Ev2

open System.Linq
open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices


[<AutoOpen>]
module Core =
    type CausalEdgeType =
        | Start
        | Reset
        | StartReset
        | SelfReset
        | Interlock

    type DsSystem(name:string) =
        inherit DsNamedObject(name)
        interface ISystem
        [<JsonProperty(Order = 2)>]
        member val Flows = ResizeArray<DsFlow>() with get, set

    type DsFlow(system:DsSystem, name:string) =
        inherit DsNamedObject(name)
        interface IFlow
                
        [<JsonIgnore>] member val System = system with get, set
        [<JsonIgnore>] member val Graph = DsGraph()
        //[<JsonProperty(Order = 2)>] member val Works = ResizeArray<DsWork>() with get, set
        [<JsonProperty(Order = 3)>] member val Vertices = ResizeArray<VertexDetail>() with get, set
        [<JsonProperty(Order = 4)>] member val GraphDTO = getNull<GraphDTO>() with get, set

    type DsWork(flow:DsFlow, name:string) =
        inherit Vertex(name, VCFlow flow)
        interface IWork

        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 2)>] member val Vertices = ResizeArray<VertexDetail>() with get, set
        [<JsonProperty(Order = 3)>] member val GraphDTO = getNull<GraphDTO>() with get, set

    //type CoinType =
    //    | Undefined
    //    | Action
    //    | Command
    //    | Operator

    //type DsCoin(parent:VertexContainer, name:string) =
    //    inherit Vertex(name)
    //    interface ICoin

    //    [<JsonProperty(Order = 2)>] member val CoinType = CoinType.Undefined with get, set

    type DsAction(name:string) =
        inherit Vertex(name)

    type DsCommand(name:string) =
        inherit Vertex(name)

    type DsOperator(name:string) =
        inherit Vertex(name)




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

    type DsFlow with
        member x.CreateWork(workName:string) = 
            if x.Vertices.Exists(fun w -> w.AsVertex().Name = workName) then
                getNull<DsWork>();
            else
                let w = DsWork(x, workName)
                x.Vertices.Add (VDWork w)
                x.Graph.AddVertex w |> ignore
                w

        [<JsonIgnore>] member x.Works = x.Vertices.Map(_.AsVertex()).OfType<DsWork>().ToArray()
        member x.AddVertex<'V when 'V :> Vertex>(vertex:'V) =
            if vertex.Container <> VCNone then
                failwith "ERROR: Vertex already has parent container"
            if x.Graph.AddVertex vertex then
                failwith "ERROR: Failed to add.  duplicated?"
            vertex

    type DsWork with
        member x.Flow = match x.Container with | VCFlow f -> f | _ -> getNull<DsFlow>()
        member x.AddVertex<'V when 'V :> Vertex>(vertex:'V) =
            if vertex.Container <> VCNone then
                failwith "ERROR: Vertex already has parent container"
            if !! x.Graph.AddVertex(vertex) then
                failwith "ERROR: Failed to add.  duplicated?"
            VertexDetail.FromVertex(vertex) |> x.Vertices.Add
            vertex
                

    //    member x.CreateAction(name:string):   DsCoin = tryCreateCoin(VCFlow x, name, fun () -> DsAction(name))   |? getNull<DsCoin>()
    //    member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(VCFlow x, name, fun () -> DsCommand(name))  |? getNull<DsCoin>()
    //    member x.CreateOperator(name:string): DsCoin = tryCreateCoin(VCFlow x, name, fun () -> DsOperator(name)) |? getNull<DsCoin>()

    //type DsWork with
    //    member x.CreateAction(name:string):   DsCoin = tryCreateCoin(VCWork x, name, CoinType.Action)   |? getNull<DsCoin>()
    //    member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(VCWork x, name, CoinType.Command)  |? getNull<DsCoin>()
    //    member x.CreateOperator(name:string): DsCoin = tryCreateCoin(VCWork x, name, CoinType.Operator) |? getNull<DsCoin>()
    //    member x.CreateEdge(src:Vertex, dst:Vertex, edgeType:CausalEdgeType) = x.Graph.CreateEdge(src, dst, edgeType)
    //    member x.CreateEdge(src:string, dst:string, edgeType:CausalEdgeType) = x.Graph.CreateEdge(src, dst, edgeType)


    //type VertexContainer with
    //    member x.GetGraph(): DsGraph =
    //        match x with
    //        | VCFlow flow -> flow.Graph
    //        | VCWork work -> work.Graph
    //        | _ -> failwith "ERROR"

    //    member x.GetCoins(): ResizeArray<DsCoin> =
    //        match x with
    //        | VCFlow flow -> flow.Coins
    //        | VCWork work -> work.Coins
    //        | _ -> failwith "ERROR"


    //let private tryCreateCoin(x:VertexContainer, coinName:string, coin:Vertex) =
    //    let coins, graph = x.GetCoins(), x.GetGraph()

    //    if coins.Exists(fun w -> (w :> INamed).Name = coinName) then
    //        None
    //    else
    //        let c = DsCoin(x, coinName)
    //        coins.Add c
    //        c.Container <- x
    //        c.CoinType <- coinType
    //        graph.AddVertex(c) |> verify
    //        Some c



[<AutoOpen>]
module CoreGraph =

    /// 이름 속성을 가진 추상 클래스
    [<AbstractClass>]
    type DsNamedObject(name: string) =
        [<JsonProperty(Order = -1)>]
        member val Name = name with get, set
        interface INamed with
            member x.Name with get() = x.Name and set(v) = x.Name <- v

    /// Template class for DS Graph<'V, 'E>.   coppied from Engine.Common.TDGraph<>
    type TGraph<'V, 'E
            when 'V :> INamed and 'V : equality
            and 'E :> EdgeBase<'V> and 'E: equality> (
            vertices_:'V seq,
            edges_:'E seq,
            vertexHandlers:GraphVertexAddRemoveHandlers option) =
        inherit Graph<'V, 'E>(vertices_, edges_, vertexHandlers)

        let isStartEdge (e:'E) = e.Edge = CausalEdgeType.Start

        new () = TGraph<'V, 'E>(Seq.empty<'V>, Seq.empty<'E>, None)
        new (vs, es) = TGraph<'V, 'E>(vs, es, None)
        new (vertexHandlers:GraphVertexAddRemoveHandlers option) = TGraph<'V, 'E>([], [], vertexHandlers)

        member x.GetIncomingVerticesWithEdgeType(vertex:'V, f: 'E -> bool) =
            x.GetIncomingEdges(vertex)
                .Where(f)
                .Select(fun e -> e.Source)

        member x.GetOutgoingVertices(vertex:'V) = x.GetOutgoingEdges(vertex).Select(fun e -> e.Target)

        member x.GetOutgoingVerticesWithEdgeType(vertex:'V, f: 'E -> bool) =
            x.GetOutgoingEdges(vertex)
                .Where(f)
                .Select(fun e -> e.Target)

        override x.Inits =
            let inits =
                x.Edges
                    .Select(fun e -> e.Source)
                    .Where(fun src -> not <| x.GetIncomingVerticesWithEdgeType(src, isStartEdge).Any())
                    .Distinct()
            x.Islands @ inits

        override x.Lasts =
            let lasts =
                x.Edges
                    .Select(fun e -> e.Target)
                    .Where(fun tgt -> not <| x.GetOutgoingVerticesWithEdgeType(tgt, isStartEdge).Any())
                    .Distinct()
            x.Islands @ lasts


    type DsGraph = TGraph<Vertex, Edge>

    [<AbstractClass>]
    type EdgeBase<'V>(source:'V, target:'V, edgeType:CausalEdgeType) =   // copied from Engine.Common.DsEdgeBase<>
        inherit EdgeBase<'V, CausalEdgeType>(source, target, edgeType)
        member _.EdgeType = edgeType

    type Edge internal (source:Vertex, target:Vertex, edgeType:CausalEdgeType) =
        inherit EdgeBase<Vertex>(source, target, edgeType)
        member _.EdgeType = edgeType


        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"



    type EdgeDTO(source:string, target:string, edgeType:CausalEdgeType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val EdgeType = edgeType with get, set

    type GraphDTO(vertices:string seq, edges:EdgeDTO seq) =
        member val Vertices = vertices |> ResizeArray with get, set
        member val Edges = edges |> ResizeArray with get, set
        static member FromGraph(graph:DsGraph) =
            let vs = graph.Vertices.Map(_.Name)
            let es = graph.Edges.Map(fun e -> EdgeDTO(e.Source.Name, e.Target.Name, e.EdgeType))
            GraphDTO(vs, es)

    /// Vertex 의 parent 구분용 type
    type VertexContainer =
        | VCNone
        | VCFlow of DsFlow
        | VCWork of DsWork

    /// Vertex 의 Polymorphic types
    type VertexDetail =
        | VDWork of DsWork
        | VDAction of DsAction
        | VDCommand of DsCommand
        | VDOperator of DsOperator
        with
            member x.AsVertex():Vertex =
                match x with
                | VDWork     w -> w :> Vertex
                | VDAction   a -> a :> Vertex
                | VDCommand  c -> c :> Vertex
                | VDOperator o -> o :> Vertex
            static member FromVertex(v:Vertex) =
                match v with
                | :? DsWork     as w -> VDWork w
                | :? DsAction   as a -> VDAction a
                | :? DsCommand  as c -> VDCommand c
                | :? DsOperator as o -> VDOperator o
                | _ -> failwith "ERROR"

    /// INamedVertex를 구현한 Vertex 추상 클래스
    [<AbstractClass>]
    type Vertex(name: string, ?container:VertexContainer) =
        inherit DsNamedObject(name)
        interface INamedVertex
        [<JsonIgnore>] member val Container = container |? VCNone with get, set


    type GraphExtension =
        [<Extension>]
        static member CreateEdge(graph:DsGraph, src:Vertex, dst:Vertex, edgeType:CausalEdgeType): Edge =
            Edge(src, dst, edgeType)
            |> tee(fun e ->
                graph.AddEdge(e) |> verifyM $"Duplicated edge [{src.Name}{edgeType}{dst.Name}]" )

        [<Extension>]
        static member CreateEdge(graph:DsGraph, src:string, dst:string, edgeType:CausalEdgeType): Edge =
            let s, e = graph.FindVertex(src), graph.FindVertex(dst)
            graph.CreateEdge(s, e, edgeType)


        [<Extension>] static member HasVertexWithName(graph:DsGraph, name:string) = graph.Vertices.Any(fun v -> v.Name = name)






