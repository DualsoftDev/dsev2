namespace Ev2.Cpu.StandardLibrary.Timers

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// TON - On-Delay Timer (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// 입력이 TRUE로 전환된 후 지정된 시간(PT)이 경과하면 출력이 TRUE가 됩니다.
/// - IN이 TRUE → ET(Elapsed Time)가 증가 시작
/// - ET >= PT → Q = TRUE
/// - IN이 FALSE → ET = 0, Q = FALSE (즉시 리셋)
///
/// 시간 단위: 밀리초(ms)
/// </remarks>
module TON =

    /// <summary>
    /// TON Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 TON FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("TON")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("IN", typeof<bool>)      // Enable input
        builder.AddInput("PT", typeof<int>)       // Preset time (ms)
        builder.AddOutput("Q", typeof<bool>)      // Output (TRUE when ET >= PT)
        builder.AddOutput("ET", typeof<int>)      // Elapsed time (ms)

        // Static 변수 (NOW() returns Int64, use typeof<double> to avoid overflow)
        builder.AddStaticWithInit("Running", typeof<bool>, box false)
        builder.AddStaticWithInit("StartTime", typeof<double>, box 0.0)
        builder.AddStaticWithInit("ElapsedTime", typeof<double>, box 0.0)

        // Temp 변수
        builder.AddTemp("CurrentTime", typeof<double>)

        let inSig = Terminal(DsTag.Bool("IN"))
        let pt = Terminal(DsTag.Int("PT"))
        let running = Terminal(DsTag.Bool("Running"))
        let startTime = Terminal(DsTag.Double("StartTime"))
        let elapsed = Terminal(DsTag.Double("ElapsedTime"))

        // IN의 상승 에지 감지 → 타이머 시작
        let risingEdge = and' inSig (not' running)

        // Running 중이면 경과 시간 계산
        // CRITICAL FIX: NOW() returns int64, use TODOUBLE (not DOUBLE) to convert
        let currentTime = Function("TODOUBLE", [Function("NOW", [])])
        builder.AddStatement(
            assignAuto "CurrentTime" typeof<double> currentTime
        )

        // Running := IF risingEdge THEN TRUE ELSIF NOT IN THEN FALSE ELSE Running
        let newRunning = Function("IF", [
            risingEdge
            boolExpr true
            Function("IF", [
                not' inSig
                boolExpr false
                running
            ])
        ])
        builder.AddStatement(assignAuto "Running" typeof<bool> newRunning)

        // CRITICAL FIX (DEFECT-CRIT-13): Move StartTime capture inside rising edge condition
        // Previous code: currentTime evaluated before IF, captures time even when risingEdge=false
        // Problem: Small timing drift accumulates over many scans (microsecond per scan)
        // Solution: Inline NOW() call within IF true branch for precise capture
        // StartTime := IF risingEdge THEN NOW() ELSE StartTime
        let newStartTime = Function("IF", [
            risingEdge
            Function("NOW", [])  // Capture NOW() precisely when edge detected
            startTime
        ])
        builder.AddStatement(assignAuto "StartTime" typeof<double> newStartTime)

        // Calculate elapsed time
        let timeCalc = sub (Terminal(DsTag.Double("CurrentTime"))) startTime
        let limitedTime = Function("IF", [gt timeCalc pt; pt; timeCalc])

        // ElapsedTime := IF NOT IN THEN 0 ELSIF Running THEN MIN(CurrentTime - StartTime, PT) ELSE ElapsedTime
        let newElapsed = Function("IF", [
            not' inSig
            intExpr 0
            Function("IF", [
                running
                limitedTime
                elapsed
            ])
        ])
        builder.AddStatement(assignAuto "ElapsedTime" typeof<double> newElapsed)

        // 출력 설정
        // Q := (Running AND ET >= PT)
        builder.AddStatement(
            assignAuto "Q" typeof<bool> (and' running (ge elapsed pt))
        )
        // ET는 typeof<int> 출력이므로 elapsed(typeof<double>)를 변환
        builder.AddStatement(
            assignAuto "ET" typeof<int> (Function("TOINT", [elapsed]))
        )

        builder.SetDescription("On-Delay Timer - Output turns ON after preset time (IEC 61131-3)")
        builder.Build()
