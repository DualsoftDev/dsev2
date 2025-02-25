namespace Dual.Ev2

open System.Runtime.Serialization
open Newtonsoft.Json

open Dual.Common.Core.FS
open Dual.Common.Base.CS

[<AutoOpen>]
module rec TTerminalModule =

    type Op with
        member x.GetFunction(): (Args -> obj) =
            match x with
            | CustomOperator f -> f
            | PredefinedOperator mnemonic -> cf mnemonic
            | _ -> failwith "ERROR: Not Yet!!"


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
                let objValue:obj = x.Operator.GetFunction() (x.Arguments.ToFSharpList())
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

