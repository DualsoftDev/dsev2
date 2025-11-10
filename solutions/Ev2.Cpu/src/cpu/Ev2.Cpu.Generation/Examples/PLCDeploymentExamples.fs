namespace Ev2.Cpu.Generation.Examples

open System
open System.IO
open Ev2.Cpu.Core
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen
open Ev2.Cpu.Generation.Make.UserFBGen
open Ev2.Cpu.Generation.Codegen.PLCCodeGen

/// PLC 배포 예제
module PLCDeploymentExamples =

    // ═════════════════════════════════════════════════════════════════════
    // 예제 1: 단일 FC를 PLC 코드로 생성
    // ═════════════════════════════════════════════════════════════════════

    let example1_GenerateSingleFC() =
        printfn "=== 예제 1: 단일 FC 생성 ==="

        // FC 생성
        let tempFC = createCelsiusToFahrenheitFC()

        // PLC 코드 생성
        let plcCode = generateFC tempFC

        printfn "%s" plcCode
        plcCode

    // ═════════════════════════════════════════════════════════════════════
    // 예제 2: 단일 FB를 PLC 코드로 생성
    // ═════════════════════════════════════════════════════════════════════

    let example2_GenerateSingleFB() =
        printfn "=== 예제 2: 단일 FB 생성 ==="

        // FB 생성
        let motorFB = createMotorControlFB()

        // PLC 코드 생성
        let plcCode = generateFB motorFB

        printfn "%s" plcCode
        plcCode

    // ═════════════════════════════════════════════════════════════════════
    // 예제 3: 전체 프로젝트 생성
    // ═════════════════════════════════════════════════════════════════════

    let example3_GenerateFullProject() =
        printfn "=== 예제 3: 전체 프로젝트 생성 ==="

        // 레지스트리 생성
        let registry = UserFBRegistry()

        // FC 등록
        registry.RegisterFC(createCelsiusToFahrenheitFC())
        registry.RegisterFC(createLinearScaleFC())

        // FB 등록
        registry.RegisterFB(createHysteresisFB())
        registry.RegisterFB(createMotorControlFB())
        registry.RegisterFB(createSequence3StepFB())

        // 인스턴스 생성 및 등록
        let motor1 = createFBInstance "Motor1" (createMotorControlFB())
        let motor2 = createFBInstance "Motor2" (createMotorControlFB())
        registry.RegisterInstance(motor1)
        registry.RegisterInstance(motor2)

        // 프로젝트 코드 생성
        let projectCode = generatePLCProject registry "IndustrialAutomation"

        printfn "%s" projectCode
        projectCode

    // ═════════════════════════════════════════════════════════════════════
    // 예제 4: 파일로 저장
    // ═════════════════════════════════════════════════════════════════════

    let example4_SaveToFile() =
        printfn "=== 예제 4: 파일로 저장 ==="

        // 레지스트리 생성
        let registry = UserFBRegistry()

        // FB/FC 등록
        registry.RegisterFC(createCelsiusToFahrenheitFC())
        registry.RegisterFB(createMotorControlFB())

        // 출력 디렉토리
        let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "PLCOutput")
        if not (Directory.Exists(outputDir)) then
            Directory.CreateDirectory(outputDir) |> ignore

        // 프로젝트 저장
        savePLCProject outputDir "MyProject" registry

        printfn "파일 저장 완료: %s" outputDir

    // ═════════════════════════════════════════════════════════════════════
    // 예제 5: TwinCAT 프로젝트 파일 생성
    // ═════════════════════════════════════════════════════════════════════

    let example5_GenerateTwinCAT() =
        printfn "=== 예제 5: TwinCAT 프로젝트 파일 생성 ==="

        // 레지스트리 생성
        let registry = UserFBRegistry()

        // FB/FC 등록
        registry.RegisterFC(createCelsiusToFahrenheitFC())
        registry.RegisterFC(createLinearScaleFC())
        registry.RegisterFB(createHysteresisFB())
        registry.RegisterFB(createMotorControlFB())

        // 출력 디렉토리
        let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "TwinCATOutput")
        if not (Directory.Exists(outputDir)) then
            Directory.CreateDirectory(outputDir) |> ignore

        // TwinCAT 파일 저장
        saveTwinCATProject outputDir registry

        printfn "TwinCAT 파일 생성 완료: %s" outputDir

    // ═════════════════════════════════════════════════════════════════════
    // 예제 6: 실전 예제 - 완전한 온도 제어 시스템
    // ═════════════════════════════════════════════════════════════════════

    let example6_TemperatureControlSystem() =
        printfn "=== 예제 6: 온도 제어 시스템 ==="

        // 레지스트리
        let registry = UserFBRegistry()

        // 1. 온도 변환 FC
        let tempConverter = createCelsiusToFahrenheitFC()
        registry.RegisterFC(tempConverter)

        // 2. 히스테리시스 제어 FB
        let hysteresis = createHysteresisFB()
        registry.RegisterFB(hysteresis)

        // 3. 경보 FB 생성
        let alarmFB = FBBuilder("TemperatureAlarm")
        alarmFB.AddInput("temperature", DsDataType.TDouble)
        alarmFB.AddInput("highAlarm", DsDataType.TDouble)
        alarmFB.AddInput("lowAlarm", DsDataType.TDouble)
        alarmFB.AddOutput("highAlarmActive", DsDataType.TBool)
        alarmFB.AddOutput("lowAlarmActive", DsDataType.TBool)

        let temp = Terminal(DsTag.Double("temperature"))
        let high = Terminal(DsTag.Double("highAlarm"))
        let low = Terminal(DsTag.Double("lowAlarm"))

        alarmFB.AddStatement(assignAuto "highAlarmActive" DsDataType.TBool (gt temp high))
        alarmFB.AddStatement(assignAuto "lowAlarmActive" DsDataType.TBool (lt temp low))

        let alarmBlock = alarmFB.Build()
        registry.RegisterFB(alarmBlock)

        // 4. 인스턴스 생성
        let heaterCtrl = createFBInstance "HeaterControl" hysteresis
        let coolerCtrl = createFBInstance "CoolerControl" hysteresis
        let tempAlarm = createFBInstance "TempAlarm" alarmBlock

        registry.RegisterInstance(heaterCtrl)
        registry.RegisterInstance(coolerCtrl)
        registry.RegisterInstance(tempAlarm)

        // 5. 프로젝트 생성
        let projectCode = generatePLCProject registry "TemperatureControl"

        // 6. 파일 저장
        let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "TemperatureControlSystem")
        if not (Directory.Exists(outputDir)) then
            Directory.CreateDirectory(outputDir) |> ignore

        savePLCProject outputDir "TemperatureControl" registry
        saveTwinCATProject outputDir registry

        printfn "온도 제어 시스템 생성 완료!"
        printfn "출력 디렉토리: %s" outputDir
        printfn ""
        printfn "생성된 파일:"
        printfn "  - TemperatureControl.st (Structured Text)"
        printfn "  - *.TcPOU (TwinCAT 파일)"

        projectCode

    // ═════════════════════════════════════════════════════════════════════
    // 예제 7: 실전 예제 - 컨베이어 시스템
    // ═════════════════════════════════════════════════════════════════════

    let example7_ConveyorSystem() =
        printfn "=== 예제 7: 컨베이어 시스템 ==="

        let registry = UserFBRegistry()

        // 1. 모터 제어 FB
        let motorFB = createMotorControlFB()
        registry.RegisterFB(motorFB)

        // 2. 카운터 FB
        let counterFB = FBBuilder("ProductCounter")
        counterFB.AddInput("trigger", DsDataType.TBool)
        counterFB.AddInput("reset", DsDataType.TBool)
        counterFB.AddInput("preset", DsDataType.TInt)
        counterFB.AddOutput("count", DsDataType.TInt)
        counterFB.AddOutput("done", DsDataType.TBool)
        counterFB.AddStaticWithInit("currentCount", DsDataType.TInt, box 0)

        // 리셋
        counterFB.AddStatement(when' (Terminal(DsTag.Bool("reset")))
            (mov (intExpr 0) (DsTag.Int("currentCount"))))

        // 카운트 업
        let trigger = rising (Terminal(DsTag.Bool("trigger")))
        let count = Terminal(DsTag.Int("currentCount"))
        let preset = Terminal(DsTag.Int("preset"))

        counterFB.AddStatement(when' (and' trigger (lt count preset))
            (mov (add count (intExpr 1)) (DsTag.Int("currentCount"))))

        counterFB.AddStatement(assignAuto "count" DsDataType.TInt count)
        counterFB.AddStatement(assignAuto "done" DsDataType.TBool (ge count preset))

        let counter = counterFB.Build()
        registry.RegisterFB(counter)

        // 3. 인스턴스 생성
        let conveyorMotor = createFBInstance "ConveyorMotor" motorFB
        let productCounter = createFBInstance "ProductCounter" counter

        registry.RegisterInstance(conveyorMotor)
        registry.RegisterInstance(productCounter)

        // 4. 프로젝트 생성 및 저장
        let outputDir = Path.Combine(Directory.GetCurrentDirectory(), "ConveyorSystem")
        if not (Directory.Exists(outputDir)) then
            Directory.CreateDirectory(outputDir) |> ignore

        savePLCProject outputDir "ConveyorSystem" registry
        saveTwinCATProject outputDir registry

        printfn "컨베이어 시스템 생성 완료!"
        printfn "출력 디렉토리: %s" outputDir

    // ═════════════════════════════════════════════════════════════════════
    // 모든 예제 실행
    // ═════════════════════════════════════════════════════════════════════

    let runAllExamples() =
        printfn "\n=========================================="
        printfn "PLC 배포 예제 실행"
        printfn "==========================================\n"

        // 예제 1-3: 코드 생성만
        example1_GenerateSingleFC() |> ignore
        printfn ""

        example2_GenerateSingleFB() |> ignore
        printfn ""

        example3_GenerateFullProject() |> ignore
        printfn ""

        // 예제 4-7: 파일 저장
        printfn "파일 저장 예제 시작...\n"

        example4_SaveToFile()
        printfn ""

        example5_GenerateTwinCAT()
        printfn ""

        example6_TemperatureControlSystem() |> ignore
        printfn ""

        example7_ConveyorSystem()
        printfn ""

        printfn "=========================================="
        printfn "모든 예제 완료!"
        printfn "==========================================\n"
