namespace PLC.CodeGen.LS

open Engine.Core
open Dual.Common.Core.FS
open PLC.CodeGen.Common

[<AutoOpen>]
module XgxTypeConvertorModule =
    (*
        [DuAssign] 문장 변환:
            - condition 이 존재하는 경우
                - BOOL type 인 경우
                    - XGK : BAND, BOR 를 이용한 변환
                    - XGI : DuPLCFunction({FunctionName = XgiConstants.FunctionNameMove ...}) 이용 변환
                - 'T type 인 경우
                    - XGK : MOVE command 이용
                    - XGI : DuPLCFunction({FunctionName = XgiConstants.FunctionNameMove ...}) 이용 변환
            - condition 이 존재하지 않는 경우
                - BOOL type 인 경우
                    - DuAssign 그대로 사용 (normal ladder)
                - 'T type 인 경우
                    - ?? XGK : MOVE command 이용
                    - XGI : DuPLCFunction({FunctionName = XgiConstants.FunctionNameMove ...}) 이용 변환
     *)
    type CommentedStatement with
        /// (Commented Statement) To (Commented Statements)
        ///
        /// S -> [XS]
        member internal x.ToCommentedStatements (prjParam: XgxProjectParams, newLocalStorages: XgxStorage) : CommentedStatements =
            let (CommentedStatement(comment, statement)) = x
            let originalComment = statement.ToText()
            debugfn $"Statement:: {originalComment}"
            let augs = Augments(newLocalStorages, StatementContainer())
            let createPack (prjParam:XgxProjectParams) (augs:Augments) : DynamicDictionary =
                let kvs:array<string*obj> =
                    [|
                        ("projectParameter", prjParam)
                        ("augments", augs)
                        ("original-statement", statement)
                    |]
                kvs |> DynamicDictionary

            let pack = createPack prjParam augs

            let statement = statement.ApplyLambda(pack)

            //statement.Do()
            match statement with
            | DuVarDecl(exp, var) ->
                // 변수 선언문에서 정확한 초기 값 및 주석 값을 가져온다.
                // Local/Global 공유되는 변수에 대해, global 변수가 parser context 에서 부정확한 주석을 얻으므로, 추후에 이를 보정하기 위함이다.
                // - GenerateXmlDocument @ LsPLCExport.Export.fs 참고
                var.Comment <- statement.ToText()
                let exp =
                    match exp.Terminal with
                    | Some _ -> exp
                    | None ->
                        exp.BoxedEvaluatedValue |> any2expr
                var.BoxedValue <- exp.BoxedEvaluatedValue
                augs.Storages.Add var

                match prjParam.TargetType with
                | XGK ->
                    DuAssign(Some fake1OnExpression, exp, var)
                    |> augs.Statements.Add
                | XGI -> () // XGI 에서는 변수 선언에 해당하는 부분을 변수의 초기값으로 할당하고 끝내므로, 더이상의 ladder 생성을 하지 않는다.
                | _ -> failwith "Not supported runtime target"

            | _ ->
                let newStatement = statement.DistributeNegate(pack)
                let newStatement = newStatement.FunctionToAssignStatement(pack)
                let newStatement = newStatement.AugmentXgiFunctionParameters(pack)

                match prjParam.TargetType with
                | XGI -> newStatement.ToStatementsXgx(pack)
                | XGK -> newStatement.ToStatementsXgk(pack)
                | _ -> failwith "Not supported runtime target"

            let rungComment =
                [
                    comment
                    if prjParam.AppendDebugInfoToRungComment then
                        let statementComment = originalComment  // newStatement.ToText()
                        statementComment
                ] |> ofNotNullAny |> String.concat "\r\n"
                |> escapeXml

            CommentedStatements(rungComment, augs.Statements.ToFSharpList())

