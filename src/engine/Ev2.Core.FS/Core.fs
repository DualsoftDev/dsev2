namespace rec Dual.Ev2

open Newtonsoft.Json

open Dual.Common.Base.FS
open Dual.Common.Base.CS
open Dual.Common.Core.FS

open Engine.Common.GraphModule

[<AutoOpen>]
module Core =
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
        [<JsonProperty(Order = 4)>] member val GraphDTO = getNull<GraphDTO>() with get, set

    type DsWork(flow:DsFlow, name:string) =
        inherit Vertex(name)
        interface IWork
        interface IWithGraph

        [<JsonIgnore>] member val Flow = flow with get, set
        [<JsonIgnore>] member val Graph = DsGraph()
        [<JsonProperty(Order = 2)>] member val Coins = ResizeArray<DsCoin>() with get, set
        [<JsonProperty(Order = 3)>] member val GraphDTO = getNull<GraphDTO>() with get, set

    type CoinType =
        | Undefined
        | Action
        | Command
        | Operator

    type DsCoin(parent:IWithGraph, name:string) =
        inherit Vertex(name)
        interface IWork
        interface IWithGraph

        [<JsonIgnore>] member val Parent:IWithGraph = getNull<IWithGraph>() with get, set
        [<JsonProperty(Order = 2)>] member val CoinType = Undefined with get, set





// 동일 파일 내에 있어야 확장을 C# 에서 볼 수 있음.
//[<AutoOpen>]
//module CoreCreate =

    type DsSystem with
        static member Create(name:string) = new DsSystem(name)
        member x.CreateFlow(flowName:string) = 
            if x.Flows.Exists(fun f -> (f :> INamed).Name = flowName) then
                getNull<DsFlow>();
            else
                DsFlow(x, flowName).Tee(fun f -> x.Flows.Add f)

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
        member x.CreateCall(name:string):     DsCoin = tryCreateCoin(x, name, CoinType.Action)   |? getNull<DsCoin>()
        member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(x, name, CoinType.Command)  |? getNull<DsCoin>()
        member x.CreateOperator(name:string): DsCoin = tryCreateCoin(x, name, CoinType.Operator) |? getNull<DsCoin>()

    type DsWork with
        member x.CreateCall(name:string):     DsCoin = tryCreateCoin(x, name, CoinType.Action)   |? getNull<DsCoin>()
        member x.CreateCommand(name:string):  DsCoin = tryCreateCoin(x, name, CoinType.Command)  |? getNull<DsCoin>()
        member x.CreateOperator(name:string): DsCoin = tryCreateCoin(x, name, CoinType.Operator) |? getNull<DsCoin>()


    type IWithGraph with
        member x.GetGraph(): DsGraph =
            match x with
            | :? DsFlow as flow -> flow.Graph
            | :? DsWork as work -> work.Graph
            | _ -> failwith "ERROR"

        member x.GetCoins(): ResizeArray<DsCoin> =
            match x with
            | :? DsFlow as flow -> flow.Coins
            | :? DsWork as work -> work.Coins
            | _ -> failwith "ERROR"

        member x.CreateEdge(src:Vertex, dst:Vertex, edgeType:CausalEdgeType) =
            let g = x.GetGraph()
            Edge.Create(g, src, dst, edgeType)

        member x.CreateEdge(src:string, dst:string, edgeType:CausalEdgeType) =
            let g = x.GetGraph()
            let s, e = g.FindVertex(src), g.FindVertex(dst)
            x.CreateEdge(s, e, edgeType)



    let private tryCreateCoin(x:IWithGraph, coinName:string, coinType:CoinType) =
        let coins, graph = x.GetCoins(), x.GetGraph()

        if coins.Exists(fun w -> (w :> INamed).Name = coinName) then
            None
        else
            let c = DsCoin(x, coinName)
            coins.Add c
            c.Parent <- x
            c.CoinType <- coinType
            graph.AddVertex(c) |> verify
            Some c










