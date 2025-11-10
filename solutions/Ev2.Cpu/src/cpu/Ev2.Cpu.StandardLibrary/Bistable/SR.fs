namespace Ev2.Cpu.StandardLibrary.Bistable

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// SR - Set-Reset Bistable (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// Set이 우선순위를 가지는 양방향 래치입니다.
/// - S1 = TRUE이면 Q1 = TRUE로 설정
/// - R = TRUE이면 Q1 = FALSE로 리셋
/// - S1과 R이 동시에 TRUE이면 S1이 우선 (Set priority)
/// </remarks>
module SR =

    /// <summary>
    /// SR Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 SR FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("SR")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("S1", DsDataType.TBool)     // Set input
        builder.AddInput("R", DsDataType.TBool)      // Reset input
        builder.AddOutput("Q1", DsDataType.TBool)    // Output

        // Static 변수: 상태 저장
        builder.AddStaticWithInit("STATE", DsDataType.TBool, box false)

        // 로직 (Set priority):
        // STATE := IF S1 THEN TRUE ELSIF R THEN FALSE ELSE STATE
        // Q1 := STATE

        let s1 = Terminal(DsTag.Bool("S1"))
        let r = Terminal(DsTag.Bool("R"))
        let state = Terminal(DsTag.Bool("STATE"))

        // STATE := IF S1 THEN TRUE ELSIF R THEN FALSE ELSE STATE (Set priority)
        let newState =
            Function("IF", [
                s1
                boolExpr true
                Function("IF", [
                    r
                    boolExpr false
                    state
                ])
            ])

        builder.AddStatement(assignAuto "STATE" DsDataType.TBool newState)
        builder.AddStatement(assignAuto "Q1" DsDataType.TBool state)

        builder.SetDescription("Set-Reset bistable with Set priority (IEC 61131-3)")
        builder.Build()
