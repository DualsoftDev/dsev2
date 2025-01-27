namespace Dual.Ev2

open System.Runtime.CompilerServices

type Ev2CSharpExtensions =
    [<Extension>] static member CsCreateEdge(work:DsWork, src:Vertex, dst:Vertex, edgeType:CausalEdgeType) = work.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsCreateEdge(work:DsWork, src:string, dst:string, edgeType:CausalEdgeType) = work.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsCreateEdge(flow:DsFlow, src:Vertex, dst:Vertex, edgeType:CausalEdgeType) = flow.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsCreateEdge(flow:DsFlow, src:string, dst:string, edgeType:CausalEdgeType) = flow.CreateEdge(src, dst, edgeType)

    [<Extension>] static member CsSerialize(dsSystem:DsSystem) = dsSystem.Serialize()

