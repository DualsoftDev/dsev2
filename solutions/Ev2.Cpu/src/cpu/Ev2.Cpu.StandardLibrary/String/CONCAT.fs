namespace Ev2.Cpu.StandardLibrary.String

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// CONCAT - String Concatenation (IEC 61131-3)
/// </summary>
/// <remarks>
/// 두 문자열을 연결합니다.
/// OUT = IN1 + IN2
///
/// 사용 예: 메시지 생성, 로그 문자열 조합
/// </remarks>
module CONCAT =

    /// <summary>
    /// CONCAT Function 생성
    /// </summary>
    /// <returns>CONCAT FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("CONCAT")

        // 입력
        builder.AddInput("IN1", DsDataType.TString)
        builder.AddInput("IN2", DsDataType.TString)

        // 출력
        builder.AddOutput("OUT", DsDataType.TString)

        // 로직: OUT = CONCAT(IN1, IN2)
        let in1 = Terminal(DsTag.String("IN1"))
        let in2 = Terminal(DsTag.String("IN2"))

        let result = Function("CONCAT", [in1; in2])

        builder.SetBody(result)
        builder.SetDescription("Concatenate two strings")

        builder.Build()
