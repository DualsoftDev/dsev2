namespace Ev2.Cpu.StandardLibrary.Timers

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// TP - Pulse Timer (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// 입력의 상승 에지에서 지정된 시간(PT) 동안 펄스를 생성합니다.
/// - IN 상승 에지 → Q = TRUE, 타이머 시작
/// - ET >= PT → Q = FALSE
/// - 펄스가 진행 중일 때 IN이 변해도 영향 없음 (펄스 완료까지 지속)
///
/// 시간 단위: 밀리초(ms)
/// </remarks>
module TP =

    /// <summary>
    /// TP Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 TP FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("TP")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("IN", typeof<bool>)
        builder.AddInput("PT", typeof<int>)       // Preset time (ms)
        builder.AddOutput("Q", typeof<bool>)
        builder.AddOutput("ET", typeof<int>)      // Elapsed time (ms)

        // Static 변수
        builder.AddStaticWithInit("Running", typeof<bool>, box false)
        builder.AddStaticWithInit("LastIN", typeof<bool>, box false)
        builder.AddStaticWithInit("StartTime", typeof<double>, box 0.0)
        builder.AddStaticWithInit("ElapsedTime", typeof<double>, box 0.0)

        // Temp 변수
        builder.AddTemp("CurrentTime", typeof<double>)
        builder.AddTemp("RisingEdge", typeof<bool>)

        let inSig = Terminal(DsTag.Bool("IN"))
        let pt = Terminal(DsTag.Int("PT"))
        let running = Terminal(DsTag.Bool("Running"))
        let lastIN = Terminal(DsTag.Bool("LastIN"))
        let startTime = Terminal(DsTag.Double("StartTime"))
        let elapsed = Terminal(DsTag.Double("ElapsedTime"))

        // 상승 에지 감지
        let risingEdge = and' inSig (not' lastIN)
        builder.AddStatement(
            assignAuto "RisingEdge" typeof<bool> risingEdge
        )

        // 상승 에지이고 실행 중이 아니면 → 펄스 시작
        let startCondition = and' (Terminal(DsTag.Bool("RisingEdge"))) (not' running)

        // Running 중이면 경과 시간 계산
        // CRITICAL FIX (DEFECT-020-5): NOW() returns Int64, wrap in TODOUBLE for typeof<double> slot
        // Previous code caused type mismatch exception at runtime (matching TON/TOF/TONR pattern)
        let currentTime = Function("TODOUBLE", [Function("NOW", [])])
        builder.AddStatement(
            assignAuto "CurrentTime" typeof<double> currentTime
        )

        let timeoutCondition = and' running (ge elapsed pt)

        // Running := IF startCondition THEN TRUE ELSIF (Running AND ET >= PT) THEN FALSE ELSE Running
        let newRunning = Function("IF", [
            startCondition
            boolExpr true
            Function("IF", [
                timeoutCondition
                boolExpr false
                running
            ])
        ])
        builder.AddStatement(assignAuto "Running" typeof<bool> newRunning)

        // StartTime := IF startCondition THEN NOW() ELSE StartTime
        let newStartTime = Function("IF", [
            startCondition
            currentTime
            startTime
        ])
        builder.AddStatement(assignAuto "StartTime" typeof<double> newStartTime)

        // Calculate elapsed time
        let timeCalc = sub (Terminal(DsTag.Double("CurrentTime"))) startTime
        let limitedTime = Function("IF", [gt timeCalc pt; pt; timeCalc])

        // ElapsedTime := IF startCondition THEN 0 ELSIF Running THEN MIN(CurrentTime - StartTime, PT) ELSE ElapsedTime
        let newElapsed = Function("IF", [
            startCondition
            intExpr 0
            Function("IF", [
                running
                limitedTime
                elapsed
            ])
        ])
        builder.AddStatement(assignAuto "ElapsedTime" typeof<double> newElapsed)

        // LastIN 업데이트 (다음 에지 감지를 위해)
        builder.AddStatement(
            assignAuto "LastIN" typeof<bool> inSig
        )

        // 출력 설정
        builder.AddStatement(assignAuto "Q" typeof<bool> running)
        // ET는 typeof<int> 출력이므로 elapsed(typeof<double>)를 변환
        builder.AddStatement(assignAuto "ET" typeof<int> (Function("TOINT", [elapsed])))

        builder.SetDescription("Pulse Timer - Generates pulse for preset time on rising edge (IEC 61131-3)")
        builder.Build()
