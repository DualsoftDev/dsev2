namespace Dual.Ev2

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System
open Gnu.Getopt

[<AutoOpen>]
module rec TTerminalModule =

    type ITerminal =
        inherit IExpression
    type ITerminal<'T> =
        inherit ITerminal
        inherit IExpression<'T>


    // 기존 Terminal<'T> 에 해당.
    type TTerminal<'T>(value:'T) =
        inherit TValueHolder<'T>(value)
        interface ITerminal<'T>
        new() = TTerminal(Unchecked.defaultof<'T>)   // for Json


    // 임시 구현
    type IExpression with
        [<Obsolete("임시")>] member x.Terminal = x :?> ITerminal |> Option.ofObj
        [<Obsolete("임시")>] member x.DataType = x.Type
        [<Obsolete("임시")>] member x.FunctionName = if x :? INonTerminal then Some x.Type.Name else None
        [<Obsolete("임시")>] member x.BoxedEvaluatedValue = tryGetPropertyValueDynamically(x, "Value") |? null

        [<Obsolete("임시")>] member x.IsLiteral = false

    type IExpression<'T> with
        [<Obsolete("임시")>]
        member x.Evaluate() =
            let xxx = x
            Unchecked.defaultof<'T>


    type Arguments = IExpression list
    type Args      = Arguments

    type INonTerminal =
        inherit IExpression
    type INonTerminal<'T> =
        inherit INonTerminal
        inherit IExpression<'T>

    /// IEvaluator: 기본 평가 클래스 (Arguments -> obj)
    type IEvaluator =
        abstract Evaluate: Arguments -> obj

    // TEvaluator<'T>: IEvaluator를 상속하여 Arguments -> 'T 를 구현
    type TEvaluator<'T>(evaluator:Arguments -> 'T) =
        interface IEvaluator with
            member x.Evaluate(args) = x.TEvaluate(args) |> box

        member x.TEvaluate(args) = evaluator args


    type Op =
    | OpUnit // Logical XOR 는 function 인 '<>' 로 구현됨

    | And
    | Or
    | Neg

    | RisingAfter
    | FallingAfter

    | OpCompare of operator: string
    | OpArithmetic of operator: string

    /// 정상 범주에서 지원되지 않는 operator
    | CustomEvaluator of IEvaluator


    // 기존 FunctionSpec<'T> 에 해당.
    type TNonTerminal<'T>(value:'T, ?opAndArgs:(Op*IExpression [])) =
        inherit TValueHolder<'T>(value)

        let (op, args) = opAndArgs |? (Op.OpUnit, [||])

        interface INonTerminal<'T>

        member val Operator: Op = op with get, set
        member val Arguments: IExpression[] = args with get, set

        override x.Evaluate() = x.TEvaluateImpl() |> box
        override x.TEvaluate():'T = x.TEvaluateImpl()

    type TNonTerminal<'T> with
        new() = TNonTerminal(Unchecked.defaultof<'T>)   // for Json
        new(op:Op, args:IExpression seq) = TNonTerminal(Unchecked.defaultof<'T>, (op, args.ToArray()))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            let op = TEvaluator<'T>(evaluator) :> IEvaluator |> CustomEvaluator

            TNonTerminal<'T>(Unchecked.defaultof<'T>, (op, args.ToArray()))
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(op:Op, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            do
                noop()
            TNonTerminal<'T>(op, args)
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        member private x.TEvaluateImpl(): 'T =
            match x.Operator with
            | Op.CustomEvaluator fn ->
                let tFn = fn :?> TEvaluator<'T>
                let value = tFn.TEvaluate(x.Arguments.ToFSharpList())
                x.Value <- box value
                value
            | _ ->
                failwith "ERROR: Not Yet!!"


    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynamically(x, "FunctionBody") :?> (TEvaluator<'T>)
            and set (v:Arguments -> 'T) = setPropertyValueDynamically(x, "FunctionBody", v)


    //type IExpression<'T> with
    //    member x.TEvaluate():'T =
    //        match x with
    //        | :? INonTerminal<'T> as nt -> nt.Eval |> Option.ofObj |> Option.map (fun nt -> nt.Evaluate()) |? Unchecked.defaultof<'T>


    //type TExpression<'T> =
    //    | DuTerminal of TTerminal<'T>
    //    | DuNonTerminal of TNonTerminal<'T>
    //    with
    //        interface IExpression<'T>
    //        static member Create(?name:string, ?arguments:Arguments, ?functionBody:TEvaluator<'T>) =
    //            TNonTerminal<'T>(Unchecked.defaultof<'T>)
    //            |> tee(fun nt ->
    //                nt.Name <- name |? null)
