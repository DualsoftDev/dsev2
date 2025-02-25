namespace Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS


// Json serialize 시의 clean namesapce 를 위해 module 로 선언
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
            let (f:Args -> obj) = fun (args:Arguments) -> evaluator args |> box
            let op = CustomOperator f
            TNonTerminal.Create(op, args, ?name=name)

module ModuleInitializer =
    let Initialize() =
        ()
        //fwdEvaluate := (
        //    fun (operator: Op) (args: Args) ->
        //        evaluateT (operator, args) :> obj
        //        //failwithlog "Should be reimplemented."
