namespace Dual.Ev2

open System
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS

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
    type TArguments<'T> = IExpression<'T> list
    type TArgs<'T> = TArguments<'T>

    type INonTerminal =
        inherit IExpression
        //abstract member Operator: Op with get, set
        //abstract member Arguments: IExpression[] with get, set

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


    let private evaluateT<'T> (op:Op, args:IExpression seq): 'T =
        match op with
        | Op.CustomEvaluator evaluator ->
            let tEvaluator = evaluator :?> TEvaluator<'T>
            tEvaluator.TEvaluate(args.ToFSharpList())
        | Op.OpUnit ->
            failwith "ERROR: Operator not specified"
        | RisingAfter
        | FallingAfter ->
            failwith "ERROR: Not Yet!!"
        | _ ->
            failwith "ERROR: Not Yet!!"
            //let mnemonic =
            //    match op with
            //    | And -> fAnd<'T> "&&"
            //    | Or -> "||"
            //    | Neg -> "!"
            //    | OpCompare op -> op
            //    | OpArithmetic op ->
            //        match op with
            //        | "+" -> fAdd<'T>

    // 기존 FunctionSpec<'T> 에 해당.
    [<DataContract>]
    type TNonTerminal<'T> private (op:Op, args:IExpression seq) =

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

        member private this.OnDeserialized() =
            lazyValue <- ResettableLazy<'T>(fun () -> evaluateT<'T> (op, args))

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
        new() = TNonTerminal<'T>(Op.OpUnit, [])   // for Json
        static member Create(op:Op, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            TNonTerminal<'T>(op, args)
                .Tee(fun nt -> nt.OnDeserialized())
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TNonTerminal<'T> =
            let op = TEvaluator<'T>(evaluator) :> IEvaluator |> CustomEvaluator
            TNonTerminal<'T>.Create(op, args, ?name=name)

    type INonTerminal<'T> with
        /// INonTerminal.FunctionBody
        member x.FunctionBody
            with get() = getPropertyValueDynamically(x, "FunctionBody") :?> (TEvaluator<'T>)
            and set (v:Arguments -> 'T) = setPropertyValueDynamically(x, "FunctionBody", v)

