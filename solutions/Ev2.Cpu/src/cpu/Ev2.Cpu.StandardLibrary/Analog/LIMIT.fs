namespace Ev2.Cpu.StandardLibrary.Analog

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// LIMIT - Value Limiting (IEC 61131-3)
/// </summary>
/// <remarks>
/// 입력 값을 MIN ~ MAX 범위로 제한합니다.
/// - IN < MIN → OUT = MIN
/// - IN > MAX → OUT = MAX
/// - 그 외 → OUT = IN
///
/// 사용 예: 제어 신호 제한, 안전 범위 보장
/// </remarks>
module LIMIT =

    /// <summary>
    /// LIMIT Function 생성
    /// </summary>
    /// <returns>LIMIT FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("LIMIT")

        // 입력
        builder.AddInput("IN", DsDataType.TDouble)      // 입력 값
        builder.AddInput("MIN", DsDataType.TDouble)     // 최소값
        builder.AddInput("MAX", DsDataType.TDouble)     // 최대값

        // 출력
        builder.AddOutput("OUT", DsDataType.TDouble)    // 제한된 출력

        // 로직:
        // IF IN < MIN THEN OUT := MIN
        // ELSIF IN > MAX THEN OUT := MAX
        // ELSE OUT := IN

        let inVal = Terminal(DsTag.Double("IN"))
        let minVal = Terminal(DsTag.Double("MIN"))
        let maxVal = Terminal(DsTag.Double("MAX"))

        // 삼항 연산으로 표현: IF(IN < MIN, MIN, IF(IN > MAX, MAX, IN))
        let limited =
            Function("IF", [
                lt inVal minVal
                minVal
                Function("IF", [
                    gt inVal maxVal
                    maxVal
                    inVal
                ])
            ])

        builder.SetBody(limited)
        builder.SetDescription("Limit value to MIN-MAX range")

        builder.Build()
