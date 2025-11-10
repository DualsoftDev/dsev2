namespace Ev2.Cpu.StandardLibrary.String

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// MID - Extract Middle Substring (IEC 61131-3)
/// </summary>
/// <remarks>
/// 문자열의 중간에서 지정된 위치와 길이만큼 추출합니다.
/// OUT = IN[P .. P+L-1]
///
/// 사용 예: 부분 문자열 추출, 데이터 파싱
/// </remarks>
module MID =

    /// <summary>
    /// MID Function 생성
    /// </summary>
    /// <returns>MID FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("MID")

        // 입력
        builder.AddInput("IN", typeof<string>)      // 입력 문자열
        builder.AddInput("L", typeof<int>)          // 추출 길이
        builder.AddInput("P", typeof<int>)          // 시작 위치 (0-based)

        // 출력
        builder.AddOutput("OUT", typeof<string>)

        // 로직: OUT = MID(IN, P, L)
        let inStr = Terminal(DsTag.String("IN"))
        let len = Terminal(DsTag.Int("L"))
        let pos = Terminal(DsTag.Int("P"))

        let result = Function("MID", [inStr; pos; len])

        builder.SetBody(result)
        builder.SetDescription("Extract middle substring from position P with length L")

        builder.Build()
