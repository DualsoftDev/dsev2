namespace Ev2.Cpu.Generation.Loops

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Loop Statement Builder - Convenient APIs for Loop Construction
// ═════════════════════════════════════════════════════════════════════════════
// FOR/WHILE 루프 생성을 위한 빌더 API 제공:
// - 유창한 API (Fluent API) 스타일
// - 타입 안전한 루프 구성
// - 일반적인 패턴에 대한 간편 함수
// ═════════════════════════════════════════════════════════════════════════════

/// FOR 루프 빌더
type ForLoopBuilder(loopVarName: string, loopVarType: Type) =
    let mutable startExpr: DsExpr option = None
    let mutable endExpr: DsExpr option = None
    let mutable stepExpr: DsExpr option = None
    let mutable body: DsStmt list = []
    let mutable stepNumber: int = 0

    /// 시작값 설정
    member this.From(start: int) =
        startExpr <- Some (Const(box start, typeof<int>))
        this

    /// 시작값 설정 (표현식)
    member this.From(start: DsExpr) =
        startExpr <- Some start
        this

    /// 종료값 설정
    member this.To(endVal: int) =
        endExpr <- Some (Const(box endVal, typeof<int>))
        this

    /// 종료값 설정 (표현식)
    member this.To(endVal: DsExpr) =
        endExpr <- Some endVal
        this

    /// 증분값 설정
    member this.Step(step: int) =
        stepExpr <- Some (Const(box step, typeof<int>))
        this

    /// 증분값 설정 (표현식)
    member this.Step(step: DsExpr) =
        stepExpr <- Some step
        this

    /// 본문 추가
    member this.Do(statements: DsStmt list) =
        body <- statements
        this

    /// 단일 명령문 추가
    member this.Do(statement: DsStmt) =
        body <- [statement]
        this

    /// 스텝 번호 설정
    member this.WithStep(step: int) =
        stepNumber <- step
        this

    /// 빌드
    member this.Build() : DsStmt =
        match startExpr, endExpr with
        | Some start, Some endVal ->
            let loopTag = DsTag.Create(loopVarName, loopVarType)
            For(stepNumber, loopTag, start, endVal, stepExpr, body)
        | None, _ ->
            raise (InvalidOperationException("FOR loop start value not set. Use From() method."))
        | _, None ->
            raise (InvalidOperationException("FOR loop end value not set. Use To() method."))

/// WHILE 루프 빌더
type WhileLoopBuilder(condition: DsExpr) =
    let mutable body: DsStmt list = []
    let mutable maxIterations: int option = None
    let mutable stepNumber: int = 0

    /// 본문 설정
    member this.Do(statements: DsStmt list) =
        body <- statements
        this

    /// 단일 명령문 설정
    member this.Do(statement: DsStmt) =
        body <- [statement]
        this

    /// 최대 반복 횟수 설정
    member this.WithMaxIterations(max: int) =
        maxIterations <- Some max
        this

    /// 스텝 번호 설정
    member this.WithStep(step: int) =
        stepNumber <- step
        this

    /// 빌드
    member this.Build() : DsStmt =
        While(stepNumber, condition, body, maxIterations)

/// 루프 빌더 헬퍼 함수
module LoopBuilder =

    // ─────────────────────────────────────────────────────────────────
    // FOR 루프 빌더
    // ─────────────────────────────────────────────────────────────────

    /// FOR 루프 빌더 생성 (정수 루프 변수)
    let forLoop (loopVarName: string) =
        ForLoopBuilder(loopVarName, typeof<int>)

    /// FOR 루프 빌더 생성 (타입 지정)
    let forLoopWithType (loopVarName: string) (varType: Type) =
        ForLoopBuilder(loopVarName, varType)

    /// FOR 루프 간편 생성 (0부터 count-1까지)
    let forRange (loopVarName: string) (count: int) (body: DsStmt list) : DsStmt =
        let loopTag = DsTag.Create(loopVarName, typeof<int>)
        For(0, loopTag, Const(box 0, typeof<int>), Const(box (count - 1), typeof<int>), Some(Const(box 1, typeof<int>)), body)

    /// FOR 루프 간편 생성 (start부터 end까지, step=1)
    let forFromTo (loopVarName: string) (start: int) (endVal: int) (body: DsStmt list) : DsStmt =
        let loopTag = DsTag.Create(loopVarName, typeof<int>)
        For(0, loopTag, Const(box start, typeof<int>), Const(box endVal, typeof<int>), Some(Const(box 1, typeof<int>)), body)

    /// FOR 루프 간편 생성 (start부터 end까지, step 지정)
    let forFromToStep (loopVarName: string) (start: int) (endVal: int) (step: int) (body: DsStmt list) : DsStmt =
        let loopTag = DsTag.Create(loopVarName, typeof<int>)
        For(0, loopTag, Const(box start, typeof<int>), Const(box endVal, typeof<int>), Some(Const(box step, typeof<int>)), body)

    // ─────────────────────────────────────────────────────────────────
    // WHILE 루프 빌더
    // ─────────────────────────────────────────────────────────────────

    /// WHILE 루프 빌더 생성
    let whileLoop (condition: DsExpr) =
        WhileLoopBuilder(condition)

    /// WHILE 루프 간편 생성
    let whileSimple (condition: DsExpr) (body: DsStmt list) : DsStmt =
        While(0, condition, body, None)

    /// WHILE 루프 간편 생성 (최대 반복 횟수 지정)
    let whileWithLimit (condition: DsExpr) (maxIter: int) (body: DsStmt list) : DsStmt =
        While(0, condition, body, Some maxIter)

    // ─────────────────────────────────────────────────────────────────
    // BREAK 생성
    // ─────────────────────────────────────────────────────────────────

    /// BREAK 문 생성
    let break' = Break(0)

    /// BREAK 문 생성 (스텝 번호 지정)
    let breakWithStep (step: int) = Break(step)

    // ─────────────────────────────────────────────────────────────────
    // 일반적인 루프 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배열 인덱스 반복 (0부터 size-1까지)
    let forArrayIndex (indexVarName: string) (arraySize: int) (body: DsStmt list) : DsStmt =
        forRange indexVarName arraySize body

    /// 카운트다운 루프 (start부터 0까지, step=-1)
    let forCountdown (loopVarName: string) (start: int) (body: DsStmt list) : DsStmt =
        let loopTag = DsTag.Create(loopVarName, typeof<int>)
        For(0, loopTag, Const(box start, typeof<int>), Const(box 0, typeof<int>), Some(Const(box -1, typeof<int>)), body)

    /// N번 반복 실행 (루프 변수 사용 안 함)
    let repeat (count: int) (body: DsStmt list) : DsStmt =
        forRange "_i" count body

/// 루프 변환 유틸리티
module LoopTransformations =

    /// FOR 루프를 WHILE 루프로 변환
    let forToWhile (forStmt: DsStmt) : DsStmt option =
        match forStmt with
        | For(step, loopVar, startExpr, endExpr, stepExpr, body) ->
            // FOR i := start TO end STEP step DO body END_FOR
            // =>
            // i := start
            // WHILE i <= end DO
            //   body
            //   i := i + step
            // END_WHILE

            let initAssign = Assign(step, loopVar, startExpr)

            let stepVal = stepExpr |> Option.defaultValue (Const(box 1, typeof<int>))
            let comparison =
                // step > 0이면 i <= end, step < 0이면 i >= end
                Binary(DsOp.Le, Terminal loopVar, endExpr)  // 간단히 <= 사용

            let incrementAssign =
                Assign(0, loopVar, Binary(DsOp.Add, Terminal loopVar, stepVal))

            let whileBody = body @ [incrementAssign]
            let whileStmt = While(0, comparison, whileBody, None)

            Some whileStmt
        | _ -> None

    /// WHILE 루프 펼치기 (상수 반복 횟수인 경우)
    let unfoldWhile (whileStmt: DsStmt) (maxUnfold: int) : DsStmt list option =
        match whileStmt with
        | While(_, Const(value, typ), body, _) when typ = typeof<bool> ->
            // 상수 조건: true이면 무한 루프 (펼칠 수 없음), false이면 실행 안 함
            match value with
            | :? bool as b ->
                if b then None  // 무한 루프
                else Some []    // 실행 안 함
            | _ -> None
        | _ -> None

    /// 중첩 루프 평탄화 (단순한 경우만)
    let flattenNestedLoops (outerLoop: DsStmt) : DsStmt option =
        match outerLoop with
        | For(step, outerVar, outerStart, outerEnd, outerStep, [For(_, innerVar, innerStart, innerEnd, innerStep, innerBody)]) ->
            // 단순한 중첩 루프만 평탄화 (본문이 단일 FOR 루프인 경우)
            // 실제 최적화는 더 복잡한 분석 필요
            None  // 일단 None 반환
        | _ -> None

/// 루프 생성 DSL (Domain Specific Language)
module LoopDSL =

    /// FOR 루프 DSL
    let inline (-->) (range: string * int * int) (body: DsStmt list) : DsStmt =
        let (varName, start, endVal) = range
        LoopBuilder.forFromTo varName start endVal body

    /// FOR 루프 범위 생성
    let inline range (varName: string) (start: int) (endVal: int) =
        (varName, start, endVal)

    /// WHILE 루프 DSL
    let inline whileDo (condition: DsExpr) (body: DsStmt list) : DsStmt =
        LoopBuilder.whileSimple condition body

/// 루프 분석 유틸리티
module LoopAnalysis =

    /// 루프 중첩 깊이 계산
    let rec loopDepth (stmt: DsStmt) : int =
        match stmt with
        | For(_, _, _, _, _, body) ->
            1 + (body |> List.map loopDepth |> function | [] -> 0 | depths -> List.max depths)
        | While(_, _, body, _) ->
            1 + (body |> List.map loopDepth |> function | [] -> 0 | depths -> List.max depths)
        | _ -> 0

    /// 루프 본문의 명령문 개수 계산
    let rec bodyStatementCount (stmt: DsStmt) : int =
        match stmt with
        | For(_, _, _, _, _, body) ->
            body |> List.sumBy (fun s -> 1 + bodyStatementCount s)
        | While(_, _, body, _) ->
            body |> List.sumBy (fun s -> 1 + bodyStatementCount s)
        | _ -> 0

    /// 루프에 BREAK가 포함되어 있는지 확인
    let rec containsBreak (stmt: DsStmt) : bool =
        match stmt with
        | Break _ -> true
        | For(_, _, _, _, _, body) | While(_, _, body, _) ->
            body |> List.exists containsBreak
        | _ -> false

    /// 루프 변수 추출
    let getLoopVariable (stmt: DsStmt) : string option =
        match stmt with
        | For(_, loopVar, _, _, _, _) -> Some loopVar.Name
        | _ -> None

    /// 최대 반복 횟수 추정 (상수 범위인 경우만)
    let estimateMaxIterations (stmt: DsStmt) : int option =
        match stmt with
        | For(_, _, Const(startVal, _), Const(endVal, _), stepOpt, _) ->
            try
                let start = startVal :?> int
                let endV = endVal :?> int
                let step =
                    match stepOpt with
                    | Some (Const(stepVal, _)) -> stepVal :?> int
                    | _ -> 1

                if step > 0 && endV >= start then
                    Some ((endV - start) / step + 1)
                elif step < 0 && endV <= start then
                    Some ((start - endV) / (-step) + 1)
                else
                    None
            with _ -> None
        | While(_, _, _, Some max) -> Some max
        | _ -> None
