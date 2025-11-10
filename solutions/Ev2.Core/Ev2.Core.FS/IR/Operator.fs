namespace Ev2.Core.FS.IR
open System
open System.Linq
open Dual.Common.Base
open Ev2.Core.FS.IR

type Arguments = IExpression[]
type Arguments<'T> = IExpression<'T>[]

[<AbstractClass>]
type Operator(name:string, arguments:Arguments) =
    member x.Name = name
    member x.Arguments = arguments


type Operator<'T>(name:string, arguments:Arguments) =
    inherit Operator(name, arguments)

    new() = Operator<'T>(nullString, [||])
    member x.ReturnType = typeof<'T>
    member val Evaluator: (Arguments -> 'T) = fun _ -> failwithMessage "Should be re-implemented" with get, set
    member x.TValue = x.Evaluator(x.Arguments)
    interface IExpression<'T> with
        member x.DataType = x.ReturnType
        member x.Value with get() = box x.TValue and set v = failwithMessage "Unsupported operation"
        member x.TValue = x.TValue


[<AutoOpen>]
module OperatorEvaluators =
    let inline private opaqueArgs (args:Arguments<'T>) = args.Cast<IExpression>().ToArray()
    let inline private valueArgs<'T> (args:Arguments) = args.Cast<IExpression<'T>>().Map _.TValue

    let inline add< ^T when ^T : (static member (+) : ^T * ^T -> ^T) > (args:Arguments< ^T >) =
        Operator< ^T >("+", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (+)) )
    let inline sub< ^T when ^T : (static member (-) : ^T * ^T -> ^T) > (args:Arguments< ^T >) =
        Operator< ^T >("-", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (-)) )
    let inline mul< ^T when ^T : (static member (*) : ^T * ^T -> ^T) > (args:Arguments< ^T >) =
        Operator< ^T >("*", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (*)) )
    let inline div< ^T when ^T : (static member (/) : ^T * ^T -> ^T) > (args:Arguments< ^T >) =
        Operator< ^T >("/", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (/)) )
    let inline modulo< ^T when ^T : (static member (%) : ^T * ^T -> ^T) > (args:Arguments< ^T >) =
        Operator< ^T >("%", opaqueArgs args, Evaluator=(valueArgs >> Seq.reduce (%)) )
    let inline pow< ^T
        when ^T : (static member ( * ) : ^T * ^T -> ^T)
        and  ^T : (static member One : ^T) >
        (bse:IExpression< ^T >, pwr:IExpression<int>)
      =
        Operator< ^T >("power", [| bse; pwr |],
            Evaluator = fun args ->
                let baseValue = (args[0] :?> IExpression< ^T >).TValue
                let powerValue = (args[1] :?> IExpression<int>).TValue
                if powerValue < 0 then
                    failwithf "power 연산은 음수 지수를 지원하지 않습니다. 값: %d" powerValue
                let rec loop acc current exponent =
                    if exponent = 0 then
                        acc
                    else
                        let nextAcc = if (exponent &&& 1) = 1 then acc * current else acc
                        loop nextAcc (current * current) (exponent >>> 1)
                loop (LanguagePrimitives.GenericOne< ^T >) baseValue powerValue)

    /// 형변환 연산자 구현
    let inline cast<'T>(value:IExpression) =
        Operator<'T>("CAST", [| value |],
            Evaluator = fun args ->
                let valueExpr = args[0]
                Convert.ChangeType(valueExpr.Value, typeof<'T>) |> unbox<'T>)



    let inline private createComparisonOperator<'T when 'T: comparison> (name, operator:'T -> 'T -> bool) (a:IExpression<'T>, b:IExpression<'T>) =
        Operator<bool>(name, [| a; b |],
            Evaluator=fun args ->
                let args = args.Cast<IExpression<'T>>().ToArray()
                operator args[0].TValue args[1].TValue)

    let ge<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">=", (>=)) (a, b)
    let gt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator (">",  (>))  (a, b)
    let lt<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<",  (<))  (a, b)
    let le<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<=", (<=)) (a, b)
    let eq<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("=",  (=))  (a, b)
    let ne<'T when 'T: comparison> (a:IExpression<'T>) (b:IExpression<'T>) = createComparisonOperator ("<>",  (<>)) (a, b)


    /// ShiftLeft 연산자 (<<<) 구현
    let inline shl<'T> (value:IExpression<'T>) (shift:IExpression<int>) =
        Operator<'T>("SHL", [| value; shift |],
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
        Operator<'T>("SHR", [| value; shift |],
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

    /// Bitwise AND 연산자 (&&&) 구현
    let band<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("AND", [| lhs; rhs |],
            Evaluator = fun args ->
                let leftExpr  = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise AND" bitwiseAndOps leftExpr rightExpr)

    /// Bitwise OR 연산자 (|||) 구현
    let bor<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("OR", [| lhs; rhs |],
            Evaluator = fun args ->
                let leftExpr  = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise OR" bitwiseOrOps leftExpr rightExpr)

    /// Bitwise XOR 연산자 (^^^) 구현
    let bxor<'T> (lhs:IExpression<'T>) (rhs:IExpression<'T>) =
        Operator<'T>("XOR", [| lhs; rhs |],
            Evaluator = fun args ->
                let leftExpr  = args[0] :?> IExpression<'T>
                let rightExpr = args[1] :?> IExpression<'T>
                applyIntegralBinary "Bitwise XOR" bitwiseXorOps leftExpr rightExpr)

    /// Bitwise NOT 연산자 (~~~) 구현
    let bnot<'T> (value:IExpression<'T>) =
        Operator<'T>("NOT", [| value |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression<'T>
                applyIntegralUnary "Bitwise NOT" bitwiseNotOps valueExpr)

    /// Bool Logical AND
    let logicalAnd (lhs:IExpression<bool>) (rhs:IExpression<bool>) =
        Operator<bool>("LAND", [| lhs; rhs |],
            Evaluator = fun args ->
                let leftExpr  = args[0] :?> IExpression<bool>
                let rightExpr = args[1] :?> IExpression<bool>
                leftExpr.TValue && rightExpr.TValue)

    /// Bool Logical OR
    let logicalOr (lhs:IExpression<bool>) (rhs:IExpression<bool>) =
        Operator<bool>("LOR", [| lhs; rhs |],
            Evaluator = fun args ->
                let leftExpr  = args[0] :?> IExpression<bool>
                let rightExpr = args[1] :?> IExpression<bool>
                leftExpr.TValue || rightExpr.TValue)

    /// Bool Logical NOT
    let logicalNot (value:IExpression<bool>) =
        Operator<bool>("LNOT", [| value |],
            Evaluator = fun args ->
                let valueExpr = args[0] :?> IExpression<bool>
                not valueExpr.TValue)

    // 단항 실수 함수(Math.* : double->double)를 IExpression<'T>용으로 리프트
    let inline private liftUnaryFloat (opName:string) (f: double -> double) (arg: IExpression<'T>) =
        Operator<'T>(
            opName,
            [| arg |],
            Evaluator = fun args ->
                let a = args[0] :?> IExpression<'T>
                match box a.TValue with
                | :? single as v -> f (float v) |> float32 |> unbox<'T>
                | :? double as v -> f v         |> unbox<'T>
                | _ -> failwithf $"지원하지 않는 형식에 대한 {opName} 연산입니다: {a.DataType}"
        )

    let cos  (x: IExpression<'T>) = liftUnaryFloat "COS"  Math.Cos  x
    let sin  (x: IExpression<'T>) = liftUnaryFloat "SIN"  Math.Sin  x
    let tan  (x: IExpression<'T>) = liftUnaryFloat "TAN"  Math.Tan  x
    let acos (x: IExpression<'T>) = liftUnaryFloat "ACOS" Math.Acos x
    let asin (x: IExpression<'T>) = liftUnaryFloat "ASIN" Math.Asin x
    let atan (x: IExpression<'T>) = liftUnaryFloat "ATAN" Math.Atan x
    let exp  (x: IExpression<'T>) = liftUnaryFloat "EXP"  Math.Exp  x
    let log  (x: IExpression<'T>) = liftUnaryFloat "LOG"  Math.Log  x
    let sqrt (x: IExpression<'T>) = liftUnaryFloat "SQRT" Math.Sqrt x
