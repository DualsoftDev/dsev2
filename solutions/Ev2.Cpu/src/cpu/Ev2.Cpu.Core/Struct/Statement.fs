namespace Ev2.Cpu.Core

open System
open Ev2.Cpu.Core.Expression

[<AutoOpen>]
module Statement =
    /// 실행 문장 (stepNumber 포함)
    type DsStmt =
        | Assign of step:int * target:DsTag * expr:DsExpr
        | Command of step:int * condition:DsExpr * action:DsExpr
        | For of step:int * loopVar:DsTag * startExpr:DsExpr * endExpr:DsExpr * stepExpr:DsExpr option * body:DsStmt list
        | While of step:int * condition:DsExpr * body:DsStmt list * maxIterations:int option
        | Break of step:int

    /// 스텝 번호 조회
    let getStepNumber = function
        | Assign(step, _, _) -> step
        | Command(step, _, _) -> step
        | For(step, _, _, _, _, _) -> step
        | While(step, _, _, _) -> step
        | Break(step) -> step

    /// 스텝 번호 설정
    let withStep step stmt =
        match stmt with
        | Assign(_, target, expr) -> Assign(step, target, expr)
        | Command(_, cond, action) -> Command(step, cond, action)
        | For(_, loopVar, startExpr, endExpr, stepExpr, body) -> For(step, loopVar, startExpr, endExpr, stepExpr, body)
        | While(_, condition, body, maxIter) -> While(step, condition, body, maxIter)
        | Break(_) -> Break(step)

    let assignWithStep step target expr = Assign(step, target, expr)
    let commandWithStep step cond action = Command(step, cond, action)

    /// 기본 스텝 간격 자동 할당
    let assignSequentialSteps (start:int) (gap:int) (stmts: DsStmt list) : DsStmt list =
        let gap = if gap <= 0 then 10 else gap
        let start = if start < 0 then 0 else start
        let mutable next = start + gap
        stmts
        |> List.map (fun stmt ->
            let step = getStepNumber stmt
            if step <> 0 then
                if step >= next then
                    next <- ((step / gap) + 1) * gap
                stmt
            else
                let stamped = withStep next stmt
                next <- next + gap
                stamped)

    /// 문장별 참조 변수 집합
    type DsStmt with
        member s.ReferencedVars =
            match s with
            | Assign(_, _, expr) -> expr.Variables
            | Command(_, cond, action) -> Set.union cond.Variables action.Variables
            | For(_, loopVar, startExpr, endExpr, stepExpr, body) ->
                let startVars = startExpr.Variables
                let endVars = endExpr.Variables
                let stepVars = stepExpr |> Option.map (fun e -> e.Variables) |> Option.defaultValue Set.empty
                let bodyVars = body |> List.map (fun stmt -> stmt.ReferencedVars) |> Set.unionMany
                Set.unionMany [Set.singleton loopVar.Name; startVars; endVars; stepVars; bodyVars]
            | While(_, condition, body, _) ->
                let condVars = condition.Variables
                let bodyVars = body |> List.map (fun stmt -> stmt.ReferencedVars) |> Set.unionMany
                Set.union condVars bodyVars
            | Break(_) -> Set.empty

        member s.ToText() =
            match s with
            | Assign(_, target, expr) -> sprintf "%s := %s" target.Name (expr.ToText())
            | Command(_, cond, action) -> sprintf "IF %s THEN %s" (cond.ToText()) (action.ToText())
            | For(_, loopVar, startExpr, endExpr, stepExpr, body) ->
                let stepText = stepExpr |> Option.map (fun e -> sprintf " BY %s" (e.ToText())) |> Option.defaultValue ""
                let bodyText = body |> List.map (fun stmt -> "  " + stmt.ToText()) |> String.concat "\n"
                sprintf "FOR %s := %s TO %s%s DO\n%s\nEND_FOR" loopVar.Name (startExpr.ToText()) (endExpr.ToText()) stepText bodyText
            | While(_, condition, body, maxIter) ->
                let maxText = maxIter |> Option.map (fun m -> sprintf " (max: %d)" m) |> Option.defaultValue ""
                let bodyText = body |> List.map (fun stmt -> "  " + stmt.ToText()) |> String.concat "\n"
                sprintf "WHILE %s%s DO\n%s\nEND_WHILE" (condition.ToText()) maxText bodyText
            | Break(_) -> "BREAK"

    // === 기본 연산자 ===
    let private makeAssign target expr = Assign(0, target, expr)
    let private makeCommand cond action = Command(0, cond, action)

    let (:=) target expr = makeAssign target expr
    let (-->) cond action = makeCommand cond action

    /// 문자열을 변수로 (타겟 표현용)
    let strVar name = Expression.strVar name

    // === 논리 연산자 ===
    let (&&.) l r = Binary(And, l, r)
    let (||.) l r = Binary(Or, l, r)
    let (!!.) e = Unary(Not, e)

    // === 비교 연산자 ===
    let (==.) l r = Binary(Eq, l, r)
    let (<>.) l r = Binary(Ne, l, r)
    let (!=.) l r = Binary(Ne, l, r)
    let (>>.) l r = Binary(Gt, l, r)
    let (<<.) l r = Binary(Lt, l, r)
    let (>=.) l r = Binary(Ge, l, r)
    let (<=.) l r = Binary(Le, l, r)

    // === 산술 연산자 ===
    let (.+.) l r = Binary(Add, l, r)
    let (.-.) l r = Binary(Sub, l, r)
    let (.*.) l r = Binary(Mul, l, r)
    let (./.) l r = Binary(Div, l, r)
    let (.%.) l r = Binary(Mod, l, r)

    // === PLC 래더 스타일 ===
    let (--|) (sets, resets) coil = makeAssign coil (sets &&. (!!. resets))

    let (==|) (sets, resets) coil =
        let coilTag = DsTag.Bool(coil)
        let coilVar = Terminal(coilTag)
        makeAssign coilTag ((sets ||. coilVar) &&. (!!. resets))

    let (--^) condition target =
        let prev = DsTag.Bool(target + "_prev")
        let targetTag = DsTag.Bool(target)
        [ makeAssign prev (Terminal targetTag)
          makeAssign targetTag (condition &&. (!!. (Terminal prev))) ]

    let (-!^) condition target =
        let prev = DsTag.Bool(target + "_prev")
        let targetTag = DsTag.Bool(target)
        [ makeAssign prev (Terminal targetTag)
          makeAssign targetTag ((!!. condition) &&. Terminal prev) ]

    // === 산술 연산 래더 ===
    let (--+) (cond, src1, src2) target =
        cond --> fn "Move" [src1 .+. src2; strVar target]

    let (---) (cond, src1, src2) target =
        cond --> fn "Move" [src1 .-. src2; strVar target]

    let (--*) (cond, src1, src2) target =
        cond --> fn "Move" [src1 .*. src2; strVar target]

    let (--/) (cond, src1, src2) target =
        cond --> fn "Move" [src1 ./. src2; strVar target]

    let (-~>) (cond, source) target =
        cond --> fn "Move" [source; strVar target]

    // === PLC 함수 ===
    // CRITICAL FIX (DEFECT-021-10): Update to 3-arg/4-arg forms (matching DEFECT-020-4)
    // Previous 2-arg forms deprecated and rejected by runtime
    // MAJOR FIX (DEFECT-022-4): Use string literal, not strVar
    // Previous code used strVar timer which reads tag value instead of using the literal name
    let (--@) condition (timer, preset) =
        // TON requires [enable; name; preset] - use condition as enable
        condition --> fn "TON" [condition; str timer; num preset]

    // MAJOR FIX (DEFECT-022-5): Use bool literal, not boolVar
    // Previous code used boolVar "false" which references a tag instead of literal FALSE
    let (--%) condition (counter, preset) =
        // CTU requires [name; countUp; reset; preset] - use condition as countUp, false as reset
        condition --> fn "CTU" [str counter; condition; bool false; num preset]

    // === 유틸리티 ===
    let inRange target min max =
        (target >=. min) &&. (target <=. max)

    let condiIf cond thenVal elseVal = fn "IF" [cond; thenVal; elseVal]

    type Program = {
        Name: string
        Inputs: (string * DsDataType) list
        Outputs: (string * DsDataType) list
        Locals: (string * DsDataType) list
        Body: DsStmt list
    }

    type Program with
        member p.AllVars =
            let defined =
                p.Inputs @ p.Outputs @ p.Locals
                |> List.map fst |> Set.ofList
            let referenced =
                p.Body |> List.map (fun s -> s.ReferencedVars) |> Set.unionMany
            Set.union defined referenced

        member p.ToText() =
            let varSection title vars =
                if List.isEmpty vars then ""
                else
                    let lines = vars |> List.map (fun (n, t) -> sprintf "    %s : %O;" n t)
                    sprintf "  %s\n%s\n  END_%s" title (String.concat "\n" lines) title
            [   sprintf "PROGRAM %s" p.Name
                varSection "VAR_INPUT" p.Inputs
                varSection "VAR_OUTPUT" p.Outputs
                varSection "VAR" p.Locals
                ""
                yield! p.Body |> List.map (fun s -> "  " + s.ToText())
                "END_PROGRAM" ]
            |> List.filter ((<>) "")
            |> String.concat "\n"
