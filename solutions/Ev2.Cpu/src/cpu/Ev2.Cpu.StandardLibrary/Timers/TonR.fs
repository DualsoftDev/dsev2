namespace Ev2.Cpu.StandardLibrary.Timers

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// TONR - Retentive On-Delay Timer (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// TON과 유사하지만, IN이 FALSE가 되어도 ET(경과 시간)를 유지합니다.
/// - IN이 TRUE → ET 증가
/// - IN이 FALSE → ET 유지 (리셋하지 않음)
/// - ET >= PT → Q = TRUE
/// - R (Reset) 입력으로만 ET를 0으로 리셋 가능
///
/// 사용 예: 누적 운전 시간 측정
/// 시간 단위: 밀리초(ms)
/// </remarks>
module TONR =

    /// <summary>
    /// TONR Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 TONR FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("TONR")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("IN", DsDataType.TBool)
        builder.AddInput("R", DsDataType.TBool)       // Reset input
        builder.AddInput("PT", DsDataType.TInt)       // Preset time (ms)
        builder.AddOutput("Q", DsDataType.TBool)
        builder.AddOutput("ET", DsDataType.TInt)      // Elapsed time (ms)

        // Static 변수 (Retentive - 상태 유지)
        builder.AddStaticWithInit("Running", DsDataType.TBool, box false)
        builder.AddStaticWithInit("LastIN", DsDataType.TBool, box false)
        builder.AddStaticWithInit("StartTime", DsDataType.TDouble, box 0.0)
        builder.AddStaticWithInit("ElapsedTime", DsDataType.TDouble, box 0.0)  // 누적 시간 (유지됨)
        builder.AddStaticWithInit("LastStopTime", DsDataType.TDouble, box 0.0)

        // Temp 변수
        builder.AddTemp("CurrentTime", DsDataType.TDouble)

        let inSig = Terminal(DsTag.Bool("IN"))
        let reset = Terminal(DsTag.Bool("R"))
        let pt = Terminal(DsTag.Int("PT"))
        let running = Terminal(DsTag.Bool("Running"))
        let lastIN = Terminal(DsTag.Bool("LastIN"))
        let startTime = Terminal(DsTag.Double("StartTime"))
        let elapsed = Terminal(DsTag.Double("ElapsedTime"))
        let lastStopTime = Terminal(DsTag.Double("LastStopTime"))

        // IN의 상승 에지 → 타이머 시작/재개
        let risingEdge = and' inSig (not' lastIN)

        // IN의 하강 에지 → 타이머 일시 정지 (ET 유지)
        let fallingEdge = and' (not' inSig) lastIN

        // Running 중이면 경과 시간 누적
        // CRITICAL FIX: NOW() returns int64, use TODOUBLE (not DOUBLE) to convert
        let currentTime = Function("TODOUBLE", [Function("NOW", [])])
        builder.AddStatement(
            assignAuto "CurrentTime" DsDataType.TDouble currentTime
        )

        // Running := IF reset THEN FALSE ELSIF (risingEdge AND NOT reset) THEN TRUE ELSIF fallingEdge THEN FALSE ELSE Running
        let newRunning = Function("IF", [
            reset
            boolExpr false
            Function("IF", [
                and' risingEdge (not' reset)
                boolExpr true
                Function("IF", [
                    fallingEdge
                    boolExpr false
                    running
                ])
            ])
        ])
        builder.AddStatement(assignAuto "Running" DsDataType.TBool newRunning)

        // StartTime := IF reset THEN 0.0 ELSIF (risingEdge AND NOT reset) THEN NOW() ELSE StartTime
        let newStartTime = Function("IF", [
            reset
            doubleExpr 0.0
            Function("IF", [
                and' risingEdge (not' reset)
                currentTime
                startTime
            ])
        ])
        builder.AddStatement(assignAuto "StartTime" DsDataType.TDouble newStartTime)

        // LastStopTime := IF fallingEdge THEN NOW() ELSE LastStopTime
        let newLastStopTime = Function("IF", [
            fallingEdge
            currentTime
            lastStopTime
        ])
        builder.AddStatement(assignAuto "LastStopTime" DsDataType.TDouble newLastStopTime)

        // 실행 중일 때: 이전 누적 시간 + 현재 실행 시간
        let currentRunTime = sub (Terminal(DsTag.Double("CurrentTime"))) startTime
        let totalTime = add elapsed currentRunTime  // Accumulated + current run
        let limitedTime = Function("IF", [gt totalTime pt; pt; totalTime])

        // Falling edge: 이번 실행 구간의 시간을 누적
        let accumulatedOnStop = add elapsed (sub lastStopTime startTime)

        // ElapsedTime := IF reset THEN 0.0
        //                ELSIF fallingEdge THEN Elapsed + (LastStopTime - StartTime) [retain this run]
        //                ELSIF Running THEN Elapsed + (CurrentTime - StartTime) [show total]
        //                ELSE ElapsedTime [keep previous]
        let newElapsed = Function("IF", [
            reset
            doubleExpr 0.0
            Function("IF", [
                fallingEdge
                accumulatedOnStop  // Retain time from this run
                Function("IF", [
                    and' running (not' reset)
                    limitedTime  // Show accumulated + current
                    elapsed
                ])
            ])
        ])
        builder.AddStatement(assignAuto "ElapsedTime" DsDataType.TDouble newElapsed)

        // LastIN 업데이트
        builder.AddStatement(
            assignAuto "LastIN" DsDataType.TBool inSig
        )

        // 출력 설정
        // Q := (ET >= PT)
        builder.AddStatement(
            assignAuto "Q" DsDataType.TBool (ge elapsed pt)
        )
        // ET는 TInt 출력이므로 elapsed(TDouble)를 변환
        builder.AddStatement(
            assignAuto "ET" DsDataType.TInt (Function("TOINT", [elapsed]))
        )

        builder.SetDescription("Retentive On-Delay Timer - Accumulates time, reset only by R input (IEC 61131-3)")
        builder.Build()
