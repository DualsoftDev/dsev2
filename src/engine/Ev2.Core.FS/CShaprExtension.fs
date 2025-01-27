namespace Dual.Ev2

open System.Runtime.CompilerServices

type Ev2CSharpExtensions =
    [<Extension>] static member CsCreateEdge(graphOwner:IWithGraph, src:Vertex, dst:Vertex, edgeType:CausalEdgeType) = graphOwner.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsSerialize(dsSystem:DsSystem) = dsSystem.Serialize()

