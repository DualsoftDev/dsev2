module Cpu

open System
open System.Collections.Generic
open System.Linq

/// 변수 정의
type Var(name: string, dependencies: string[], initialValue: int) =
    member val Name = name
    member val Dependencies = dependencies |> HashSet
    member val Value = initialValue with get, set

/// 변수 컬렉션을 관리하는 타입
type Vars(varDict: Dictionary<string, Var>) =
    member val VarDict = varDict
    member val Changes = Dictionary<string, int option>()

    /// 특정 변수 값을 변경
    member x.AddChange(name, value) =
        // None 값으로 기존 설정된 Some 값을 overwrite 하지 않는다.
        match value with
        | Some v -> x.Changes.[name] <- value
        | None ->
            match x.Changes.TryGetValue(name) with
            | true, Some _ -> ()
            | _ -> x.Changes.[name] <- None

    /// 변수 값 조회 (변경 내역이 있으면 반영)
    member x.GetValue(name) =
        match x.Changes.TryGetValue(name) with
        | true, Some(value) -> value
        | _ -> x.VarDict.[name].Value



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

/// 프로그램 실행부
[<EntryPoint>]
let main argv =
    let rand = Random()
    let numVars = 1000
    let maxDependencies = 10
    let useHelloWorld = true
    let varStart = if useHelloWorld then 3 else 0

    let vtr = Var(tr, [|            |], Val.OFF)
    let vw1 = Var(w1, [|tr;       w2|], Val.R)
    let vw2 = Var(w2, [|tr; w1;     |], Val.F)

    /// 변수 네트워크 생성
    let varDict =
        let vars = [
            if useHelloWorld then
                yield! [vtr; vw1; vw2]
            //for i in varStart..numVars-1 do
            //    let dependencies =
            //        [| for d in 0..rand.Next(maxDependencies) do
            //            if i <> d then
            //                $"var{rand.Next(numVars)}" |]
            //    yield Var($"var{i}", dependencies, rand.Next(100))
        ]
        let varDict = Dictionary<string, Var>()
        vars |> List.iter (fun v -> varDict.Add(v.Name, v))
        varDict

    let mutable currentSet = Vars(varDict)

    /// 초기 변경 사항 추가
    //[varStart..numVars-1] |> List.iter (fun i -> currentSet.AddChange($"var{i}", Some (rand.Next(100))))

    if useHelloWorld then
        currentSet.AddChange(w2, Some Val.F)
        currentSet.AddChange(w1, Some Val.R)
        currentSet.AddChange(tr, Some Val.ON)

    let mutable counter = 0
    while true do
        counter <- counter + 1

        let mutable nextSet = Vars(varDict)

        /// 특정 변수를 평가하여 새로운 값 계산
        let evalVar (var: Var) : int option =
            match var.Name with
            //| "tr" -> var.Value
            | (Val.w1 | Val.w2) when useHelloWorld ->
                if var.Name = w2 then
                    ()
                let otherWork = if var.Name = w1 then w2 else w1
                match var.Value with
                | Val.R when (currentSet.GetValue tr = Val.ON && var.Name = w1) || (var.Name = w2 && currentSet.GetValue w1 = Val.F) -> Some Val.G
                | Val.G -> Some Val.F
                | Val.F when currentSet.GetValue otherWork = Val.G -> Some Val.H
                | Val.H -> Some Val.R
                | _ -> None
            | _ ->
                let dependencies = var.Dependencies
                let depValues = dependencies |> Seq.map (fun d -> int64 (currentSet.GetValue d))
                let newValue = depValues.Sum() + int64 var.Value

                //// 의존 변수 목록을 `"varName"(value)` 형태로 변환
                //let dependencyInfo =
                //    dependencies
                //    |> Seq.map (fun d -> let v = currentSet.GetValue d in $"\"{d}\"({v})")
                //    |> String.concat "; "

                // 변수 값 변경 추적
                //printfn $"[TRACE] \"{var.Name}\"({var.Value}) -> {newValue} (from [| {dependencyInfo} |])"

                if newValue > int64 (Int32.MaxValue / 2) then 0 else int newValue
                |> Some

        /// 현재 변경된 변수들을 기준으로 값 업데이트
        let mutable changedCount = 0
        for (KeyValue(k, _)) in currentSet.Changes do
            let var = currentSet.VarDict.[k]
            let prevValue = var.Value
            var.Value <- currentSet.GetValue k // 현재 변경된 값 반영
            let newValue = evalVar var

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
        //if counter % 100 = 0 then
        if true then
            let details =
                let kvs = [
                    for c in currentSet.Changes.OrderBy(fun c -> c.Key) do
                        $"{c.Key}({c.Value})".PadRight(20)
                ]

                String.Join(", ", kvs)
            printfn $"Scan {counter} : {details} ({changedCount}) variables changed"

            //printfn $"Scan {counter} : ({changedCount} / {xxx}) variables changed"

            //printfn $"[GC] Running Garbage Collection..."
            GC.Collect()

        currentSet <- nextSet

    1

