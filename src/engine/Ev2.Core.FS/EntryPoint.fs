namespace Dual.Ev2


open Dual.Common.Base.FS
open Dual.Common.Core.FS


// Json serialize 시의 clean namesapce 를 위해 module 로 선언
[<AutoOpen>]
module T =
    type TValue<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        new() = TValue(Unchecked.defaultof<'T>)   // for Json

    type TFunction<'T> private (op:Op, args:IExpression seq) =
        inherit TFunctionImpl<'T>(op, args)

    type TFunction<'T> with
        new() = TFunction<'T>(Op.Unit, [])   // for Json
        static member Create(op:Op, args:IExpression seq, ?name:string): TFunctionImpl<'T> =
            TFunctionImpl<'T>(op, args)
                .Tee(fun nt -> nt.OnDeserialized())
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TFunctionImpl<'T> =
            let (f:Evaluator) = fun (args:Arguments) -> evaluator args |> box
            let op = CustomOperator f
            TFunction.Create(op, args, ?name=name)

module ModuleInitializer =
    let Initialize() =
        ()
        //fwdEvaluate := (
        //    fun (operator: Op) (args: Args) ->
        //        evaluateT (operator, args) :> obj
        //        //failwithlog "Should be reimplemented."
