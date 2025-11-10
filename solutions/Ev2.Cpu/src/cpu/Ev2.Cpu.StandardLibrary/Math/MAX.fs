namespace Ev2.Cpu.StandardLibrary.Math

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// MAX - Maximum Value Selection
/// </summary>
/// <remarks>
/// 여러 입력 값 중 최대값을 반환합니다.
///
/// 사용 예: 최대 센서 값 선택, 피크 값 추적
/// </remarks>
module MAX =

    /// <summary>
    /// MAX Function 생성 (4개 입력)
    /// </summary>
    /// <returns>MAX FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("MAX")

        // 입력 (4개 값)
        builder.AddInput("IN1", DsDataType.TDouble)
        builder.AddInput("IN2", DsDataType.TDouble)
        builder.AddInput("IN3", DsDataType.TDouble)
        builder.AddInput("IN4", DsDataType.TDouble)

        // 출력
        builder.AddOutput("OUT", DsDataType.TDouble)

        // 로직: MAX(MAX(MAX(IN1, IN2), IN3), IN4)
        let in1 = Terminal(DsTag.Double("IN1"))
        let in2 = Terminal(DsTag.Double("IN2"))
        let in3 = Terminal(DsTag.Double("IN3"))
        let in4 = Terminal(DsTag.Double("IN4"))

        // IF(IN1 > IN2, IN1, IN2)
        let max12 = Function("IF", [gt in1 in2; in1; in2])
        let max123 = Function("IF", [gt max12 in3; max12; in3])
        let max1234 = Function("IF", [gt max123 in4; max123; in4])
        
        builder.SetBody(max1234)
        builder.SetDescription("Return maximum of 4 input values")

        builder.Build()
