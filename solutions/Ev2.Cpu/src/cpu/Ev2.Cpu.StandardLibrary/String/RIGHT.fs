namespace Ev2.Cpu.StandardLibrary.String

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// RIGHT - Extract Right Substring (IEC 61131-3)
/// </summary>
/// <remarks>
/// 문자열의 오른쪽에서 지정된 길이만큼 추출합니다.
/// OUT = IN[Length-L .. Length-1]
///
/// 사용 예: 접미사 추출, 확장자 추출
/// </remarks>
module RIGHT =

    /// <summary>
    /// RIGHT Function 생성
    /// </summary>
    /// <returns>RIGHT FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("RIGHT")

        // 입력
        builder.AddInput("IN", typeof<string>)      // 입력 문자열
        builder.AddInput("L", typeof<int>)          // 추출 길이

        // 출력
        builder.AddOutput("OUT", typeof<string>)

        // 로직: OUT = RIGHT(IN, L)
        let inStr = Terminal(DsTag.String("IN"))
        let len = Terminal(DsTag.Int("L"))

        let result = Function("RIGHT", [inStr; len])

        builder.SetBody(result)
        builder.SetDescription("Extract right substring of length L")

        builder.Build()
