namespace PLC.CodeGen.LS

open System.Linq

open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common


[<AutoOpen>]
module XgxExpressionConvertorModule =
    type DynamicDictionary with
        member x.Unpack() = x.Get<XgxProjectParams>("projectParameter"), x.Get<Augments>("augments")

    type IExpression with
        /// expression 내부의 비교 및 사칙 연산을 xgi/xgk function 으로 대체
        ///
        /// - 인자로 받은 {exp, expandFunctionStatements, newLocalStorages} 를 이용해서,
        ///
        ///   * 추가되는 statement 는 expandFunctionStatements 에 추가하고,
        ///
        ///   * 추가되는 local variable 은 newLocalStorages 에 추가한다.
        ///
        ///   * 새로 생성되는 expression 을 반환한다.
        member internal exp.ReplaceInnerArithmeticOrComparisionToFunctionStatements
            (pack:DynamicDictionary)
          : IExpression =
            let prjParam, augs = pack.Unpack()
            let newLocalStorages, expandFunctionStatements, expStore =
                augs.Storages, augs.Statements, augs.ExpressionStore

            let functionTransformer (_level:int, functionExpression:IExpression, expStore:IStorage option) =
                match functionExpression.FunctionName with
                | Some(IsOpAC op) -> //when level <> 0 ->
                    let args = functionExpression.FunctionArguments
                    let var:IStorage =
                        expStore |?? (fun () -> prjParam.CreateAutoVariableWithFunctionExpression(functionExpression))

                    expandFunctionStatements.Add
                    <| DuPLCFunction {
                        Condition = None
                        FunctionName = op
                        Arguments = args
                        OriginalExpression = functionExpression
                        Output = var }

                    newLocalStorages.Add var
                    var.ToExpression()
                | _ ->
                    functionExpression

            let transformers = {TerminalHandler = snd; FunctionHandler = functionTransformer}
            let newExpression = exp.Transform(transformers, expStore)
            newExpression


        member internal exp.BinaryToNary
          ( pack:DynamicDictionary
            , operatorsToChange: string seq
            , currentOp: string
          ) : IExpression list =
            let prjParam, augs = pack.Unpack()
            let storage, augmentedStatementsStorage = augs.Storages, augs.Statements
            let withAugmentedPLCFunction (exp: IExpression) =
                let out = prjParam.CreateAutoVariableWithFunctionExpression(exp)
                storage.Add out
                let op = exp.FunctionName.Value

                let args =
                    exp.FunctionArguments
                    |> List.bind (fun arg -> arg.BinaryToNary(pack, operatorsToChange, op) )

                DuPLCFunction {
                    Condition = None
                    FunctionName = op
                    Arguments = args
                    OriginalExpression = exp
                    Output = out }
                |> augmentedStatementsStorage.Add

                out.ToExpression()

            match exp.FunctionName with
            | Some op when operatorsToChange.Contains(op) -> // ("+"|"-"|"*"|"/"   (*|"&&"|"||"*) as op) ->
                if op = currentOp then
                    let args =
                        [ for arg in exp.FunctionArguments do
                              match arg.Terminal, arg.FunctionName with
                              | Some _, _ -> yield arg
                              | None, Some("-" | "/") -> yield withAugmentedPLCFunction arg
                              | None, Some _fn -> yield! arg.BinaryToNary(pack, operatorsToChange, op)
                              | _ -> failwithlog "ERROR" ]

                    args
                else
                    [ withAugmentedPLCFunction exp ]
            | _ -> [ exp ]

        (* see ``ADD 3 items test`` *)
        /// 사칙 연산 처리
        /// - a + b + c => + [a; b; c] 로 변환 (flat 처리)
        ///     * '+' or '*' 연산에서 argument 갯수가 8 개 이상이면 분할해서 PLC function 생성 (XGI function block 의 다릿발 갯수 제한 때문)
        /// - a + (b * c) + d => +[a; x; d], *[b; c] 두개의 expression 으로 변환.  부가적으로 생성된 *[b;c] 는 새로운 statement 를 생성해서 augmentedStatementsStorage 에 추가된다.
        member internal exp.FlattenArithmeticOperator
          (  pack:DynamicDictionary
             , outputStore: IStorage option
          ) : IExpression =
            let prjParam, augs = pack.Unpack()
            let newLocalStorages, augmentedStatementsStorage, _expStore =
                augs.Storages, augs.Statements, augs.ExpressionStore

            match exp.FunctionName with
            | Some (IsOpAB op) ->
                let newArgs =
                    exp.BinaryToNary(pack, K.arithmaticOperators @ K.bitwiseOperators, op)

                match op with
                | "+"
                | "*" when newArgs.Length >= 8 ->
                    let rec chunkBy8 (prevSum: IExpression list) (argsRemaining: IExpression list) : IExpression =
                        let allArgs = prevSum @ argsRemaining
                        let numSkip = min 8 allArgs.Length
                        let args = allArgs.Take(numSkip).ToFSharpList()
                        let argsRemaining = allArgs |> List.skip numSkip

                        let out =
                            if argsRemaining.IsEmpty then
                                outputStore.Value
                            else
                                prjParam.CreateAutoVariableWithFunctionExpression(exp)

                        let outexp = out.ToExpression()

                        DuPLCFunction {
                            Condition = None
                            FunctionName = op
                            Arguments = args
                            OriginalExpression = exp
                            Output = out }
                        |> augmentedStatementsStorage.Add

                        out |> newLocalStorages.Add

                        if allArgs.Length <= 8 then
                            outexp
                        else
                            chunkBy8 [ outexp ] argsRemaining

                    chunkBy8 [] newArgs
                | _ ->
                    exp.WithNewFunctionArguments newArgs

            | Some(">"|">="|"<"|"<="|"=="|"!="|"<>"  |  "&&"|"||" as op) ->
                let newArgs = exp.BinaryToNary(pack, [ op ], op)
                exp.WithNewFunctionArguments newArgs

            | _ ->
                exp

        member internal exp.ZipAndExpression (pack:DynamicDictionary, allowCallback:bool) : IExpression =
            let prjParam, augs = pack.Unpack()
            let newLocalStorages, expandFunctionStatements = augs.Storages, augs.Statements

            let flatExpression = exp.Flatten() :?> FlatExpression
            let w, _h = flatExpression |> precalculateSpan

            if w > maxNumHorizontalContact then
                let exp = if allowCallback then exp.ZipVisitor(pack) else exp

                match exp.FunctionName with
                | Some op when op.IsOneOf("||") ->
                    exp.ZipVisitor(pack)
                | Some op when op = "&&" ->
                    let mutable partSpanX = 0
                    let maxX = maxNumHorizontalContact

                    let folder (z: IExpression list list * IExpression list) (e: IExpression) =
                        let built, building = z
                        let _flatExp = e.Flatten() :?> FlatExpression
                        let spanX = e.Flatten() :?> FlatExpression |> precalculateSpan |> fst

                        let max, remaining =
                            if partSpanX + spanX > maxX then
                                partSpanX <- spanX
                                built +++ building, [ e ]
                            else
                                partSpanX <- partSpanX + spanX
                                built, building @ [ e ]
                        (max |> filter List.any), remaining

                    let maxs, remaining = List.fold folder ([], []) exp.FunctionArguments

                    let subSums =
                        [
                            for max in maxs do
                                let out = prjParam.CreateTypedAutoVariable("split", false, $"{op} split output")
                                newLocalStorages.Add out

                                DuPLCFunction {
                                    Condition = None
                                    FunctionName = op
                                    Arguments = max
                                    OriginalExpression = exp
                                    Output = out }
                                |> expandFunctionStatements.Add

                                var2expr out :> IExpression ]

                    let grandTotal = prjParam.CreateTypedAutoVariable("split", false, $"{op} split output")
                    newLocalStorages.Add grandTotal

                    DuPLCFunction {
                        Condition = None
                        FunctionName = op
                        Arguments = subSums @ remaining
                        OriginalExpression = exp
                        Output = grandTotal }
                    |> expandFunctionStatements.Add

                    var2expr grandTotal :> IExpression
                | _ -> exp
            else
                exp

        member private exp.ZipVisitor (pack:DynamicDictionary) : IExpression =
            let exp = exp.FlattenArithmeticOperator(pack, None)
            let w, _h = exp.Flatten() :?> FlatExpression |> precalculateSpan

            if w > maxNumHorizontalContact && exp.FunctionName.IsSome && exp.FunctionName.Value.IsOneOf("&&", "||") then
                if exp.FunctionArguments.Any(fun e -> e.Flatten() :?> FlatExpression |> precalculateSpan |> fst >= 20 ) then
                    let args = [
                        for arg in exp.FunctionArguments do
                            arg.ZipAndExpression(pack, true)
                    ]

                    exp.WithNewFunctionArguments args

                else
                    let allowCallback = false
                    exp.ZipAndExpression(pack, allowCallback)
            else
                exp

        member internal exp.CollectExpandedExpression (pack:DynamicDictionary) : IExpression =
            let newExp =
                exp.ReplaceInnerArithmeticOrComparisionToFunctionStatements(pack)

            let newExp = newExp.ZipVisitor(pack)
            newExp

    type ExpressionConversionResult = IExpression * IStorage list * Statement list
    type IExpression with
        /// expression 을 임시 auto 변수에 저장하는 statement 로 만들고, 그 statement 와 auto variable 를 반환
        member x.ToAssignStatement (pack:DynamicDictionary, replacableFunctionNames:string seq) : IExpression =
            let prjParam, augs = pack.Unpack()
            match x.FunctionName with
            | Some fn when replacableFunctionNames.Contains fn ->
                match fn with
                | "==" | "<>" | "!=" when prjParam.TargetType = XGK && x.FunctionArguments[0].DataType = typedefof<bool> ->
                    x.AugmentXgk(pack, None, None)
                | _ ->
                    let var =
                        match pack.TryGet<Statement>("statement") with
                        | Some (DuAssign(_cond, exp, stg)) when exp = x -> stg
                        | _ ->
                            let var = prjParam.CreateAutoVariableWithFunctionExpression(x)
                            augs.Storages.Add var
                            var

                    let stmt =
                        match prjParam.TargetType with
                        | XGK -> DuAssign(None, x, var)
                        | XGI ->
                            let newExp = x.FlattenArithmeticOperator(pack, Some var)
                            DuPLCFunction {
                                Condition = None
                                FunctionName = fn
                                Arguments = newExp.FunctionArguments
                                OriginalExpression = newExp
                                Output = var }
                        | _ -> failwithlog "ERROR"

                    augs.Statements.Add <| stmt
                    var.ToExpression()
            | _ -> x


        /// exp 내에 포함된, {문장(statement)으로 추출 해야만 할 요소}를 newStatements 에 추가한다.
        /// 이 과정에서 추가할 필요가 있는 storate 는 newLocalStorages 에 추가한다.
        /// 반환 : exp, 추가된 storage, 추가된 statement
        ///
        /// e.g: XGK 의 경우, 함수를 지원하지 않으므로,
        ///     입력 exp: "2 + 3 > 4"
        ///     추가 statement : "tmp1 = 2 + 3"
        ///     추가 storage : tmp2
        ///     최종 exp: "tmp1 > 4"
        ///     반환 : exp, [tmp2], [tmp1 = 2 + 3]
        member x.AugmentXgk (
                pack:DynamicDictionary
                , assignCondition:IExpression<bool> option
                , expStore:IStorage option)
          : IExpression =
            let prjParam, augs = pack.Unpack()
            let rec helper (nestLevel:int) (exp: IExpression, expStore:IStorage option) : ExpressionConversionResult =
                match exp.FunctionName, exp.FunctionArguments with
                | Some fn, l::r::[] ->
                    let lexpr, lstgs, lstmts = helper (nestLevel + 1) (l, None)
                    let rexpr, rstgs, rstmts = helper (nestLevel + 1) (r, None)

                    if (*isXgk &&*) lexpr.DataType = typeof<bool> && fn.IsOneOf("!=", "==", "<>") then
                        // XGK 에는 bit 의 비교 연산이 없다.  따라서, bool 타입의 비교 연산을 수행할 경우, 이를 OR, AND 로 변환한다.
                        let l, r, nl, nr = lexpr, rexpr, lexpr.NegateBool() , rexpr.NegateBool()
                        let newExp =
                            match fn with
                            | ("!=" | "<>") -> fbLogicalOr([fbLogicalAnd [l; nr]; fbLogicalAnd [nl; r]])
                            | "==" -> fbLogicalOr([fbLogicalAnd [l; r]; fbLogicalAnd [nl; nr]])
                            | _ -> failwithlog "ERROR"
                        newExp, (lstgs @ rstgs), (lstmts @ rstmts)
                    else
                        // XGK 에는 IEC Function 을 이용할 수 없으므로,
                        // XGI 에는 사칙 연산을 중간 expression 으로 이용은 가능하나, ladder 그리는 로직이 너무 복잡해 지므로,
                        // 수식 내에 포함된 사칙 연산이나 비교 연산을 따로 빼내어서 임시 변수에 대입하는 assign 문장으로 으로 변환한다.
                        let newExp = exp.WithNewFunctionArguments [lexpr; rexpr]

                        match fn with
                        | IsOpABC _ ->
                            let stg = expStore |?? (fun () -> prjParam.CreateAutoVariableWithFunctionExpression(exp))
                            let stmt = DuAssign(assignCondition, newExp, stg)
                            let varExp = stg.ToExpression()
                            varExp, (lstgs @ rstgs @ [ stg ]), (lstmts @ rstmts @ [ stmt ])
                        | _ ->
                            if lstgs.Any() || rstgs.Any() then
                                newExp, (lstgs @ rstgs), (lstmts @ rstmts)
                            else
                                exp, [], []
                | _ ->
                    exp, [], []

            if x.Terminal.IsSome then
                x
            else
                let exp, stgs, stmts = helper 0 (x, expStore)
                augs.Storages.AddRange(stgs)
                augs.Statements.AddRange(stmts)
                exp



