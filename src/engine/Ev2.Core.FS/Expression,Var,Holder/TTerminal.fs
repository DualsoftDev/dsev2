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
            evaluator (args.ToFSharpList()) :?> 'T

        | Op.PredefinedOperator mnemonic ->
            match mnemonic with
            | "+" -> fAdd<'T> args
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
    type TNonTerminalImpl<'T> internal (op:Op, args:IExpression seq) =

        let args = args.ToFSharpList()
        let mutable lazyValue:ResettableLazy<'T> = null

        interface INonTerminal<'T>

        interface IExpression<'T> with
            member x.Value = x.Value
            member x.TValue = x.TValue


        [<DataMember>] member val Operator: Op = op with get, set
        [<DataMember>] member val Arguments: IExpression[] = args.ToArray() with get, set

        [<JsonIgnore>] member x.Value = lazyValue.Value |> box
        [<JsonIgnore>] member x.TValue = lazyValue.Value
        member x.Invalidate() = lazyValue.Reset()

        member internal x.OnDeserialized() =
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


    //type INonTerminal<'T> with
    //    /// INonTerminal.FunctionBody
    //    member x.FunctionBody
    //        with get() = getPropertyValueDynamically(x, "FunctionBody") :?> (TEvaluator<'T>)
    //        and set (v:Arguments -> 'T) = setPropertyValueDynamically(x, "FunctionBody", v)

