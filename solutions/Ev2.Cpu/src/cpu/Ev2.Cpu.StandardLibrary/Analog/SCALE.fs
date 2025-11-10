namespace Ev2.Cpu.StandardLibrary.Analog

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// SCALE - Linear Scaling (IEC 61131-3)
/// </summary>
/// <remarks>
/// 입력 범위(IN_MIN ~ IN_MAX)를 출력 범위(OUT_MIN ~ OUT_MAX)로 선형 변환합니다.
/// 공식: OUT = OUT_MIN + (IN - IN_MIN) * (OUT_MAX - OUT_MIN) / (IN_MAX - IN_MIN)
///
/// 사용 예: 센서 값(0-1023) → 온도(0-100°C)
/// </remarks>
module SCALE =

    /// <summary>
    /// SCALE Function 생성 (상태 없음)
    /// </summary>
    /// <returns>SCALE FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("SCALE")

        // 입력
        builder.AddInput("IN", typeof<double>)          // 입력 값
        builder.AddInput("IN_MIN", typeof<double>)      // 입력 최소값
        builder.AddInput("IN_MAX", typeof<double>)      // 입력 최대값
        builder.AddInput("OUT_MIN", typeof<double>)     // 출력 최소값
        builder.AddInput("OUT_MAX", typeof<double>)     // 출력 최대값

        // 출력
        builder.AddOutput("OUT", typeof<double>)        // 스케일된 출력

        // 로직:
        // range_in = IN_MAX - IN_MIN
        // range_out = OUT_MAX - OUT_MIN
        // OUT = OUT_MIN + (IN - IN_MIN) * range_out / range_in

        let inVal = Terminal(DsTag.Double("IN"))
        let inMin = Terminal(DsTag.Double("IN_MIN"))
        let inMax = Terminal(DsTag.Double("IN_MAX"))
        let outMin = Terminal(DsTag.Double("OUT_MIN"))
        let outMax = Terminal(DsTag.Double("OUT_MAX"))

        let rangeIn = sub inMax inMin
        let rangeOut = sub outMax outMin

        // Check for degenerate case: if rangeIn is zero, return OUT_MIN
        // This prevents divide-by-zero when IN_MIN = IN_MAX
        let isZeroRange = eq rangeIn (doubleExpr 0.0)
        let normalized = div (sub inVal inMin) rangeIn
        let scaled = add outMin (mul normalized rangeOut)

        let result = call "IF" [isZeroRange; outMin; scaled]

        builder.SetBody(result)
        builder.SetDescription("Linear scaling from input range to output range")

        builder.Build()
