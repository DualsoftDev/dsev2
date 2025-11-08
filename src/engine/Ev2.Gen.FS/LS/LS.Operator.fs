namespace Ev2.Gen
open System
open System.Linq
open Dual.Common.Base

type Arguments = IExpression[]
type Arguments<'T> = IExpression<'T>[]

type Operator<'T>(name:string, arguments:Arguments) =
    new() = Operator<'T>(nullString, [||])
    member x.Name = name
    member x.Arguments = arguments
    member x.ReturnType = typeof<'T>
    member val Evaluator: (Arguments -> 'T) = fun _ -> fail() with get, set
    member x.TValue = x.Evaluator(x.Arguments)
    interface IExpression<'T> with
        member x.DataType = x.ReturnType
        //member x.Value = x.TValue
        member x.Value with get() = box x.TValue and set v = fail()
        member x.TValue = x.TValue


[<AutoOpen>]
module OperatorEvaluators =
    let inline private opaqueArgs (args:Arguments<'T>) = args.Cast<IExpression>().ToArray()
    let inline private valueArgs<'T> (args:Arguments) = args.Cast<IExpression<'T>>().Map _.TValue

    let inline add<'T when 'T : (static member (+) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("+", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (+)) )
    let inline sub<'T when 'T : (static member (-) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("-", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (-)) )
    let inline mul<'T when 'T : (static member (*) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("*", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (*)) )
    let inline div<'T when 'T : (static member (/) : 'T * 'T -> 'T)> (args:Arguments<'T>) =
        Operator<'T>("/", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (/)) )


    let inline private createComparisonOperator<'T when 'T: comparison> (name, operator:'T -> 'T -> bool) (a:IExpression<'T>, b:IExpression<'T>) =
        Operator<bool>(name, [| a :> IExpression; b |],
            Evaluator=fun args ->
                let args = args.Cast<IExpression<'T>>().ToArray()
                operator args[0].TValue args[1].TValue)

    let ge<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">=", (>=)) (a, b)
    let gt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">",  (>))  (a, b)
    let eq<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("=",  (=))  (a, b)
    let lt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<",  (<))  (a, b)
    let le<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<=", (<=)) (a, b)


    /// ShiftLeft 연산자 (<<<) 구현
    let inline shl<'T> (value:IExpression<'T>) (shift:IExpression<int>) =
        Operator<'T>("SHL", [| value :> IExpression; shift :> IExpression |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression< 'T >
                let shift = args[1] :?> IExpression<int> |> _.TValue
                match box valueExpr.TValue with
                | :? int8   as v -> v <<< shift |> unbox<'T>
                | :? int16  as v -> v <<< shift |> unbox<'T>
                | :? int32  as v -> v <<< shift |> unbox<'T>
                | :? int64  as v -> v <<< shift |> unbox<'T>
                | :? uint8  as v -> v <<< shift |> unbox<'T>
                | :? uint16 as v -> v <<< shift |> unbox<'T>
                | :? uint32 as v -> v <<< shift |> unbox<'T>
                | :? uint64 as v -> v <<< shift |> unbox<'T>
                | _ -> failwithf "지원하지 않는 형식에 대한 ShiftLeft 연산입니다: %s" (valueExpr.DataType.FullName)
        )


    /// ShiftRight 연산자 (>>>) 구현
    let inline shr<'T> (value:IExpression<'T>) (shift:IExpression<int>) =
        Operator<'T>("SHR", [| value :> IExpression; shift :> IExpression |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression< 'T >
                let shift = args[1] :?> IExpression<int> |> _.TValue
                match box valueExpr.TValue with
                | :? int8   as v -> v >>> shift |> unbox<'T>
                | :? int16  as v -> v >>> shift |> unbox<'T>
                | :? int32  as v -> v >>> shift |> unbox<'T>
                | :? int64  as v -> v >>> shift |> unbox<'T>
                | :? uint8  as v -> v >>> shift |> unbox<'T>
                | :? uint16 as v -> v >>> shift |> unbox<'T>
                | :? uint32 as v -> v >>> shift |> unbox<'T>
                | :? uint64 as v -> v >>> shift |> unbox<'T>
                | _ -> failwithf "지원하지 않는 형식에 대한 ShiftRight 연산입니다: %s" (valueExpr.DataType.FullName)
        )

    type private IntegralBinaryOps = {
        Int8  : int8   -> int8   -> int8
        Int16 : int16  -> int16  -> int16
        Int32 : int32  -> int32  -> int32
        Int64 : int64  -> int64  -> int64
        UInt8 : uint8  -> uint8  -> uint8
        UInt16: uint16 -> uint16 -> uint16
        UInt32: uint32 -> uint32 -> uint32
        UInt64: uint64 -> uint64 -> uint64
    }

    type private IntegralUnaryOps = {
        Int8  : int8   -> int8
        Int16 : int16  -> int16
        Int32 : int32  -> int32
        Int64 : int64  -> int64
        UInt8 : uint8  -> uint8
        UInt16: uint16 -> uint16
        UInt32: uint32 -> uint32
        UInt64: uint64 -> uint64
    }

    let inline private applyIntegralBinary opName (ops:IntegralBinaryOps) (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        let leftObj = box lhs.TValue
        let rightObj = box rhs.TValue
        match leftObj with
        | :? int8   -> ops.Int8   (Unchecked.unbox<int8>   leftObj) (Unchecked.unbox<int8>   rightObj) |> box |> unbox<'T>
        | :? int16  -> ops.Int16  (Unchecked.unbox<int16>  leftObj) (Unchecked.unbox<int16>  rightObj) |> box |> unbox<'T>
        | :? int32  -> ops.Int32  (Unchecked.unbox<int32>  leftObj) (Unchecked.unbox<int32>  rightObj) |> box |> unbox<'T>
        | :? int64  -> ops.Int64  (Unchecked.unbox<int64>  leftObj) (Unchecked.unbox<int64>  rightObj) |> box |> unbox<'T>
        | :? uint8  -> ops.UInt8  (Unchecked.unbox<uint8>  leftObj) (Unchecked.unbox<uint8>  rightObj) |> box |> unbox<'T>
        | :? uint16 -> ops.UInt16 (Unchecked.unbox<uint16> leftObj) (Unchecked.unbox<uint16> rightObj) |> box |> unbox<'T>
        | :? uint32 -> ops.UInt32 (Unchecked.unbox<uint32> leftObj) (Unchecked.unbox<uint32> rightObj) |> box |> unbox<'T>
        | :? uint64 -> ops.UInt64 (Unchecked.unbox<uint64> leftObj) (Unchecked.unbox<uint64> rightObj) |> box |> unbox<'T>
        | _ -> failwithf "지원하지 않는 형식에 대한 %s 연산입니다: %s" opName (lhs.DataType.FullName)

    let inline private applyIntegralUnary opName (ops:IntegralUnaryOps) (value:IExpression<'T>) =
        let valueObj = box value.TValue
        match valueObj with
        | :? int8   -> ops.Int8   (Unchecked.unbox<int8>   valueObj) |> box |> unbox<'T>
        | :? int16  -> ops.Int16  (Unchecked.unbox<int16>  valueObj) |> box |> unbox<'T>
        | :? int32  -> ops.Int32  (Unchecked.unbox<int32>  valueObj) |> box |> unbox<'T>
        | :? int64  -> ops.Int64  (Unchecked.unbox<int64>  valueObj) |> box |> unbox<'T>
        | :? uint8  -> ops.UInt8  (Unchecked.unbox<uint8>  valueObj) |> box |> unbox<'T>
        | :? uint16 -> ops.UInt16 (Unchecked.unbox<uint16> valueObj) |> box |> unbox<'T>
        | :? uint32 -> ops.UInt32 (Unchecked.unbox<uint32> valueObj) |> box |> unbox<'T>
        | :? uint64 -> ops.UInt64 (Unchecked.unbox<uint64> valueObj) |> box |> unbox<'T>
        | _ -> failwithf "지원하지 않는 형식에 대한 %s 연산입니다: %s" opName (value.DataType.FullName)


    let private bitwiseAndOps : IntegralBinaryOps = {
        Int8   = (fun l r -> l &&& r)
        Int16  = (fun l r -> l &&& r)
        Int32  = (fun l r -> l &&& r)
        Int64  = (fun l r -> l &&& r)
        UInt8  = (fun l r -> l &&& r)
        UInt16 = (fun l r -> l &&& r)
        UInt32 = (fun l r -> l &&& r)
        UInt64 = (fun l r -> l &&& r)
    }

    let private bitwiseOrOps : IntegralBinaryOps = {
        Int8   = (fun l r -> l ||| r)
        Int16  = (fun l r -> l ||| r)
        Int32  = (fun l r -> l ||| r)
        Int64  = (fun l r -> l ||| r)
        UInt8  = (fun l r -> l ||| r)
        UInt16 = (fun l r -> l ||| r)
        UInt32 = (fun l r -> l ||| r)
        UInt64 = (fun l r -> l ||| r)
    }

    let private bitwiseXorOps : IntegralBinaryOps = {
        Int8   = (fun l r -> l ^^^ r)
        Int16  = (fun l r -> l ^^^ r)
        Int32  = (fun l r -> l ^^^ r)
        Int64  = (fun l r -> l ^^^ r)
        UInt8  = (fun l r -> l ^^^ r)
        UInt16 = (fun l r -> l ^^^ r)
        UInt32 = (fun l r -> l ^^^ r)
        UInt64 = (fun l r -> l ^^^ r)
    }

    let private bitwiseNotOps : IntegralUnaryOps = {
        Int8   = (fun x -> ~~~x)
        Int16  = (fun x -> ~~~x)
        Int32  = (fun x -> ~~~x)
        Int64  = (fun x -> ~~~x)
        UInt8  = (fun x -> ~~~x)
        UInt16 = (fun x -> ~~~x)
        UInt32 = (fun x -> ~~~x)
        UInt64 = (fun x -> ~~~x)
    }

    let band<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("AND", [| lhs :> IExpression; rhs :> IExpression |],
            Evaluator = fun args ->
                let leftExpr = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise AND" bitwiseAndOps leftExpr rightExpr)

    let bor<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("OR", [| lhs :> IExpression; rhs :> IExpression |],
            Evaluator = fun args ->
                let leftExpr = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise OR" bitwiseOrOps leftExpr rightExpr)

    let bxor<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("XOR", [| lhs :> IExpression; rhs :> IExpression |],
            Evaluator = fun args ->
                let leftExpr = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise XOR" bitwiseXorOps leftExpr rightExpr)

    let bnot<'T> (value:IExpression<'T>) =
        Operator<'T>("NOT", [| value :> IExpression |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression<'T>
                applyIntegralUnary "Bitwise NOT" bitwiseNotOps valueExpr)
