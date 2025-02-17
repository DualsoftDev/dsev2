namespace CpuEvalTest

open System.Collections.Generic
open System.Linq
open System

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
