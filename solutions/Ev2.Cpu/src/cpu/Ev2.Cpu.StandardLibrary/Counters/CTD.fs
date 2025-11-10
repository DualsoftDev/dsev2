namespace Ev2.Cpu.StandardLibrary.Counters

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// CTD - Count Down Counter (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// CD 입력의 상승 에지마다 카운트를 감소시킵니다.
/// - LD = TRUE → CV = PV (프리셋 값 로드)
/// - CD 상승 에지 → CV = CV - 1
/// - CV <= 0 → Q = TRUE
///
/// 사용 예: 잔여 개수 카운터, 타임아웃 카운터
/// </remarks>
module CTD =

    /// <summary>
    /// CTD Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 CTD FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("CTD")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("CD", DsDataType.TBool)      // Count down input
        builder.AddInput("LD", DsDataType.TBool)      // Load input
        builder.AddInput("PV", DsDataType.TInt)       // Preset value
        builder.AddOutput("Q", DsDataType.TBool)      // Output (TRUE when CV <= 0)
        builder.AddOutput("CV", DsDataType.TInt)      // Current value

        // Static 변수
        builder.AddStaticWithInit("Count", DsDataType.TInt, box 0)
        builder.AddStaticWithInit("LastCD", DsDataType.TBool, box false)

        let cd = Terminal(DsTag.Bool("CD"))
        let load = Terminal(DsTag.Bool("LD"))
        let pv = Terminal(DsTag.Int("PV"))
        let count = Terminal(DsTag.Int("Count"))
        let lastCD = Terminal(DsTag.Bool("LastCD"))

        // CD 상승 에지 감지
        let risingEdge = and' cd (not' lastCD)

        // CRITICAL FIX (DEFECT-CRIT-10): Apply LD after CD, not before (IEC 61131-3 §2.5.2.3.2)
        // Previous code: IF LD THEN PV ELSIF CD THEN Count-1 (Load takes priority)
        // IEC standard: CD pulse should decrement BEFORE LD loads PV in same scan
        // Problem: CD pulse lost when LD active simultaneously, breaks synchronous state machines
        // Solution: Decrement first, then load if LD active (load overwrites decremented value)

        // CD 상승 에지이고 Count > 0이면 → Count 감소 (Load 무관하게 처리)
        let decrementCondition = and' risingEdge (gt count (intExpr 0))

        // Step 1: Apply CD decrement if conditions met
        let afterDecrement =
            Function("IF", [
                decrementCondition
                sub count (intExpr 1)
                count
            ])

        // Step 2: Apply LD load (overwrites decrement if both active)
        let newCount =
            Function("IF", [
                load
                pv
                afterDecrement
            ])

        builder.AddStatement(assignAuto "Count" DsDataType.TInt newCount)

        // LastCD 업데이트
        builder.AddStatement(
            assignAuto "LastCD" DsDataType.TBool cd
        )

        // 출력 설정
        // Q := (CV <= 0)
        builder.AddStatement(
            assignAuto "Q" DsDataType.TBool (le count (intExpr 0))
        )
        builder.AddStatement(
            assignAuto "CV" DsDataType.TInt count
        )

        builder.SetDescription("Count Down counter - decrements on CD rising edge (IEC 61131-3)")
        builder.Build()
