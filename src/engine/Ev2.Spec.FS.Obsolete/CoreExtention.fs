namespace Dual.EV2.CoreExtension

open Dual.EV2.Core

[<AutoOpen>]
module Extensions =

    type Call with
        member this.AddAutoPre(cond: string) =
            this.Param.AutoPreConditions.Add(cond)
        member this.AddSafety(cond: string) =
            this.Param.SafetyConditions.Add(cond)

    type Work with
        member this.AddCall(call: Call) =
            this.Calls.Add(call)
        member this.AddCallEdge(source: Call, target: Call) =
            this.CallGraph.Add(source, target)

    
    type System with
        member this.AddFlow(flow: Flow) =
            this.Flows.Add(flow)
        member this.AddWorkEdge(w1: Work, w2: Work) =
            this.WorkGraph.Add(w1, w2)

    type Project with
        member this.AddSystem(system: System) =
            this.Systems.Add(system)
        member this.AddTargetSystem(id: string) =
            this.TargetSystemIds.Add(id)
