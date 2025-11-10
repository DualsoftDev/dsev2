namespace Ev2.Cpu.StandardLibrary.Analog

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// HYSTERESIS - Hysteresis Control (IEC 61131-3)
/// </summary>
/// <remarks>
/// 히스테리시스 제어를 제공합니다.
/// - IN < LOW → OUT = FALSE
/// - IN > HIGH → OUT = TRUE
/// - LOW <= IN <= HIGH → OUT 유지 (이전 상태)
///
/// 사용 예: 온도 제어, 레벨 제어 (떨림 방지)
/// </remarks>
module HYSTERESIS =

    /// <summary>
    /// HYSTERESIS Function Block 생성
    /// </summary>
    /// <returns>HYSTERESIS FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("HYSTERESIS")

        // 입력
        builder.AddInput("IN", typeof<double>)      // 입력 값
        builder.AddInput("HIGH", typeof<double>)    // 상한 (ON 임계값)
        builder.AddInput("LOW", typeof<double>)     // 하한 (OFF 임계값)

        // 출력
        builder.AddOutput("OUT", typeof<bool>)      // 출력 상태

        // Static 변수
        builder.AddStaticWithInit("State", typeof<bool>, box false)

        let inVal = Terminal(DsTag.Double("IN"))
        let high = Terminal(DsTag.Double("HIGH"))
        let low = Terminal(DsTag.Double("LOW"))
        let state = Terminal(DsTag.Bool("State"))

        // State := IF (IN > HIGH) THEN TRUE ELSIF (IN < LOW) THEN FALSE ELSE State
        let newState = Function("IF", [
            gt inVal high
            boolExpr true
            Function("IF", [
                lt inVal low
                boolExpr false
                state
            ])
        ])
        builder.AddStatement(assignAuto "State" typeof<bool> newState)

        // 출력
        builder.AddStatement(assignAuto "OUT" typeof<bool> state)

        builder.SetDescription("Hysteresis control with HIGH/LOW thresholds")
        builder.Build()
