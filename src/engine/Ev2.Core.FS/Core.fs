namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Base.CS
open Dual.Common.Core
open Dual.Common.Core.FS
open System.Collections.Generic

open System.Text.Json.Serialization

open Engine.Common.GraphModule
open Newtonsoft.Json


[<AutoOpen>]
module rec Core =
    type DsSystem(name:string) =
        inherit DsNamedObject(name)
        interface ISystem
        [<JsonProperty(Order = 2)>]
        member val Flows = ResizeArray<DsFlow>() with get, set

    type DsFlow(system:DsSystem, name:string) =
        inherit DsNamedObject(name)
        interface IFlow
        interface IWithGraph
                
        [<JsonIgnore>] member val System = system with get, set
        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 2)>] member val Works = ResizeArray<DsWork>() with get, set
        [<JsonProperty(Order = 3)>] member val Coins = ResizeArray<DsCoin>() with get, set

    type DsWork(flow:DsFlow, name:string) =
        inherit Vertex(name)
        interface IWork
        interface IWithGraph

        [<JsonIgnore>] member val Flow = flow with get, set
        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 2)>] member val Coins = ResizeArray<DsCoin>() with get, set

    type DsCoin(parent:IWithGraph, name:string) =
        inherit Vertex(name)
        interface IWork
        interface IWithGraph

        [<JsonIgnore>] member val Parent:IWithGraph = getNull<IWithGraph>() with get, set
        [<JsonProperty(Order = 2)>] member val CoinType = "" with get, set  // 임시








    type DsSystem with
        static member Create(name:string) = new DsSystem(name)
        member x.CreateFlow(flowName:string) = 
            if x.Flows.Exists(fun f -> (f :> INamed).Name = flowName) then
                getNull<DsFlow>();
            else
                DsFlow(x, flowName).Tee(fun f -> x.Flows.Add f);
    type DsFlow with
        member x.CreateWork(workName:string) = 
            if x.Works.Exists(fun w -> (w :> INamed).Name = workName) then
                getNull<DsWork>();
            else
                let w = DsWork(x, workName)
                x.Works.Add w
                x.Graph.AddVertex w |> ignore
                w


    type DsFlow with
        member x.CreateCall(name:string):     DsCoin = tryCreateCoin(x, name, "Call")     |? getNull<DsCoin>()
        member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(x, name, "Command")  |? getNull<DsCoin>()
        member x.CreateOperator(name:string): DsCoin = tryCreateCoin(x, name, "Operator") |? getNull<DsCoin>()

    type DsWork with
        member x.CreateCall(name:string):     DsCoin = tryCreateCoin(x, name, "Call")     |? getNull<DsCoin>()
        member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(x, name, "Command")  |? getNull<DsCoin>()
        member x.CreateOperator(name:string): DsCoin = tryCreateCoin(x, name, "Operator") |? getNull<DsCoin>()

    let tryCreateCoin(x:IWithGraph, coinName:string, coinType:string) =
        let coins, graph =
            match x with
            | :? DsFlow as flow -> flow.Coins, flow.Graph
            | :? DsWork as work -> work.Coins, work.Graph

        if coins.Exists(fun w -> (w :> INamed).Name = coinName) then
            None
        else
            let c = DsCoin(x, coinName)
            coins.Add c
            c.Parent <- x
            c.CoinType <- coinType
            graph.AddVertex(c)
            Some c

    let tryCreateEdge(x:IWithGraph, src:Vertex, dst:Vertex) =
        let coins, graph =
            match x with
            | :? DsFlow as flow -> flow.Coins, flow.Graph
            | :? DsWork as work -> work.Coins, work.Graph

        if coins.Exists(fun w -> (w :> INamed).Name = coinName) then
            None
        else
            let c = DsCoin(x, coinName)
            coins.Add c
            c.Parent <- x
            c.CoinType <- coinType
            graph.AddVertex(c)
            Some c
