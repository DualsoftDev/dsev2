namespace Ev2.Cpu.Generation.Examples

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Make

/// UserFB 사용 예제 모음
module UserFBExamples =

    // ═════════════════════════════════════════════════════════════════════
    // 예제 1: 간단한 FC - 피타고라스 정리
    // ═════════════════════════════════════════════════════════════════════

    let example1_Pythagoras() =
        let builder = FCBuilder("Pythagoras")
        builder.AddInput("a", typeof<double>)
        builder.AddInput("b", typeof<double>)
        builder.AddOutput("c", typeof<double>)

        // c = sqrt(a^2 + b^2)
        let a = Terminal(DsTag.Double("a"))
        let b = Terminal(DsTag.Double("b"))
        let aSquared = mul a a
        let bSquared = mul b b
        let sum = add aSquared bSquared
        let c = sqrt' sum

        builder.SetBody(c)
        builder.SetDescription("피타고라스 정리: c = sqrt(a² + b²)")

        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 2: FC - 평균 계산 (3개 값)
    // ═════════════════════════════════════════════════════════════════════

    let example2_Average3() =
        let builder = FCBuilder("Average3")
        builder.AddInput("value1", typeof<double>)
        builder.AddInput("value2", typeof<double>)
        builder.AddInput("value3", typeof<double>)
        builder.AddOutput("average", typeof<double>)

        let v1 = Terminal(DsTag.Double("value1"))
        let v2 = Terminal(DsTag.Double("value2"))
        let v3 = Terminal(DsTag.Double("value3"))

        // avg = (v1 + v2 + v3) / 3
        let sum = add (add v1 v2) v3
        let avg = div sum (doubleExpr 3.0)

        builder.SetBody(avg)
        builder.SetDescription("3개 값의 평균 계산")

        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 3: FC - 범위 체크
    // ═════════════════════════════════════════════════════════════════════

    let example3_InRange() =
        let builder = FCBuilder("InRange")
        builder.AddInput("value", typeof<double>)
        builder.AddInput("min", typeof<double>)
        builder.AddInput("max", typeof<double>)
        builder.AddOutput("result", typeof<bool>)

        let value = Terminal(DsTag.Double("value"))
        let min = Terminal(DsTag.Double("min"))
        let max = Terminal(DsTag.Double("max"))

        // result = (value >= min) AND (value <= max)
        let inRange = and' (ge value min) (le value max)

        builder.SetBody(inRange)
        builder.SetDescription("값이 범위 내에 있는지 체크")

        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 4: FB - 카운터 (CTU와 유사)
    // ═════════════════════════════════════════════════════════════════════

    let example4_Counter() =
        let builder = FBBuilder("MyCounter")

        builder.AddInput("countUp", typeof<bool>)
        builder.AddInput("reset", typeof<bool>)
        builder.AddInput("preset", typeof<int>)

        builder.AddOutput("count", typeof<int>)
        builder.AddOutput("done", typeof<bool>)

        builder.AddStaticWithInit("currentCount", typeof<int>, box 0)

        // 리셋
        let reset = Terminal(DsTag.Bool("reset"))
        builder.AddStatement(when' reset (mov (intExpr 0) (DsTag.Int("currentCount"))))

        // 카운트 업 (상승 에지)
        let countUp = rising (Terminal(DsTag.Bool("countUp")))
        let currentCount = Terminal(DsTag.Int("currentCount"))
        let preset = Terminal(DsTag.Int("preset"))

        let incrementStmt =
            when'
                (and' countUp (lt currentCount preset))
                (mov (add currentCount (intExpr 1)) (DsTag.Int("currentCount")))
        builder.AddStatement(incrementStmt)

        // 출력 설정
        builder.AddStatement(assignAuto "count" typeof<int> currentCount)
        builder.AddStatement(assignAuto "done" typeof<bool> (ge currentCount preset))

        builder.SetDescription("상향 카운터 (리셋 기능 포함)")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 5: FB - 점멸 타이머 (Blink)
    // ═════════════════════════════════════════════════════════════════════

    let example5_Blink() =
        let builder = FBBuilder("Blink")

        builder.AddInput("enable", typeof<bool>)
        builder.AddInput("onTime", typeof<int>)     // ms
        builder.AddInput("offTime", typeof<int>)    // ms

        builder.AddOutput("output", typeof<bool>)

        builder.AddStaticWithInit("state", typeof<bool>, box false)
        builder.AddStaticWithInit("timer", typeof<int>, box 0)

        let enable = Terminal(DsTag.Bool("enable"))
        let state = Terminal(DsTag.Bool("state"))
        let timer = Terminal(DsTag.Int("timer"))
        let onTime = Terminal(DsTag.Int("onTime"))
        let offTime = Terminal(DsTag.Int("offTime"))

        // Enable이 꺼지면 리셋
        builder.AddStatement(when' (not' enable) (mov (boolExpr false) (DsTag.Bool("state"))))
        builder.AddStatement(when' (not' enable) (mov (intExpr 0) (DsTag.Int("timer"))))

        // 타이머 증가 (Enable일 때)
        let incrementTimer =
            when'
                enable
                (mov (add timer (intExpr 100)) (DsTag.Int("timer")))  // 100ms씩 증가
        builder.AddStatement(incrementTimer)

        // ON 상태에서 onTime 경과 시 OFF로 전환
        let switchToOff =
            whenAt 10
                (and' (and' enable state) (ge timer onTime))
                (mov (boolExpr false) (DsTag.Bool("state")))
        builder.AddStatement(switchToOff)

        builder.AddStatement(
            whenAt 11
                (and' (and' enable state) (ge timer onTime))
                (mov (intExpr 0) (DsTag.Int("timer"))))

        // OFF 상태에서 offTime 경과 시 ON으로 전환
        let switchToOn =
            whenAt 20
                (and' (and' enable (not' state)) (ge timer offTime))
                (mov (boolExpr true) (DsTag.Bool("state")))
        builder.AddStatement(switchToOn)

        builder.AddStatement(
            whenAt 21
                (and' (and' enable (not' state)) (ge timer offTime))
                (mov (intExpr 0) (DsTag.Int("timer"))))

        // 출력
        builder.AddStatement(assignAuto "output" typeof<bool> state)

        builder.SetDescription("점멸 타이머 (ON/OFF 시간 설정 가능)")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 6: FB - PID 제어기 (간단 버전)
    // ═════════════════════════════════════════════════════════════════════

    let example6_SimplePID() =
        let builder = FBBuilder("SimplePID")

        // 입력
        builder.AddInput("enable", typeof<bool>)
        builder.AddInput("setpoint", typeof<double>)      // 목표값
        builder.AddInput("processValue", typeof<double>)  // 현재값
        builder.AddInput("kp", typeof<double>)            // 비례 게인
        builder.AddInput("ki", typeof<double>)            // 적분 게인
        builder.AddInput("kd", typeof<double>)            // 미분 게인

        // 출력
        builder.AddOutput("output", typeof<double>)       // 제어 출력

        // 내부 상태
        builder.AddStaticWithInit("integral", typeof<double>, box 0.0)
        builder.AddStaticWithInit("lastError", typeof<double>, box 0.0)

        let enable = Terminal(DsTag.Bool("enable"))
        let sp = Terminal(DsTag.Double("setpoint"))
        let pv = Terminal(DsTag.Double("processValue"))
        let kp = Terminal(DsTag.Double("kp"))
        let ki = Terminal(DsTag.Double("ki"))
        let kd = Terminal(DsTag.Double("kd"))

        // 임시 변수
        builder.AddTemp("error", typeof<double>)
        builder.AddTemp("pTerm", typeof<double>)
        builder.AddTemp("iTerm", typeof<double>)
        builder.AddTemp("dTerm", typeof<double>)
        builder.AddTemp("result", typeof<double>)

        // 오차 계산
        let error = sub sp pv
        builder.AddStatement(assignAuto "error" typeof<double> error)

        // P항
        let pTerm = mul (Terminal(DsTag.Double("error"))) kp
        builder.AddStatement(assignAuto "pTerm" typeof<double> pTerm)

        // I항 (적분)
        let integral = Terminal(DsTag.Double("integral"))
        let newIntegral = add integral (Terminal(DsTag.Double("error")))
        builder.AddStatement(when' enable (mov newIntegral (DsTag.Double("integral"))))

        let iTerm = mul integral ki
        builder.AddStatement(assignAuto "iTerm" typeof<double> iTerm)

        // D항 (미분)
        let lastError = Terminal(DsTag.Double("lastError"))
        let derivative = sub (Terminal(DsTag.Double("error"))) lastError
        let dTerm = mul derivative kd
        builder.AddStatement(assignAuto "dTerm" typeof<double> dTerm)

        // 이전 오차 저장
        builder.AddStatement(when' enable (mov (Terminal(DsTag.Double("error"))) (DsTag.Double("lastError"))))

        // 결과 = P + I + D
        let result = add (add (Terminal(DsTag.Double("pTerm"))) (Terminal(DsTag.Double("iTerm"))))
                         (Terminal(DsTag.Double("dTerm")))
        builder.AddStatement(assignAuto "result" typeof<double> result)

        // 출력
        let finalOutput = Function("IF", [enable; Terminal(DsTag.Double("result")); doubleExpr 0.0])
        builder.AddStatement(assignAuto "output" typeof<double> finalOutput)

        builder.SetDescription("간단한 PID 제어기")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 7: FB - 컨베이어 제어
    // ═════════════════════════════════════════════════════════════════════

    let example7_ConveyorControl() =
        let builder = FBBuilder("ConveyorControl")

        // 입력
        builder.AddInput("startButton", typeof<bool>)
        builder.AddInput("stopButton", typeof<bool>)
        builder.AddInput("emergency", typeof<bool>)
        builder.AddInput("sensorStart", typeof<bool>)      // 시작 센서
        builder.AddInput("sensorEnd", typeof<bool>)        // 끝 센서
        builder.AddInput("overload", typeof<bool>)         // 과부하

        // 출력
        builder.AddOutput("motorRunning", typeof<bool>)
        builder.AddOutput("alarmLight", typeof<bool>)
        builder.AddOutput("productCount", typeof<int>)

        // 내부 상태
        builder.AddStaticWithInit("running", typeof<bool>, box false)
        builder.AddStaticWithInit("alarm", typeof<bool>, box false)
        builder.AddStaticWithInit("count", typeof<int>, box 0)

        let start = Terminal(DsTag.Bool("startButton"))
        let stop = Terminal(DsTag.Bool("stopButton"))
        let emergency = Terminal(DsTag.Bool("emergency"))
        let overload = Terminal(DsTag.Bool("overload"))

        // 알람 조건
        let alarmCondition = or' emergency overload
        let alarmRelay = Relay.Create(
            DsTag.Bool("alarm"),
            alarmCondition,
            not' alarmCondition
        )
        builder.AddRelay(alarmRelay)

        // 모터 제어 (알람 시 정지)
        let alarm = Terminal(DsTag.Bool("alarm"))
        let motorRelay = Relay.CreateFull(
            DsTag.Bool("running"),
            and' start (not' alarm),
            or' stop alarm,
            RelayMode.SR,
            RelayPriority.ResetFirst,
            false
        )
        builder.AddRelay(motorRelay)

        // 제품 카운터 (끝 센서 상승 에지)
        let sensorEnd = rising (Terminal(DsTag.Bool("sensorEnd")))
        let count = Terminal(DsTag.Int("count"))
        let incrementCount =
            when'
                (and' (Terminal(DsTag.Bool("running"))) sensorEnd)
                (mov (add count (intExpr 1)) (DsTag.Int("count")))
        builder.AddStatement(incrementCount)

        // 출력 설정
        builder.AddStatement(assignAuto "motorRunning" typeof<bool> (Terminal(DsTag.Bool("running"))))
        builder.AddStatement(assignAuto "alarmLight" typeof<bool> alarm)
        builder.AddStatement(assignAuto "productCount" typeof<int> count)

        builder.SetDescription("컨베이어 제어 (시작/정지/카운터)")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 8: 사용 예제 - FB 인스턴스 생성 및 호출
    // ═════════════════════════════════════════════════════════════════════

    let example8_UsageDemo() =
        // FC 사용 예제
        let pythagoras = example1_Pythagoras()
        let resultExpr =
            match callFC pythagoras [doubleExpr 3.0; doubleExpr 4.0] with
            | Ok expr -> expr
            | Error msg -> failwithf "callFC failed: %s" msg  // sqrt(3² + 4²) = 5.0

        // FB 사용 예제
        let motorFB = createMotorControlFB()
        let motorInstance = createFBInstance "Motor1" motorFB

        // 레지스트리에 등록
        let registry = UserFBRegistry()
        registry.RegisterFC(pythagoras)
        registry.RegisterFB(motorFB)
        registry.RegisterInstance(motorInstance)

        // 프로그램에서 사용
        let program = ProgramGen.ProgramBuilder("MainProgram")

        // FC 결과를 변수에 저장
        program.AddLocal("hypotenuse", typeof<double>)
        program.AddStatement(assignAuto "hypotenuse" typeof<double> resultExpr)

        // FB 인스턴스 호출
        let motorCall =
            callFB motorInstance (
                Map.ofList [
                    "start", boolVar "StartButton"
                    "stop", boolVar "StopButton"
                    "emergency", boolVar "EmergencyStop"
                    "overload", boolVar "Overload"
                ])

        match motorCall with
        | Ok stmt -> program.AddStatement(stmt)
        | Error msg -> failwithf "callFB failed: %s" msg

        program.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 예제 9: FB - 밸브 제어 (타이머 포함)
    // ═════════════════════════════════════════════════════════════════════

    let example9_ValveControl() =
        let builder = FBBuilder("ValveControl")

        // 입력
        builder.AddInput("open", typeof<bool>)
        builder.AddInput("close", typeof<bool>)
        builder.AddInput("openTime", typeof<int>)          // ms
        builder.AddInput("closeTime", typeof<int>)         // ms

        // 출력
        builder.AddOutput("valveOpen", typeof<bool>)
        builder.AddOutput("valveClose", typeof<bool>)
        builder.AddOutput("isOpening", typeof<bool>)
        builder.AddOutput("isClosing", typeof<bool>)
        builder.AddOutput("fullyOpen", typeof<bool>)
        builder.AddOutput("fullyClosed", typeof<bool>)

        // 내부 상태
        builder.AddStaticWithInit("state", typeof<int>, box 0)  // 0:closed, 1:opening, 2:open, 3:closing

        let open' = Terminal(DsTag.Bool("open"))
        let close = Terminal(DsTag.Bool("close"))
        let openTime = Terminal(DsTag.Int("openTime"))
        let closeTime = Terminal(DsTag.Int("closeTime"))
        let state = Terminal(DsTag.Int("state"))

        // State 0 (Closed) -> 1 (Opening)
        builder.AddStatement(whenAt 10
            (and' (eq state (intExpr 0)) open')
            (mov (intExpr 1) (DsTag.Int("state"))))

        // State 1 (Opening) -> 2 (Fully Open) after openTime
        let openTimer = ton (eq state (intExpr 1)) openTime
        builder.AddStatement(whenAt 20
            openTimer
            (mov (intExpr 2) (DsTag.Int("state"))))

        // State 2 (Open) -> 3 (Closing)
        builder.AddStatement(whenAt 30
            (and' (eq state (intExpr 2)) close)
            (mov (intExpr 3) (DsTag.Int("state"))))

        // State 3 (Closing) -> 0 (Fully Closed) after closeTime
        let closeTimer = ton (eq state (intExpr 3)) closeTime
        builder.AddStatement(whenAt 40
            closeTimer
            (mov (intExpr 0) (DsTag.Int("state"))))

        // 출력 설정
        builder.AddStatement(assignAuto "valveOpen" typeof<bool>
            (or' (eq state (intExpr 1)) (eq state (intExpr 2))))

        builder.AddStatement(assignAuto "valveClose" typeof<bool>
            (or' (eq state (intExpr 0)) (eq state (intExpr 3))))

        builder.AddStatement(assignAuto "isOpening" typeof<bool> (eq state (intExpr 1)))
        builder.AddStatement(assignAuto "isClosing" typeof<bool> (eq state (intExpr 3)))
        builder.AddStatement(assignAuto "fullyOpen" typeof<bool> (eq state (intExpr 2)))
        builder.AddStatement(assignAuto "fullyClosed" typeof<bool> (eq state (intExpr 0)))

        builder.SetDescription("밸브 제어 (개폐 시간 포함)")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // 모든 예제 실행
    // ═════════════════════════════════════════════════════════════════════

    let getAllExamples() =
        [
            "Pythagoras", example1_Pythagoras() :> obj
            "Average3", example2_Average3() :> obj
            "InRange", example3_InRange() :> obj
            "Counter", example4_Counter() :> obj
            "Blink", example5_Blink() :> obj
            "SimplePID", example6_SimplePID() :> obj
            "ConveyorControl", example7_ConveyorControl() :> obj
            "ValveControl", example9_ValveControl() :> obj
        ]
