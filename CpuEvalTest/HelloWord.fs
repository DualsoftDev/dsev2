namespace CpuEvalTest

open System.Collections.Generic
open System.Linq
open System
open Dual.Common.Core.FS

[<AutoOpen>]
module HelloWord =

    [<AutoOpen>]
    module Val =
        let [<Literal>] OFF = 0
        let [<Literal>] ON = 1
        let [<Literal>] R = 2
        let [<Literal>] G = 3
        let [<Literal>] F = 4
        let [<Literal>] H = 5

        let [<Literal>] tr = "var0"
        let [<Literal>] w1 = "var1"
        let [<Literal>] w2 = "var2"


    type HelloWorldChangeSet(variables: Var seq) =
        inherit ChangeSet(variables)
        new (oldChangeSet: ChangeSet) = HelloWorldChangeSet(oldChangeSet.VarDict.Values)
        override x.Evaluate(var:Var) =
            match var.Name with
            | Val.tr -> Some var.Value
            | (Val.w1 | Val.w2) ->
                if var.Name = w2 then
                    ()
                let otherWork = if var.Name = w1 then w2 else w1
                match var.Value with
                | Val.R when (x.GetValue tr = Val.ON && var.Name = w1) || (var.Name = w2 && x.GetValue w1 = Val.F) -> Some Val.G
                | Val.G -> Some Val.F
                | Val.F when x.GetValue otherWork = Val.G -> Some Val.H
                | Val.H -> Some Val.R
                | _ -> None
            | _ -> None

    let initialize(): ChangeSet =
        let vtr = Var(tr, [|            |], Val.OFF)
        let vw1 = Var(w1, [|tr;       w2|], Val.R)
        let vw2 = Var(w2, [|tr; w1;     |], Val.F)

        let changeSet = HelloWorldChangeSet([vtr; vw1; vw2])
        /// 초기 변경 사항 추가
        changeSet.AddChange(w2, Some Val.F)
        changeSet.AddChange(w1, Some Val.R)
        changeSet.AddChange(tr, Some Val.ON)

        changeSet

[<AutoOpen>]
module General =
    let rand = Random()
    let numVars = 1000
    let maxDependencies = 10
    let useHelloWorld = true
    let initialize(): ChangeSet =
        let variables = [|
            for i in 0..numVars-1 do
                let dependencies =
                    [| for d in 0..rand.Next(maxDependencies) do
                        if i <> d then
                            $"var{rand.Next(numVars)}" |]
                yield Var($"var{i}", dependencies, rand.Next(100))
        |]
        let changeSet = ChangeSet(variables)
        /// 초기 변경 사항 추가
        [0..numVars-1] |> List.iter (fun i -> changeSet.AddChange($"var{i}", Some (rand.Next(100))))


        changeSet


[<AutoOpen>]
module Main =
    let asyncScanLoop() : Async<unit> =
        async {
            let useHelloWorld = true


            let changeSet = if useHelloWorld then HelloWord.initialize() else General.initialize()
            /// current change set
            let mutable ccs = changeSet
            let vars = ccs.VarDict

            let mutable nScan = 0
            while true do
                nScan <- nScan + 1

                do! Async.Sleep 10

                // 외부 변경 내역 merge
                ccs.MergeChanges()

                if ccs.Changes.Count <> 0 then

                    printf ""

                    /// next change set
                    let mutable ncs:ChangeSet = if useHelloWorld then HelloWorldChangeSet(ccs) else ChangeSet(ccs)

                    let changes =
                        ccs.Changes.Choose(fun (KeyValue(k, v)) ->
                            match v with
                            | None -> Some (k, v)
                            | Some v when vars[k].Value <> v -> Some (k, Some v)
                            | _ -> None)
                            |> Tuple.toDictionary

                    let oldValues = changes.Keys.Select(fun k -> k, vars[k].Value) |> dict |> Dictionary
                    for (KeyValue(k, _)) in changes do
                        let cv = ccs.GetValue k
                        vars[k].Value <- cv // 현재 변경된 값 반영

                    /// 현재 변경된 변수들을 기준으로 값 업데이트
                    let mutable nChanged = 0
                    for (KeyValue(k, _)) in changes do
                        let var = vars[k]
                        let newValue = ccs.Evaluate var

                        // 변경 감지 후 반영
                        match newValue with
                        | Some nv when oldValues[k] <> nv ->
                            ncs.AddChange(k, newValue)
                            let deps = vars.Values.Where(fun v -> v.Dependencies.Contains(k)).ToArray()

                            for d in deps do
                                ncs.AddChange(d.Name, None)

                            nChanged <- nChanged + 1
                        | _ -> ()

                    let xxx = changes.Count
                    /// 매 X회 반복마다 GC 수행
                    let checkPoint = if useHelloWorld then 10000 else 100
                    let checkPoint = 1
                    if nScan % checkPoint = 0 then
                        if useHelloWorld then
                            let details =
                                let kvs = [
                                    if !! changes.ContainsKey("var0") then
                                        $"".PadRight(20)
                                    for c in changes.OrderBy(fun c -> c.Key) do
                                        $"{c.Key}({c.Value})".PadRight(20)
                                ]

                                String.Join(", ", kvs)
                            printfn $"Scan {nScan} : {details} (current={nChanged}, {changes.Values.Count(Option.isNone)}+{changes.Values.Count(Option.isSome)}, next={ncs.Changes.Count}) variables changed"
                        else
                            printfn $"Scan {nScan} : ({nChanged} / {xxx}) variables changed"

                    if nScan % 100 = 0 then
                        //printfn $"[GC] Running Garbage Collection..."
                        GC.Collect()

                    ccs <- ncs



        }
    let scanLoopAsync() = asyncScanLoop() |> Async.StartAsTask