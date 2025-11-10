namespace Ev2.Cpu.StandardLibrary.EdgeDetection

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// R_TRIG - Rising Edge Detection (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// 입력 신호의 상승 에지(FALSE → TRUE)를 감지합니다.
/// Q 출력은 에지가 감지된 스캔 사이클 동안만 TRUE가 됩니다.
/// </remarks>
module R_TRIG =

    /// <summary>
    /// R_TRIG Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 R_TRIG FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("R_TRIG")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("CLK", typeof<bool>)
        builder.AddOutput("Q", typeof<bool>)

        // Static 변수: 이전 상태 저장
        builder.AddStaticWithInit("M", typeof<bool>, box false)

        // 로직:
        // Q := CLK AND NOT M
        // M := CLK

        let clk = Terminal(DsTag.Bool("CLK"))
        let m = Terminal(DsTag.Bool("M"))

        // Q := CLK AND NOT M (상승 에지 감지)
        let risingEdge = and' clk (not' m)
        builder.AddStatement(assignAuto "Q" typeof<bool> risingEdge)

        // M := CLK (현재 상태 저장)
        builder.AddStatement(assignAuto "M" typeof<bool> clk)

        builder.SetDescription("Rising edge detection - detects FALSE to TRUE transition")
        builder.Build()
