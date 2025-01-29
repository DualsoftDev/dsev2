namespace rec Dual.Ev2

open System.Linq
open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System.Xml.Serialization

type CausalEdgeType =
    | Start
    | Reset
    | StartReset
    | SelfReset
    | Interlock
    | Group


/// 이름 속성을 가진 추상 클래스
[<AbstractClass>]
type DsNamedObject(name: string) =
    [<JsonProperty(Order = -1)>]
    member val Name = name with get, set
    interface INamed with
        member x.Name with get() = x.Name and set(v) = x.Name <- v



[<AutoOpen>]
module CoreGraphBase =

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



    /// DsGraph 의 edge type
    [<AbstractClass>]
    type EdgeBase<'V> internal (source:'V, target:'V, edgeType:CausalEdgeType) =   // copied from Engine.Common.DsEdgeBase<>
        inherit Dual.Common.Core.FS.GraphModule.EdgeBase<'V, CausalEdgeType>(source, target, edgeType)
        member _.EdgeType = edgeType
