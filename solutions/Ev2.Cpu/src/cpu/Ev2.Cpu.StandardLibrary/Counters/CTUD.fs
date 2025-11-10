namespace Ev2.Cpu.StandardLibrary.Counters

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// CTUD - Count Up/Down Counter (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// CTU와 CTD를 결합한 양방향 카운터입니다.
/// - CU 상승 에지 → CV = CV + 1
/// - CD 상승 에지 → CV = CV - 1
/// - R = TRUE → CV = 0
/// - LD = TRUE → CV = PV
/// - CV >= PV → QU = TRUE
/// - CV <= 0 → QD = TRUE
///
/// 사용 예: 재고 관리 (입고/출고), 위치 제어
/// </remarks>
module CTUD =

    /// <summary>
    /// CTUD Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 CTUD FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("CTUD")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("CU", typeof<bool>)      // Count up input
        builder.AddInput("CD", typeof<bool>)      // Count down input
        builder.AddInput("R", typeof<bool>)       // Reset input
        builder.AddInput("LD", typeof<bool>)      // Load input
        builder.AddInput("PV", typeof<int>)       // Preset value
        builder.AddOutput("QU", typeof<bool>)     // Count up done (CV >= PV)
        builder.AddOutput("QD", typeof<bool>)     // Count down done (CV <= 0)
        builder.AddOutput("CV", typeof<int>)      // Current value

        // Static 변수
        builder.AddStaticWithInit("Count", typeof<int>, box 0)
        builder.AddStaticWithInit("LastCU", typeof<bool>, box false)
        builder.AddStaticWithInit("LastCD", typeof<bool>, box false)

        let cu = Terminal(DsTag.Bool("CU"))
        let cd = Terminal(DsTag.Bool("CD"))
        let reset = Terminal(DsTag.Bool("R"))
        let load = Terminal(DsTag.Bool("LD"))
        let pv = Terminal(DsTag.Int("PV"))
        let count = Terminal(DsTag.Int("Count"))
        let lastCU = Terminal(DsTag.Bool("LastCU"))
        let lastCD = Terminal(DsTag.Bool("LastCD"))

        // CU 상승 에지 감지
        let cuRisingEdge = and' cu (not' lastCU)

        // CU 상승 에지 → Count 증가
        let incrementCondition = and' (and' cuRisingEdge (not' reset)) (not' load)

        // CD 상승 에지 감지
        let cdRisingEdge = and' cd (not' lastCD)

        // CD 상승 에지 → Count 감소
        let decrementCondition = and' (and' cdRisingEdge (not' reset)) (not' load)

        // Count := IF R THEN 0 ELSIF (LD AND NOT R) THEN PV
        //          ELSIF incrementCondition THEN Count + 1
        //          ELSIF decrementCondition THEN Count - 1
        //          ELSE Count
        let newCount =
            Function("IF", [
                reset
                intExpr 0
                Function("IF", [
                    and' load (not' reset)
                    pv
                    Function("IF", [
                        incrementCondition
                        add count (intExpr 1)
                        Function("IF", [
                            decrementCondition
                            sub count (intExpr 1)
                            count
                        ])
                    ])
                ])
            ])

        builder.AddStatement(assignAuto "Count" typeof<int> newCount)

        // LastCU, LastCD 업데이트
        builder.AddStatement(
            assignAuto "LastCU" typeof<bool> cu
        )
        builder.AddStatement(
            assignAuto "LastCD" typeof<bool> cd
        )

        // 출력 설정
        // QU := (CV >= PV)
        builder.AddStatement(
            assignAuto "QU" typeof<bool> (ge count pv)
        )
        // QD := (CV <= 0)
        builder.AddStatement(
            assignAuto "QD" typeof<bool> (le count (intExpr 0))
        )
        builder.AddStatement(
            assignAuto "CV" typeof<int> count
        )

        builder.SetDescription("Count Up/Down counter - bidirectional counter (IEC 61131-3)")
        builder.Build()
