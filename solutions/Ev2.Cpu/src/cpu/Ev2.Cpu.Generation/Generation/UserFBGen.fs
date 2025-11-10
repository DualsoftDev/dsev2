namespace Ev2.Cpu.Generation.Make

open System
open Ev2.Cpu.Core
open Ev2.Cpu.Ast
open Ev2.Cpu.Generation.Core
open Ev2.Cpu.Generation.Make.ExpressionGen
open Ev2.Cpu.Generation.Make.StatementGen
open Ev2.Cpu.Core.UserDefined.UserExprConverter
open Ev2.Cpu.Core.UserDefined.UserStmtConverter

/// <summary>
/// UserFB/FC 생성 모듈 - 사용자 정의 함수 블록 및 함수 생성
/// </summary>
/// <remarks>
/// 이 모듈은 PLC 프로그래밍에서 재사용 가능한 제어 로직을 만들기 위한 핵심 API를 제공합니다.
/// - FC (Function): 상태가 없는 순수 함수
/// - FB (Function Block): 상태를 유지하는 함수 블록 (Static 변수 포함)
/// </remarks>
module UserFBGen =

    // ═════════════════════════════════════════════════════════════════════
    // 파라미터 헬퍼 함수
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 입력 파라미터 생성
    /// </summary>
    /// <param name="name">파라미터 이름</param>
    /// <param name="dataType">데이터 타입 (typeof<bool>, typeof<int>, typeof<double>, typeof<string>)</param>
    /// <returns>입력 방향의 함수 파라미터</returns>
    let inputParam name dataType =
        { Name = name
          DataType = dataType
          Direction = ParamDirection.Input
          DefaultValue = None
          Description = None
          IsOptional = false }

    /// 입력 파라미터 생성 (기본값 포함)
    let inputParamWithDefault name dataType defaultValue =
        { Name = name
          DataType = dataType
          Direction = ParamDirection.Input
          DefaultValue = Some defaultValue
          Description = None
          IsOptional = true }

    /// 출력 파라미터 생성
    let outputParam name dataType =
        { Name = name
          DataType = dataType
          Direction = ParamDirection.Output
          DefaultValue = None
          Description = None
          IsOptional = false }

    /// InOut 파라미터 생성
    let inoutParam name dataType =
        { Name = name
          DataType = dataType
          Direction = ParamDirection.InOut
          DefaultValue = None
          Description = None
          IsOptional = false }

    // ═════════════════════════════════════════════════════════════════════
    // FC (Function) Builder
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// FC (Function) 빌더 - 상태가 없는 순수 함수 생성
    /// </summary>
    /// <param name="name">함수 이름 (PLC에서 사용될 함수명)</param>
    /// <remarks>
    /// FC는 입력을 받아 출력을 반환하는 순수 함수입니다.
    /// Static 변수나 내부 상태를 가질 수 없으며, 같은 입력에 대해 항상 같은 출력을 반환합니다.
    /// </remarks>
    /// <example>
    /// <code>
    /// // 온도 변환 함수 (섭씨 → 화씨)
    /// let builder = FCBuilder("CelsiusToFahrenheit")
    /// builder.AddInput("celsius", typeof<double>)
    /// builder.AddOutput("fahrenheit", typeof<double>)
    ///
    /// // 본문: fahrenheit = celsius * 1.8 + 32
    /// let body =
    ///     add
    ///         (mul (Terminal(DsTag.Double("celsius"))) (doubleExpr 1.8))
    ///         (doubleExpr 32.0)
    /// builder.SetBody(body)
    ///
    /// match builder.Build() with
    /// | Ok fc -> printfn "FC 생성 성공: %s" fc.Name
    /// | Error msg -> printfn "FC 생성 실패: %s" msg
    /// </code>
    /// </example>
    type FCBuilder(name: string) =
        let mutable inputs = []
        let mutable outputs = []
        let mutable body = Ev2.Cpu.Core.Expression.Const(box 0, typeof<int>) // 기본값
        let mutable description = None

        let toCoreDirection = function
            | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
            | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
            | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut

        /// 입력 파라미터 추가
        member _.AddInput(name: string, dataType: Type) =
            inputs <- inputParam name dataType :: inputs

        /// 입력 파라미터 추가 (기본값 포함)
        member _.AddInputWithDefault(name: string, dataType: Type, defaultValue: obj) =
            inputs <- inputParamWithDefault name dataType defaultValue :: inputs

        /// 출력 추가
        member _.AddOutput(name: string, dataType: Type) =
            outputs <- outputParam name dataType :: outputs

        /// 본문 설정 (수식)
        member _.SetBody(expr: Ev2.Cpu.Core.Expression.DsExpr) =
            body <- expr

        /// 설명 설정
        member _.SetDescription(desc: string) =
            description <- Some desc

        /// FC 빌드 (검증 포함)
        member _.Build() : Result<UserFC, string> =
            let metadata =
                match description with
                | Some desc -> { UserFCMetadata.Empty with Description = Some desc }
                | None -> UserFCMetadata.Empty
            let finalizedInputs = List.rev inputs
            let finalizedOutputs = List.rev outputs
            let fc = {
                Name = name
                Inputs = finalizedInputs
                Outputs = finalizedOutputs
                Body = body
                Metadata = metadata
            }

            let coreInputs =
                finalizedInputs
                |> List.map (fun p ->
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, toCoreDirection p.Direction,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let coreOutputs =
                finalizedOutputs
                |> List.map (fun p ->
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, toCoreDirection p.Direction,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            match dsExprToUserExpr None body with
            | None ->
                Error (sprintf "FC '%s' contains expressions that cannot be converted for validation." name)
            | Some userExpr ->
                let coreMetadata : Ev2.Cpu.Core.UserDefined.UserFCMetadata =
                    { Author = metadata.Author
                      Version = metadata.Version
                      Description = metadata.Description
                      CreatedDate = metadata.CreatedDate
                      ModifiedDate = metadata.ModifiedDate
                      Tags = metadata.Tags
                      Dependencies = metadata.Dependencies }

                let coreFc : Ev2.Cpu.Core.UserDefined.UserFC =
                    { Name = name
                      Inputs = coreInputs
                      Outputs = coreOutputs
                      Body = userExpr
                      Metadata = coreMetadata }

                match Ev2.Cpu.Core.UserDefined.UserDefinitionValidation.validateUserFC coreFc with
                | Ok () -> Ok fc
                | Error err -> Error (err.Format())

    // ═════════════════════════════════════════════════════════════════════
    // FB (Function Block) Builder
    // ═════════════════════════════════════════════════════════════════════

    /// FB 빌더
    type FBBuilder(name: string) =
        let mutable inputs = []
        let mutable outputs = []
        let mutable inouts = []
        let mutable statics = []
        let mutable temps = []
        let mutable body = []
        let mutable description = None

        /// 입력 파라미터 추가
        member _.AddInput(name: string, dataType: Type) =
            inputs <- inputParam name dataType :: inputs

        /// 입력 파라미터 추가 (기본값 포함)
        member _.AddInputWithDefault(name: string, dataType: Type, defaultValue: obj) =
            inputs <- inputParamWithDefault name dataType defaultValue :: inputs

        /// 출력 파라미터 추가
        member _.AddOutput(name: string, dataType: Type) =
            outputs <- outputParam name dataType :: outputs

        /// InOut 파라미터 추가
        member _.AddInOut(name: string, dataType: Type) =
            inouts <- inoutParam name dataType :: inouts

        /// Static 변수 추가 (상태 저장)
        member _.AddStatic(name: string, dataType: Type) =
            statics <- (name, dataType, None) :: statics

        /// Static 변수 추가 (초기값 포함)
        member _.AddStaticWithInit(name: string, dataType: Type, initValue: obj) =
            statics <- (name, dataType, Some initValue) :: statics

        /// Temp 변수 추가 (임시)
        member _.AddTemp(name: string, dataType: Type) =
            temps <- (name, dataType) :: temps

        /// 명령문 추가
        member _.AddStatement(stmt: Ev2.Cpu.Core.Statement.DsStmt) =
            body <- stmt :: body

        /// 명령문 리스트 추가
        member _.AddStatements(stmts: Ev2.Cpu.Core.Statement.DsStmt list) =
            body <- (List.rev stmts) @ body

        /// 릴레이 추가
        member _.AddRelay(relay: Relay) =
            body <- (GenerationUtils.relayToStmt relay) :: body

        /// 설명 설정
        member _.SetDescription(desc: string) =
            description <- Some desc

        /// FB 빌드 (검증 포함)
        member _.Build() : Result<UserFB, string> =
            let metadata =
                match description with
                | Some desc -> { UserFCMetadata.Empty with Description = Some desc }
                | None -> UserFCMetadata.Empty
            let finalizedInputs = List.rev inputs
            let finalizedOutputs = List.rev outputs
            let finalizedInouts = List.rev inouts
            let finalizedStatics = List.rev statics
            let finalizedTemps = List.rev temps
            let finalizedBody = List.rev body

            let fb = {
                Name = name
                Inputs = finalizedInputs
                Outputs = finalizedOutputs
                InOuts = finalizedInouts
                Statics = finalizedStatics
                Temps = finalizedTemps
                Body = finalizedBody
                Metadata = metadata
            }

            let toCoreDirection = function
                | ParamDirection.Input -> Ev2.Cpu.Core.UserDefined.ParamDirection.Input
                | ParamDirection.Output -> Ev2.Cpu.Core.UserDefined.ParamDirection.Output
                | ParamDirection.InOut -> Ev2.Cpu.Core.UserDefined.ParamDirection.InOut

            let coreInputs =
                finalizedInputs
                |> List.map (fun p ->
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, toCoreDirection p.Direction,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let coreOutputs =
                finalizedOutputs
                |> List.map (fun p ->
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, toCoreDirection p.Direction,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let coreInouts =
                finalizedInouts
                |> List.map (fun p ->
                    Ev2.Cpu.Core.UserDefined.FunctionParam.Create(p.Name, p.DataType, toCoreDirection p.Direction,
                                                                  ?defaultValue = p.DefaultValue,
                                                                  ?description = p.Description))

            let userBodyResult =
                finalizedBody
                |> List.fold (fun acc stmt ->
                    match acc with
                    | Error msg -> Error msg
                    | Ok accStmts ->
                        match Ev2.Cpu.Core.UserDefined.UserStmtConverter.dsStmtToUserStmt None stmt with
                        | Some userStmt -> Ok (userStmt :: accStmts)
                        | None ->
                            Error (sprintf "FB '%s' contains statements that cannot be converted for validation." name)) (Ok [])

            match userBodyResult with
            | Error msg -> Error msg
            | Ok reversedUserBody ->
                let coreMetadata : Ev2.Cpu.Core.UserDefined.UserFCMetadata =
                    { Author = metadata.Author
                      Version = metadata.Version
                      Description = metadata.Description
                      CreatedDate = metadata.CreatedDate
                      ModifiedDate = metadata.ModifiedDate
                      Tags = metadata.Tags
                      Dependencies = metadata.Dependencies }

                let coreFb : Ev2.Cpu.Core.UserDefined.UserFB =
                    { Name = name
                      Inputs = coreInputs
                      Outputs = coreOutputs
                      InOuts = coreInouts
                      Statics = finalizedStatics
                      Temps = finalizedTemps
                      Body = List.rev reversedUserBody
                      Metadata = coreMetadata }

                match Ev2.Cpu.Core.UserDefined.UserDefinitionValidation.validateUserFB coreFb with
                | Ok () -> Ok fb
                | Error err -> Error (err.Format())

    // ═════════════════════════════════════════════════════════════════════
    // 인스턴스 생성
    // ═════════════════════════════════════════════════════════════════════

    /// FB 인스턴스 생성
    let createFBInstance (name: string) (fbType: UserFB) =
        { Name = name
          FBType = fbType
          StateStorage = None }

    /// FB 인스턴스 생성 (초기 상태 포함)
    let createFBInstanceWithState (name: string) (fbType: UserFB) (initialState: Map<string, obj>) =
        { Name = name
          FBType = fbType
          StateStorage = Some initialState }

    // ═════════════════════════════════════════════════════════════════════
    // FC/FB 호출 헬퍼
    // ═════════════════════════════════════════════════════════════════════

    /// FC 호출 (수식으로 사용)
    let callFC (fc: UserFC) (args: DsExpr list) : Result<DsExpr, string> =
        let required =
            fc.Inputs |> List.filter (fun p -> not p.IsOptional) |> List.length

        if args.Length < required then
            Error (sprintf "FC '%s' requires at least %d arguments but received %d."
                        fc.Name required args.Length)
        elif args.Length > fc.Inputs.Length then
            Error (sprintf "FC '%s' accepts at most %d arguments but received %d."
                        fc.Name fc.Inputs.Length args.Length)
        else
            Ok (Ev2.Cpu.Ast.ExprBuilder.userFCCall fc.Name args (Some fc.ReturnType) fc.Signature)

    /// FB 호출 (명령문으로 사용)
    /// args는 이름-값 매핑으로 전달
    let callFB (instance: FBInstance) (args: Map<string, DsExpr>) : Result<Ev2.Cpu.Ast.DsStatement, string> =
        let fb = instance.FBType

        let requiredInputs =
            fb.Inputs
            |> List.filter (fun p -> not p.IsOptional)
            |> List.map (fun p -> p.Name)
            |> Set.ofList

        let providedInputs = args |> Map.toSeq |> Seq.map fst |> Set.ofSeq

        let missingInputs = Set.difference requiredInputs providedInputs
        if not (Set.isEmpty missingInputs) then
            Error (sprintf "FB '%s' missing required inputs: %s"
                        fb.Name (missingInputs |> Set.toList |> String.concat ", "))
        else
            let unknownInputs =
                providedInputs
                |> Set.filter (fun name ->
                    fb.Inputs |> List.exists (fun p -> p.Name = name) |> not)
            if not (Set.isEmpty unknownInputs) then
                Error (sprintf "FB '%s' does not define inputs: %s"
                            fb.Name (unknownInputs |> Set.toList |> String.concat ", "))
            else
                let outputNames = fb.Outputs |> List.map (fun p -> p.Name) |> Set.ofList
                let stateLayout = fb.Statics |> List.map (fun (n, t, _) -> (n, t))
                Ok (Ev2.Cpu.Ast.StmtBuilder.userFB instance.Name fb.Name args outputNames stateLayout)

    // ═════════════════════════════════════════════════════════════════════
    // 일반적인 FC/FB 템플릿
    // ═════════════════════════════════════════════════════════════════════

    /// 온도 변환 FC (섭씨 -> 화씨)
    let createCelsiusToFahrenheitFC() =
        let builder = FCBuilder("CelsiusToFahrenheit")
        builder.AddInput("celsius", typeof<double>)
        builder.AddOutput("fahrenheit", typeof<double>)

        // F = C * 1.8 + 32
        let celsius = Terminal(DsTag.Double("celsius"))
        let formula = add (mul celsius (doubleExpr 1.8)) (doubleExpr 32.0)
        builder.SetBody(formula)
        builder.SetDescription("섭씨를 화씨로 변환")

        builder.Build()

    /// 선형 스케일링 FC (0-100% -> Min-Max)
    let createLinearScaleFC() =
        let builder = FCBuilder("LinearScale")
        builder.AddInput("input", typeof<double>)    // 0-100
        builder.AddInput("minOut", typeof<double>)   // 출력 최소값
        builder.AddInput("maxOut", typeof<double>)   // 출력 최대값
        builder.AddOutput("output", typeof<double>)

        // output = minOut + (input / 100.0) * (maxOut - minOut)
        let input = Terminal(DsTag.Double("input"))
        let minOut = Terminal(DsTag.Double("minOut"))
        let maxOut = Terminal(DsTag.Double("maxOut"))
        let range = sub maxOut minOut
        let ratio = div input (doubleExpr 100.0)
        let formula = add minOut (mul ratio range)

        builder.SetBody(formula)
        builder.SetDescription("선형 스케일 변환 (0-100% -> Min-Max)")

        builder.Build()

    /// 히스테리시스 FB (온도 제어 등에 사용)
    let createHysteresisFB() =
        let builder = FBBuilder("Hysteresis")

        // 입력
        builder.AddInput("input", typeof<double>)       // 입력값
        builder.AddInput("highLimit", typeof<double>)   // 상한
        builder.AddInput("lowLimit", typeof<double>)    // 하한

        // 출력
        builder.AddOutput("output", typeof<bool>)       // ON/OFF

        // 내부 상태
        builder.AddStaticWithInit("state", typeof<bool>, box false)

        // 로직: input > highLimit이면 OFF, input < lowLimit이면 ON
        let input = Terminal(DsTag.Double("input"))
        let high = Terminal(DsTag.Double("highLimit"))
        let low = Terminal(DsTag.Double("lowLimit"))
        let state = Terminal(DsTag.Bool("state"))

        // 상승 조건: input < lowLimit
        let setCondition = lt input low

        // 하강 조건: input > highLimit
        let resetCondition = gt input high

        // 릴레이 로직
        let relay = Relay.Create(DsTag.Bool("state"), setCondition, resetCondition)
        builder.AddRelay(relay)

        // 출력에 상태 복사
        builder.AddStatement(assignAuto "output" typeof<bool> state)

        builder.SetDescription("히스테리시스 제어 (상한/하한)")
        builder.Build()

    /// 모터 제어 FB (시작/정지 인터록 포함)
    let createMotorControlFB() =
        let builder = FBBuilder("MotorControl")

        // 입력
        builder.AddInput("start", typeof<bool>)          // 시작 명령
        builder.AddInput("stop", typeof<bool>)           // 정지 명령
        builder.AddInput("emergency", typeof<bool>)      // 비상정지
        builder.AddInput("overload", typeof<bool>)       // 과부하

        // 출력
        builder.AddOutput("running", typeof<bool>)       // 운전 중
        builder.AddOutput("fault", typeof<bool>)         // 고장
        builder.AddOutput("runTime", typeof<int>)        // 운전 시간 (ms)

        // 내부 상태
        builder.AddStaticWithInit("motorState", typeof<bool>, box false)
        builder.AddStaticWithInit("faultState", typeof<bool>, box false)
        builder.AddStaticWithInit("timer", typeof<int>, box 0)

        // 안전 조건 체크
        let emergency = Terminal(DsTag.Bool("emergency"))
        let overload = Terminal(DsTag.Bool("overload"))
        let faultCondition = or' emergency overload

        // 고장 릴레이
        let faultRelay = Relay.Create(
            DsTag.Bool("faultState"),
            faultCondition,
            not' faultCondition
        )
        builder.AddRelay(faultRelay)

        // 모터 릴레이 (고장 시 정지)
        let start = Terminal(DsTag.Bool("start"))
        let stop = Terminal(DsTag.Bool("stop"))
        let faultState = Terminal(DsTag.Bool("faultState"))

        let motorRelay = Relay.CreateFull(
            DsTag.Bool("motorState"),
            and' start (not' faultState),              // SET: 시작 && !고장
            or' stop faultState,                       // RESET: 정지 || 고장
            RelayMode.SR,
            RelayPriority.ResetFirst,                  // 안전 우선
            false
        )
        builder.AddRelay(motorRelay)

        // 운전 시간 카운터
        let motorState = Terminal(DsTag.Bool("motorState"))
        let timerIncrement =
            when'
                motorState
                (mov (add (Terminal(DsTag.Int("timer"))) (intExpr 100)) (DsTag.Int("timer")))
        builder.AddStatement(timerIncrement)

        // 출력 설정
        builder.AddStatement(assignAuto "running" typeof<bool> motorState)
        builder.AddStatement(assignAuto "fault" typeof<bool> faultState)
        builder.AddStatement(assignAuto "runTime" typeof<int> (Terminal(DsTag.Int("timer"))))

        builder.SetDescription("모터 제어 (시작/정지/인터록)")
        builder.Build()

    /// 3단계 시퀀스 FB
    let createSequence3StepFB() =
        let builder = FBBuilder("Sequence3Step")

        // 입력
        builder.AddInput("start", typeof<bool>)
        builder.AddInput("reset", typeof<bool>)
        builder.AddInput("step1Done", typeof<bool>)
        builder.AddInput("step2Done", typeof<bool>)
        builder.AddInput("step3Done", typeof<bool>)

        // 출력
        builder.AddOutput("step1Active", typeof<bool>)
        builder.AddOutput("step2Active", typeof<bool>)
        builder.AddOutput("step3Active", typeof<bool>)
        builder.AddOutput("complete", typeof<bool>)

        // 내부 상태
        builder.AddStaticWithInit("currentStep", typeof<int>, box 0)

        let step = Terminal(DsTag.Int("currentStep"))
        let start = Terminal(DsTag.Bool("start"))
        let reset = Terminal(DsTag.Bool("reset"))

        // 리셋
        builder.AddStatement(when' reset (mov (intExpr 0) (DsTag.Int("currentStep"))))

        // Step 0 -> 1: start 신호
        builder.AddStatement(whenAt 10
            (and' (eq step (intExpr 0)) start)
            (mov (intExpr 1) (DsTag.Int("currentStep"))))

        // Step 1 -> 2: step1Done
        builder.AddStatement(whenAt 20
            (and' (eq step (intExpr 1)) (Terminal(DsTag.Bool("step1Done"))))
            (mov (intExpr 2) (DsTag.Int("currentStep"))))

        // Step 2 -> 3: step2Done
        builder.AddStatement(whenAt 30
            (and' (eq step (intExpr 2)) (Terminal(DsTag.Bool("step2Done"))))
            (mov (intExpr 3) (DsTag.Int("currentStep"))))

        // Step 3 -> 0: step3Done
        builder.AddStatement(whenAt 40
            (and' (eq step (intExpr 3)) (Terminal(DsTag.Bool("step3Done"))))
            (mov (intExpr 0) (DsTag.Int("currentStep"))))

        // 출력 설정
        builder.AddStatement(assignAuto "step1Active" typeof<bool> (eq step (intExpr 1)))
        builder.AddStatement(assignAuto "step2Active" typeof<bool> (eq step (intExpr 2)))
        builder.AddStatement(assignAuto "step3Active" typeof<bool> (eq step (intExpr 3)))
        builder.AddStatement(assignAuto "complete" typeof<bool>
            (and' (eq step (intExpr 0)) (not' start)))

        builder.SetDescription("3단계 시퀀스 제어")
        builder.Build()

    // ═════════════════════════════════════════════════════════════════════
    // UserFB 레지스트리 (재사용을 위한 저장소)
    // ═════════════════════════════════════════════════════════════════════

    /// UserFB 레지스트리
    type UserFBRegistry() =
        let mutable fcs = Map.empty<string, UserFC>
        let mutable fbs = Map.empty<string, UserFB>
        let mutable instances = Map.empty<string, FBInstance>

        /// FC 등록
        member _.RegisterFC(fc: UserFC) : Result<unit, string> =
            match fc.Validate() with
            | Error err -> Error err
            | Ok () ->
                if Map.containsKey fc.Name fcs then
                    Error (sprintf "FC '%s' is already registered." fc.Name)
                else
                    fcs <- Map.add fc.Name fc fcs
                    Ok ()

        /// FB 등록
        member _.RegisterFB(fb: UserFB) : Result<unit, string> =
            match fb.Validate() with
            | Error err -> Error err
            | Ok () ->
                if Map.containsKey fb.Name fbs then
                    Error (sprintf "FB '%s' is already registered." fb.Name)
                else
                    fbs <- Map.add fb.Name fb fbs
                    Ok ()

        /// FB 인스턴스 등록
        member this.RegisterInstance(instance: FBInstance) : Result<unit, string> =
            if not (Map.containsKey instance.FBType.Name fbs) then
                Error (sprintf "FB '%s' is not registered." instance.FBType.Name)
            elif Map.containsKey instance.Name instances then
                Error (sprintf "FB instance '%s' is already registered." instance.Name)
            else
                instances <- Map.add instance.Name instance instances
                Ok ()

        /// FC 찾기
        member _.TryFindFC(name: string) =
            Map.tryFind name fcs

        /// FC 조회 (테스트 호환성)
        member _.GetFC(name: string) =
            Map.tryFind name fcs

        /// FB 찾기
        member _.TryFindFB(name: string) =
            Map.tryFind name fbs

        /// FB 조회 (테스트 호환성)
        member _.GetFB(name: string) =
            Map.tryFind name fbs

        /// 인스턴스 찾기
        member _.TryFindInstance(name: string) =
            Map.tryFind name instances

        /// 등록된 모든 FC 목록
        member _.GetAllFCs() =
            Map.toList fcs |> List.map snd

        /// 등록된 모든 FB 목록
        member _.GetAllFBs() =
            Map.toList fbs |> List.map snd

        /// 등록된 모든 인스턴스 목록
        member _.GetAllInstances() =
            Map.toList instances |> List.map snd

        /// 모든 FC/FB 검증
        member _.ValidateAll() : Result<unit, string list> =
            let fcErrors =
                fcs
                |> Map.toList
                |> List.choose (fun (_, fc) ->
                    match fc.Validate() with
                    | Error err -> Some err
                    | Ok () -> None)

            let fbErrors =
                fbs
                |> Map.toList
                |> List.choose (fun (_, fb) ->
                    match fb.Validate() with
                    | Error err -> Some err
                    | Ok () -> None)

            let allErrors = fcErrors @ fbErrors
            if List.isEmpty allErrors then
                Ok ()
            else
                Error allErrors
