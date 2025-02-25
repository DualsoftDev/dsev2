namespace Dual.Ev2

open System
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS
open Dual.Common.Core

[<AutoOpen>]
module rec TTerminalModule =

    let private evaluateT<'T> (op:Op, args:Args): 'T =
        match op with
        | Op.Unit ->
            failwith "ERROR: Operator not specified"
        | Op.CustomOperator evaluator ->
            let tEvaluator = evaluator :?> TEvaluator<'T>
            tEvaluator.TEvaluate(args.ToFSharpList())
        | Op.PredefinedOperator mnemonic ->
            match mnemonic with
            | "+" -> (fAdd<'T> args) |> _.Evaluate() :?> 'T
            //| "&&" -> fAnd<'T>
            //| Or -> "||"
            //| Neg -> "!"
            //| OpCompare op -> op
            //| OpArithmetic op ->
            //    match op with
            //    | "+" -> fAdd<'T>


        //| RisingAfter
        //| FallingAfter ->
        //    failwith "ERROR: Not Yet!!"
        | _ ->
            failwith "ERROR: Not Yet!!"


    // 기존 FunctionSpec<'T> 에 해당.
    [<DataContract>]
    type TNonTerminal<'T> private (op:Op, args:IExpression seq) =

        let args = args.ToFSharpList()
        let mutable lazyValue:ResettableLazy<'T> = null

        interface INonTerminal<'T>
        //interface INonTerminal<'T> with
        //    member x.Operator with get() = x.Operator and set v = x.Operator <- v
        //    member x.Arguments with get() = x.Arguments and set v = x.Arguments <- v

        interface IExpression<'T> with
            member x.Evaluate() = x.Evaluate()
            member x.TEvaluate():'T = x.TEvaluate()


        [<DataMember>] member val Operator: Op = op with get, set
        [<DataMember>] member val Arguments: IExpression[] = args.ToArray() with get, set

        [<JsonIgnore>] member x.Value = lazyValue.Value |> box
        [<JsonIgnore>] member x.TValue = lazyValue.Value
        member x.Evaluate() = x.Value
        member x.TEvaluate():'T = x.TValue

        member private x.OnDeserialized() =
            lazyValue <- ResettableLazy<'T>(fun () ->
                let objValue:obj = evaluateT<'T> (x.Operator, x.Arguments.ToFSharpList())
                objValue :?> 'T
            )
            noop()

        // F#에서는 어트리뷰트를 [<OnDeserialized>] 형식으로 사용해야 합니다.
        /// Deserialize 된 이후에 처리해야 할 작업 지정
        [<OnDeserialized>]
        member this.OnDeserializedMethod(context: StreamingContext) = this.OnDeserialized()

        /// DynamicDictionary.
        ///
        /// NsJsonS11nSafeObject 의 부가 속성 정의 용.  e.g Name, Address, Rising, Negation 등
        [<DataMember>] member val PropertiesDto:DynamicDictionary = null with get, set
        /// PropertiesDto 접근용
        [<JsonIgnore>]
        member x.DD =
            if x.PropertiesDto = null then
                x.PropertiesDto <- DynamicDictionary()
            x.PropertiesDto


    type TNonTerminal<'T> with
        new() = TNonTerminal<'T>(Op.Unit, [])   // for Json
        static member Create(op:Op, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            TNonTerminal<'T>(op, args)
                .Tee(fun nt -> nt.OnDeserialized())
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            let op = TEvaluator<'T>(evaluator) :> IEvaluator |> CustomOperator
            TNonTerminal<'T>.Create(op, args, ?name=name)

    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynamically(x, "FunctionBody") :?> (TEvaluator<'T>)
            and set (v:Arguments -> 'T) = setPropertyValueDynamically(x, "FunctionBody", v)

