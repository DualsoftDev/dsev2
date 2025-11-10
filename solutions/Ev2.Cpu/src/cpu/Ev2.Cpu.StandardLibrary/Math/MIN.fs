namespace Ev2.Cpu.StandardLibrary.Math

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// MIN - Minimum Value Selection
/// </summary>
/// <remarks>
/// 여러 입력 값 중 최소값을 반환합니다.
///
/// 사용 예: 안전 제한값 선택, 최소 센서 값 선택
/// </remarks>
module MIN =

    /// <summary>
    /// MIN Function 생성 (4개 입력)
    /// </summary>
    /// <returns>MIN FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("MIN")

        // 입력 (4개 값)
        builder.AddInput("IN1", typeof<double>)
        builder.AddInput("IN2", typeof<double>)
        builder.AddInput("IN3", typeof<double>)
        builder.AddInput("IN4", typeof<double>)

        // 출력
        builder.AddOutput("OUT", typeof<double>)

        // 로직: MIN(MIN(MIN(IN1, IN2), IN3), IN4)
        let in1 = Terminal(DsTag.Double("IN1"))
        let in2 = Terminal(DsTag.Double("IN2"))
        let in3 = Terminal(DsTag.Double("IN3"))
        let in4 = Terminal(DsTag.Double("IN4"))

        // IF(IN1 < IN2, IN1, IN2)
        let min12 = Function("IF", [lt in1 in2; in1; in2])
        let min123 = Function("IF", [lt min12 in3; min12; in3])
        let min1234 = Function("IF", [lt min123 in4; min123; in4])

        builder.SetBody(min1234)
        builder.SetDescription("Return minimum of 4 input values")

        builder.Build()
