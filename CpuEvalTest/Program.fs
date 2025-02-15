module Cpu

open System
open System.Collections.Generic
open System.Linq

/// 변수 정의
type Var(name: string, dependencies: string[], initialValue: int) =
    member val Name = name
    member val Dependencies = dependencies
    member val Value = initialValue with get, set

/// 변수 컬렉션을 관리하는 타입
type Vars(varDict: Dictionary<string, Var>) =
    member val VarDict = varDict
    member val Changes = Dictionary<string, int>()

    /// 특정 변수 값을 변경
    member x.AddChange(name, value) =
        x.Changes.[name] <- value

    /// 변수 값 조회 (변경 내역이 있으면 반영)
    member x.GetValue(name) =
        match x.Changes.TryGetValue(name) with
        | true, value -> value
        | _ -> x.VarDict.[name].Value

/// 프로그램 실행부
[<EntryPoint>]
let main argv =
    let rand = Random()
    let numVars = 1000
    let maxDependencies = 10

    /// 변수 네트워크 생성
    let varDict =
        let vars =
            [ for i in 0..numVars-1 do
                let dependencies =
                    [ for _ in 0..rand.Next(maxDependencies) do
                        "var" + rand.Next(numVars).ToString() ]
                yield Var("var" + i.ToString(), dependencies.ToArray(), rand.Next(100)) ]
        let varDict = Dictionary<string, Var>()
        vars |> List.iter (fun v -> varDict.Add(v.Name, v))
        varDict

    let mutable currentSet = Vars(varDict)

    /// 초기 변경 사항 추가
    [0..numVars-1] |> List.iter (fun i -> currentSet.AddChange("var" + i.ToString(), rand.Next(100)))
    //currentSet.AddChange("var0", 100)
    //currentSet.AddChange("var2", 9)
    //currentSet.AddChange("var10", 10)

    let mutable counter = 0
    while true do
        counter <- counter + 1

        let mutable nextSet = Vars(varDict)

        /// 특정 변수를 평가하여 새로운 값 계산
        let evalVar (var: Var) =
            let dependencies = var.Dependencies
            let depValues = dependencies |> Seq.map (fun d -> currentSet.GetValue d)
            let newValue = depValues.Sum() + var.Value

            //// 의존 변수 목록을 `"varName"(value)` 형태로 변환
            //let dependencyInfo =
            //    dependencies
            //    |> Seq.map (fun d -> let v = currentSet.GetValue d in $"\"{d}\"({v})")
            //    |> String.concat "; "

            // 변수 값 변경 추적
            //printfn $"[TRACE] \"{var.Name}\"({var.Value}) -> {newValue} (from [| {dependencyInfo} |])"

            if newValue > (Int32.MaxValue / 100) then 0 else newValue

        /// 현재 변경된 변수들을 기준으로 값 업데이트
        let changedCount =
            currentSet.Changes
            |> Seq.fold (fun acc (KeyValue(k, _)) ->
                let var = currentSet.VarDict.[k]
                let prevValue = var.Value
                var.Value <- currentSet.GetValue k // 현재 변경된 값 반영
                let newValue = evalVar var

                // 변경 감지 후 반영
                if prevValue <> newValue then
                    nextSet.AddChange(k, newValue)
                    acc + 1
                else acc
            ) 0

        let xxx = currentSet.Changes.Count
        /// 매 X회 반복마다 GC 수행
        if (counter < 1000 && counter % 100 = 0) || counter % 10000 = 0 then
            printfn $"Scan {counter} : {changedCount} variables changed"

            //printfn $"[GC] Running Garbage Collection..."
            GC.Collect()

        currentSet <- nextSet

    1

