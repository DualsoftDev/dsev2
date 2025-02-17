namespace CpuEvalTest

open System.Collections.Generic
open System.Linq
open System
open Dual.Common.Core.FS

[<AutoOpen>]
module rec Common =
    /// 변수 정의
    type Var(name: string, dependencies: string[], initialValue: int) =
        let mutable value = initialValue
        member val Name = name
        member val Dependencies = dependencies |> HashSet
        member x.Value
            with get() = value
            and set v =
                if v <> value then
                    //printfn $"Setting {x.Name} = {v}"
                    value <-v
                else
                    noop()


    /// 변수 컬렉션(고정) + ChangeSet 을 관리하는 타입
    type ChangeSet(varDict: Dictionary<string, Var>) =
        member val VarDict = varDict
        member val Changes = Dictionary<string, int option>()

        abstract member Evaluate: Var -> int option
        default x.Evaluate(var:Var) =
            let dependencies = var.Dependencies
            let depValues = dependencies |> Seq.map (fun d -> int64 (x.GetValue d))
            let newValue = depValues.Sum() + int64 var.Value

            if newValue > int64 (Int32.MaxValue / 2) then 0 else int newValue
            |> Some


    let externalChanges = Dictionary<string, int option>()
    type ChangeSet with
        new (vars:Var seq) =
            let varDict = vars.ToDictionary( (fun v -> v.Name), id)
            ChangeSet(varDict)
        new (oldChangeSet: ChangeSet) = ChangeSet(oldChangeSet.VarDict)

        /// 변수 값 조회 (변경 내역이 있으면 반영)
        member x.GetValue(name) =
            match x.Changes.TryGetValue(name) with
            | true, Some(value) -> value
            | _ -> x.VarDict.[name].Value

        /// 내부 연산에 의한 특정 변수 값을 변경
        member internal x.AddInternalChange(name, value) =
            // None 값으로 기존 설정된 Some 값을 overwrite 하지 않는다.
            match value with
            | Some v -> x.Changes.[name] <- value
            | None ->
                match x.Changes.TryGetValue(name) with
                | true, Some _ -> ()
                | _ -> x.Changes.[name] <- None

        /// 외부 값 변경(e.g 센서 값)에 의한 특정 변수 값을 변경
        static member AddExternalChange(name:string, value:int) =
            lock externalChanges (fun () ->
                externalChanges.AddOrReplace(name, Some value) |> ignore )

        /// 외부 변경 내역 merge
        member x.MergeChanges() =
            lock externalChanges (fun () ->
                for (KeyValue(k, v)) in externalChanges do
                    x.Changes[k] <- v
                externalChanges.Clear()
            )
