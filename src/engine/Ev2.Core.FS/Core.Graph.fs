namespace rec Dual.Ev2

open System.Linq
open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.CompilerServices
open System.Xml.Serialization
open System

type CausalEdgeType =
    | Start
    | Reset
    | StartReset
    | SelfReset
    | Interlock
    | Group


/// 이름 속성을 가진 추상 클래스
[<AbstractClass>]
type NamedObject(name: string) =
    [<JsonProperty(Order = -100)>] member val Name = name with get, set
    interface INamed with
        member x.Name with get() = x.Name and set(v) = x.Name <- v

[<AbstractClass>]
type GuidObject(?guid:Guid) =
    interface IGuid with
        member x.Guid with get () = x.Guid and set v = x.Guid <- v
    [<JsonProperty(Order = -99)>] member val Guid = guid |?? (fun () -> Guid.NewGuid()) with get, set

[<AbstractClass>]
type NamedGuidObject(name: string, ?guid:Guid) =
    inherit GuidObject(?guid=guid)
    [<JsonProperty(Order = -100)>] member val Name = name with get, set
    interface INamed with
        member x.Name with get() = x.Name and set(v) = x.Name <- v


type GuidVertex(content:GuidObject, ?vertexGuid:Guid) =  // content: 실체는 DsItem
    inherit GuidObject(?guid=vertexGuid)

    interface IVertexKey with
        member x.VertexKey with get() = x.Guid.ToString() and set(v) = x.Guid <- Guid(v)
    member internal x.ContentImpl = content

type GuidEdge internal (source:GuidVertex, target:GuidVertex, edgeType:CausalEdgeType) =
    inherit EdgeBase<GuidVertex>(source, target, edgeType)
    //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"

type GuidVertex with    // Content
    [<JsonIgnore>] member internal x.Content = x.ContentImpl |> box :?> DsItem
    [<JsonIgnore>] member internal x.Name = x.Content.Name


[<AutoOpen>]
module CoreGraphBase =

    /// Template class for DS Graph<'V, 'E>.   coppied from Engine.Common.TDGraph<>
    type TGraph<'V, 'E
            when 'V :> IVertexKey and 'V : equality
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



    /// DsGraphObsolete 의 edge type
    [<AbstractClass>]
    type EdgeBase<'V> internal (source:'V, target:'V, edgeType:CausalEdgeType) =   // copied from Engine.Common.DsEdgeBase<>
        inherit Dual.Common.Core.FS.GraphModule.EdgeBase<'V, CausalEdgeType>(source, target, edgeType)
        member _.EdgeType = edgeType



[<AutoOpen>]
module CoreProlog =
    let internal getContainer (container:DsItem option): DsItem =
        match container with
        | None -> getNull<DsItem>()
        | Some c when isItNull(c) -> getNull<DsItem>()
        | Some c -> c

    type DsItem(name:string, ?container:DsItem) =
        inherit NamedGuidObject(name, Guid.NewGuid())
        [<JsonIgnore>] member val Container = getContainer container with get, set


    type DsGraph = TGraph<GuidVertex, GuidEdge>

    /// Edge 구조 serialization 용도.
    type EdgeDTO(source:Guid, target:Guid, edgeType:CausalEdgeType) =
        member val Source = source with get, set
        member val Target = target with get, set
        member val EdgeType = edgeType with get, set
        static member FromGraph(graph:DsGraph): EdgeDTO[] =
            let es = graph.Edges.Map(fun e -> EdgeDTO(e.Source.Guid, e.Target.Guid, e.EdgeType)).ToArray()
            es


    type VertexDTO = {
        //mutable Name:string
        mutable Guid:Guid
        mutable ContentGuid:Guid
    } with
        static member FromGraph(graph:DsGraph): VertexDTO[] =
            let vs = graph.Vertices.Map(fun v -> { Guid = v.Guid; ContentGuid = v.Content.Guid }).ToArray()
            vs

    type DsItemWithGraph(name:string, ?container:DsItem) =
        inherit DsItem(name, ?container=container)
        interface IGraph

        [<JsonIgnore>] member val Graph = TGraph<GuidVertex, GuidEdge>()

        [<JsonIgnore>] member x.Vertices = x.Graph.Vertices
        [<JsonIgnore>] member x.Edges = x.Graph.Edges

        [<JsonProperty(Order = 3)>] member val VertexDTOs:VertexDTO[] = [||] with get, set
        [<JsonProperty(Order = 4)>] member val EdgeDTOs:EdgeDTO[] = [||] with get, set

