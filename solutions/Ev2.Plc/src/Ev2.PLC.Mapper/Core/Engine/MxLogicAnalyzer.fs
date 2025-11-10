namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// Mitsubishi 로직 분석기 인터페이스
type IMxLogicAnalyzer =
    inherit IVendorLogicAnalyzer

    /// LD (Load) 명령어 파싱
    abstract member ParseLDAsync: instruction: string -> Task<Condition option>

    /// LDI (Load Inverse) 명령어 파싱
    abstract member ParseLDIAsync: instruction: string -> Task<Condition option>

    /// AND 명령어 파싱
    abstract member ParseANDAsync: instruction: string -> Task<Condition option>

    /// ANI (AND Inverse) 명령어 파싱
    abstract member ParseANIAsync: instruction: string -> Task<Condition option>

    /// OUT (Output) 명령어 파싱
    abstract member ParseOUTAsync: instruction: string -> Task<Action option>

    /// MOV (Move) 명령어 파싱
    abstract member ParseMOVAsync: instruction: string -> Task<Action option>

    /// 산술 연산 명령어 파싱 (ADD, SUB, MUL, DIV)
    abstract member ParseArithmeticAsync: instruction: string -> Task<Action option>

    /// 비교 명령어 파싱 (CMP, GT, LT, EQ, etc.)
    abstract member ParseComparisonAsync: instruction: string -> Task<Condition option>

/// Mitsubishi 로직 분석기 구현
type MxLogicAnalyzer(logger: ILogger<MxLogicAnalyzer>) =

    /// Mitsubishi 명령어 패턴
    let ldPattern = @"^LD\s+(\S+)"           // LD (Load)
    let ldiPattern = @"^LDI\s+(\S+)"         // LDI (Load Inverse)
    let andPattern = @"^AND\s+(\S+)"         // AND
    let aniPattern = @"^ANI\s+(\S+)"         // ANI (AND Inverse)
    let orPattern = @"^OR\s+(\S+)"           // OR
    let oriPattern = @"^ORI\s+(\S+)"         // ORI (OR Inverse)
    let outPattern = @"^OUT\s+(\S+)"         // OUT (Output)
    let setPattern = @"^SET\s+(\S+)"         // SET
    let rstPattern = @"^RST\s+(\S+)"         // RST (Reset)
    let movPattern = @"^MOV\s+(\S+)\s+(\S+)" // MOV (Move)
    let cmpPattern = @"^CMP\s+(\S+)\s+(\S+)\s+(\S+)" // CMP (Compare)
    let addPattern = @"^ADD\s+(\S+)\s+(\S+)\s+(\S+)" // ADD
    let subPattern = @"^SUB\s+(\S+)\s+(\S+)\s+(\S+)" // SUB
    let mulPattern = @"^MUL\s+(\S+)\s+(\S+)\s+(\S+)" // MUL
    let divPattern = @"^DIV\s+(\S+)\s+(\S+)\s+(\S+)" // DIV

    /// Timer 명령어 패턴
    let tonPattern = @"^TON\s+(\S+)\s+(\S+)" // TON (Timer ON delay)
    let tofPattern = @"^TOF\s+(\S+)\s+(\S+)" // TOF (Timer OFF delay)

    /// Counter 명령어 패턴
    let ctuPattern = @"^CTU\s+(\S+)\s+(\S+)" // CTU (Count Up)
    let ctdPattern = @"^CTD\s+(\S+)\s+(\S+)" // CTD (Count Down)

    /// 변수 이름 정규화
    let normalizeVariable (varName: string) =
        varName.Trim()

    /// LD (Load) 명령어 파싱
    let parseLD (instruction: string) : Condition option =
        let m = Regex.Match(instruction, ldPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"LD: {variable}"
            }
        else
            None

    /// LDI (Load Inverse) 명령어 파싱
    let parseLDI (instruction: string) : Condition option =
        let m = Regex.Match(instruction, ldiPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"LDI: {variable}"
            }
        else
            None

    /// AND 명령어 파싱
    let parseAND (instruction: string) : Condition option =
        let m = Regex.Match(instruction, andPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"AND: {variable}"
            }
        else
            None

    /// ANI (AND Inverse) 명령어 파싱
    let parseANI (instruction: string) : Condition option =
        let m = Regex.Match(instruction, aniPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"ANI: {variable}"
            }
        else
            None

    /// OR 명령어 파싱
    let parseOR (instruction: string) : Condition option =
        let m = Regex.Match(instruction, orPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"OR: {variable}"
            }
        else
            None

    /// ORI (OR Inverse) 명령어 파싱
    let parseORI (instruction: string) : Condition option =
        let m = Regex.Match(instruction, oriPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"ORI: {variable}"
            }
        else
            None

    /// OUT (Output) 명령어 파싱
    let parseOUT (instruction: string) : Action option =
        let m = Regex.Match(instruction, outPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Assign
                Value = Some "True"
                Delay = None
                Description = $"OUT: {variable}"
            }
        else
            None

    /// SET 명령어 파싱
    let parseSET (instruction: string) : Action option =
        let m = Regex.Match(instruction, setPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Set
                Value = Some "True"
                Delay = None
                Description = $"SET: {variable}"
            }
        else
            None

    /// RST (Reset) 명령어 파싱
    let parseRST (instruction: string) : Action option =
        let m = Regex.Match(instruction, rstPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Reset
                Value = Some "False"
                Delay = None
                Description = $"RST: {variable}"
            }
        else
            None

    /// MOV (Move) 명령어 파싱
    let parseMOV (instruction: string) : Action option =
        let m = Regex.Match(instruction, movPattern)
        if m.Success then
            let source = normalizeVariable m.Groups.[1].Value
            let dest = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = dest
                Operation = ActionOperation.Assign
                Value = Some source
                Delay = None
                Description = $"MOV: {source} -> {dest}"
            }
        else
            None

    /// CMP (Compare) 명령어 파싱
    let parseCMP (instruction: string) : Condition option =
        let m = Regex.Match(instruction, cmpPattern)
        if m.Success then
            let operand1 = normalizeVariable m.Groups.[1].Value
            let operand2 = normalizeVariable m.Groups.[2].Value
            let result = normalizeVariable m.Groups.[3].Value
            Some {
                Variable = operand1
                Operator = ConditionOperator.Equal
                Value = operand2
                Description = $"CMP: {operand1} == {operand2} -> {result}"
            }
        else
            None

    /// 산술 연산 명령어 파싱 (ADD, SUB, MUL, DIV)
    let parseArithmetic (instruction: string) : Action option =
        let patterns = [
            (addPattern, "ADD", ActionOperation.Increment)
            (subPattern, "SUB", ActionOperation.Decrement)
            (mulPattern, "MUL", ActionOperation.Assign)
            (divPattern, "DIV", ActionOperation.Assign)
        ]

        patterns
        |> List.tryPick (fun (pattern, opName, operation) ->
            let m = Regex.Match(instruction, pattern)
            if m.Success then
                let source1 = normalizeVariable m.Groups.[1].Value
                let source2 = normalizeVariable m.Groups.[2].Value
                let dest = normalizeVariable m.Groups.[3].Value
                Some {
                    Variable = dest
                    Operation = operation
                    Value = Some $"{source1} {opName} {source2}"
                    Delay = None
                    Description = $"{opName}: {source1} {opName} {source2} -> {dest}"
                }
            else
                None
        )

    /// Timer 명령어 파싱 (TON, TOF)
    let parseTimer (instruction: string) : (Condition option * Action option) =
        let tonMatch = Regex.Match(instruction, tonPattern)
        let tofMatch = Regex.Match(instruction, tofPattern)

        if tonMatch.Success then
            let timerName = normalizeVariable tonMatch.Groups.[1].Value
            let preset = normalizeVariable tonMatch.Groups.[2].Value

            let condition = Some {
                Variable = $"{timerName}.Q"
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"TON done: {timerName}"
            }

            let delay =
                match Int32.TryParse(preset) with
                | true, ms -> Some (TimeSpan.FromMilliseconds(float ms))
                | _ -> None

            let action = Some {
                Variable = timerName
                Operation = ActionOperation.Call
                Value = Some preset
                Delay = delay
                Description = $"Start TON: {timerName} ({preset}ms)"
            }

            (condition, action)

        elif tofMatch.Success then
            let timerName = normalizeVariable tofMatch.Groups.[1].Value
            let preset = normalizeVariable tofMatch.Groups.[2].Value

            let condition = Some {
                Variable = $"{timerName}.Q"
                Operator = ConditionOperator.Equal
                Value = "False"
                Description = $"TOF done: {timerName}"
            }

            let delay =
                match Int32.TryParse(preset) with
                | true, ms -> Some (TimeSpan.FromMilliseconds(float ms))
                | _ -> None

            let action = Some {
                Variable = timerName
                Operation = ActionOperation.Call
                Value = Some preset
                Delay = delay
                Description = $"Start TOF: {timerName} ({preset}ms)"
            }

            (condition, action)
        else
            (None, None)

    /// Counter 명령어 파싱 (CTU, CTD)
    let parseCounter (instruction: string) : (Condition option * Action option) =
        let ctuMatch = Regex.Match(instruction, ctuPattern)
        let ctdMatch = Regex.Match(instruction, ctdPattern)

        if ctuMatch.Success then
            let counterName = normalizeVariable ctuMatch.Groups.[1].Value
            let preset = normalizeVariable ctuMatch.Groups.[2].Value

            let condition = Some {
                Variable = $"{counterName}.Q"
                Operator = ConditionOperator.GreaterOrEqual
                Value = preset
                Description = $"CTU done: {counterName} >= {preset}"
            }

            let action = Some {
                Variable = counterName
                Operation = ActionOperation.Increment
                Value = None
                Delay = None
                Description = $"CTU: {counterName}"
            }

            (condition, action)

        elif ctdMatch.Success then
            let counterName = normalizeVariable ctdMatch.Groups.[1].Value
            let preset = normalizeVariable ctdMatch.Groups.[2].Value

            let condition = Some {
                Variable = $"{counterName}.Q"
                Operator = ConditionOperator.LessOrEqual
                Value = preset
                Description = $"CTD done: {counterName} <= {preset}"
            }

            let action = Some {
                Variable = counterName
                Operation = ActionOperation.Decrement
                Value = None
                Delay = None
                Description = $"CTD: {counterName}"
            }

            (condition, action)
        else
            (None, None)

    /// 로직 흐름 타입 감지
    let detectLogicFlowType (content: string) =
        let hasTimers = Regex.IsMatch(content, @"TON|TOF")
        let hasCounters = Regex.IsMatch(content, @"CTU|CTD")
        let hasCompare = Regex.IsMatch(content, @"CMP|GT|LT|EQ|NE|GE|LE")
        let hasMath = Regex.IsMatch(content, @"ADD|SUB|MUL|DIV")

        if hasTimers then LogicFlowType.Timer
        elif hasCounters then LogicFlowType.Counter
        elif hasCompare then LogicFlowType.Conditional
        elif hasMath then LogicFlowType.Math
        else LogicFlowType.Simple

    /// 인터페이스 구현
    interface IMxLogicAnalyzer with
        /// 지원하는 제조사
        member _.SupportedVendor = PlcVendor.Mitsubishi (Some MitsubishiModel.IQ_R, None)

        /// 특정 로직 타입 지원 여부
        member _.CanAnalyze(logicType: LogicType) =
            match logicType with
            | LogicType.LadderRung | LogicType.StructuredText | LogicType.InstructionList -> true
            | _ -> false

        /// LD 명령어 파싱
        member _.ParseLDAsync(instruction: string) =
            Task.FromResult(parseLD instruction)

        /// LDI 명령어 파싱
        member _.ParseLDIAsync(instruction: string) =
            Task.FromResult(parseLDI instruction)

        /// AND 명령어 파싱
        member _.ParseANDAsync(instruction: string) =
            Task.FromResult(parseAND instruction)

        /// ANI 명령어 파싱
        member _.ParseANIAsync(instruction: string) =
            Task.FromResult(parseANI instruction)

        /// OUT 명령어 파싱
        member _.ParseOUTAsync(instruction: string) =
            Task.FromResult(parseOUT instruction)

        /// MOV 명령어 파싱
        member _.ParseMOVAsync(instruction: string) =
            Task.FromResult(parseMOV instruction)

        /// 산술 연산 명령어 파싱
        member _.ParseArithmeticAsync(instruction: string) =
            Task.FromResult(parseArithmetic instruction)

        /// 비교 명령어 파싱
        member _.ParseComparisonAsync(instruction: string) =
            Task.FromResult(parseCMP instruction)

        /// 특수 명령어 파싱
        member _.ParseSpecialInstructionAsync(content: string) =
            task {
                try
                    let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
                    let mutable conditions = []
                    let mutable actions = []

                    for line in lines do
                        let trimmed = line.Trim()

                        // 조건 명령어들
                        match trimmed with
                        | _ when parseLD trimmed |> Option.isSome ->
                            conditions <- (parseLD trimmed).Value :: conditions
                        | _ when parseLDI trimmed |> Option.isSome ->
                            conditions <- (parseLDI trimmed).Value :: conditions
                        | _ when parseAND trimmed |> Option.isSome ->
                            conditions <- (parseAND trimmed).Value :: conditions
                        | _ when parseANI trimmed |> Option.isSome ->
                            conditions <- (parseANI trimmed).Value :: conditions
                        | _ when parseOR trimmed |> Option.isSome ->
                            conditions <- (parseOR trimmed).Value :: conditions
                        | _ when parseORI trimmed |> Option.isSome ->
                            conditions <- (parseORI trimmed).Value :: conditions
                        | _ when parseCMP trimmed |> Option.isSome ->
                            conditions <- (parseCMP trimmed).Value :: conditions
                        | _ -> ()

                        // 액션 명령어들
                        match trimmed with
                        | _ when parseOUT trimmed |> Option.isSome ->
                            actions <- (parseOUT trimmed).Value :: actions
                        | _ when parseSET trimmed |> Option.isSome ->
                            actions <- (parseSET trimmed).Value :: actions
                        | _ when parseRST trimmed |> Option.isSome ->
                            actions <- (parseRST trimmed).Value :: actions
                        | _ when parseMOV trimmed |> Option.isSome ->
                            actions <- (parseMOV trimmed).Value :: actions
                        | _ when parseArithmetic trimmed |> Option.isSome ->
                            actions <- (parseArithmetic trimmed).Value :: actions
                        | _ -> ()

                        // Timer/Counter 처리
                        let timerResult = parseTimer trimmed
                        match timerResult with
                        | Some cond, Some act ->
                            conditions <- cond :: conditions
                            actions <- act :: actions
                        | Some cond, None -> conditions <- cond :: conditions
                        | None, Some act -> actions <- act :: actions
                        | _ -> ()

                        let counterResult = parseCounter trimmed
                        match counterResult with
                        | Some cond, Some act ->
                            conditions <- cond :: conditions
                            actions <- act :: actions
                        | Some cond, None -> conditions <- cond :: conditions
                        | None, Some act -> actions <- act :: actions
                        | _ -> ()

                    return Some (List.rev conditions, List.rev actions)
                with
                | ex ->
                    logger.LogWarning(ex, "Failed to parse special instruction: {Content}", content)
                    return None
            }

    /// ILogicAnalyzer 인터페이스 구현
    interface ILogicAnalyzer with
        /// 단일 Rung 분석
        member this.AnalyzeRungAsync(rung: RawLogic) =
            task {
                try
                    let analyzer = this :> IMxLogicAnalyzer
                    let result = analyzer.ParseSpecialInstructionAsync(rung.Content) |> Async.AwaitTask |> Async.RunSynchronously

                    match result with
                    | Some (conditions, actions) when conditions.Length > 0 || actions.Length > 0 ->
                        let flowType = detectLogicFlowType rung.Content

                        return Some {
                            Id = Guid.NewGuid().ToString()
                            Type = flowType
                            InputVariables = conditions |> List.map (fun c -> c.Variable) |> List.distinct
                            OutputVariables = actions |> List.map (fun a -> a.Variable) |> List.distinct
                            Conditions = conditions
                            Actions = actions
                            Sequence = rung.Number
                            Description = rung.Name |> Option.defaultValue "MX Rung"
                        }
                    | _ ->
                        return None
                with
                | ex ->
                    logger.LogError(ex, "Error analyzing Mitsubishi rung: {Name}", rung.Name)
                    return None
            }

        /// Rung에서 조건 추출
        member this.ExtractConditionsAsync(rung: RawLogic) =
            task {
                let analyzer = this :> IMxLogicAnalyzer
                let result = analyzer.ParseSpecialInstructionAsync(rung.Content) |> Async.AwaitTask |> Async.RunSynchronously

                match result with
                | Some (conditions, _) -> return conditions
                | None -> return []
            }

        /// Rung에서 액션 추출
        member this.ExtractActionsAsync(rung: RawLogic) =
            task {
                let analyzer = this :> IMxLogicAnalyzer
                let result = analyzer.ParseSpecialInstructionAsync(rung.Content) |> Async.AwaitTask |> Async.RunSynchronously

                match result with
                | Some (_, actions) -> return actions
                | None -> return []
            }

        /// 로직 흐름 타입 감지
        member _.DetectLogicFlowTypeAsync(rung: RawLogic) =
            Task.FromResult(detectLogicFlowType rung.Content)

        /// 입력/출력 변수 추출
        member this.ExtractIOVariablesAsync(rung: RawLogic) =
            task {
                let analyzer = this :> IMxLogicAnalyzer
                let result = analyzer.ParseSpecialInstructionAsync(rung.Content) |> Async.AwaitTask |> Async.RunSynchronously

                match result with
                | Some (conditions, actions) ->
                    let inputs = conditions |> List.map (fun c -> c.Variable) |> List.distinct
                    let outputs = actions |> List.map (fun a -> a.Variable) |> List.distinct
                    return (inputs, outputs)
                | None ->
                    return ([], [])
            }

        /// 배치 로직 분석
        member this.AnalyzeRungsBatchAsync(rungs: RawLogic list) =
            task {
                let! results =
                    rungs
                    |> List.map (fun rung -> (this :> IMxLogicAnalyzer).AnalyzeRungAsync(rung))
                    |> Task.WhenAll

                return results |> Array.choose id |> Array.toList
            }

        /// 시퀀스 분석
        member this.AnalyzeSequenceAsync(rungs: RawLogic list) =
            task {
                let! logicFlows = (this :> IMxLogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

                // 의존성 분석
                let dependencies =
                    logicFlows
                    |> List.mapi (fun i flow ->
                        let outputVars = flow.Actions |> List.map (fun a -> a.Variable)
                        let dependents =
                            logicFlows
                            |> List.skip (i + 1)
                            |> List.filter (fun nextFlow ->
                                nextFlow.Conditions
                                |> List.exists (fun c -> List.contains c.Variable outputVars)
                            )
                            |> List.map (fun f -> f.Id)
                        (flow.Id, dependents)
                    )
                    |> Map.ofList

                // 병렬 실행 가능 그룹 찾기
                let parallelGroups =
                    logicFlows
                    |> List.groupBy (fun flow ->
                        dependencies
                        |> Map.tryFind flow.Id
                        |> Option.defaultValue []
                        |> List.isEmpty
                    )
                    |> List.map snd

                // 통계 계산
                let statistics = {
                    TotalRungs = rungs.Length
                    TotalConditions = logicFlows |> List.sumBy (fun f -> f.Conditions.Length)
                    TotalActions = logicFlows |> List.sumBy (fun f -> f.Actions.Length)
                    AverageConditionsPerRung =
                        if logicFlows.Length > 0 then
                            float (logicFlows |> List.sumBy (fun f -> f.Conditions.Length)) / float logicFlows.Length
                        else 0.0
                    AverageActionsPerRung =
                        if logicFlows.Length > 0 then
                            float (logicFlows |> List.sumBy (fun f -> f.Actions.Length)) / float logicFlows.Length
                        else 0.0
                    CyclomaticComplexity =
                        logicFlows |> List.sumBy (fun f -> f.Conditions.Length + 1)
                }

                return {
                    LogicFlows = logicFlows
                    ExecutionOrder = logicFlows |> List.map (fun f -> f.Id)
                    Dependencies = dependencies
                    ParallelGroups = parallelGroups |> List.map (fun g -> g |> List.map (fun f -> f.Id))
                    Statistics = statistics
                }
            }

        /// API 의존성 추출
        member this.ExtractApiDependenciesAsync(rungs: RawLogic list, devices: Device list) =
            task {
                let! logicFlows = (this :> IMxLogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

                let dependencies =
                    logicFlows
                    |> List.collect (fun flow ->
                        flow.Actions
                        |> List.map (fun action ->
                            let device =
                                devices
                                |> List.tryFind (fun d ->
                                    // Simple heuristic: check if action variable contains device name
                                    action.Variable.Contains(d.Name))
                                |> Option.defaultValue (Device.Create("Unknown", DeviceType.Custom "Unknown", "Unknown"))

                            {
                                Api = action.Variable  // Required field
                                Device = device.Name
                                SourceDevice = device.Name
                                TargetApi = action.Variable
                                DependencyType =
                                    match action.Operation with
                                    | ActionOperation.Call -> "Call"
                                    | ActionOperation.Assign -> "Write"
                                    | _ -> "Control"
                                Parameters = action.Value |> Option.map (fun v -> [v]) |> Option.defaultValue []
                                IsRequired = true
                                PrecedingApis = []
                                InterlockApis = []
                                SafetyInterlocks = []
                                TimingConstraints = []
                                Description = action.Description
                            }
                        )
                    )

                return dependencies
            }

/// Mitsubishi 로직 분석기 팩토리
module MxLogicAnalyzerFactory =
    /// 로직 분석기 생성
    let create (logger: ILogger<MxLogicAnalyzer>) : ILogicAnalyzer =
        MxLogicAnalyzer(logger) :> ILogicAnalyzer