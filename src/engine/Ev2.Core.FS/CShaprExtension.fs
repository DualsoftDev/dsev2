namespace Dual.Ev2

open System.Runtime.CompilerServices
open Engine.Common

type Ev2CSharpExtensions =
    [<Extension>] static member CsCreateEdge(graphOwner:IWithGraph, src:Vertex, dst:Vertex, edgeType:EdgeType) = graphOwner.CreateEdge(src, dst, edgeType)
    [<Extension>] static member CsSerialize(dsSystem:DsSystem) = dsSystem.Serialize()

