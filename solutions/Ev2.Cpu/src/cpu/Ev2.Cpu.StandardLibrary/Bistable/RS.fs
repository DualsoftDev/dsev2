namespace Ev2.Cpu.StandardLibrary.Bistable

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// RS - Reset-Set Bistable (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// Reset이 우선순위를 가지는 양방향 래치입니다.
/// - S = TRUE이면 Q1 = TRUE로 설정
/// - R1 = TRUE이면 Q1 = FALSE로 리셋
/// - S와 R1이 동시에 TRUE이면 R1이 우선 (Reset priority)
/// </remarks>
module RS =

    /// <summary>
    /// RS Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 RS FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("RS")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("S", typeof<bool>)      // Set input
        builder.AddInput("R1", typeof<bool>)     // Reset input
        builder.AddOutput("Q1", typeof<bool>)    // Output

        // Static 변수: 상태 저장
        builder.AddStaticWithInit("STATE", typeof<bool>, box false)

        // 로직 (Reset priority):
        // STATE := IF R1 THEN FALSE ELSIF S THEN TRUE ELSE STATE
        // Q1 := STATE

        let s = Terminal(DsTag.Bool("S"))
        let r1 = Terminal(DsTag.Bool("R1"))
        let state = Terminal(DsTag.Bool("STATE"))

        // STATE := IF R1 THEN FALSE ELSIF S THEN TRUE ELSE STATE (Reset priority)
        let newState =
            Function("IF", [
                r1
                boolExpr false
                Function("IF", [
                    s
                    boolExpr true
                    state
                ])
            ])

        builder.AddStatement(assignAuto "STATE" typeof<bool> newState)
        builder.AddStatement(assignAuto "Q1" typeof<bool> state)

        builder.SetDescription("Reset-Set bistable with Reset priority (IEC 61131-3)")
        builder.Build()
