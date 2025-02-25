namespace rec Dual.Ev2

(* 기존 Engine.Core.Expression.Function.fs *)

open System
open System.Linq
open System.Runtime.CompilerServices
open Dual.Common.Core.FS
open System.Collections.Generic


module private ExpressionHelperModule =
    let expectN (n:int) (xs:'a seq) = if xs.Count() <> n then failwith $"Wrong number of arguments: expect {n}"
    let expect1 xs = expectN 1 xs; xs.First()
    let expect2 xs = expectN 2 xs; Array.ofSeq xs
    let expectGteN (n:int) (xs:'a seq) =
        if xs.Count() < n then
            failwith $"Wrong number of arguments: expect at least {n} arguments"

        xs

    let evalArg (x:IExpression) = x.BoxedEvaluatedValue
    let castTo<'T> (x:obj) = x :?> 'T
    let evalTo<'T> (x:IExpression) = x |> evalArg |> castTo<'T>

    /// 모든 args 의 data type 이 동일한지 여부 반환
    let isAllExpressionSameType(args:Args) =
        let types = args |> Seq.distinctBy(fun a -> a.DataType) |> Seq.map(fun a ->a.BoxedEvaluatedValue, a.DataType)
        types|> Seq.length = 1
    let verifyAllExpressionSameType = isAllExpressionSameType >> verifyM "Type mismatch"

    let private operatorsRequireAllArgumentsSameType =
        [   "+" ; "-" ; "*" ; "/" ; "%"
            ">";">=";"<";"<=";"==";"!=";"<>"
            "&&" ; "||"
            "&" ; "|" ; "&&&" ; "|||"
            "add"; "sub"; "mul"; "div"
            "gt"; "gte"; "lt"; "lte"
            "equal"; "notEqual"; "and"; "or"
        ] |> HashSet<string>

    let isThisOperatorRequireAllArgumentsSameType: (string -> bool) = operatorsRequireAllArgumentsSameType.Contains

    let verifyArgumentsTypes operator args =
        if isThisOperatorRequireAllArgumentsSameType operator && not <| isAllExpressionSameType args then
            failwith $"Type mismatch for operator   '{operator}'"
[<AutoOpen>]
module ExpressionFunctionModule =
    open ExpressionHelperModule

    /// Expression<'T> 를 IExpression 으로 casting
    let internal iexpr any = (box any) :?> IExpression
    let NullFunction<'T> (_args:Args):'T = failwithlog "THIS IS PSEUDO FUNCTION.  SHOULD NOT BE EVALUATED!!!!"

    /// argument 는 TERMINAL(variable) 이어야 함.  해당 argument 의 rising 검출 함수
    let [<Literal>] FunctionNameRising  = "rising"
    let [<Literal>] FunctionNameFalling = "falling"
    /// argument 는 boolean expression.  해당 expression 전체 수행 결과의 rising 검출 함수.  ladder 상에서 해당 expression 뒤에(AFTER) rising 검출
    let [<Literal>] FunctionNameRisingAfter  = "risingAfter"
    let [<Literal>] FunctionNameFallingAfter = "fallingAfter"



    let predefinedFunctionNames =
        [|
            "+"; "add";
            "-"; "sub";
            "*"; "mul";
            "/"; "div";

            ">" ; "gt";
            ">="; "gte";
            "<" ; "lt";
            "<="; "lte";

            "==" ; "equal";
            "==" ; "equal";
            "!="; "<>"; "notEqual"
            "!="; "<>"; "^^"; "notEqual"

            "<<"; "<<<"; "shiftLeft"
            ">>"; ">>>"; "shiftRight"

            "&&"; "and"
            "||"; "or"

            "!" ; "not";
            "&"; "&&&" ;
            "|"; "|||" ;
            "^"; "^^^" ;
            "~"; "~~~" ;

            FunctionNameRising;
            FunctionNameFalling;


            FunctionNameRisingAfter;
            FunctionNameFallingAfter;


            "bool"  ; "toBool";
            "sbyte" ; "toSByte"; "toInt8";
            "byte"  ; "toByte" ; "toUInt8";
            "short" ; "toShort"; "toInt16";
            "ushort"; "toUShort"; "toUInt16";
            "int"   ; "toInt"  ; "toInt32";
            "uint"  ; "toUInt" ; "toUInt32";
            "long"  ; "toLong" ; "toInt64";
            "ulong" ; "toULong"; "toUInt64";

            "single"; "float"; "float32"; "toSingle"; "toFloat"; "toFloat32";
            "double"; "float64"; "toDouble"; "toFloat64";

            "sin";
            "cos";
            "tan";
            "abs";

            "createXgiCTU"; "createXgiCTD"; "createXgiCTUD"; "createXgiCTR";
            "createXgkCTU"; "createXgkCTD"; "createXgkCTUD"; "createXgkCTR";
            "createWinCTU"; "createWinCTD"; "createWinCTUD"; "createWinCTR";
            "createAbCTU" ; "createAbCTD" ; "createAbCTUD" ; "createAbCTR";
            "createXgiTON"; "createXgiTOF"; "createXgiCRTO";
            "createXgkTON"; "createXgkTOF"; "createXgkCRTO";
            "createWinTON"; "createWinTOF"; "createWinCRTO";
            "createAbTON" ; "createAbTOF" ; "createAbCRTO";
            "createTag"
        |] |> HashSet


    /// (TEvaluator<'T>: Args -> 'T) 함수를 (Evaluator: Args -> obj) 로 boxing
    let private boxF<'T> (f:TEvaluator<'T>) : Evaluator = fun (args:Args) -> f args |> box

    /// Create (known) function
    [<Obsolete("Todo: Uncomment")>]
    let cf<'T> (funName:string) : Evaluator =
        predefinedFunctionNames.Contains(funName) |> verifyM $"Undefined function: {funName}"


        match funName with
        | ("+" | "add") -> boxF fAdd<'T>
        | ("-" | "sub") -> boxF fSub<'T>
        | ("*" | "mul") -> boxF fMul<'T>
        | ("/" | "div") -> boxF fDiv<'T>

        | (">"  | "gt")  -> boxF fGt
        | (">=" | "gte") -> boxF fGte
        | ("<"  | "lt")  -> boxF fLt
        | ("<=" | "lte") -> boxF fLte

        | ("=="  | "equal") -> boxF fEqual
        | ("!=" | "<>" | "notEqual") -> boxF fNotEqual

        | ("<<" | "<<<" | "shiftLeft")  -> boxF fShiftLeft<'T>
        | (">>" | ">>>" | "shiftRight") -> boxF fShiftLeft<'T>

        | ("&&" | "and") -> boxF fbLogicalAnd
        | ("||" | "or")  -> boxF fbLogicalOr

        | ("!"  | "not") -> boxF fbLogicalNot

        | ("&" | "&&&") -> boxF fBitwiseAnd<'T>
        | ("|" | "|||") -> boxF fBitwiseOr<'T>
        | ("^" | "^^^") -> boxF fBitwiseXor<'T>
        | ("~" | "~~~") -> boxF fBitwiseNot<'T>

        | FunctionNameRising  -> boxF fbRising
        | FunctionNameFalling -> boxF fbFalling

        | FunctionNameRisingAfter  -> boxF fbRisingAfter
        | FunctionNameFallingAfter -> boxF fbFallingAfter

        //| "neg"     -> fNegate  args
        //| "set"     -> fSet     args
        //| "reset"   -> fReset   args


        | ("bool"   | "toBool") -> boxF fCastBool
        | ("sbyte"  | "toSByte" | "toInt8")     -> boxF fCastInt8
        | ("byte"   | "toByte"  | "toUInt8")    -> boxF fCastUInt8
        | ("short"  | "toShort" | "toInt16")    -> boxF fCastInt16
        | ("ushort" | "toUShort"| "toUInt16")   -> boxF fCastUInt16
        | ("int"    | "toInt"   | "toInt32")    -> boxF fCastInt32
        | ("uint"   | "toUInt"  | "toUInt32")   -> boxF fCastUInt32
        | ("long"   | "toLong"  | "toInt64")    -> boxF fCastInt64
        | ("ulong"  | "toULong" | "toUInt64")   -> boxF fCastUInt64

        | ("single" | "float" | "float32" | "toSingle"| "toFloat" | "toFloat32") -> boxF fCastFloat32
        | ("double" | "float64" | "toDouble"| "toFloat64" ) -> boxF fCastFloat64

        | "sin" -> boxF fSin
        | "cos" -> boxF fCos
        | "tan" -> boxF fTan
        | "abs" -> boxF fAbs

        // todo : uncomment

        //(* Timer/Counter
        //  - 실제로 function/expression 은 아니지만, parsing 편의를 고려해 function 처럼 취급.
        //  - evaluate 등은 수행해서는 안된다.
        //*)
        //| (   "createXgiCTU" | "createXgiCTD" | "createXgiCTUD" | "createXgiCTR"
        //    | "createXgkCTU" | "createXgkCTD" | "createXgkCTUD" | "createXgkCTR"
        //    | "createWinCTU" | "createWinCTD" | "createWinCTUD" | "createWinCTR"
        //    | "createAbCTU"  | "createAbCTD"  | "createAbCTUD"  | "createAbCTR" ) ->
        //        TExpression<Counter>.Create(funName, args, NullFunction<Counter>)
        //        //DuFunction { FunctionBody=NullFunction<Counter>; Name=funName; Arguments=args; LambdaDecl=None; LambdaApplication=None }
        //| (   "createXgiTON" | "createXgiTOF" | "createXgiCRTO"
        //    | "createXgkTON" | "createXgkTOF" | "createXgkCRTO"
        //    | "createWinTON" | "createWinTOF" | "createWinCRTO"
        //    | "createAbTON"  | "createAbTOF"  | "createAbCRTO") ->
        //        DuFunction { FunctionBody=NullFunction<Timer>; Name=funName; Arguments=args; LambdaDecl=None; LambdaApplication=None }
        //| "createTag" ->
        //        DuFunction { FunctionBody=NullFunction<ITag>; Name=funName; Arguments=args; LambdaDecl=None; LambdaApplication=None }

        | _ -> failwith $"NOT yet: {funName}"


    [<AutoOpen>]
    module internal FunctionImpl =
        type SeqExt =
            [<Extension>] static member ExpectGteN(xs:'a seq, n) = expectGteN n xs
            [<Extension>] static member Expect1(xs:'a seq) = expect1 xs
            [<Extension>] static member Expect2(xs:'a seq) = expect2 xs
            [<Extension>]
            static member ExpectTyped2<'U, 'V>(Array(xs:IExpression [])) =
                let arg0 = xs[0] |> evalTo<'U>
                let arg1 = xs[1] |> evalTo<'V>
                arg0, arg1

        let fEqual   (args:Args): bool = args.ExpectGteN(2).Select(evalArg).Pairwise().All(fun (x, y) -> isEqual x y)
        let fNotEqual (args:Args): bool = not <| fEqual args

        let private convertToDoublePair (args:Args) = args.ExpectGteN(2).Select(fun x -> x.BoxedEvaluatedValue |> toFloat64).Pairwise()
        let fGt  (args:Args): bool = convertToDoublePair(args).All(fun (x, y) -> x > y)
        let fLt  (args:Args): bool = convertToDoublePair(args).All(fun (x, y) -> x < y)
        let fGte (args:Args): bool = convertToDoublePair(args).All(fun (x, y) -> x >= y)
        let fLte (args:Args): bool = convertToDoublePair(args).All(fun (x, y) -> x <= y)

        let fConcat     (args:Args): string = args.ExpectGteN(2).Select(evalArg).Cast<string>().Reduce( + )
        let fbLogicalAnd (args:Args): bool = args.ExpectGteN(2).Select(evalArg).Cast<bool>()  .Reduce( && )
        let fbLogicalOr  (args:Args): bool = args.ExpectGteN(2).Select(evalArg).Cast<bool>()  .Reduce( || )
        let fbLogicalNot (args:Args): bool = args.Select(evalArg).Cast<bool>().Expect1() |> not

        //let errorPCRunmode(_args:Args, funName:string) =  //PC 모드일때만 예외 (위치 수정 필요)
        //    if RuntimeDS.Package.IsPCorPCSIM() then
        //        failwithlog $"""Error: {funName} is a PLC-only formula. ({String.Join(", ", _args.Map(fun a->a.ToText()))})"""
        //    else false

        let fbRising (_args:Args) : bool =       false//    errorPCRunmode(_args, "rising")
        let fbFalling (_args:Args) : bool =      false//    errorPCRunmode(_args, "falling")
        let fbRisingAfter (_args:Args) : bool =  false//    errorPCRunmode(_args, "risingAfter")
        let fbFallingAfter (_args:Args) : bool=  false//    errorPCRunmode(_args, "fallingAfter")

        let fCastUInt8   (args:Args) = args.Select(evalArg >> toUInt8)   .Expect1()
        let fCastInt8    (args:Args) = args.Select(evalArg >> toInt8)    .Expect1()
        let fCastInt16   (args:Args) = args.Select(evalArg >> toInt16)   .Expect1()
        let fCastUInt16  (args:Args) = args.Select(evalArg >> toUInt16)  .Expect1()
        let fCastInt32   (args:Args) = args.Select(evalArg >> toInt32)   .Expect1()
        let fCastUInt32  (args:Args) = args.Select(evalArg >> toUInt32)  .Expect1()
        let fCastInt64   (args:Args) = args.Select(evalArg >> toInt64)   .Expect1()
        let fCastUInt64  (args:Args) = args.Select(evalArg >> toUInt64)  .Expect1()

        let fCastBool    (args:Args) = args.Select(evalArg >> toBool)    .Expect1()
        let fCastFloat32 (args:Args) = args.Select(evalArg >> toFloat32) .Expect1()
        let fCastFloat64 (args:Args) = args.Select(evalArg >> toFloat64) .Expect1()

    [<AutoOpen>]
    module FunctionModule =
        (*
             .f  | Single       | single
             .   | Double       | double    float (!! 헷갈림 주이)
             y   | SByte        | int8      sbyte
             uy  | Byte         | uint8     byte
             s   | Int16        | int16
             us  | UInt16       | uint16
             -   | Int32        | int32     int
             u   | UInt32       | uint32
             L   | Int64        | int64
             UL  | UInt64       | uint64
        *)
        let castArgs<'T>  (args:Args): 'T seq   = args.Select(fun x-> x.BoxedEvaluatedValue :?> 'T)
        let castArg<'T>   (args:Args): 'T       = args.ExactlyOne().BoxedEvaluatedValue :?> 'T
        let shiftArgs<'T> (args:Args): 'T * int = args.ExpectTyped2<'T, int>()




        // EV1: createBinaryExpression: IExpression -> (op:string) -> IExpression -> IExpression
        let createBinaryFunction<'T> (op: string) : 'T -> 'T -> 'T =
            let t = typeof<'T>

            fun (x:'T) (y:'T) ->
                let tn =
                    if t.Name = OBJECT then
                        x.GetType().Name
                    else
                        t.Name
                match op with
                | "+" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  + toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x + toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x + toFloat64 y)
                    | STRING                            -> box (x.ToString() + y.ToString())
                    | _ -> failwith "ERROR"
                | "-" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  - toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x - toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x - toFloat64 y)
                    | _ -> failwith "ERROR"
                | "*" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  * toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x * toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x * toFloat64 y)
                    | _ -> failwith "ERROR"
                | "/" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  / toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x / toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x / toFloat64 y)
                    | _ -> failwith "ERROR"
                | "%" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  % toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x % toUInt64 y)
                    | _ -> failwith "ERROR"

                | ">" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  > toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x > toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x > toFloat64 y)
                    | _ -> failwith "ERROR"
                | ">=" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  >= toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x >= toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x >= toFloat64 y)
                    | _ -> failwith "ERROR"
                | "<" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  < toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x < toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x < toFloat64 y)
                    | _ -> failwith "ERROR"
                | "<=" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  <= toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x <= toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x <= toFloat64 y)
                    | _ -> failwith "ERROR"
                | "==" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  = toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x = toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x = toFloat64 y)
                    | _ -> failwith "ERROR"
                | "!=" | "<>" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  <> toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x <> toUInt64 y)
                    | FLOAT32 | FLOAT64                 -> box (toFloat64 x <> toFloat64 y)
                    | _ -> failwith "ERROR"
                | "<<" | "<<<" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  <<< toInt32 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x <<< toInt32 y)
                    | _ -> failwith "ERROR"
                | ">>" | ">>>" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  >>> toInt32 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x >>> toInt32 y)
                    | _ -> failwith "ERROR"
                | "&" | "&&&" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  &&& toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x &&& toUInt64 y)
                    | _ -> failwith "ERROR"
                | "|" | "|||" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  ||| toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x ||| toUInt64 y)
                    | _ -> failwith "ERROR"
                | "^" | "^^^" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (toInt64 x  ^^^ toInt64 y)
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (toUInt64 x ^^^ toUInt64 y)
                    | _ -> failwith "ERROR"

                | "&&" when tn = BOOL -> box (toBool x && toBool y)
                | "||" when tn = BOOL -> box (toBool x || toBool y)


                | _ -> failwith $"ERROR: Operator {op}"

                |> fun v ->
                    if t.Name = OBJECT then
                        tryConvert2 (x.GetType()) v |> Option.get :?> 'T
                    else
                        tryConvert<'T> v |> Option.get

        let createUnaryFunction<'T> (op: string) : 'T -> 'T =
            let t = typeof<'T>
            fun (x:'T) ->
                let tn =
                    if t.Name = OBJECT then
                        x.GetType().Name
                    else
                        t.Name

                match op with
                | "!" when tn = BOOL -> (box x) :?> bool |> not |> box
                | "~" | "~~~" ->
                    match typeof<'T>.Name with
                    | INT8   | INT16  | INT32  | INT64  -> box (~~~ (toInt64 x))
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box (~~~ (toUInt64 x))
                    | _ -> failwith "ERROR"
                | "abs" ->
                    match tn with
                    | INT8   | INT16  | INT32  | INT64  -> box (Math.Abs(toInt64 x))
                    | UINT8  | UINT16 | UINT32 | UINT64 -> box x
                    | FLOAT32 | FLOAT64                 -> box (Math.Abs(toFloat64 x))
                    | _ -> failwith "ERROR"
                | ("sin" | "cos" | "tan") ->
                    let trigono =
                        match op with
                        | "sin" -> Math.Sin
                        | "cos" -> Math.Cos
                        | "tan" -> Math.Tan
                        | _ -> failwith "ERROR"

                    match tn with
                    | FLOAT32 -> box (toFloat32 <| trigono(toFloat64 x))
                    | FLOAT64 -> box (trigono(toFloat64 x))
                    | _ -> failwith "ERROR"

                |> fun v ->
                    if t.Name = OBJECT then
                        tryConvert2 (x.GetType()) v |> Option.get :?> 'T
                    else
                        tryConvert<'T> v |> Option.get

        let createShiftFunction<'T> (op: string) : 'T -> int -> 'T =
            let t = typeof<'T>
            fun (x:'T) (y:int) ->
                let tn =
                    if t.Name = OBJECT then
                        x.GetType().Name
                    else
                        t.Name
                match op with
                | "<<<" ->
                    // shift 연산은 최대치인 int64 나 uint64 로 변환 후 shift 연산을 수행할 수 없으므로 개별 type 별로 결과를 얻어야 한다.
                    match typeof<'T>.Name with
                    | INT8 -> toInt8 x <<< y |> box
                    | INT16 -> toInt16 x <<< y |> box
                    | INT32 -> toInt32 x <<< y |> box
                    | INT64 -> toInt64 x <<< y |> box
                    | UINT8 -> toUInt8 x <<< y |> box
                    | UINT16 -> toUInt16 x <<< y |> box
                    | UINT32 -> toUInt32 x <<< y |> box
                    | UINT64 -> toUInt64 x <<< y |> box
                    | _ -> failwith "ERROR"
                | ">>>" ->
                    match typeof<'T>.Name with
                    | INT8 -> toInt8 x >>> y |> box
                    | INT16 -> toInt16 x >>> y |> box
                    | INT32 -> toInt32 x >>> y |> box
                    | INT64 -> toInt64 x >>> y |> box
                    | UINT8 -> toUInt8 x >>> y |> box
                    | UINT16 -> toUInt16 x >>> y |> box
                    | UINT32 -> toUInt32 x >>> y |> box
                    | UINT64 -> toUInt64 x >>> y |> box
                    | _ -> failwith "ERROR"
                | _ -> failwith "ERROR"
                |> fun v ->
                    if t.Name = OBJECT then
                        tryConvert2 (x.GetType()) v |> Option.get :?> 'T
                    else
                        tryConvert<'T> v |> Option.get

        let private createBinaryArgsFunction<'T> (mnemonic:string) (args:Args) : 'T =
            expectGteN 2 args |> ignore
            let op:'T->'T->'T = createBinaryFunction<'T>(mnemonic)
            castArgs<'T> args |> Seq.reduce op

        let private createUnaryArgsFunction<'T> (mnemonic:string) (args:Args) : 'T =
            expect1 args |> ignore
            let op:'T->'T = createUnaryFunction<'T>(mnemonic)
            castArgs<'T> args |> Seq.head |> op

        let private createShiftArgsFunction<'T> (mnemonic:string) (args:Args) : 'T =
            expect2 args |> ignore
            let (x:'T), (y:int) = shiftArgs<'T> args
            createShiftFunction<'T>(mnemonic) x y


        let fAdd<'T>        (args: Args) : 'T = createBinaryArgsFunction<'T> "+" args
        let fSub<'T>        (args: Args) : 'T = createBinaryArgsFunction<'T> "-" args
        let fMul<'T>        (args: Args) : 'T = createBinaryArgsFunction<'T> "*" args
        let fDiv<'T>        (args: Args) : 'T = createBinaryArgsFunction<'T> "/" args
        let fMod<'T>        (args: Args) : 'T = createBinaryArgsFunction<'T> "%" args

        let fAbs<'T>        (args: Args) : 'T = createUnaryArgsFunction< 'T> "abs" args     // Unary
        let fBitwiseNot<'T> (args: Args) : 'T = createUnaryArgsFunction< 'T> "~~~" args     // Unary
        let fSin<'T>        (args: Args) : 'T = createUnaryArgsFunction< 'T> "sin" args     // Unary
        let fCos<'T>        (args: Args) : 'T = createUnaryArgsFunction< 'T> "cos" args     // Unary
        let fTan<'T>        (args: Args) : 'T = createUnaryArgsFunction< 'T> "tan" args     // Unary


        let fBitwiseAnd<'T> (args: Args) : 'T = createBinaryArgsFunction<'T> "&&&" args
        let fBitwiseOr<'T>  (args: Args) : 'T = createBinaryArgsFunction<'T> "|||" args
        let fBitwiseXor<'T> (args: Args) : 'T = createBinaryArgsFunction<'T> "^^^" args
        let fShiftLeft<'T>  (args: Args) : 'T = createShiftArgsFunction< 'T> "<<<" args     // Shift
        let fShiftRight<'T> (args: Args) : 'T = createShiftArgsFunction< 'T> ">>>" args     // Shift



        [<Obsolete("Todo: Fix")>]
        /// expression 내부에 변수가 하나도 없이 상수, 혹은 상수의 연산만으로 이루어진 경우에만 true 반환
        let isLiteralizable exp : bool =
            true
            //let rec visit (exp:IExpression) : bool =
            //    match exp.Terminal, exp.FunctionName with
            //    | Some terminal, _ ->
            //        //terminal.Literal.IsSome
            //        terminal.IsLiteral
            //    | None, Some _fn ->
            //        exp.FunctionArguments |> map visit |> Seq.forall id
            //    | _ ->
            //        failwith "Invalid expression"
            //visit exp

        // tryGetLiteralValue helper
        let private tryGetLiteralValueT (expr:IExpression<'T>) : obj =
            if isLiteralizable expr then
                expr.Evaluate() |> box
            else
                null

        /// 주어진 expression 에 대한 literal value 반환.  내부에 변수가 하나라도 포함되어 있으면 null 반환
        let tryGetLiteralValue (expr:IExpression) =
            match expr with
            | :? IExpression<bool>   as exp -> tryGetLiteralValueT exp
            | :? IExpression<int8>   as exp -> tryGetLiteralValueT exp
            | :? IExpression<uint8>  as exp -> tryGetLiteralValueT exp
            | :? IExpression<int16>  as exp -> tryGetLiteralValueT exp
            | :? IExpression<uint16> as exp -> tryGetLiteralValueT exp
            | :? IExpression<int32>  as exp -> tryGetLiteralValueT exp
            | :? IExpression<uint32> as exp -> tryGetLiteralValueT exp
            | :? IExpression<int64>  as exp -> tryGetLiteralValueT exp
            | :? IExpression<uint64> as exp -> tryGetLiteralValueT exp
            | :? IExpression<single> as exp -> tryGetLiteralValueT exp
            | :? IExpression<double> as exp -> tryGetLiteralValueT exp
            | :? IExpression<string> as exp -> tryGetLiteralValueT exp
            | :? IExpression<char>   as exp -> tryGetLiteralValueT exp
            | _ -> null


    type IExpression with
        /// 주어진 Expression 을 negation : negateBool 함수와 동일
        member exp.NegateBool() = fbLogicalNot [exp]
        /// 주어진 expression 에 대한 literal value 반환.  내부에 변수가 하나라도 포함되어 있으면 null 반환
        member exp.TryGetLiteralValue() = tryGetLiteralValue exp
        /// expression 내부에 변수가 하나도 없이 상수, 혹은 상수의 연산만으로 이루어진 경우에만 true 반환
        member exp.IsLiteralizable() = isLiteralizable exp
