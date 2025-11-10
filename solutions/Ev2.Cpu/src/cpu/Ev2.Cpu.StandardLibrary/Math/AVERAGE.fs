namespace Ev2.Cpu.StandardLibrary.Math

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen

/// <summary>
/// AVERAGE - Average Calculation
/// </summary>
/// <remarks>
/// 여러 입력 값의 평균을 계산합니다.
/// AVG = (IN1 + IN2 + IN3 + IN4) / 4
///
/// 사용 예: 센서 값 평균, 신호 평활화
/// </remarks>
module AVERAGE =

    /// <summary>
    /// AVERAGE Function 생성 (4개 입력)
    /// </summary>
    /// <returns>AVERAGE FC</returns>
    let create() : Result<UserFC, string> =
        let builder = FCBuilder("AVERAGE")

        // 입력 (4개 값)
        builder.AddInput("IN1", typeof<double>)
        builder.AddInput("IN2", typeof<double>)
        builder.AddInput("IN3", typeof<double>)
        builder.AddInput("IN4", typeof<double>)

        // 출력
        builder.AddOutput("OUT", typeof<double>)

        // 로직: (IN1 + IN2 + IN3 + IN4) / 4
        let in1 = Terminal(DsTag.Double("IN1"))
        let in2 = Terminal(DsTag.Double("IN2"))
        let in3 = Terminal(DsTag.Double("IN3"))
        let in4 = Terminal(DsTag.Double("IN4"))

        let sum = add (add (add in1 in2) in3) in4
        let avg = div sum (doubleExpr 4.0)

        builder.SetBody(avg)
        builder.SetDescription("Calculate average of 4 input values")

        builder.Build()
