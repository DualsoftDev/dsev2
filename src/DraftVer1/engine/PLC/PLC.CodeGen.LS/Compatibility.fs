namespace PLC.CodeGen.LS

open System.Linq
open System
open System.Linq
open System.Globalization
open System.Text.RegularExpressions
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

open Dual.Common.Core.FS
open Dual.Ev2

[<AutoOpen>]

module rec Compatibility =
    // 다음 컴파일 에러 회피하기 위한 boxing
    // error FS0030: 값 제한이 있습니다. 값 'fwdCreateVariableWithValue'은(는) 제네릭 형식    val mutable fwdCreateVariableWithValue: (string -> '_a -> IVariable)을(를) 가지는 것으로 유추되었습니다.    'fwdCreateVariableWithValue'에 대한 인수를 명시적으로 만들거나, 제네릭 요소로 만들지 않으려는 경우 형식 주석을 추가하세요.
    type BoxedObjectHolder = { Object:obj }

    type IVariable = IStorage
    type IVariable<'T> = IStorage<'T>
    type IText = interface end

    let private error() = failwith "ERROR: Not implemented"


    type FlatExpression = IExpression
    type IExpression with
        member x.ToText() =
            "XXXToText()"
        member x.Flatten() =
            x
        member x.WithNewFunctionArguments (args:Args): IExpression =
            error()
        member x.FunctionArguments:Args = error()
        member x.ApplyNegate() : IExpression = error()
        /// 주어진 Expression 을 negation : negateBool 함수와 동일
        member exp.NegateBool() = TFunction<bool>.Create(fbLogicalNot, [exp])
        member x.ToAssignStatement (pack:DynamicDictionary, replacableFunctionNames:string seq) : IExpression = error()
        member x.ToTextWithoutTypeSuffix() = (x :?> ValueHolder).ToTextWithoutTypeSuffix()


    // 직접변수일 경우 주소 출력
    let getStorageText (stg:TValue<_>) =
        if stg.AliasNames.Any() then stg.Address else stg.Name

    let var2expr = id
    let literal2expr (x:'T) = TValue(x)
    let any2expr (value:obj) : IExpression = TValue.Create(value) :?> IExpression

    [<RequireQualifiedAccess>]
    module Expression =
        let True  = literal2expr true
        let False = literal2expr false
        let Zero  = literal2expr 0

    /// 주어진 expression 에 대한 negated expression 반환
    ///
    /// - createUnaryExpression "!" expr 와 기능 유사
    let negateBool (expr:IExpression) : IExpression<bool> =
        assert (expr.DataType = typedefof<bool>)
        let boolExp = expr :?> IExpression<bool>
        match boolExp with
        | :? ITerminal<bool> as t when t.IsLiteralizable() ->
            t.TValue ?=(Expression.False, Expression.True)
        | _ ->
            TFunction<bool>.Create(fbLogicalNot, [expr])

        //match boolExp with
        //| DuTerminal(DuLiteral {Value = v}) ->
        //    if v then Expression.False else Expression.True
        ////| DuFunction({Name="!"; Arguments=[expr]}) ->
        ////    expr
        //| _ ->
        //    fbLogicalNot [expr]

    let precalculateSpan (expr:IExpression): int * int = error()

    let isFunctionBlockConnectable(expr: FlatExpression): bool = error()
    let isHangul(ch:char) = Char.GetUnicodeCategory(ch) = UnicodeCategory.OtherLetter
    let isValidStart(ch:char) = ch = '_' || Char.IsLetter(ch) || isHangul(ch)
    let inline address x = ( ^T: (member Address: string) x )

    type OFunction with
        member x.WithNewFunctionArguments (args:Args): IExpression =
            error()

    type Statement with
        member x.ToText(): string = error()
        member x.IsDuCaseVarDecl(): bool = error()

    type IStorage with
        member x.Address:string = error()
        member x.TryGetSystemTagKind   () = DU.tryGetEnumValue<SystemTag> ((x :?> ValueHolder).TagKind)

    type ValueHolder with
        member x.ToTextWithoutTypeSuffix() =
            match x.OValue with
            | :? string as s when not <| s.ToUpper().StartsWith("T#") ->
                // string 이면서, timer 의 preset 값인 "T#1s" 와 같은 형태가 아니면, single quote 로 감싼다.
                $"'{x.OValue}'"
            | _ -> $"{x.OValue}"

    type TimerCounterBaseStruct with
        member x.StorageName = x.Name
        member x.Variable = Some x.RES
        member x.Literal = None
