namespace Ev2.Cpu.StandardLibrary.EdgeDetection

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// F_TRIG - Falling Edge Detection (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// 입력 신호의 하강 에지(TRUE → FALSE)를 감지합니다.
/// Q 출력은 에지가 감지된 스캔 사이클 동안만 TRUE가 됩니다.
/// </remarks>
module F_TRIG =

    /// <summary>
    /// F_TRIG Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 F_TRIG FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("F_TRIG")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("CLK", typeof<bool>)
        builder.AddOutput("Q", typeof<bool>)

        // Static 변수: 이전 상태 저장
        builder.AddStaticWithInit("M", typeof<bool>, box false)

        // 로직:
        // Q := NOT CLK AND M
        // M := CLK

        let clk = Terminal(DsTag.Bool("CLK"))
        let m = Terminal(DsTag.Bool("M"))

        // Q := NOT CLK AND M (하강 에지 감지)
        let fallingEdge = and' (not' clk) m
        builder.AddStatement(assignAuto "Q" typeof<bool> fallingEdge)

        // M := CLK (현재 상태 저장)
        builder.AddStatement(assignAuto "M" typeof<bool> clk)

        builder.SetDescription("Falling edge detection - detects TRUE to FALSE transition")
        builder.Build()
