namespace Dual.Ev2

open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Dual.Common.Base.FS
open Dual.Common.Core.FS
open System.Runtime.Serialization
open Dual.Common.Base.CS
[<AutoOpen>]
module rec TExpressionModule =
    type ValueHolder(typ: Type, ?value: obj) =
        // NsJsonS11nSafeObject 에서 상속받아서는 안됨.  Sealed class
        member val ObjectHolder = NsJsonS11nSafeObject(typ, ?value=value) with get, set

        // 향후 전개 가능한 interface 목록.  실제 interface 의 method 는 구현하지 않고, 확장 method 를 통해 접근
        // e.g IWithName 은 확장을 통해 Name 속성의 get, set 제공
        interface IWithName
        interface IWithAddress
        interface IWithType
        interface IStorage
        interface IValue with
            member x.Value with get() = x.Value and set v = x.Value <- v

        interface IExpression

        /// DynamicDictionary.
        ///
        /// NsJsonS11nSafeObject 의 부가 속성 정의 용.  e.g Name, Address, Rising, Negation 등
        member val PropertiesDto = getNull<DynamicDictionary>() with get, set
        /// PropertiesDto 접근용
        [<JsonIgnore>]
        member x.DD =
            if x.PropertiesDto = null then
                x.PropertiesDto <- DynamicDictionary()
            x.PropertiesDto

    type ValueHolder with
        new () = ValueHolder(typeof<obj>, null)
        [<JsonIgnore>] member x.ValueType = x.ObjectHolder.Type

        /// Holded value
        [<JsonIgnore>]
        member x.Value
            with get() = x.ObjectHolder.Value
            and set (v:obj) =
                if x.IsLiteral then
                    failwith $"ERROR: {x.Name} is CONSTANT.  It's read-only"
                x.ObjectHolder.Value <- v

        [<JsonIgnore>] member x.Type = x.ObjectHolder.Type


        /// NsJsonS11nSafeObject.Name with DD
        [<JsonIgnore>]
        member x.Name
            with get() = x.DD.TryGet<string>("Name") |? null
            and set (v:string) = x.DD.Set<string>("Name", v)


        [<JsonIgnore>]
        member x.Address
            with get() = x.DD.TryGet<string>("Address") |? null
            and set (v:string) = x.DD.Set<string>("Address", v)

        [<JsonIgnore>]
        member x.Comment
            with get() = x.DD.TryGet<string>("Comment") |? null
            and set (v:string) = x.DD.Set<string>("Comment", v)

        [<JsonIgnore>]
        member x.IsLiteral
            with get() = x.DD.TryGet<bool>("IsLiteral") |? false
            and set (v:bool) = x.DD.Set<bool>("IsLiteral", v)

        /// Timer/Counter 등의 member 변수.  DN,
        [<JsonIgnore>]
        member x.IsMemberVariable
            with get() = x.DD.TryGet<bool>("IsMemberVariable") |? false
            and set (v:bool) = x.DD.Set<bool>("IsMemberVariable", v)

        [<JsonIgnore>]
        member x.TagKind
            with get() = x.DD.TryGet<uint64>("TagKind") |? 0UL
            and set (v:uint64) = x.DD.Set<uint64>("TagKind", v)

    type TValue<'T>(value:'T) =
        inherit ValueHolder(typedefof<'T>, value)
        new() = TValue(Unchecked.defaultof<'T>)   // for Json

        interface IWithAddress
        interface ITerminal<'T>
        interface IWithType<'T>
        interface IExpression<'T>

        interface IValue<'T> with
            member x.TValue with get() = x.TValue and set v = x.Value <- v
        abstract member TValue: 'T with get, set
        [<JsonIgnore>] default x.TValue with get() = x.Value :?> 'T and set v = x.Value <- v





    type Op with
        member x.GetFunction(): Evaluator =
            match x with
            | CustomOperator f -> f
            | PredefinedOperator mnemonic -> cf mnemonic
            | _ -> failwith "ERROR: Not Yet!!"


    // 기존 FunctionSpec<'T> 에 해당.
    [<DataContract>]
    type TFunction<'T> private (op:Op, args:IExpression seq) =

        let args = args.ToFSharpList()
        let mutable lazyValue:ResettableLazy<'T> = null

        interface INonTerminal<'T>

        interface IExpression<'T> with
            member x.Value with get() = x.Value and set v = x.Value <- v
            member x.TValue with get() = x.TValue and set v = x.Value <- v


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


    type TFunction<'T> with
        new() = TFunction<'T>(Op.Unit, [])   // for Json
        static member Create(op:Op, args:IExpression seq, ?name:string): TFunction<'T> =
            TFunction<'T>(op, args)
                .Tee(fun nt -> nt.OnDeserialized())
                .Tee(fun nt -> name.Iter(fun n -> nt.DD.Add("Name", n)))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string): TFunction<'T> =
            let (f:Evaluator) = fun (args:Arguments) -> evaluator args |> box
            let op = CustomOperator f
            TFunction.Create(op, args, ?name=name)
