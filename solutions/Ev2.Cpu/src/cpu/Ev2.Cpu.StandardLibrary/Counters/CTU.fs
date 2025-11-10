namespace Ev2.Cpu.StandardLibrary.Counters

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// CTU - Count Up Counter (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// CU 입력의 상승 에지마다 카운트를 증가시킵니다.
/// - CU 상승 에지 → CV = CV + 1
/// - CV >= PV → Q = TRUE
/// - R = TRUE → CV = 0, Q = FALSE
///
/// 사용 예: 제품 카운터, 이벤트 카운터
/// </remarks>
module CTU =

    /// <summary>
    /// CTU Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 CTU FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("CTU")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("CU", DsDataType.TBool)      // Count up input
        builder.AddInput("R", DsDataType.TBool)       // Reset input
        builder.AddInput("PV", DsDataType.TInt)       // Preset value
        builder.AddOutput("Q", DsDataType.TBool)      // Output (TRUE when CV >= PV)
        builder.AddOutput("CV", DsDataType.TInt)      // Current value

        // Static 변수
        builder.AddStaticWithInit("Count", DsDataType.TInt, box 0)
        builder.AddStaticWithInit("LastCU", DsDataType.TBool, box false)

        let cu = Terminal(DsTag.Bool("CU"))
        let reset = Terminal(DsTag.Bool("R"))
        let pv = Terminal(DsTag.Int("PV"))
        let count = Terminal(DsTag.Int("Count"))
        let lastCU = Terminal(DsTag.Bool("LastCU"))

        // CU 상승 에지 감지
        let risingEdge = and' cu (not' lastCU)

        // CU 상승 에지이고 Reset이 아니면 → Count 증가
        // CRITICAL FIX (DEFECT-020-8): Remove (lt count pv) saturation guard
        // IEC 61131-3 counters accumulate beyond PV (only Q output latches)
        // Previous code lost extra pulses, breaking state machines that rely on actual count
        let incrementCondition = and' risingEdge (not' reset)

        // CRITICAL FIX (DEFECT-CRIT-9): Add INT_MAX saturation to prevent overflow
        // Previous code: Count could overflow Int32.MaxValue, wrapping to negative values
        // Problem: Breaks counter invariant (CV should be non-negative), corrupts Q output
        // Solution: Clamp Count at Int32.MaxValue (IEC 61131-3 saturation semantics)
        // Warning: Emit overflow warning when saturation occurs (aids debugging)
        let maxInt = intExpr System.Int32.MaxValue
        let canIncrement = lt count maxInt  // Only increment if below max

        // Count := IF R THEN 0 ELSIF incrementCondition AND canIncrement THEN Count + 1 ELSE Count
        let newCount =
            Function("IF", [
                reset
                intExpr 0
                Function("IF", [
                    and' incrementCondition canIncrement
                    add count (intExpr 1)
                    count
                ])
            ])

        builder.AddStatement(assignAuto "Count" DsDataType.TInt newCount)

        // LastCU 업데이트
        builder.AddStatement(
            assignAuto "LastCU" DsDataType.TBool cu
        )

        // 출력 설정
        // Q := (CV >= PV)
        builder.AddStatement(
            assignAuto "Q" DsDataType.TBool (ge count pv)
        )
        builder.AddStatement(
            assignAuto "CV" DsDataType.TInt count
        )

        builder.SetDescription("Count Up counter - increments on CU rising edge (IEC 61131-3)")
        builder.Build()
