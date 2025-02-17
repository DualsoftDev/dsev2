namespace CpuEvalTest

//module Cpu

open System
open System.Collections.Generic
open System.Linq


[<AutoOpen>]
module MainModule =
    /// 프로그램 실행부
    [<EntryPoint>]
    let main argv =
        let useHelloWorld = true


        let changeSet = if useHelloWorld then HelloWord.initialize() else General.initialize()
        /// current change set
        let mutable ccs = changeSet
        let vars = ccs.VarDict

        let mutable nScan = 0
        while true do
            nScan <- nScan + 1

            /// next change set
            let mutable ncs:ChangeSet = if useHelloWorld then HelloWorldChangeSet(ccs) else ChangeSet(ccs)

            let oldValues = ccs.Changes.Keys.Select(fun k -> k, vars[k].Value) |> dict |> Dictionary
            for (KeyValue(k, _)) in ccs.Changes do
                vars[k].Value <- ccs.GetValue k // 현재 변경된 값 반영

            /// 현재 변경된 변수들을 기준으로 값 업데이트
            let mutable nChanged = 0
            for (KeyValue(k, _)) in ccs.Changes do
                let var = vars[k]
                let newValue = ccs.Evaluate var

                // 변경 감지 후 반영
                match newValue with
                | Some nv when oldValues[k] <> nv ->
                    ncs.AddChange(k, newValue)

                    let deps = vars.Values.Where(fun v -> v.Dependencies.Contains(k)).ToArray()
                    ()
                    for d in deps do
                        ncs.AddChange(d.Name, None)

                    nChanged <- nChanged + 1
                | _ -> ()

            let xxx = ccs.Changes.Count
            /// 매 X회 반복마다 GC 수행
            let checkPoint = if useHelloWorld then 10000 else 100
            let checkPoint = 1
            if nScan % checkPoint = 0 then
                if useHelloWorld then
                    let details =
                        let kvs = [
                            for c in ccs.Changes.OrderBy(fun c -> c.Key) do
                                $"{c.Key}({c.Value})".PadRight(20)
                        ]

                        String.Join(", ", kvs)
                    printfn $"Scan {nScan} : {details} ({nChanged} / {xxx}) variables changed"
                else
                    printfn $"Scan {nScan} : ({nChanged} / {xxx}) variables changed"

            if nScan % 100 = 0 then
                //printfn $"[GC] Running Garbage Collection..."
                GC.Collect()

            ccs <- ncs

        1

