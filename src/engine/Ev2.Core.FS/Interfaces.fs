namespace rec Dual.Ev2

open Dual.Common.Base.FS

open System.Text.Json.Serialization

open Engine.Common.GraphModule
open Newtonsoft.Json


[<AutoOpen>]
module GraphModule =
    /// <summary>
    /// IFlow 또는 IWork를 나타내는 인터페이스
    /// </summary>
    type IWithGraph =
        inherit IContainer

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


    type Edge private (source:Vertex, target:Vertex, edgeType:EdgeType) =
        inherit DsEdgeBase<Vertex>(source, target, edgeType)

        static member Create(graph:TDsGraph<_,_>, source, target, edgeType:EdgeType) =
            let edge = Edge(source, target, edgeType)
            graph.AddEdge(edge) //|> verifyM $"중복 edge [{source.Name}{edgeType.ToText()}{target.Name}]"
            edge

        //override x.ToString() = $"{x.Source.QualifiedName} {x.EdgeType.ToText()} {x.Target.QualifiedName}"

    type DsGraph = TDsGraph<Vertex, Edge>


    type EdgeDTO(source:string, target:string, edgeType:EdgeType) =
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




[<AutoOpen>]
module Interfaces =
    /// <summary>
    /// 내부에 IContainee 형식의 다른 요소를 포함할 수 있는 parent 역할을 수행
    /// </summary>
    type IContainer = interface end

    /// <summary>
    /// IContainer에 포함될 수 있는 요소의 인터페이스. child 역할을 수행
    /// </summary>
    type IContainee = interface end

    /// IContainer와 IContainee 역할을 모두 수행하는 인터페이스
    type IContain =
        inherit IContainer
        inherit IContainee


    /// 기본 객체 인터페이스
    type IDsObject = interface end

    /// IDsObject와 INamed를 구현한 인터페이스
    type IDsDsNamedObject =
        inherit IDsObject
        inherit INamed

    /// 시스템 인터페이스
    type ISystem =
        inherit IDsDsNamedObject
        inherit IContainer

    /// 흐름 인터페이스
    type IFlow =
        inherit IDsDsNamedObject
        inherit IContain

    /// 작업 인터페이스
    type IWork =
        inherit IDsDsNamedObject
        inherit IContain
        inherit INamedVertex

    /// 코인 인터페이스
    type ICoin =
        inherit IDsObject
        inherit INamedVertex
        inherit IContainee

    /// 호출 인터페이스
    type ICall =
        inherit ICoin

