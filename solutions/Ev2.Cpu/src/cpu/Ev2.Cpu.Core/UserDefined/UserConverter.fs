namespace Ev2.Cpu.Core.UserDefined

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Type Conversion between UserExpr/UserStmt and DsExpr/DsStmt
// ═════════════════════════════════════════════════════════════════════════════
// UserFC/FB 타입과 Core AST 타입 간 변환
// ═════════════════════════════════════════════════════════════════════════════

/// UserExpr ↔ DsExpr 변환
module UserExprConverter =

    /// 변수 이름에 스코프 접두사 추가
    /// 예: "temperature" → "FC_TempConvert_temperature"
    let scopeName (scope: string option) (name: string) : string =
        match scope with
        | Some s -> sprintf "%s_%s" s name
        | None -> name

    /// UserExpr → DsExpr 변환
    /// scope: FC/FB 이름 (파라미터 스코핑용)
    let rec userExprToDsExpr (scope: string option) (expr: UserExpr) : DsExpr =
        match expr with
        | UConst(value, dataType) ->
            Const(value, dataType)

        | UParam(name, dataType) ->
            // 파라미터는 스코프가 적용된 태그로 변환
            let scopedName = scopeName scope name
            Terminal(DsTag.Create(scopedName, dataType))

        | UStatic(name, dataType) ->
            // Static 변수도 스코프가 적용된 태그로 변환
            let scopedName = scopeName scope name
            Terminal(DsTag.Create(scopedName, dataType))

        | UTemp(name, dataType) ->
            // Temp 변수도 스코프가 적용된 태그로 변환
            let scopedName = scopeName scope name
            Terminal(DsTag.Create(scopedName, dataType))

        | UUnary(op, e) ->
            let dsExpr = userExprToDsExpr scope e
            Unary(op, dsExpr)

        | UBinary(op, left, right) ->
            let dsLeft = userExprToDsExpr scope left
            let dsRight = userExprToDsExpr scope right
            Binary(op, dsLeft, dsRight)

        | UCall(funcName, args) ->
            // Built-in 함수 호출은 그대로 변환
            let dsArgs = args |> List.map (userExprToDsExpr scope)
            Function(funcName, dsArgs)

        | UUserFCCall(fcName, args) ->
            // UserFC 호출은 일반 함수 호출로 변환
            // Runtime에서 UserFC인지 Built-in인지 구별하여 처리
            let dsArgs = args |> List.map (userExprToDsExpr scope)
            Function(fcName, dsArgs)

        | UConditional(condition, trueExpr, falseExpr) ->
            // IF-THEN-ELSE는 IF 함수로 변환
            let dsCond = userExprToDsExpr scope condition
            let dsTrue = userExprToDsExpr scope trueExpr
            let dsFalse = userExprToDsExpr scope falseExpr
            Function("IF", [dsCond; dsTrue; dsFalse])

    /// DsExpr → UserExpr 변환 (부분 변환)
    /// 모든 DsExpr가 UserExpr로 변환 가능한 것은 아님
    /// scope를 제거하여 원래 변수 이름 복원
    let rec dsExprToUserExpr (scope: string option) (expr: DsExpr) : UserExpr option =
        match expr with
        | Const(value, dataType) ->
            Some (UConst(value, dataType))

        | Terminal(tag) ->
            // 스코프가 적용된 태그 이름에서 원래 이름 추출
            let originalName =
                match scope with
                | Some s ->
                    let prefix = s + "_"
                    if tag.Name.StartsWith(prefix) then
                        tag.Name.Substring(prefix.Length)
                    else
                        tag.Name
                | None -> tag.Name
            // UParam인지 UStatic인지 구분 불가 → UParam으로 변환
            Some (UParam(originalName, tag.StructType))

        | Unary(op, e) ->
            dsExprToUserExpr scope e
            |> Option.map (fun ue -> UUnary(op, ue))

        | Binary(op, left, right) ->
            match dsExprToUserExpr scope left, dsExprToUserExpr scope right with
            | Some l, Some r -> Some (UBinary(op, l, r))
            | _ -> None

        | Function(funcName, args) ->
            // IF 함수는 UConditional로 변환
            if funcName.ToUpper() = "IF" && args.Length = 3 then
                match List.map (dsExprToUserExpr scope) args with
                | [Some cond; Some trueExpr; Some falseExpr] ->
                    Some (UConditional(cond, trueExpr, falseExpr))
                | _ -> None
            else
                // 일반 함수 호출
                let userArgs = args |> List.map (dsExprToUserExpr scope)
                if userArgs |> List.forall Option.isSome then
                    Some (UCall(funcName, userArgs |> List.map Option.get))
                else
                    None

/// UserStmt ↔ DsStmt 변환
module UserStmtConverter =

    open UserExprConverter

    /// 스텝 번호 자동 할당을 위한 카운터
    let mutable private stepCounter = 0

    /// 새로운 스텝 번호 생성
    let private nextStep() =
        stepCounter <- stepCounter + 10
        stepCounter

    /// 스텝 카운터 리셋
    let resetStepCounter() =
        stepCounter <- 0

    /// UserStmt → DsStmt list 변환
    /// 하나의 UserStmt가 여러 DsStmt로 변환될 수 있음
    let rec userStmtToDsStmts (scope: string option) (stmt: UserStmt) : DsStmt list =
        match stmt with
        | UAssign(target, expr) ->
            // 할당문은 Assign으로 변환
            let scopedTarget =
                match scope with
                | Some s -> sprintf "%s_%s" s target
                | None -> target
            let dsExpr = userExprToDsExpr scope expr
            // 타입 추론 - 추론 실패 시 에러 발생
            let dataType =
                match expr.InferType() with
                | Some dt -> dt
                | None ->
                    raise (InvalidOperationException($"Cannot infer type for assignment to '{scopedTarget}'. Expression: {expr}"))
            let tag = DsTag.Create(scopedTarget, dataType)
            [Assign(nextStep(), tag, dsExpr)]

        | UWhen(condition, action) ->
            // 조건부 실행은 IF 표현식으로 래핑된 Assign으로 변환
            // tag := IF(condition, expr, tag) - 조건이 참이면 expr, 거짓이면 현재 값 유지
            let dsCond = userExprToDsExpr scope condition
            let actionStmts = userStmtToDsStmts scope action
            // 각 Assign을 조건부 Assign으로 변환
            actionStmts |> List.map (fun s ->
                match s with
                | Assign(step, tag, expr) ->
                    // tag := IF(condition, expr, tag)
                    let conditionalExpr = Function("IF", [dsCond; expr; Terminal(tag)])
                    Assign(step, tag, conditionalExpr)
                | cmd -> cmd)

        | USequence(stmts) ->
            // 시퀀스는 순차적으로 변환
            stmts |> List.collect (userStmtToDsStmts scope)

        | UFBCall(instanceName, fbName, inputs) ->
            // FB 호출은 Function 호출로 변환
            // FB는 출력 파라미터를 통해 결과를 반환하므로
            // 반환값을 저장할 필요 없음 - 단순히 호출만 수행
            let inputExprs =
                inputs
                |> Map.toList
                |> List.sortBy fst
                |> List.map (snd >> (userExprToDsExpr scope))
            let callExpr = Function(fbName, inputExprs)
            // FB 호출을 부울 상태 플래그로 저장 (호출 완료 표시용)
            // 인스턴스별로 고유한 Done 플래그 사용
            let doneTag = DsTag.Create(sprintf "%s.Done" instanceName, typeof<bool>)
            [Assign(nextStep(), doneTag, callExpr)]

        | UNoop ->
            // NOOP은 빈 리스트
            []

    /// DsStmt → UserStmt 변환 (부분 변환)
    /// 단순한 경우만 변환 가능
    let rec dsStmtToUserStmt (scope: string option) (stmt: DsStmt) : UserStmt option =
        match stmt with
        | Assign(_, tag, expr) ->
            // Assign은 UAssign으로 변환
            let originalName =
                match scope with
                | Some s ->
                    let prefix = s + "_"
                    if tag.Name.StartsWith(prefix) then
                        tag.Name.Substring(prefix.Length)
                    else
                        tag.Name
                | None -> tag.Name
            dsExprToUserExpr scope expr
            |> Option.map (fun ue -> UAssign(originalName, ue))

        | Command(_, condition, action) ->
            // Command는 UWhen으로 변환
            match dsExprToUserExpr scope condition, dsExprToUserExpr scope action with
            | Some cond, Some act ->
                // action을 UserStmt로 변환해야 하는데, act는 UserExpr
                // 단순화: Terminal이면 UAssign으로 간주
                match act with
                | UParam(name, _) ->
                    Some (UWhen(cond, UAssign(name, UConst(box true, typeof<bool>))))
                | _ ->
                    None
            | _ -> None

/// 변환 유틸리티
module ConversionUtils =

    /// UserFC 본문을 DsExpr로 변환
    let userFCBodyToDsExpr (fc: UserFC) : DsExpr =
        UserExprConverter.userExprToDsExpr (Some fc.Name) fc.Body

    /// UserFB 본문을 DsStmt list로 변환
    let userFBBodyToDsStmts (fb: UserFB) : DsStmt list =
        UserStmtConverter.resetStepCounter()
        fb.Body |> List.collect (UserStmtConverter.userStmtToDsStmts (Some fb.Name))

    /// 파라미터를 DsTag로 변환 (스코핑 적용)
    let paramToDsTag (scope: string) (param: FunctionParam) : DsTag =
        let scopedName = sprintf "%s_%s" scope param.Name
        DsTag.Create(scopedName, param.DataType)

    /// Static 변수를 DsTag로 변환 (스코핑 적용)
    let staticToDsTag (scope: string) (name: string) (dataType: Type) : DsTag =
        let scopedName = sprintf "%s_%s" scope name
        DsTag.Create(scopedName, dataType)

    /// UserFC를 실행 가능한 형태로 변환
    /// 반환: (입력 태그 목록, 출력 태그 목록, 본문 표현식)
    let prepareUserFCForExecution (fc: UserFC) : (DsTag list * DsTag list * DsExpr) =
        let inputTags = fc.Inputs |> List.map (paramToDsTag fc.Name)
        let outputTags = fc.Outputs |> List.map (paramToDsTag fc.Name)
        let body = userFCBodyToDsExpr fc
        (inputTags, outputTags, body)

    /// UserFB를 실행 가능한 형태로 변환
    /// 반환: (입력 태그, 출력 태그, Static 태그, 본문 명령문)
    let prepareUserFBForExecution (fb: UserFB) : (DsTag list * DsTag list * DsTag list * DsTag list * DsStmt list) =
        let inputTags = fb.Inputs |> List.map (paramToDsTag fb.Name)
        let outputTags = fb.Outputs |> List.map (paramToDsTag fb.Name)
        let inoutTags = fb.InOuts |> List.map (paramToDsTag fb.Name)
        let staticTags = fb.Statics |> List.map (fun (n, dt, _) -> staticToDsTag fb.Name n dt)
        let body = userFBBodyToDsStmts fb
        (inputTags, outputTags, inoutTags, staticTags, body)
