namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// Siemens S7 로직 분석기 인터페이스
type IS7LogicAnalyzer =
    inherit IVendorLogicAnalyzer

    /// A (AND) 명령어 파싱
    abstract member ParseAAsync: instruction: string -> Task<Condition option>

    /// AN (AND NOT) 명령어 파싱
    abstract member ParseANAsync: instruction: string -> Task<Condition option>

    /// O (OR) 명령어 파싱
    abstract member ParseOAsync: instruction: string -> Task<Condition option>

    /// ON (OR NOT) 명령어 파싱
    abstract member ParseONAsync: instruction: string -> Task<Condition option>

    /// = (Assign) 명령어 파싱
    abstract member ParseAssignAsync: instruction: string -> Task<Action option>

    /// S (Set) 명령어 파싱
    abstract member ParseSetAsync: instruction: string -> Task<Action option>

    /// R (Reset) 명령어 파싱
    abstract member ParseResetAsync: instruction: string -> Task<Action option>

    /// 비교 명령어 파싱
    abstract member ParseComparisonAsync: instruction: string * leftOperand: string option * rightOperand: string option -> Task<Condition option>

/// Siemens S7 로직 분석기 구현
type S7LogicAnalyzer(logger: ILogger<S7LogicAnalyzer>) =

    /// S7 STL 명령어 패턴
    let anPattern = @"^\s*A\s+(\S+)"        // A (AND)
    let anNotPattern = @"^\s*AN\s+(\S+)"    // AN (AND NOT)
    let orPattern = @"^\s*O\s+(\S+)"        // O (OR)
    let orNotPattern = @"^\s*ON\s+(\S+)"    // ON (OR NOT)
    let assignPattern = @"^\s*=\s+(\S+)"    // = (Assign)
    let setPattern = @"^\s*S\s+(\S+)"       // S (Set)
    let resetPattern = @"^\s*R\s+(\S+)"     // R (Reset)
    let loadPattern = @"^\s*L\s+(\S+)"      // L (Load)
    let transferPattern = @"^\s*T\s+(\S+)"  // T (Transfer)

    /// 비교 명령어 패턴
    let cmpEqPattern = @"^\s*==I"           // Equal Integer
    let cmpNePattern = @"^\s*<>I"           // Not Equal Integer
    let cmpGtPattern = @"^\s*>I"            // Greater Than Integer
    let cmpLtPattern = @"^\s*<I"            // Less Than Integer
    let cmpGePattern = @"^\s*>=I"           // Greater or Equal Integer
    let cmpLePattern = @"^\s*<=I"           // Less or Equal Integer

    /// Timer 명령어 패턴
    let tonPattern = @"^\s*TON\s+(\S+)"     // Timer ON delay
    let tofPattern = @"^\s*TOF\s+(\S+)"     // Timer OFF delay
    let tpPattern = @"^\s*TP\s+(\S+)"       // Timer Pulse

    /// Counter 명령어 패턴
    let ctuPattern = @"^\s*CTU\s+(\S+)"     // Count Up
    let ctdPattern = @"^\s*CTD\s+(\S+)"     // Count Down
    let ctudPattern = @"^\s*CTUD\s+(\S+)"   // Count Up/Down

    /// SCL (Structured Control Language) 패턴
    let ifPattern = @"^\s*IF\s+(.+)\s+THEN"
    let elseifPattern = @"^\s*ELSIF\s+(.+)\s+THEN"
    let elsePattern = @"^\s*ELSE"
    let endifPattern = @"^\s*END_IF"
    let assignmentPattern = @"^\s*(\S+)\s*:=\s*(.+);"
    let callPattern = @"^\s*CALL\s+(\S+)\s*\(([^)]*)\)"

    /// LAD (Ladder) 요소 패턴
    let contactPattern = @"\[([/]?)(\S+?)\]"    // Contact: [tag] or [/tag]
    let coilPattern = @"\(([/]?)(\S+?)\)"       // Coil: (tag) or (/tag)

    /// 변수 이름 정규화
    let normalizeVariable (varName: string) =
        varName.Trim().Replace("\"", "")

    /// STL AND 명령어 파싱
    let parseAN (instruction: string) : Condition option =
        let m = Regex.Match(instruction, anPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"A: {variable}"
            }
        else
            None

    /// STL AND NOT 명령어 파싱
    let parseANNot (instruction: string) : Condition option =
        let m = Regex.Match(instruction, anNotPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"AN: {variable}"
            }
        else
            None

    /// STL OR 명령어 파싱
    let parseOR (instruction: string) : Condition option =
        let m = Regex.Match(instruction, orPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"O: {variable}"
            }
        else
            None

    /// STL OR NOT 명령어 파싱
    let parseORNot (instruction: string) : Condition option =
        let m = Regex.Match(instruction, orNotPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"ON: {variable}"
            }
        else
            None

    /// STL Assign 명령어 파싱
    let parseAssign (instruction: string) : Action option =
        let m = Regex.Match(instruction, assignPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Assign
                Value = Some "True"
                Delay = None
                Description = $"=: {variable}"
            }
        else
            None

    /// STL Set 명령어 파싱
    let parseSet (instruction: string) : Action option =
        let m = Regex.Match(instruction, setPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Set
                Value = Some "True"
                Delay = None
                Description = $"S: {variable}"
            }
        else
            None

    /// STL Reset 명령어 파싱
    let parseReset (instruction: string) : Action option =
        let m = Regex.Match(instruction, resetPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Reset
                Value = Some "False"
                Delay = None
                Description = $"R: {variable}"
            }
        else
            None

    /// STL Transfer 명령어 파싱
    let parseTransfer (instruction: string, loadedValue: string option) : Action option =
        let m = Regex.Match(instruction, transferPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Assign
                Value = loadedValue
                Delay = None
                Description = sprintf "T: %s -> %s" (loadedValue |> Option.defaultValue "") variable
            }
        else
            None

    /// 비교 명령어 파싱
    let parseComparison (instruction: string) (leftOperand: string option) (rightOperand: string option) : Condition option =
        let operator, opName =
            if Regex.IsMatch(instruction, cmpEqPattern) then (ConditionOperator.Equal, "==")
            elif Regex.IsMatch(instruction, cmpNePattern) then (ConditionOperator.NotEqual, "<>")
            elif Regex.IsMatch(instruction, cmpGtPattern) then (ConditionOperator.Greater, ">")
            elif Regex.IsMatch(instruction, cmpLtPattern) then (ConditionOperator.Less, "<")
            elif Regex.IsMatch(instruction, cmpGePattern) then (ConditionOperator.GreaterOrEqual, ">=")
            elif Regex.IsMatch(instruction, cmpLePattern) then (ConditionOperator.LessOrEqual, "<=")
            else (ConditionOperator.Equal, "?")

        match leftOperand, rightOperand with
        | Some left, Some right ->
            Some {
                Variable = left
                Operator = operator
                Value = right
                Description = $"{left} {opName} {right}"
            }
        | _ -> None

    /// SCL IF 문 파싱
    let parseSclIf (instruction: string) : Condition option =
        let m = Regex.Match(instruction, ifPattern)
        if m.Success then
            let condition = m.Groups.[1].Value.Trim()
            // 간단한 조건 파싱 (variable = value 형태)
            let condParts = condition.Split([|'='; '<'; '>'; '!'|])
            if condParts.Length >= 2 then
                Some {
                    Variable = normalizeVariable condParts.[0]
                    Operator = ConditionOperator.Equal
                    Value = normalizeVariable condParts.[1]
                    Description = $"IF {condition}"
                }
            else
                None
        else
            None

    /// SCL 할당문 파싱
    let parseSclAssignment (instruction: string) : Action option =
        let m = Regex.Match(instruction, assignmentPattern)
        if m.Success then
            let variable = normalizeVariable m.Groups.[1].Value
            let value = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Assign
                Value = Some value
                Delay = None
                Description = $"{variable} := {value}"
            }
        else
            None

    /// LAD Contact 파싱 (multiple contacts in one line)
    let parseLadContacts (instruction: string) : Condition list =
        let matches = Regex.Matches(instruction, contactPattern)
        [ for m in matches do
            let isNegated = m.Groups.[1].Value = "/"
            let variable = normalizeVariable m.Groups.[2].Value
            yield {
                Variable = variable
                Operator = if isNegated then ConditionOperator.Not else ConditionOperator.Equal
                Value = if isNegated then "False" else "True"
                Description = sprintf "Contact %s: %s" (if isNegated then "NC" else "NO") variable
            }
        ]

    /// LAD Coil 파싱 (multiple coils in one line)
    let parseLadCoils (instruction: string) : Action list =
        let matches = Regex.Matches(instruction, coilPattern)
        [ for m in matches do
            let isNegated = m.Groups.[1].Value = "/"
            let variable = normalizeVariable m.Groups.[2].Value
            yield {
                Variable = variable
                Operation = if isNegated then ActionOperation.Reset else ActionOperation.Assign
                Value = Some (if isNegated then "False" else "True")
                Delay = None
                Description = sprintf "Coil %s: %s" (if isNegated then "Negated" else "Normal") variable
            }
        ]

    /// Timer 명령어 파싱
    let parseTimer (instruction: string) : (Condition option * Action option) =
        let patterns = [
            (tonPattern, "TON", true)
            (tofPattern, "TOF", false)
            (tpPattern, "TP", true)
        ]

        patterns
        |> List.tryPick (fun (pattern, timerType, outputWhenDone) ->
            let m = Regex.Match(instruction, pattern)
            if m.Success then
                let timerName = normalizeVariable m.Groups.[1].Value

                let condition = Some {
                    Variable = $"{timerName}.Q"
                    Operator = ConditionOperator.Equal
                    Value = if outputWhenDone then "True" else "False"
                    Description = $"{timerType} done: {timerName}"
                }

                let action = Some {
                    Variable = timerName
                    Operation = ActionOperation.Call
                    Value = Some "1000"  // Default 1 second
                    Delay = Some (TimeSpan.FromMilliseconds(1000.0))
                    Description = $"Start {timerType}: {timerName}"
                }

                Some (condition, action)
            else
                None
        )
        |> Option.defaultValue (None, None)

    /// Counter 명령어 파싱
    let parseCounter (instruction: string) : (Condition option * Action option) =
        let patterns = [
            (ctuPattern, "CTU", ActionOperation.Increment, ConditionOperator.GreaterOrEqual)
            (ctdPattern, "CTD", ActionOperation.Decrement, ConditionOperator.LessOrEqual)
            (ctudPattern, "CTUD", ActionOperation.Increment, ConditionOperator.Equal)
        ]

        patterns
        |> List.tryPick (fun (pattern, counterType, operation, operator) ->
            let m = Regex.Match(instruction, pattern)
            if m.Success then
                let counterName = normalizeVariable m.Groups.[1].Value

                let condition = Some {
                    Variable = $"{counterName}.Q"
                    Operator = operator
                    Value = "100"  // Default preset
                    Description = $"{counterType} done: {counterName}"
                }

                let action = Some {
                    Variable = counterName
                    Operation = operation
                    Value = None
                    Delay = None
                    Description = $"{counterType}: {counterName}"
                }

                Some (condition, action)
            else
                None
        )
        |> Option.defaultValue (None, None)

    /// 로직 흐름 타입 감지
    let detectLogicFlowType (content: string) =
        let hasTimers = Regex.IsMatch(content, @"TON|TOF|TP")
        let hasCounters = Regex.IsMatch(content, @"CTU|CTD|CTUD")
        let hasComparison = Regex.IsMatch(content, @"==|<>|>=|<=|>|<")
        let hasIfThen = Regex.IsMatch(content, @"IF.*THEN")
        let hasCall = Regex.IsMatch(content, @"CALL\s+")

        if hasTimers then LogicFlowType.Timer
        elif hasCounters then LogicFlowType.Counter
        elif hasIfThen then LogicFlowType.Conditional
        elif hasComparison then LogicFlowType.Conditional
        elif hasCall then LogicFlowType.Sequential
        else LogicFlowType.Simple

    /// 인터페이스 구현
    interface IS7LogicAnalyzer with
        /// 지원하는 제조사
        member _.SupportedVendor = PlcVendor.Siemens (Some SiemensModel.S7_1500, None)

        /// 특정 로직 타입 지원 여부
        member _.CanAnalyze(logicType: LogicType) =
            match logicType with
            | LogicType.LadderRung | LogicType.StructuredText | LogicType.FunctionBlock -> true
            | LogicType.InstructionList -> true
            | _ -> false

        /// A (AND) 명령어 파싱
        member _.ParseAAsync(instruction: string) =
            Task.FromResult(parseAN instruction)

        /// AN (AND NOT) 명령어 파싱
        member _.ParseANAsync(instruction: string) =
            Task.FromResult(parseANNot instruction)

        /// O (OR) 명령어 파싱
        member _.ParseOAsync(instruction: string) =
            Task.FromResult(parseOR instruction)

        /// ON (OR NOT) 명령어 파싱
        member _.ParseONAsync(instruction: string) =
            Task.FromResult(parseORNot instruction)

        /// = (Assign) 명령어 파싱
        member _.ParseAssignAsync(instruction: string) =
            Task.FromResult(parseAssign instruction)

        /// S (Set) 명령어 파싱
        member _.ParseSetAsync(instruction: string) =
            Task.FromResult(parseSet instruction)

        /// R (Reset) 명령어 파싱
        member _.ParseResetAsync(instruction: string) =
            Task.FromResult(parseReset instruction)

        /// 비교 명령어 파싱
        member _.ParseComparisonAsync(instruction: string, leftOperand: string option, rightOperand: string option) =
            Task.FromResult(parseComparison instruction leftOperand rightOperand)

        /// 특수 명령어 파싱
        member _.ParseSpecialInstructionAsync(content: string) =
            task {
                try
                    let lines = content.Split([|'\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
                    let mutable conditions = []
                    let mutable actions = []
                    let mutable loadedValue : string option = None

                    for line in lines do
                        let trimmed = line.Trim()

                        // STL 명령어 처리
                        // Load 명령어 처리 (다음 Transfer를 위해 값 저장)
                        let loadMatch = Regex.Match(trimmed, loadPattern)
                        if loadMatch.Success then
                            loadedValue <- Some (normalizeVariable loadMatch.Groups.[1].Value)

                        // 조건 명령어들
                        match trimmed with
                        | _ when parseAN trimmed |> Option.isSome ->
                            conditions <- (parseAN trimmed).Value :: conditions
                        | _ when parseANNot trimmed |> Option.isSome ->
                            conditions <- (parseANNot trimmed).Value :: conditions
                        | _ when parseOR trimmed |> Option.isSome ->
                            conditions <- (parseOR trimmed).Value :: conditions
                        | _ when parseORNot trimmed |> Option.isSome ->
                            conditions <- (parseORNot trimmed).Value :: conditions
                        | _ -> ()

                        // 액션 명령어들
                        match trimmed with
                        | _ when parseAssign trimmed |> Option.isSome ->
                            actions <- (parseAssign trimmed).Value :: actions
                        | _ when parseSet trimmed |> Option.isSome ->
                            actions <- (parseSet trimmed).Value :: actions
                        | _ when parseReset trimmed |> Option.isSome ->
                            actions <- (parseReset trimmed).Value :: actions
                        | _ ->
                            match parseTransfer (trimmed, loadedValue) with
                            | Some action ->
                                actions <- action :: actions
                                loadedValue <- None
                            | None -> ()

                        // SCL 명령어 처리
                        match trimmed with
                        | _ when parseSclIf trimmed |> Option.isSome ->
                            conditions <- (parseSclIf trimmed).Value :: conditions
                        | _ when parseSclAssignment trimmed |> Option.isSome ->
                            actions <- (parseSclAssignment trimmed).Value :: actions
                        | _ -> ()

                        // LAD 요소 처리 (multiple elements in one line)
                        let ladContacts = parseLadContacts trimmed
                        let ladCoils = parseLadCoils trimmed
                        if not ladContacts.IsEmpty then
                            // Reverse LAD contacts since the whole list will be reversed at the end
                            conditions <- conditions @ (List.rev ladContacts)
                        if not ladCoils.IsEmpty then
                            // Reverse LAD coils since the whole list will be reversed at the end
                            actions <- actions @ (List.rev ladCoils)

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
                    let analyzer = this :> IS7LogicAnalyzer
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
                            Description = rung.Name |> Option.defaultValue "S7 Network"
                        }
                    | _ ->
                        return None
                with
                | ex ->
                    logger.LogError(ex, "Error analyzing Siemens rung: {Name}", rung.Name)
                    return None
            }

        /// Rung에서 조건 추출
        member this.ExtractConditionsAsync(rung: RawLogic) =
            task {
                let analyzer = this :> IS7LogicAnalyzer
                let result = analyzer.ParseSpecialInstructionAsync(rung.Content) |> Async.AwaitTask |> Async.RunSynchronously

                match result with
                | Some (conditions, _) -> return conditions
                | None -> return []
            }

        /// Rung에서 액션 추출
        member this.ExtractActionsAsync(rung: RawLogic) =
            task {
                let analyzer = this :> IS7LogicAnalyzer
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
                let analyzer = this :> IS7LogicAnalyzer
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
                    |> List.map (fun rung -> (this :> IS7LogicAnalyzer).AnalyzeRungAsync(rung))
                    |> Task.WhenAll

                return results |> Array.choose id |> Array.toList
            }

        /// 시퀀스 분석
        member this.AnalyzeSequenceAsync(rungs: RawLogic list) =
            task {
                let! logicFlows = (this :> IS7LogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

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
                let! logicFlows = (this :> IS7LogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

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

/// Siemens S7 로직 분석기 팩토리
module S7LogicAnalyzerFactory =
    /// 로직 분석기 생성
    let create (logger: ILogger<S7LogicAnalyzer>) : ILogicAnalyzer =
        S7LogicAnalyzer(logger) :> ILogicAnalyzer