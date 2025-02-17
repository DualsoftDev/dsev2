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
        let useHelloWorld = false


        let changeSet = if useHelloWorld then HelloWord.initialize() else General.initialize()
        let mutable currentSet = changeSet
        let vars = currentSet.VarDict

        let mutable counter = 0
        while true do
            counter <- counter + 1

            let mutable nextSet:ChangeSet = if useHelloWorld then HelloWorldChangeSet(currentSet) else ChangeSet(currentSet)


            /// 현재 변경된 변수들을 기준으로 값 업데이트
            let mutable changedCount = 0
            for (KeyValue(k, _)) in currentSet.Changes do
                let var = currentSet.VarDict.[k]
                let prevValue = var.Value
                var.Value <- currentSet.GetValue k // 현재 변경된 값 반영
                let newValue = currentSet.Evaluate var

                // 변경 감지 후 반영
                match newValue with
                | Some nv when prevValue <> nv ->
                    nextSet.AddChange(k, newValue)

                    let deps = currentSet.VarDict.Values.Where(fun v -> v.Dependencies.Contains(k)).ToArray()
                    ()
                    for d in deps do
                        nextSet.AddChange(d.Name, None)

                    changedCount <- changedCount + 1
                | _ -> ()

            let xxx = currentSet.Changes.Count
            /// 매 X회 반복마다 GC 수행
            let checkPoint = if useHelloWorld then 10000 else 100
            if counter % checkPoint = 0 then
                if useHelloWorld then
                    let details =
                        let kvs = [
                            for c in currentSet.Changes.OrderBy(fun c -> c.Key) do
                                $"{c.Key}({c.Value})".PadRight(20)
                        ]

                        String.Join(", ", kvs)
                    printfn $"Scan {counter} : {details} ({changedCount} / {xxx}) variables changed"
                else
                    printfn $"Scan {counter} : ({changedCount} / {xxx}) variables changed"

            if counter % 100 = 0 then
                //printfn $"[GC] Running Garbage Collection..."
                GC.Collect()

            currentSet <- nextSet

        1

