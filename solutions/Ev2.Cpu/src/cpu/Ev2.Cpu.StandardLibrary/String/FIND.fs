namespace Ev2.Cpu.StandardLibrary.String

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// FIND - Find Substring Position (IEC 61131-3)
/// </summary>
/// <remarks>
/// 문자열에서 부분 문자열의 위치를 찾습니다.
/// OUT = IN에서 IN2가 처음 나타나는 위치 (0-based)
/// 찾지 못하면 -1 반환
///
/// 사용 예: 문자열 검색, 패턴 매칭
/// </remarks>
module FIND =

    /// <summary>
    /// FIND Function 생성
    /// </summary>
    /// <returns>FIND FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("FIND")

        // 입력
        builder.AddInput("IN", typeof<string>)      // 검색 대상 문자열
        builder.AddInput("IN2", typeof<string>)     // 찾을 부분 문자열

        // 출력
        builder.AddOutput("OUT", typeof<int>)       // 위치 (-1 = 찾지 못함)

        // 로직: OUT = FIND(IN, IN2)
        let inStr = Terminal(DsTag.String("IN"))
        let searchStr = Terminal(DsTag.String("IN2"))

        let result = Function("FIND", [inStr; searchStr])

        builder.SetBody(result)
        builder.SetDescription("Find position of substring IN2 in IN (-1 if not found)")

        builder.Build()
