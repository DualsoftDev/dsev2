namespace rec Dual.Ev2

open System.Linq
open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS


[<AutoOpen>]
module GraphModule =
    type CausalEdgeType =
        | Start
        | Reset
        | StartReset
        | SelfReset
        | Interlock

    type INamedVertex =
        inherit IVertex
        inherit INamed


    /// 이름 속성을 가진 추상 클래스
    [<AbstractClass>]
    type DsNamedObject(name: string) =
        [<JsonProperty(Order = -1)>]
        member val Name = name with get, set
        interface INamed with
            member x.Name with get() = x.Name and set(v) = x.Name <- v

    /// INamedVertex를 구현한 Vertex 추상 클래스
    [<AbstractClass>]
    type Vertex(name: string) =
        inherit DsNamedObject(name)
        interface INamedVertex



    /// Template class for DS Graph<'V, 'E>.   coppied from Engine.Common.TDGraph<>
    type TGraph<'V, 'E
            when 'V :> INamed and 'V : equality
            and 'E :> Ev2EdgeBase<'V> and 'E: equality> (
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


    [<AbstractClass>]
    type Ev2EdgeBase<'V>(source:'V, target:'V, edgeType:CausalEdgeType) =   // copied from Engine.Common.DsEdgeBase<>
        inherit EdgeBase<'V, CausalEdgeType>(source, target, edgeType)
        member _.EdgeType = edgeType

    type Edge private (source:Vertex, target:Vertex, edgeType:CausalEdgeType) =
        inherit Ev2EdgeBase<Vertex>(source, target, edgeType)
        member _.EdgeType = edgeType

        static member Create(graph:TGraph<_,_>, source, target, edgeType:CausalEdgeType) =
            let edge = Edge(source, target, edgeType)
            graph.AddEdge(edge) |> verifyM $"Duplicated edge [{source.Name}{edgeType}{target.Name}]"
            edge

        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"

    type DsGraph = TGraph<Vertex, Edge>


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





