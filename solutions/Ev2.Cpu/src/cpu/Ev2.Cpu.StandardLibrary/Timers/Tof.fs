namespace Ev2.Cpu.StandardLibrary.Timers

open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen

/// <summary>
/// TOF - Off-Delay Timer (IEC 61131-3 Standard)
/// </summary>
/// <remarks>
/// 입력이 FALSE로 전환된 후 지정된 시간(PT)이 경과하면 출력이 FALSE가 됩니다.
/// - IN이 TRUE → Q = TRUE (즉시), ET = 0
/// - IN이 FALSE → ET 증가 시작, Q는 여전히 TRUE
/// - ET >= PT → Q = FALSE
///
/// 시간 단위: 밀리초(ms)
/// </remarks>
module TOF =

    /// <summary>
    /// TOF Function Block 생성
    /// </summary>
    /// <returns>IEC 61131-3 표준 TOF FB</returns>
    let create() : Result<UserFB, string> =
        let builder = FBBuilder("TOF")

        // IEC 61131-3 표준 시그니처
        builder.AddInput("IN", DsDataType.TBool)
        builder.AddInput("PT", DsDataType.TInt)       // Preset time (ms)
        builder.AddOutput("Q", DsDataType.TBool)
        builder.AddOutput("ET", DsDataType.TInt)      // Elapsed time (ms)

        // Static 변수
        builder.AddStaticWithInit("Running", DsDataType.TBool, box false)
        builder.AddStaticWithInit("StartTime", DsDataType.TDouble, box 0.0)
        builder.AddStaticWithInit("ElapsedTime", DsDataType.TDouble, box 0.0)
        builder.AddStaticWithInit("OutputState", DsDataType.TBool, box false)

        // Temp 변수
        builder.AddTemp("CurrentTime", DsDataType.TDouble)

        let inSig = Terminal(DsTag.Bool("IN"))
        let pt = Terminal(DsTag.Int("PT"))
        let running = Terminal(DsTag.Bool("Running"))
        let startTime = Terminal(DsTag.Double("StartTime"))
        let elapsed = Terminal(DsTag.Double("ElapsedTime"))
        let outputState = Terminal(DsTag.Bool("OutputState"))

        // IN의 하강 에지 → 타이머 시작
        let fallingEdge = and' (and' (not' inSig) (not' running)) outputState

        // Running 중이면 경과 시간 계산
        // CRITICAL FIX: NOW() returns int64, use TODOUBLE (not DOUBLE) to convert
        let currentTime = Function("TODOUBLE", [Function("NOW", [])])
        builder.AddStatement(
            assignAuto "CurrentTime" DsDataType.TDouble currentTime
        )

        let timeoutCondition = and' running (ge elapsed pt)

        // OutputState := IF IN THEN TRUE ELSIF (Running AND ET >= PT) THEN FALSE ELSE OutputState
        let newOutputState = Function("IF", [
            inSig
            boolExpr true
            Function("IF", [
                timeoutCondition
                boolExpr false
                outputState
            ])
        ])
        builder.AddStatement(assignAuto "OutputState" DsDataType.TBool newOutputState)

        // Running := IF IN THEN FALSE ELSIF fallingEdge THEN TRUE ELSIF (Running AND ET >= PT) THEN FALSE ELSE Running
        let newRunning = Function("IF", [
            inSig
            boolExpr false
            Function("IF", [
                fallingEdge
                boolExpr true
                Function("IF", [
                    timeoutCondition
                    boolExpr false
                    running
                ])
            ])
        ])
        builder.AddStatement(assignAuto "Running" DsDataType.TBool newRunning)

        // StartTime := IF fallingEdge THEN NOW() ELSE StartTime
        let newStartTime = Function("IF", [
            fallingEdge
            currentTime
            startTime
        ])
        builder.AddStatement(assignAuto "StartTime" DsDataType.TDouble newStartTime)

        // Calculate elapsed time
        let timeCalc = sub (Terminal(DsTag.Double("CurrentTime"))) startTime
        let limitedTime = Function("IF", [gt timeCalc pt; pt; timeCalc])

        // ElapsedTime := IF IN THEN 0 ELSIF Running THEN MIN(CurrentTime - StartTime, PT) ELSE ElapsedTime
        let newElapsed = Function("IF", [
            inSig
            intExpr 0
            Function("IF", [
                running
                limitedTime
                elapsed
            ])
        ])
        builder.AddStatement(assignAuto "ElapsedTime" DsDataType.TDouble newElapsed)

        // 출력 설정
        builder.AddStatement(assignAuto "Q" DsDataType.TBool outputState)
        // ET는 TInt 출력이므로 elapsed(TDouble)를 변환
        builder.AddStatement(assignAuto "ET" DsDataType.TInt (Function("TOINT", [elapsed])))

        builder.SetDescription("Off-Delay Timer - Output turns OFF after preset time (IEC 61131-3)")
        builder.Build()
