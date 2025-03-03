namespace Dual.Ev2

open System
open System.Runtime.Serialization
open System.Reactive.Subjects

open Newtonsoft.Json
open Newtonsoft.Json.Linq

open Dual.Common.Base.FS
open Dual.Common.Core.FS
open Dual.Common.Base.CS
open System.Collections.Generic

[<AutoOpen>]
module rec TExpressionModule =
    type DependentDicType = Dictionary<IValue, HashSet<INonTerminal>>

    type ValueBag private (values:IValue seq, valueChangedSubject:Subject<IValue * obj>, dependents:DependentDicType) =

        interface IValueBag

        member val ValueChangedSubject = valueChangedSubject
        member val Dependents          = dependents
        member val Values              = HashSet(values)     // 사용된 전제 value list

        // todo: call site 에서 assign statement
        // => .Do() 실행 시, 값만 복사해 주는 코드 추가 필요  (따로 dependency 처리는 필요 없을 듯)


        /// dependency -> [dependents]: dependency 가 변경될 때 dependents 가 영향을 받음
        ///
        /// A = B + C, D = A + B  와 같은 함수 수식이 존재할 경우, Dependents dictionary 구성
        ///
        /// A -> [D]
        /// B -> [A; D]
        /// C -> [A]
        member x.AddDependent(dependency:IValue, dependent:INonTerminal) =
            x.Values.Add(dependency) |> ignore
            x.Values.Add(dependent) |> ignore

            match x.Dependents.TryGet(dependency) with
            | Some ds -> ds.Add(dependent) |> ignore
            | None -> x.Dependents.Add( dependency, HashSet([dependent]) ) |> ignore

        /// function (nonTerminal) 내부의 의존성 반전 처리
        member x.AddIntraFunctionDependency(nonTerminal:OFunction) =
            x.Values.Add(nonTerminal) |> ignore
            for arg in nonTerminal.Arguments do
                x.Values.Add(arg) |> ignore
                match arg with
                | :? ValueHolder as vh when vh.IsLiteral -> ()  // literal은 dependency에서 제외
                | _ ->
                    let dependency = arg :> IValue
                    x.AddDependent(dependency, nonTerminal)

        static member Create(?values:IValue seq, ?valueChangedSubject:Subject<IValue * obj>, ?dependents:DependentDicType) =
            let values = values |? Seq.empty
            let dependents          = dependents          |?? (fun () -> DependentDicType())
            let valueChangedSubject = valueChangedSubject |?? (fun () -> new Subject<IValue * obj>())

            // v 의 값 변경으로 인해서 영향 받는 nonTerminal 들을 재귀적으로 찾아서 invalidate 처리.  다음 value 참조시, 새로 계산됨
            let rec invalidate (bag:ValueBag) (v:IValue) =
                match bag.Dependents.TryGet(v) with
                | Some ds ->
                    for nonTerminal in ds do
                        if nonTerminal.Invalidate() then
                            invalidate bag nonTerminal
                | None -> ()

            ValueBag(values, valueChangedSubject, dependents)
            |> tee( fun bag -> valueChangedSubject.Subscribe(fun (v, _) -> invalidate bag v) )



    let theValueBag = ValueBag.Create()

    ///// 값 변경 공지
    //let ValueChangedSubject = theValueBag.ValueChangedSubject

    /// DS 에서는 TValue<'T> 부터 사용.
    type ValueHolder (typ: Type, ?value: obj, ?valueBag:ValueBag) as this =
        let valueBag = valueBag |? theValueBag
        do
            valueBag.Values.Add this |> ignore

        // NsJsonS11nSafeObject 에서 상속받아서는 안됨.  Sealed class
        member val ObjectHolder = NsJsonS11nSafeObject(typ, ?value=value) with get, set

        // 향후 전개 가능한 interface 목록.  실제 interface 의 method 는 구현하지 않고, 확장 method 를 통해 접근
        // e.g IWithName 은 확장을 통해 Name 속성의 get, set 제공
        interface IWithName
        interface IWithAddress
        interface IWithType
        interface IStorage
        interface IValue with
            member x.OValue with get() = x.OValue and set v = x.OValue <- v

        interface IExpression

        [<JsonIgnore>] member val internal ValueBag = valueBag

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
        new () = ValueHolder(typeof<obj>, null)     // *ONLY* for Json
        static member Create(typ:Type, ?value:obj, ?name:string, ?valueBag:ValueBag): ValueHolder =
            ValueHolder(typ, ?value=value, ?valueBag=valueBag)
            |> tee( fun vh -> name.Iter(fun n -> vh.DD.Add("Name", n)) )



        [<JsonIgnore>] member x.ValueType = x.ObjectHolder.Type

        /// ValueHolder.OValue.  Holded value
        [<JsonIgnore>]
        member x.OValue
            with get() = x.ObjectHolder.Value
            and set (v:obj) =
                if x.ObjectHolder.Value <> v then
                    if x.IsLiteral then
                        failwith $"ERROR: {x.Name} is CONSTANT.  It's read-only"
                    x.ObjectHolder.Value <- v
                    x.ValueBag.ValueChangedSubject.OnNext(x, v)

        [<JsonIgnore>] member x.Type = x.ObjectHolder.Type


        /// ValueHolder.Name with DD
        [<JsonIgnore>]
        member x.Name
            with get() = x.DD.TryGet<string>("Name") |? null
            and set (v:string) = x.DD.Set<string>("Name", v)


        /// ValueHolder.Address with DD
        [<JsonIgnore>]
        member x.Address
            with get() = x.DD.TryGet<string>("Address") |? null
            and set (v:string) = x.DD.Set<string>("Address", v)

        /// ValueHolder.Comment with DD
        [<JsonIgnore>]
        member x.Comment
            with get() = x.DD.TryGet<string>("Comment") |? null
            and set (v:string) = x.DD.Set<string>("Comment", v)

        /// ValueHolder.IsLiteral with DD
        [<JsonIgnore>]
        member x.IsLiteral
            with get() = x.DD.TryGet<bool>("IsLiteral") |? false
            and set (v:bool) = x.DD.Set<bool>("IsLiteral", v)

        /// ValueHolder.IsMemberVariable with DD
        /// Timer/Counter 등의 member 변수.  DN,
        [<JsonIgnore>]
        member x.IsMemberVariable
            with get() = x.DD.TryGet<bool>("IsMemberVariable") |? false
            and set (v:bool) = x.DD.Set<bool>("IsMemberVariable", v)

        /// ValueHolder.TagKind with DD
        [<JsonIgnore>]
        member x.TagKind
            with get() = x.DD.TryGet<int>("TagKind") |? 0
            and set (v:int) = x.DD.Set<int>("TagKind", v)

        /// ValueHolder.DsSystem with DD
        [<JsonIgnore>]
        member x.DsSystem
            with get() = x.DD.TryGet<ISystem>("DsSystem") |? getNull<ISystem>()
            and set (v:ISystem) = x.DD.Set<ISystem>("DsSystem", v)

    type TValue<'T>(value:'T, ?valueBag:ValueBag) as this =
        inherit ValueHolder(typedefof<'T>, value, ?valueBag=valueBag)

        let valueBag = valueBag |? theValueBag
        do
            valueBag.Values.Add this |> ignore

        new() = TValue(Unchecked.defaultof<'T>)   // for Json

        interface IWithAddress
        interface ITerminal<'T>
        interface IWithType<'T>
        interface IExpression<'T>
        interface IStorage<'T>

        interface IValue<'T> with
            /// IValue<'T>.OValue interface 구현
            member x.TValue with get() = x.TValue and set v = x.OValue <- v

        /// TValue<'T>.TValue abstract member
        abstract member TValue: 'T with get, set
        /// TValue<'T>.TValue member
        [<JsonIgnore>] default x.TValue with get() = x.OValue :?> 'T and set v = x.OValue <- v





    type Op with
        member x.GetFunction(): Evaluator =
            match x with
            | CustomOperator f -> f
            | PredefinedOperator mnemonic -> cf mnemonic
            | _ -> failwith "ERROR: Not Yet!!"


    // 기존 FunctionSpec<'T> 에 해당.
    [<DataContract>]
    [<AbstractClass>]
    type OFunction internal (op:Op, args:IExpression seq) =

        let args = args.ToFSharpList()

        interface INonTerminal
        interface IValue with
            member x.OValue with get() = x.OValue and set v = failwith "ERROR: Setter unsupported for function value."

        abstract member OValue: obj with get
        abstract member Invalidate: unit -> bool


        [<DataMember>] member val Operator: Op = op with get, set
        [<DataMember>] member val Arguments: IExpression[] = args.ToArray() with get, set

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


    //[<DataContract>]
    type TFunction<'T> private (op:Op, args:IExpression seq, ?valueBag:ValueBag) =
        inherit OFunction(op, args)

        let valueBag = valueBag |? theValueBag
        let mutable lazyValue:ResettableLazy<'T> = null

        interface INonTerminal<'T>

        interface IExpression<'T> with
            /// IExpression<'T>.OValue interface 구현 (@TFunction<'T>)
            member x.OValue with get() = x.OValue and set v = x.OValue <- v
            /// IExpression<'T>.TValue interface 구현 (@TFunction<'T>)
            member x.TValue with get() = x.TValue and set v = x.OValue <- v

        /// TFunction<'T>.OValue member
        [<JsonIgnore>] override x.OValue = lazyValue.Value |> box
        /// TFunction<'T>.TValue member
        [<JsonIgnore>] member x.TValue = lazyValue.Value

        //[<JsonIgnore>] member val internal ValueBag = valueBag |? theValueBag

        override x.Invalidate() =
            lazyValue.Reset()

        member internal x.OnDeserialized() =
            lazyValue <- ResettableLazy<'T>(fun () ->
                let objValue:obj =
                    let f, args = x.Operator.GetFunction(), x.Arguments.ToFSharpList()
                    f args
                objValue :?> 'T
            )
            lazyValue.OnValueChanged <- fun v -> valueBag.ValueChangedSubject.OnNext(x, v)
            noop()

        // F#에서는 어트리뷰트를 [<OnDeserialized>] 형식으로 사용해야 합니다.
        /// Deserialize 된 이후에 처리해야 할 작업 지정
        [<OnDeserialized>]
        member this.OnDeserializedMethod(context: StreamingContext) = this.OnDeserialized()



    type TFunction<'T> with
        /// *ONLY* for Json
        new() = TFunction<'T>(Op.Unit, [])

        static member Create(op:Op, args:IExpression seq, ?name:string, ?valueBag:ValueBag): TFunction<'T> =
            let valueBag = valueBag |? theValueBag
            TFunction<'T>(op, args, valueBag)
            |> tee(fun nt ->
                nt.OnDeserialized()
                name.Iter(fun n -> nt.DD.Add("Name", n))
                valueBag.AddIntraFunctionDependency(nt)
                valueBag.Values.Add(nt))

        static member Create(evaluator:Arguments -> 'T, args:IExpression seq, ?name:string, ?valueBag:ValueBag): TFunction<'T> =
            let (f:Evaluator) = fun (args:Arguments) -> evaluator args |> box
            let op = CustomOperator f
            TFunction.Create(op, args, ?name=name, ?valueBag=valueBag)


    type IValue with
        member x.EnumerateValueObjects(?includeMe:bool, ?evaluator:obj -> bool): IValue seq =
            let includeMe = includeMe |? false
            let evaluator = evaluator |? (fun _ -> true)
            seq {
                if includeMe && evaluator x then
                    yield x
                match x with
                | :? OFunction as f ->
                    for arg in f.Arguments |> Seq.cast<IValue> do
                        yield! arg.EnumerateValueObjects(true)
                | _ -> ()
            }

    type INonTerminal with
        member x.Arguments =
            match x with
            | :? OFunction as f -> f.Arguments
            | _ -> failwith "ERROR: Not Yet!!"
        member x.Invalidate(): bool =
            match x with
            | :? OFunction as f -> f.Invalidate()
            | _ -> failwith "ERROR: Not Yet!!"
