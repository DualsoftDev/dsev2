namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// Allen-Bradley 로직 분석기 구현
type ABLogicAnalyzer(logger: ILogger<ABLogicAnalyzer>) =

    /// 명령어 파싱용 정규식 (NAME(param1,param2,...))
    let instructionPattern = @"([A-Z]+)\(([^)]*)\)"
    let instructionRegex = Regex(instructionPattern, RegexOptions.Compiled)

    /// 브랜치 파싱용 정규식 ([...])
    let branchPattern = @"\[([^\[\]]*)\]"
    let branchRegex = Regex(branchPattern, RegexOptions.Compiled)

    /// 변수 이름 정규화 (공백 제거)
    let normalizeVariable (varName: string) =
        varName.Trim()

    /// 매개변수 파싱 (쉼표로 분리, 배열 인덱스 고려)
    let parseParameters (paramStr: string) : string list =
        if String.IsNullOrWhiteSpace(paramStr) then
            []
        else
            // 괄호 깊이를 추적하여 쉼표 분리
            let mutable depth = 0
            let mutable currentParam = ""
            let mutable parameters = []

            for c in paramStr do
                match c with
                | '[' | '(' ->
                    depth <- depth + 1
                    currentParam <- currentParam + string c
                | ']' | ')' ->
                    depth <- depth - 1
                    currentParam <- currentParam + string c
                | ',' when depth = 0 ->
                    if currentParam.Length > 0 then
                        parameters <- currentParam.Trim() :: parameters
                    currentParam <- ""
                | _ ->
                    currentParam <- currentParam + string c

            if currentParam.Length > 0 then
                parameters <- currentParam.Trim() :: parameters

            List.rev parameters

    /// XIC (Examine If Closed) 명령어 파싱
    let parseXIC (instruction: string) : Condition option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "XIC" then
            let variable = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = $"XIC: {variable}"
            }
        else
            None

    /// XIO (Examine If Open) 명령어 파싱
    let parseXIO (instruction: string) : Condition option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "XIO" then
            let variable = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operator = ConditionOperator.Not
                Value = "False"
                Description = $"XIO: {variable}"
            }
        else
            None

    /// 비교 명령어 파싱 (EQU, NEQ, GRT, LES, GEQ, LEQ)
    let parseComparison (instruction: string) : Condition option =
        let m = instructionRegex.Match(instruction)
        if m.Success then
            let opName = m.Groups.[1].Value
            let parameters = parseParameters m.Groups.[2].Value

            match opName, parameters with
            | "EQU", [value; variable] | "EQU", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.Equal
                    Value = value
                    Description = $"EQU: {variable} = {value}"
                }
            | "NEQ", [value; variable] | "NEQ", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.NotEqual
                    Value = value
                    Description = $"NEQ: {variable} <> {value}"
                }
            | "GRT", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.GreaterThan
                    Value = value
                    Description = $"GRT: {variable} > {value}"
                }
            | "LES", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.LessThan
                    Value = value
                    Description = $"LES: {variable} < {value}"
                }
            | "GEQ", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.GreaterOrEqual
                    Value = value
                    Description = $"GEQ: {variable} >= {value}"
                }
            | "LEQ", [variable; value] ->
                Some {
                    Variable = normalizeVariable variable
                    Operator = ConditionOperator.LessOrEqual
                    Value = value
                    Description = $"LEQ: {variable} <= {value}"
                }
            | _ -> None
        else
            None

    /// OTE (Output Energize) 명령어 파싱
    let parseOTE (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "OTE" then
            let variable = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Assign
                Value = Some "True"
                Delay = None
                Description = $"OTE: {variable}"
            }
        else
            None

    /// OTL (Output Latch) 명령어 파싱
    let parseOTL (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "OTL" then
            let variable = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Set
                Value = Some "True"
                Delay = None
                Description = $"OTL: {variable}"
            }
        else
            None

    /// OTU (Output Unlatch) 명령어 파싱
    let parseOTU (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "OTU" then
            let variable = normalizeVariable m.Groups.[2].Value
            Some {
                Variable = variable
                Operation = ActionOperation.Reset
                Value = Some "False"
                Delay = None
                Description = $"OTU: {variable}"
            }
        else
            None

    /// MOV (Move) 명령어 파싱
    let parseMOV (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success && m.Groups.[1].Value = "MOV" then
            let parameters = parseParameters m.Groups.[2].Value
            match parameters with
            | [source; dest] ->
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some source
                    Delay = None
                    Description = $"MOV: {dest} := {source}"
                }
            | _ -> None
        else
            None

    /// 산술 연산 명령어 파싱 (ADD, SUB, MUL, DIV)
    let parseArithmetic (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success then
            let opName = m.Groups.[1].Value
            let parameters = parseParameters m.Groups.[2].Value

            match opName, parameters with
            | "ADD", [a; b; dest] ->
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"({a} + {b})"
                    Delay = None
                    Description = $"ADD: {dest} := {a} + {b}"
                }
            | "SUB", [a; b; dest] ->
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"({a} - {b})"
                    Delay = None
                    Description = $"SUB: {dest} := {a} - {b}"
                }
            | "MUL", [a; b; dest] ->
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"({a} * {b})"
                    Delay = None
                    Description = $"MUL: {dest} := {a} * {b}"
                }
            | "DIV", [a; b; dest] ->
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"({a} / {b})"
                    Delay = None
                    Description = $"DIV: {dest} := {a} / {b}"
                }
            | _ -> None
        else
            None

    /// 기타 명령어 파싱 (CLR, COP, FLL, JSR, RET, TON 등)
    let parseOtherInstruction (instruction: string) : Action option =
        let m = instructionRegex.Match(instruction)
        if m.Success then
            let opName = m.Groups.[1].Value
            let parameters = parseParameters m.Groups.[2].Value

            match opName with
            | "CLR" when parameters.Length = 1 ->
                Some {
                    Variable = normalizeVariable parameters.[0]
                    Operation = ActionOperation.Reset
                    Value = Some "0"
                    Delay = None
                    Description = $"CLR: {parameters.[0]}"
                }
            | "COP" when parameters.Length = 3 ->
                let [source; dest; length] = parameters
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"Copy({source}, {length})"
                    Delay = None
                    Description = $"COP: Copy {length} from {source} to {dest}"
                }
            | "FLL" when parameters.Length = 3 ->
                let [value; dest; length] = parameters
                Some {
                    Variable = normalizeVariable dest
                    Operation = ActionOperation.Assign
                    Value = Some $"Fill({value}, {length})"
                    Delay = None
                    Description = $"FLL: Fill {dest} with {value} ({length} elements)"
                }
            | "JSR" ->
                // JSR은 서브루틴 호출 - Call operation
                let routine = if parameters.Length > 0 then parameters.[0] else "Unknown"
                Some {
                    Variable = routine
                    Operation = ActionOperation.Call
                    Value = None
                    Delay = None
                    Description = $"JSR: Call {routine}"
                }
            | "RET" ->
                // RET은 리턴
                Some {
                    Variable = "Return"
                    Operation = ActionOperation.Jump
                    Value = None
                    Delay = None
                    Description = "RET: Return from subroutine"
                }
            | "TON" when parameters.Length >= 1 ->
                // TON(Timer, ?, ?) - Timer On Delay
                let timer = normalizeVariable parameters.[0]
                Some {
                    Variable = timer
                    Operation = ActionOperation.Call
                    Value = Some "TimerOn"
                    Delay = None
                    Description = $"TON: Timer {timer}"
                }
            | "TOF" when parameters.Length >= 1 ->
                // TOF(Timer, ?, ?) - Timer Off Delay
                let timer = normalizeVariable parameters.[0]
                Some {
                    Variable = timer
                    Operation = ActionOperation.Call
                    Value = Some "TimerOff"
                    Delay = None
                    Description = $"TOF: Timer {timer}"
                }
            | "CTU" when parameters.Length >= 1 ->
                // CTU - Count Up
                let counter = normalizeVariable parameters.[0]
                Some {
                    Variable = counter
                    Operation = ActionOperation.Increment
                    Value = None
                    Delay = None
                    Description = $"CTU: Count up {counter}"
                }
            | "CTD" when parameters.Length >= 1 ->
                // CTD - Count Down
                let counter = normalizeVariable parameters.[0]
                Some {
                    Variable = counter
                    Operation = ActionOperation.Decrement
                    Value = None
                    Delay = None
                    Description = $"CTD: Count down {counter}"
                }
            | "ONS" when parameters.Length = 1 ->
                // ONS - One Shot (특수 조건으로 처리할 수도 있지만 액션으로 분류)
                let bit = normalizeVariable parameters.[0]
                Some {
                    Variable = bit
                    Operation = ActionOperation.Call
                    Value = Some "OneShot"
                    Delay = None
                    Description = $"ONS: One shot {bit}"
                }
            | "NOP" ->
                // NOP - No Operation (건너뜀)
                None
            | _ ->
                // 알 수 없는 명령어는 Call로 처리
                logger.LogDebug("Unknown instruction: {Instruction}", opName)
                None
        else
            None

    /// 단일 명령어 파싱 (조건 또는 액션)
    let parseInstruction (instruction: string) : (Condition option * Action option) =
        // 조건 명령어 체크
        let condition =
            parseXIC instruction
            |> Option.orElse (parseXIO instruction)
            |> Option.orElse (parseComparison instruction)

        // 액션 명령어 체크
        let action =
            parseOTE instruction
            |> Option.orElse (parseOTL instruction)
            |> Option.orElse (parseOTU instruction)
            |> Option.orElse (parseMOV instruction)
            |> Option.orElse (parseArithmetic instruction)
            |> Option.orElse (parseOtherInstruction instruction)

        (condition, action)

    /// Rung 내용에서 모든 명령어 추출
    let extractInstructions (rungContent: string) : (Condition list * Action list) =
        let mutable conditions = []
        let mutable actions = []

        try
            // N: 접두사 제거 및 세미콜론 제거
            let content = rungContent.Trim().Replace("N:", "").Replace(";", "").Trim()

            // 모든 명령어 매칭
            for m in instructionRegex.Matches(content) do
                let instruction = m.Value
                match parseInstruction instruction with
                | Some cond, _ -> conditions <- cond :: conditions
                | _, Some act -> actions <- act :: actions
                | None, None -> ()

        with
        | ex ->
            logger.LogWarning(ex, "Failed to extract instructions from rung: {Content}", rungContent)

        (List.rev conditions, List.rev actions)

    /// 로직 흐름 타입 감지
    let detectLogicFlowType (conditions: Condition list) (actions: Action list) : LogicFlowType =
        // Safety 키워드 감지
        let hasSafetyKeywords =
            let allTexts =
                (conditions |> List.map (fun c -> c.Variable + c.Description)) @
                (actions |> List.map (fun a -> a.Variable + a.Description))
            allTexts |> List.exists (fun text ->
                let lower = text.ToLower()
                lower.Contains("emergency") ||
                lower.Contains("safety") ||
                lower.Contains("interlock") ||
                lower.Contains("alarm") ||
                lower.Contains("error"))

        if hasSafetyKeywords then
            LogicFlowType.Safety
        elif actions |> List.exists (fun a -> a.Operation = Jump || a.Operation = Call) then
            LogicFlowType.Conditional
        elif conditions.Length > 5 then
            LogicFlowType.Conditional
        else
            LogicFlowType.Sequential

    /// 입력/출력 변수 추출
    let extractIOVariables (conditions: Condition list) (actions: Action list) : (string list * string list) =
        let inputVars = conditions |> List.map (fun c -> c.Variable) |> List.distinct
        let outputVars = actions |> List.map (fun a -> a.Variable) |> List.distinct
        (inputVars, outputVars)

    interface IABLogicAnalyzer with
        member this.SupportedVendor = PlcVendor.CreateAllenBradley()

        member this.CanAnalyze(logicType: LogicType) =
            match logicType with
            | LadderRung | StructuredText -> true
            | _ -> false

        member this.ParseSpecialInstructionAsync(content: string) = task {
            let (conditions, actions) = extractInstructions content
            return Some (conditions, actions)
        }

        member this.ParseXICAsync(instruction: string) = task {
            return parseXIC instruction
        }

        member this.ParseXIOAsync(instruction: string) = task {
            return parseXIO instruction
        }

        member this.ParseOTEAsync(instruction: string) = task {
            return parseOTE instruction
        }

        member this.ParseOTLAsync(instruction: string) = task {
            return parseOTL instruction
        }

        member this.ParseOTUAsync(instruction: string) = task {
            return parseOTU instruction
        }

        member this.ParseMOVAsync(instruction: string) = task {
            return parseMOV instruction
        }

        member this.ParseArithmeticAsync(instruction: string) = task {
            return parseArithmetic instruction
        }

        member this.ParseComparisonAsync(instruction: string) = task {
            return parseComparison instruction
        }

        member this.AnalyzeRungAsync(rung: RawLogic) = task {
            try
                let (conditions, actions) = extractInstructions rung.Content

                if conditions.IsEmpty && actions.IsEmpty then
                    logger.LogDebug("No conditions or actions found in rung {RungId}", rung.Id)
                    return None
                else
                    let flowType = detectLogicFlowType conditions actions
                    let (inputVars, outputVars) = extractIOVariables conditions actions

                    let logicFlow = {
                        Id = rung.Id |> Option.defaultValue ""
                        Type = flowType
                        InputVariables = inputVars
                        OutputVariables = outputVars
                        Conditions = conditions
                        Actions = actions
                        Sequence = 0  // Will be set by sequence analyzer
                        Description = sprintf "Rung %s: %d conditions, %d actions" (rung.Id |> Option.defaultValue "") conditions.Length actions.Length
                    }

                    logger.LogDebug("Analyzed AB rung {RungId}: {CondCount} conditions, {ActionCount} actions",
                                   rung.Id, conditions.Length, actions.Length)

                    return Some logicFlow
            with
            | ex ->
                logger.LogError(ex, "Error analyzing AB rung {RungId}", rung.Id)
                return None
        }

        member this.ExtractConditionsAsync(rung: RawLogic) = task {
            let (conditions, _) = extractInstructions rung.Content
            return conditions
        }

        member this.ExtractActionsAsync(rung: RawLogic) = task {
            let (_, actions) = extractInstructions rung.Content
            return actions
        }

        member this.DetectLogicFlowTypeAsync(rung: RawLogic) = task {
            let (conditions, actions) = extractInstructions rung.Content
            return detectLogicFlowType conditions actions
        }

        member this.ExtractIOVariablesAsync(rung: RawLogic) = task {
            let (conditions, actions) = extractInstructions rung.Content
            return extractIOVariables conditions actions
        }

        member this.AnalyzeRungsBatchAsync(rungs: RawLogic list) = task {
            let! results =
                rungs
                |> List.map (fun rung -> (this :> IABLogicAnalyzer).AnalyzeRungAsync(rung))
                |> Task.WhenAll

            let logicFlows =
                results
                |> Array.choose id
                |> Array.mapi (fun i flow -> { flow with Sequence = i })
                |> Array.toList

            logger.LogInformation("Batch analyzed {Count} AB rungs, produced {FlowCount} logic flows",
                                rungs.Length, logicFlows.Length)

            return logicFlows
        }

        member this.AnalyzeSequenceAsync(rungs: RawLogic list) = task {
            let! logicFlows = (this :> IABLogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

            // 안전 시퀀스 감지
            let safetySequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Safety)

            // 전역 시퀀스 (모든 로직 흐름)
            let globalSequence = logicFlows

            // 통계 계산
            // 통계 계산
            let totalConditions = logicFlows |> List.sumBy (fun f -> f.Conditions.Length)
            let totalActions = logicFlows |> List.sumBy (fun f -> f.Actions.Length)
            let statistics = {
                TotalRungs = logicFlows.Length
                TotalConditions = totalConditions
                TotalActions = totalActions
                AverageConditionsPerRung =
                    if logicFlows.IsEmpty then 0.0
                    else float totalConditions / float logicFlows.Length
                AverageActionsPerRung =
                    if logicFlows.IsEmpty then 0.0
                    else float totalActions / float logicFlows.Length
                CyclomaticComplexity = totalConditions + 1  // Basic complexity calculation
            }

            // 실행 순서 추출
            let executionOrder = logicFlows |> List.map (fun f -> f.Id)

            // 의존성 맵 구성 (간단한 구현)
            let dependencies = Map.empty<string, string list>

            // 병렬 그룹 (간단한 구현 - 독립적인 플로우들)
            let parallelGroups = []

            let sequenceAnalysis = {
                LogicFlows = logicFlows
                ExecutionOrder = executionOrder
                Dependencies = dependencies
                ParallelGroups = parallelGroups
                Statistics = statistics
            }

            logger.LogInformation("AB Sequence analysis complete: {TotalRungs} rungs, {SafetySeq} safety sequences",
                                statistics.TotalRungs, safetySequences.Length)

            return sequenceAnalysis
        }

        member this.ExtractApiDependenciesAsync(rungs: RawLogic list, devices: Device list) = task {
            // API 의존성 추출은 고급 분석기에서 수행
            return []
        }

/// Allen-Bradley 로직 분석기 팩토리
module ABLogicAnalyzerFactory =
    let create (logger: ILogger<ABLogicAnalyzer>) : IABLogicAnalyzer =
        ABLogicAnalyzer(logger) :> IABLogicAnalyzer
