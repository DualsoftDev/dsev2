namespace Ev2.Cpu.Generation.Loops

open System
open Ev2.Cpu.Core

// ═════════════════════════════════════════════════════════════════════════════
// Sequence Control Patterns - Standard Patterns for PLC/DCS Sequences
// ═════════════════════════════════════════════════════════════════════════════
// PLC/DCS 시퀀스 제어에서 자주 사용되는 패턴 제공:
// - 단계 기반 시퀀스 (Step Sequence)
// - 타임아웃이 있는 대기 (Wait with Timeout)
// - 재시도 로직 (Retry Logic)
// - 인터록 검사 (Interlock Checking)
// - 조건 대기 (Wait Until Condition)
// - 순차 시작/정지 (Sequential Start/Stop)
// ═════════════════════════════════════════════════════════════════════════════

/// 시퀀스 제어 패턴
module SequencePatterns =

    // ─────────────────────────────────────────────────────────────────
    // 타임아웃 및 대기 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 조건이 참이 될 때까지 대기 (최대 시간 제한)
    /// 예: waitUntil condition maxIterations "timedOut"
    ///  => timedOut := FALSE;
    ///     iterCount := 0;
    ///     WHILE NOT condition AND iterCount < maxIterations DO
    ///       iterCount := iterCount + 1;
    ///     END_WHILE;
    ///     IF iterCount >= maxIterations THEN timedOut := TRUE; END_IF
    let waitUntil (condition: DsExpr) (maxIterations: int) (timeoutVarName: string) : DsStmt list =
        let iterCountTag = DsTag.Create("_wait_count", DsDataType.TInt)
        let timeoutTag = DsTag.Create(timeoutVarName, DsDataType.TBool)

        // timedOut := FALSE
        let initTimeout =
            Assign(
                0,
                timeoutTag,
                Const(box false, DsDataType.TBool)
            )

        // iterCount := 0
        let initCount =
            Assign(
                0,
                iterCountTag,
                Const(box 0, DsDataType.TInt)
            )

        // WHILE NOT condition AND iterCount < maxIterations
        let whileCondition =
            Binary(
                DsOp.And,
                Unary(DsOp.Not, condition),
                Binary(
                    DsOp.Lt,
                    Terminal iterCountTag,
                    Const(box maxIterations, DsDataType.TInt)
                )
            )

        // iterCount := iterCount + 1
        let incrementCount =
            Assign(
                0,
                iterCountTag,
                Binary(
                    DsOp.Add,
                    Terminal iterCountTag,
                    Const(box 1, DsDataType.TInt)
                )
            )

        let whileLoop =
            While(
                0,
                whileCondition,
                [incrementCount],
                Some maxIterations
            )

        // IF iterCount >= maxIterations THEN timedOut := TRUE
        let timeoutCondition =
            Binary(
                DsOp.Ge,
                Terminal iterCountTag,
                Const(box maxIterations, DsDataType.TInt)
            )

        let setTimeoutTrue =
            Assign(
                0,
                timeoutTag,
                Const(box true, DsDataType.TBool)
            )

        let timeoutCheck = Command(0, timeoutCondition, Terminal (DsTag.Bool("_dummy")))

        [initTimeout; initCount; whileLoop; setTimeoutTrue]

    /// 고정 횟수만큼 반복 실행 (폴링 패턴)
    /// 예: pollWithDelay condition checkCount "success"
    ///  => success := FALSE;
    ///     FOR i := 0 TO checkCount-1 DO
    ///       IF condition THEN success := TRUE; EXIT; END_IF
    ///     END_FOR
    let pollWithDelay (condition: DsExpr) (checkCount: int) (successVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_poll_i", DsDataType.TInt)
        let successTag = DsTag.Create(successVarName, DsDataType.TBool)

        // success := FALSE
        let initStmt =
            Assign(
                0,
                successTag,
                Const(box false, DsDataType.TBool)
            )

        // IF condition THEN success := TRUE; EXIT
        let setSuccess =
            Assign(
                0,
                successTag,
                Const(box true, DsDataType.TBool)
            )

        let breakStmt = Break(0)

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, DsDataType.TInt),
                Const(box (checkCount - 1), DsDataType.TInt),
                Some(Const(box 1, DsDataType.TInt)),
                [setSuccess; breakStmt]
            )

        [initStmt; forLoop]

    // ─────────────────────────────────────────────────────────────────
    // 재시도 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 작업 재시도 로직 (최대 재시도 횟수)
    /// 예: retryOperation action checkSuccess maxRetries "succeeded"
    ///  => succeeded := FALSE;
    ///     retryCount := 0;
    ///     WHILE NOT succeeded AND retryCount < maxRetries DO
    ///       action;
    ///       IF checkSuccess THEN succeeded := TRUE; END_IF
    ///       retryCount := retryCount + 1;
    ///     END_WHILE
    let retryOperation (action: DsStmt list) (checkSuccess: DsExpr) (maxRetries: int) (successVarName: string) : DsStmt list =
        let retryCountTag = DsTag.Create("_retry_count", DsDataType.TInt)
        let successTag = DsTag.Create(successVarName, DsDataType.TBool)

        // succeeded := FALSE
        let initSuccess =
            Assign(
                0,
                successTag,
                Const(box false, DsDataType.TBool)
            )

        // retryCount := 0
        let initCount =
            Assign(
                0,
                retryCountTag,
                Const(box 0, DsDataType.TInt)
            )

        // WHILE NOT succeeded AND retryCount < maxRetries
        let whileCondition =
            Binary(
                DsOp.And,
                Unary(DsOp.Not, Terminal successTag),
                Binary(
                    DsOp.Lt,
                    Terminal retryCountTag,
                    Const(box maxRetries, DsDataType.TInt)
                )
            )

        // IF checkSuccess THEN succeeded := TRUE
        let setSuccess =
            Assign(
                0,
                successTag,
                Const(box true, DsDataType.TBool)
            )

        let checkStmt = Command(0, checkSuccess, Terminal (DsTag.Bool("_dummy")))

        // retryCount := retryCount + 1
        let incrementCount =
            Assign(
                0,
                retryCountTag,
                Binary(
                    DsOp.Add,
                    Terminal retryCountTag,
                    Const(box 1, DsDataType.TInt)
                )
            )

        let whileBody = action @ [setSuccess; incrementCount]

        let whileLoop =
            While(
                0,
                whileCondition,
                whileBody,
                Some maxRetries
            )

        [initSuccess; initCount; whileLoop]

    // ─────────────────────────────────────────────────────────────────
    // 순차 시작/정지 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 장비 순차 시작 (각 장비를 순서대로 시작, 시간 간격 대기)
    /// 예: sequentialStart ["motor1"; "motor2"; "motor3"] delayCount
    ///  => FOR i := 0 TO 2 DO
    ///       equipmentStart[i] := TRUE;
    ///       FOR delay := 0 TO delayCount DO (* wait *) END_FOR
    ///     END_FOR
    let sequentialStart (equipmentNames: string list) (delayCount: int) : DsStmt =
        let indexVar = DsTag.Create("_seq_start_i", DsDataType.TInt)
        let delayVar = DsTag.Create("_seq_delay", DsDataType.TInt)

        // 각 장비에 대한 시작 명령 (실제로는 배열 또는 개별 변수 접근)
        // 단순화: equipmentStart[i] := TRUE
        let equipmentTag = DsTag.Create("equipmentStart", DsDataType.TBool)

        let startCmd =
            Assign(
                0,
                equipmentTag,
                Const(box true, DsDataType.TBool)
            )

        // 지연 루프 (빈 루프로 시간 소모)
        let delayLoop =
            For(
                0,
                delayVar,
                Const(box 0, DsDataType.TInt),
                Const(box delayCount, DsDataType.TInt),
                Some(Const(box 1, DsDataType.TInt)),
                []  // 빈 본문
            )

        For(
            0,
            indexVar,
            Const(box 0, DsDataType.TInt),
            Const(box (equipmentNames.Length - 1), DsDataType.TInt),
            Some(Const(box 1, DsDataType.TInt)),
            [startCmd; delayLoop]
        )

    /// 장비 순차 정지 (역순으로 정지)
    /// 예: sequentialStop ["motor1"; "motor2"; "motor3"] delayCount
    ///  => FOR i := 2 TO 0 BY -1 DO
    ///       equipmentStop[i] := TRUE;
    ///       FOR delay := 0 TO delayCount DO (* wait *) END_FOR
    ///     END_FOR
    let sequentialStop (equipmentNames: string list) (delayCount: int) : DsStmt =
        let indexVar = DsTag.Create("_seq_stop_i", DsDataType.TInt)
        let delayVar = DsTag.Create("_seq_delay", DsDataType.TInt)

        let equipmentTag = DsTag.Create("equipmentStop", DsDataType.TBool)

        let stopCmd =
            Assign(
                0,
                equipmentTag,
                Const(box true, DsDataType.TBool)
            )

        // 지연 루프
        let delayLoop =
            For(
                0,
                delayVar,
                Const(box 0, DsDataType.TInt),
                Const(box delayCount, DsDataType.TInt),
                Some(Const(box 1, DsDataType.TInt)),
                []
            )

        For(
            0,
            indexVar,
            Const(box (equipmentNames.Length - 1), DsDataType.TInt),
            Const(box 0, DsDataType.TInt),
            Some(Const(box -1, DsDataType.TInt)),  // 역순
            [stopCmd; delayLoop]
        )

    // ─────────────────────────────────────────────────────────────────
    // 인터록 검사 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 다중 인터록 조건 검사
    /// 예: checkInterlocks ["safetyOK"; "doorClosed"; "pressureOK"] "allOK"
    ///  => allOK := TRUE;
    ///     FOR i := 0 TO 2 DO
    ///       IF NOT interlock[i] THEN allOK := FALSE; EXIT; END_IF
    ///     END_FOR
    let checkInterlocks (interlockNames: string list) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_interlock_i", DsDataType.TInt)
        let resultTag = DsTag.Create(resultVarName, DsDataType.TBool)
        let interlockTag = DsTag.Create("interlock", DsDataType.TBool)

        // allOK := TRUE
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box true, DsDataType.TBool)
            )

        // IF NOT interlock[i] THEN allOK := FALSE; EXIT
        let condition =
            Unary(DsOp.Not, Terminal interlockTag)

        let setFalse =
            Assign(
                0,
                resultTag,
                Const(box false, DsDataType.TBool)
            )

        let breakStmt = Break(0)

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, DsDataType.TInt),
                Const(box (interlockNames.Length - 1), DsDataType.TInt),
                Some(Const(box 1, DsDataType.TInt)),
                [setFalse; breakStmt]
            )

        [initStmt; forLoop]

    // ─────────────────────────────────────────────────────────────────
    // 단계 기반 시퀀스 패턴 (ISA-88 스타일)
    // ─────────────────────────────────────────────────────────────────

    /// 단계 실행 루프 (현재 단계에 따라 다른 동작)
    /// 예: stepSequence maxSteps currentStepVar
    ///  => WHILE currentStep <= maxSteps DO
    ///       (* 각 단계별 로직은 외부에서 추가 *)
    ///       currentStep := currentStep + 1;
    ///     END_WHILE
    let stepSequence (maxSteps: int) (currentStepVarName: string) : DsStmt =
        let stepTag = DsTag.Create(currentStepVarName, DsDataType.TInt)

        // WHILE currentStep <= maxSteps
        let whileCondition =
            Binary(
                DsOp.Le,
                Terminal stepTag,
                Const(box maxSteps, DsDataType.TInt)
            )

        // currentStep := currentStep + 1
        let incrementStep =
            Assign(
                0,
                stepTag,
                Binary(
                    DsOp.Add,
                    Terminal stepTag,
                    Const(box 1, DsDataType.TInt)
                )
            )

        While(
            0,
            whileCondition,
            [incrementStep],
            Some (maxSteps + 10)  // 안전을 위한 여유
        )

    /// 조건부 단계 진행 (조건이 만족되면 다음 단계로)
    /// 예: conditionalStep currentStepVar condition nextStep
    ///  => IF currentStep = thisStep AND condition THEN
    ///       currentStep := nextStep;
    ///     END_IF
    let conditionalStep (currentStepVarName: string) (thisStep: int) (condition: DsExpr) (nextStep: int) : DsStmt =
        let stepTag = DsTag.Create(currentStepVarName, DsDataType.TInt)

        // currentStep = thisStep AND condition
        let fullCondition =
            Binary(
                DsOp.And,
                Binary(
                    DsOp.Eq,
                    Terminal stepTag,
                    Const(box thisStep, DsDataType.TInt)
                ),
                condition
            )

        // currentStep := nextStep
        let assignNextStep =
            Assign(
                0,
                stepTag,
                Const(box nextStep, DsDataType.TInt)
            )

        Command(0, fullCondition, Terminal (DsTag.Bool("_dummy")))

    // ─────────────────────────────────────────────────────────────────
    // 배치 처리 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 배치 작업 처리 (N개씩 묶어서 처리)
    /// 예: batchProcess totalItems batchSize processBatchAction
    ///  => FOR batchStart := 0 TO totalItems-1 BY batchSize DO
    ///       batchEnd := MIN(batchStart + batchSize - 1, totalItems - 1);
    ///       (* process batch from batchStart to batchEnd *)
    ///     END_FOR
    let batchProcess (totalItems: int) (batchSize: int) (processBatch: DsStmt list) : DsStmt =
        let batchStartVar = DsTag.Create("_batch_start", DsDataType.TInt)
        let batchEndVar = DsTag.Create("_batch_end", DsDataType.TInt)

        // batchEnd := MIN(batchStart + batchSize - 1, totalItems - 1)
        let calculateEnd =
            Assign(
                0,
                batchEndVar,
                Binary(
                    DsOp.Add,
                    Terminal batchStartVar,
                    Const(box (batchSize - 1), DsDataType.TInt)
                )
            )

        let forBody = calculateEnd :: processBatch

        For(
            0,
            batchStartVar,
            Const(box 0, DsDataType.TInt),
            Const(box (totalItems - 1), DsDataType.TInt),
            Some(Const(box batchSize, DsDataType.TInt)),
            forBody
        )

    // ─────────────────────────────────────────────────────────────────
    // 우선순위 스캔 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 우선순위가 높은 항목부터 스캔 (첫 번째 조건 만족 항목 찾기)
    /// 예: priorityScan conditions maxPriority "selectedIndex"
    ///  => selectedIndex := -1;
    ///     FOR i := 0 TO maxPriority DO
    ///       IF condition[i] AND selectedIndex = -1 THEN
    ///         selectedIndex := i;
    ///         EXIT;
    ///       END_IF
    ///     END_FOR
    let priorityScan (maxPriority: int) (conditionCheck: DsExpr) (resultVarName: string) : DsStmt list =
        let indexVar = DsTag.Create("_priority_i", DsDataType.TInt)
        let resultTag = DsTag.Create(resultVarName, DsDataType.TInt)

        // selectedIndex := -1
        let initStmt =
            Assign(
                0,
                resultTag,
                Const(box -1, DsDataType.TInt)
            )

        // IF condition[i] AND selectedIndex = -1
        let condition =
            Binary(
                DsOp.And,
                conditionCheck,
                Binary(
                    DsOp.Eq,
                    Terminal resultTag,
                    Const(box -1, DsDataType.TInt)
                )
            )

        // selectedIndex := i
        let assignIndex =
            Assign(
                0,
                resultTag,
                Terminal indexVar
            )

        let breakStmt = Break(0)

        let ifStmt = Command(0, condition, Terminal (DsTag.Bool("_dummy")))

        let forLoop =
            For(
                0,
                indexVar,
                Const(box 0, DsDataType.TInt),
                Const(box maxPriority, DsDataType.TInt),
                Some(Const(box 1, DsDataType.TInt)),
                [assignIndex; breakStmt]
            )

        [initStmt; forLoop]

    // ─────────────────────────────────────────────────────────────────
    // 라운드 로빈 스캔 패턴
    // ─────────────────────────────────────────────────────────────────

    /// 라운드 로빈 방식으로 항목 스캔 (공정한 분배)
    /// 예: roundRobinScan totalItems lastIndexVar "nextIndex"
    ///  => nextIndex := (lastIndex + 1) MOD totalItems;
    ///     (* scan from nextIndex *)
    ///     lastIndex := nextIndex;
    let roundRobinScan (totalItems: int) (lastIndexVarName: string) (nextIndexVarName: string) : DsStmt list =
        let lastIndexTag = DsTag.Create(lastIndexVarName, DsDataType.TInt)
        let nextIndexTag = DsTag.Create(nextIndexVarName, DsDataType.TInt)

        // nextIndex := (lastIndex + 1) MOD totalItems
        let calculateNext =
            Assign(
                0,
                nextIndexTag,
                Binary(
                    DsOp.Mod,
                    Binary(
                        DsOp.Add,
                        Terminal lastIndexTag,
                        Const(box 1, DsDataType.TInt)
                    ),
                    Const(box totalItems, DsDataType.TInt)
                )
            )

        // lastIndex := nextIndex
        let updateLast =
            Assign(
                0,
                lastIndexTag,
                Terminal nextIndexTag
            )

        [calculateNext; updateLast]
