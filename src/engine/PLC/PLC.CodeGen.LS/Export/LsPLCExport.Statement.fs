namespace PLC.CodeGen.LS
open System.Linq

open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common
open System

[<AutoOpen>]
module StatementExtensionModule =
    let rec functionToAssignStatementVisitor (pack:DynamicDictionary) (expPath:IExpression list) (exp:IExpression): IExpression =
        let prjParam, _augs = pack.Unpack()
        if exp.Terminal.IsSome then
            exp
        else
            debugfn $"exp: {exp.ToText()}"
            let newExp =
                let args =
                    exp.FunctionArguments
                    |> map (fun ex ->
                        functionToAssignStatementVisitor pack (exp::expPath) ex)
                exp.WithNewFunctionArguments args

            let statement = pack.Get<Statement>("statement")
            let isXgi = prjParam.TargetType = XGI
            let isAssignStatementWithNoCondition =  // condition NULL 인 Assign 은 특수 case 로 취급.  function 없이 bit logic 만으로 처리가능.
                match statement with
                | DuAssign (None, _, _) -> true
                | _ -> false

            match newExp.FunctionName with
            | Some (IsOpAB _fn) when isXgi && expPath.IsEmpty ->
                match statement with
                | DuAssign(_, e, _t) when e = exp ->
                    newExp
                | _ ->
                    newExp.ToAssignStatement(pack, K.arithmaticOrBitwiseOperators)

            | Some (IsOpABC fn) when expPath.Any() ->
                let augment =
                    match prjParam.TargetType, expPath with
                    | XGK, _head::_ -> true
                    | XGI, _head::_ when _head.FunctionName <> Some fn -> true
                    | _ ->
                        false

                if augment then
                    newExp.ToAssignStatement(pack, K.arithmaticOrBitwiseOrComparisionOperators)
                else
                    newExp
            | Some (IsOpC _fn) when isXgi && (expPath.Any() || not isAssignStatementWithNoCondition) ->
                newExp.ToAssignStatement(pack, K.comparisonOperators)
            | _ ->
                newExp

    type ExpressionVisitor = DynamicDictionary -> IExpression list -> IExpression -> IExpression
    type Statement with
        /// Statement to XGx Statements. XGK/XGI 공용 Statement 확장
        member internal x.ToStatementsXgx (pack:DynamicDictionary) : unit =
            let statement = x
            let _prjParam, augs = pack.Unpack()

            match statement with
            | (DuVarDecl _ | DuUdtDecl _ | DuUdtDef _) -> failwith "Should have been processed in early stage"
            | DuAssign(condition, exp, target) ->
                assert(exp.DataType = target.DataType)
                // todo : "sum = tag1 + tag2" 의 처리 : DuPLCFunction 하나로 만들고, 'OUT' output 에 sum 을 할당하여야 한다.
                match condition, exp.FunctionName with
                | _, Some(IsOpABC op) ->
                    // XGI, XGK 공용!!
                    let exp = exp.FlattenArithmeticOperator(pack, Some target)
                    if exp.FunctionArguments.Any() then
                        let augFunc =
                            DuPLCFunction {
                                Condition = condition
                                FunctionName = op
                                Arguments = exp.FunctionArguments
                                OriginalExpression = exp
                                Output = target }
                        augs.Statements.Add augFunc

                | _, Some op when not (isOpL op) ->
                    failwith $"ERROR: {op} unexpected."

                | Some _cond, None -> // terminal 을 target 으로 assign 하는 경우
                    let newExp = exp.CollectExpandedExpression(pack)
                    let augFunc =
                        DuPLCFunction {
                            Condition = condition
                            FunctionName = XgiConstants.FunctionNameMove
                            Arguments = [newExp]
                            OriginalExpression = exp
                            Output = target }
                    augs.Statements.Add augFunc

                | _ ->
                    assert(exp.FunctionName.IsNone || isOpL(exp.FunctionName.Value))
                    assert(condition.IsNone)
                    let newExp = exp.CollectExpandedExpression(pack)
                    DuAssign(None, newExp, target) |> augs.Statements.Add

            | (DuTimer _ | DuCounter _ | DuPLCFunction _) ->
                augs.Statements.Add statement

            | DuAction(DuCopyUdt _) ->
                statement |> augs.Statements.Add
            | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현


        /// statement 내부에 존재하는 모든 expression 을 visit 함수를 이용해서 변환한다.   visit 의 예: exp.MakeFlatten()
        /// visit: [상위로부터 부모까지의 expression 경로] -> 자신 expression -> 반환 expression : 아래의 FunctionToAssignStatement 샘플 참고
        member x.VisitExpression (pack:DynamicDictionary, visit:ExpressionVisitor) : Statement =
            let statement = x
            /// IExpression option 인 경우의 visitor
            let tryVisit (expPath:IExpression list) (exp:IExpression<bool> option) : IExpression<bool> option =
                exp |> map (fun exp -> visit pack expPath exp :?> IExpression<bool> )

            let visitTop exp = visit pack [] exp
            let tryVisitTop exp = tryVisit [] exp

            pack.["statement"] <- x
            let newStatement =
                match statement with
                | DuAssign(condition, exp, tgt) ->
                    Some <| DuAssign(tryVisitTop condition, visitTop exp, tgt)

                | DuTimer ({ RungInCondition = rungIn; ResetCondition = reset } as tmr) ->
                    Some <| DuTimer {
                        tmr with
                            RungInCondition = tryVisitTop rungIn
                            ResetCondition  = tryVisitTop reset }
                | DuCounter ({UpCondition = up; DownCondition = down; ResetCondition = reset; LoadCondition = load} as ctr) ->
                    Some <| DuCounter {
                        ctr with
                            UpCondition    = tryVisitTop up
                            DownCondition  = tryVisitTop down
                            ResetCondition = tryVisitTop reset
                            LoadCondition  = tryVisitTop load }

                | DuAction (DuCopyUdt ({ Condition=condition; } as udt)) ->
                    let cond = (visitTop condition) :?> IExpression<bool>
                    Some <| DuAction(DuCopyUdt {udt with Condition = cond})

                | DuPLCFunction ({Arguments = args} as functionParameters) ->
                    let newArgs = args |> map (fun arg -> visitTop arg)
                    Some <| DuPLCFunction { functionParameters with Arguments = newArgs }
                | DuVarDecl (exp, stg) when pack.Get<bool>("visit-vardecl-statement") ->
                    let newExp = visitTop exp
                    Some <| DuVarDecl (newExp, stg)
                | (DuVarDecl _ | DuUdtDecl _ | DuUdtDef _ ) -> failwith "Should have been processed in early stage"

                | (DuLambdaDecl _ | DuProcDecl _ | DuProcCall _) ->
                    failwith "ERROR: Not yet implemented"       // 추후 subroutine 사용시, 필요에 따라 세부 구현
                //| DuLambdaDecl _ -> None


            pack.Remove("statement") |> ignore
            newStatement |> Option.defaultValue x

        /// expression 의 parent 정보 없이 visit 함수를 이용해서 모든 expression 을 변환한다.
        member x.VisitExpression (pack:DynamicDictionary, visit:IExpression -> IExpression) : Statement =
            let statement = x
            let visit2 _pack _ (exp:IExpression) = visit exp
            statement.VisitExpression (pack, visit2)

        /// Expression 을 flattern 할 수 있는 형태로 변환 : e.g !(a>b) => (a<=b)
        member x.DistributeNegate(pack:DynamicDictionary) =
            let statement = x
            let visitor (exp:IExpression) : IExpression = exp.ApplyNegate()
            statement.VisitExpression(pack, visitor)


        /// XGI Timer/Counter 의 RungInCondition, ResetCondition 이 Non-terminal 인 경우, assign statement 로 변환한다.
        ///
        /// - 현재, 구현 편의상 XGI Timer/Counter 의 다릿발에는 boolean expression 만 수용하므로 사칙/비교 연산을 assign statement 로 변환한다.
        member x.AugmentXgiFunctionParameters (pack:DynamicDictionary) : Statement =
            let prjParam, _augs = pack.Unpack()
            let toAssignOndemand (exp:IExpression<bool> option) : IExpression<bool> option =
                exp |> map (fun exp -> exp.ToAssignStatement(pack, K.arithmaticOrBitwiseOrComparisionOperators) :?> IExpression<bool>)

            match prjParam.TargetType, x with
            | XGK, _ -> x
            | XGI, DuTimer ({ RungInCondition = rungIn; ResetCondition = reset } as tmr) ->
                DuTimer {
                    tmr with
                        RungInCondition = toAssignOndemand rungIn
                        ResetCondition  = toAssignOndemand reset }
            | XGI, DuCounter ({
                UpCondition = up; DownCondition = down; ResetCondition = reset; LoadCondition = load} as ctr) ->
                DuCounter {
                    ctr with
                        UpCondition    = toAssignOndemand up
                        DownCondition  = toAssignOndemand down
                        ResetCondition = toAssignOndemand reset
                        LoadCondition  = toAssignOndemand load }
            | _ -> x


        /// x 로 주어진 XGK statement 내의 expression 들을 모두 검사해서 사칙/비교연산을 assign statement 로 변환한다.
        member x.FunctionToAssignStatement (pack:DynamicDictionary) : Statement =
            // visitor 를 이용해서 statement 내의 모든 expression 을 변환한다.
            x.VisitExpression(pack, functionToAssignStatementVisitor)

        member x.ApplyLambda (pack:DynamicDictionary) : Statement =
            let prjParam, _augs = pack.Unpack()
            let rec visitor (pack:DynamicDictionary) (expPath:IExpression list) (exp:IExpression): IExpression =
                if exp.Terminal.IsSome then
                    exp
                else
                    debugfn $"ApplyLambda:: exp: {exp.ToText()}"
                    // exp 이 FunctionSpec 값을 가지면서, FunctionSpec 내부에 LambdaApplication 이 존재하면
                    // 해당 LambdaApplication 적용 결과를 임시 변수에 저장하고, 그 값을 반환한다.
                    match exp.FunctionSpec |> bind (fun fs -> fs.LambdaApplication) with     // e.g LambdaDecl: "int sum(int a,int b) = $a + $b;"
                    | Some la ->
                        let ld = la.LambdaDecl
                        let funName = ld.Prototype.Name
                        for i in [0..la.Arguments.Length-1] do
                            let declVarName = ld.Arguments[i].Name   // a
                            let encryptedFormalParamName = getFormalParameterName funName declVarName      // e.g "_local_sum_a"
                            let value =
                                let arg = visitor pack (exp::expPath) la.Arguments.[i]
                                match arg.Terminal with
                                | Some _ -> arg
                                | None -> arg.BoxedEvaluatedValue |> any2expr
                            let stgVar = prjParam.GlobalStorages[encryptedFormalParamName]

                            prjParam.GlobalStorages[encryptedFormalParamName].BoxedValue <- value.BoxedEvaluatedValue
                            if prjParam.TargetType = XGI && pack.Get<Statement>("original-statement").IsDuCaseVarDecl() then
                                ()
                            else
                                DuAssign(None, value, stgVar) |> _augs.Statements.Add
                        match prjParam.TargetType with
                        | XGK ->
                            let newExp = functionToAssignStatementVisitor pack expPath exp
                            match newExp.FunctionName with
                            | Some _ ->
                                let result = prjParam.CreateAutoVariableWithFunctionExpression(pack, newExp)
                                result.ToExpression()
                            | None ->
                                newExp
                        | XGI -> exp
                        | _ -> failwith "Not supported runtime target"
                    | None ->
                        let args = exp.FunctionArguments |> map (fun ex -> visitor pack (exp::expPath) ex)
                        exp.WithNewFunctionArguments args

            // visitor 를 이용해서 statement 내의 모든 expression 을 변환한다.
            pack["visit-vardecl-statement"] <- true
            let newStatement = x.VisitExpression(pack, visitor)
            pack["visit-vardecl-statement"] <- false
            newStatement

