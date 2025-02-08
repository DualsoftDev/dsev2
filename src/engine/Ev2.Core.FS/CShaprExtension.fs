namespace Dual.Ev2

open System.Runtime.CompilerServices

type Ev2CSharpExtensions =
    [<Extension>] static member CsCreateEdge(work:DsItemWithGraph, src:GuidVertex, dst:GuidVertex, edgeType:CausalEdgeType) = work.CreateEdge(src, dst, edgeType)
    //[<Extension>] static member CsCreateEdge(work:DsWork, src:DsAction, dst:DsAction, edgeType:CausalEdgeType) = work.CreateEdge(src, dst, edgeType)
    //[<Extension>] static member CsCreateEdge(work:DsWork, src:string, dst:string, edgeType:CausalEdgeType) = work.CreateEdge(src, dst, edgeType)
    //[<Extension>] static member CsCreateEdge(flow:DsFlow, src:DsWork, dst:DsWork, edgeType:CausalEdgeType) = flow.CreateEdge(src, dst, edgeType)
    //[<Extension>] static member CsCreateEdge(flow:DsFlow, src:string, dst:string, edgeType:CausalEdgeType) = flow.CreateEdge(src, dst, edgeType)

    [<Extension>] static member CsSerialize(dsSystem:DsSystem) = dsSystem.ToJson()

    [<Extension>] static member CsAddWork(flow:DsFlow, workName:string) = flow.AddWork(workName)
