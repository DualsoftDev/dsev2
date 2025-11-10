namespace Ev2.Cpu.StandardLibrary.String

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// LEFT - Extract Left Substring (IEC 61131-3)
/// </summary>
/// <remarks>
/// 문자열의 왼쪽에서 지정된 길이만큼 추출합니다.
/// OUT = IN[0..L-1]
///
/// 사용 예: 접두사 추출, 코드 파싱
/// </remarks>
module LEFT =

    /// <summary>
    /// LEFT Function 생성
    /// </summary>
    /// <returns>LEFT FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("LEFT")

        // 입력
        builder.AddInput("IN", typeof<string>)      // 입력 문자열
        builder.AddInput("L", typeof<int>)          // 추출 길이

        // 출력
        builder.AddOutput("OUT", typeof<string>)

        // 로직: OUT = LEFT(IN, L)
        let inStr = Terminal(DsTag.String("IN"))
        let len = Terminal(DsTag.Int("L"))

        let result = Function("LEFT", [inStr; len])

        builder.SetBody(result)
        builder.SetDescription("Extract left substring of length L")

        builder.Build()
