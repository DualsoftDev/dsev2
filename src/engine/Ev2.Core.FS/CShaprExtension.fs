namespace Dual.Ev2

open System.Runtime.CompilerServices

type Ev2CSharpExtensions =
    [<Extension>] static member CsCreateEdge(work:DsWork, src:Vertex, dst:Vertex, edgeType:CausalEdgeType) = work.Graph.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsSerialize(dsSystem:DsSystem) = dsSystem.Serialize()

