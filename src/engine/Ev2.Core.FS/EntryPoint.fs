namespace Dual.Ev2

open System
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS


[<AutoOpen>]
module T =
    type TTerminal<'T>(value:'T) =
        inherit TTerminalImpl<'T>(value)

    type TNonTerminal<'T> private (op:Op, args:IExpression seq) =
        inherit TNonTerminalImpl<'T>(op, args)

    type TNonTerminal<'T> with
        new() = TNonTerminal<'T>(Op.Unit, [])   // for Json
        static member Create(op:Op, args:IExpression seq, ?name:string): TNonTerminalImpl<'T> =
            TNonTerminalImpl<'T>(op, args)
                .Tee(fun nt -> nt.OnDeserialized())
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TNonTerminalImpl<'T> =
            let op = TEvaluator<'T>(evaluator) :> IEvaluator |> CustomOperator
            TNonTerminal.Create(op, args, ?name=name)

module ModuleInitializer =
    let Initialize() =
        ()
        //fwdEvaluate := (
        //    fun (operator: Op) (args: Args) ->
        //        evaluateT (operator, args) :> obj
        //        //failwithlog "Should be reimplemented."
