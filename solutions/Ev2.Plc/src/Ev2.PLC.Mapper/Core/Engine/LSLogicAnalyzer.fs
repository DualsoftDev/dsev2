namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Xml.Linq
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// LS Electric 로직 분석기 구현
type LSLogicAnalyzer(logger: ILogger<LSLogicAnalyzer>) =

    /// XML Element에서 속성 값 추출
    let getAttributeValue (element: XElement) (name: string) =
        element.Attribute(XName.Get name)
        |> Option.ofObj
        |> Option.map (fun attr -> attr.Value)

    /// Contact 요소 파싱 (XIC - Examine If Closed, XIO - Examine If Open)
    let parseContact (contactElement: string) : Condition option =
        try
            let doc = XDocument.Parse($"<root>{contactElement}</root>")
            let contactNode = doc.Root.Descendants() |> Seq.tryHead

            match contactNode with
            | Some node ->
                let variable = getAttributeValue node "Variable" |> Option.defaultValue ""
                let contactType = getAttributeValue node "Type" |> Option.defaultValue "NO"

                // NO (Normally Open) = Equal to True, NC (Normally Closed) = Equal to False
                let operator = if contactType = "NC" then Not else Equal

                Some {
                    Variable = variable
                    Operator = operator
                    Value = if operator = Not then "False" else "True"
                    Description = $"Contact {contactType}: {variable}"
                }
            | None -> None
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse contact element: {Content}", contactElement)
            None

    /// Coil 요소 파싱 (OTE - Output Energize, OTL - Output Latch, OTU - Output Unlatch)
    let parseCoil (coilElement: string) : Action option =
        try
            let doc = XDocument.Parse($"<root>{coilElement}</root>")
            let coilNode = doc.Root.Descendants() |> Seq.tryHead

            match coilNode with
            | Some node ->
                let variable = getAttributeValue node "Variable" |> Option.defaultValue ""
                let coilType = getAttributeValue node "Type" |> Option.defaultValue "OTE"

                let operation =
                    match coilType with
                    | "OTL" -> Set  // Output Latch = Set
                    | "OTU" -> Reset  // Output Unlatch = Reset
                    | _ -> Assign  // OTE = Assign

                Some {
                    Variable = variable
                    Operation = operation
                    Value = Some "True"
                    Delay = None
                    Description = $"Coil {coilType}: {variable}"
                }
            | None -> None
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse coil element: {Content}", coilElement)
            None

    /// Timer 요소 파싱 (TON - Timer On Delay, TOF - Timer Off Delay, TP - Pulse Timer)
    let parseTimer (timerElement: string) : (Condition option * Action option) =
        try
            let doc = XDocument.Parse($"<root>{timerElement}</root>")
            let timerNode = doc.Root.Descendants() |> Seq.tryHead

            match timerNode with
            | Some node ->
                let variable = getAttributeValue node "Variable" |> Option.defaultValue ""
                let timerType = getAttributeValue node "Type" |> Option.defaultValue "TON"
                let preset = getAttributeValue node "Preset" |> Option.defaultValue "0"

                // Timer의 .Q (Output) 비트를 조건으로 사용
                let condition = Some {
                    Variable = $"{variable}.Q"
                    Operator = Equal
                    Value = "True"
                    Description = $"Timer {timerType} done: {variable}"
                }

                // Timer 시작 액션
                let action = Some {
                    Variable = variable
                    Operation = Call
                    Value = Some preset
                    Delay =
                        match Int32.TryParse(preset) with
                        | true, ms -> Some (TimeSpan.FromMilliseconds(float ms))
                        | _ -> None
                    Description = $"Start timer {timerType}: {variable} ({preset}ms)"
                }

                (condition, action)
            | None -> (None, None)
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse timer element: {Content}", timerElement)
            (None, None)

    /// Counter 요소 파싱 (CTU - Count Up, CTD - Count Down, CTUD - Count Up/Down)
    let parseCounter (counterElement: string) : (Condition option * Action option) =
        try
            let doc = XDocument.Parse($"<root>{counterElement}</root>")
            let counterNode = doc.Root.Descendants() |> Seq.tryHead

            match counterNode with
            | Some node ->
                let variable = getAttributeValue node "Variable" |> Option.defaultValue ""
                let counterType = getAttributeValue node "Type" |> Option.defaultValue "CTU"
                let preset = getAttributeValue node "Preset" |> Option.defaultValue "0"

                // Counter의 .Q (Output) 비트를 조건으로 사용
                let condition = Some {
                    Variable = $"{variable}.Q"
                    Operator = GreaterOrEqual
                    Value = preset
                    Description = $"Counter {counterType} done: {variable} >= {preset}"
                }

                // Counter 증가/감소 액션
                let operation = if counterType = "CTD" then Decrement else Increment
                let action = Some {
                    Variable = variable
                    Operation = operation
                    Value = None
                    Delay = None
                    Description = $"{counterType}: {variable}"
                }

                (condition, action)
            | None -> (None, None)
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse counter element: {Content}", counterElement)
            (None, None)

    /// Compare 요소 파싱 (EQU, NEQ, GRT, LES, GEQ, LEQ)
    let parseCompare (compareElement: string) : Condition option =
        try
            let doc = XDocument.Parse($"<root>{compareElement}</root>")
            let compareNode = doc.Root.Descendants() |> Seq.tryHead

            match compareNode with
            | Some node ->
                let variable = getAttributeValue node "Variable" |> Option.defaultValue ""
                let compareType = getAttributeValue node "Type" |> Option.defaultValue "EQU"
                let value = getAttributeValue node "Value" |> Option.defaultValue "0"

                let operator =
                    match compareType with
                    | "EQU" -> Equal
                    | "NEQ" -> NotEqual
                    | "GRT" -> GreaterThan
                    | "LES" -> LessThan
                    | "GEQ" -> GreaterOrEqual
                    | "LEQ" -> LessOrEqual
                    | _ -> Equal

                Some {
                    Variable = variable
                    Operator = operator
                    Value = value
                    Description = $"Compare {compareType}: {variable} {operator.Symbol} {value}"
                }
            | None -> None
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse compare element: {Content}", compareElement)
            None

    /// Rung 내용에서 조건과 액션 추출
    let extractConditionsAndActions (rungContent: string) : (Condition list * Action list) =
        let mutable conditions = []
        let mutable actions = []

        try
            // Rung content를 XML로 파싱
            let doc = XDocument.Parse($"<root>{rungContent}</root>")

            // Contact 요소 추출
            for contactNode in doc.Descendants(XName.Get "Contact") do
                match parseContact (contactNode.ToString()) with
                | Some cond -> conditions <- cond :: conditions
                | None -> ()

            // Coil 요소 추출
            for coilNode in doc.Descendants(XName.Get "Coil") do
                match parseCoil (coilNode.ToString()) with
                | Some act -> actions <- act :: actions
                | None -> ()

            // Timer 요소 추출
            for timerNode in doc.Descendants(XName.Get "Timer") do
                match parseTimer (timerNode.ToString()) with
                | Some cond, Some act ->
                    conditions <- cond :: conditions
                    actions <- act :: actions
                | Some cond, None ->
                    conditions <- cond :: conditions
                | None, Some act ->
                    actions <- act :: actions
                | None, None -> ()

            // Counter 요소 추출
            for counterNode in doc.Descendants(XName.Get "Counter") do
                match parseCounter (counterNode.ToString()) with
                | Some cond, Some act ->
                    conditions <- cond :: conditions
                    actions <- act :: actions
                | Some cond, None ->
                    conditions <- cond :: conditions
                | None, Some act ->
                    actions <- act :: actions
                | None, None -> ()

            // Compare 요소 추출
            for compareNode in doc.Descendants(XName.Get "Compare") do
                match parseCompare (compareNode.ToString()) with
                | Some cond -> conditions <- cond :: conditions
                | None -> ()

        with
        | ex ->
            logger.LogWarning(ex, "Failed to extract conditions and actions from rung content")
            ()

        (List.rev conditions, List.rev actions)

    /// 로직 흐름 타입 감지
    let detectLogicFlowType (conditions: Condition list) (actions: Action list) : LogicFlowType =
        // Safety 키워드 감지
        let hasSafetyKeywords =
            let allTexts =
                (conditions |> List.map (fun c -> c.Variable + c.Description)) @
                (actions |> List.map (fun a -> a.Variable + a.Description))
            allTexts |> List.exists (fun text ->
                text.ToLower().Contains("emergency") ||
                text.ToLower().Contains("safety") ||
                text.ToLower().Contains("interlock") ||
                text.ToLower().Contains("err"))

        if hasSafetyKeywords then
            LogicFlowType.Safety
        elif actions |> List.exists (fun a -> a.Operation = Jump) then
            LogicFlowType.Conditional
        elif actions |> List.exists (fun a -> a.Delay.IsSome) then
            LogicFlowType.Sequential
        elif conditions.Length > 3 then
            LogicFlowType.Conditional
        else
            LogicFlowType.Sequential

    /// 입력/출력 변수 추출
    let extractIOVariables (conditions: Condition list) (actions: Action list) : (string list * string list) =
        let inputVars = conditions |> List.map (fun c -> c.Variable) |> List.distinct
        let outputVars = actions |> List.map (fun a -> a.Variable) |> List.distinct
        (inputVars, outputVars)

    interface ILSLogicAnalyzer with
        member this.SupportedVendor = PlcVendor.CreateLSElectric()

        member this.CanAnalyze(logicType: LogicType) =
            match logicType with
            | LadderRung | StructuredText -> true
            | _ -> false

        member this.ParseSpecialInstructionAsync(content: string) = task {
            let (conditions, actions) = extractConditionsAndActions content
            return Some (conditions, actions)
        }

        member this.ParseContactAsync(contactElement: string) = task {
            return parseContact contactElement
        }

        member this.ParseCoilAsync(coilElement: string) = task {
            return parseCoil coilElement
        }

        member this.ParseTimerAsync(timerElement: string) = task {
            return parseTimer timerElement
        }

        member this.ParseCounterAsync(counterElement: string) = task {
            return parseCounter counterElement
        }

        member this.ParseCompareAsync(compareElement: string) = task {
            return parseCompare compareElement
        }

        member this.AnalyzeRungAsync(rung: RawLogic) = task {
            try
                let (conditions, actions) = extractConditionsAndActions rung.Content

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

                    logger.LogDebug("Analyzed rung {RungId}: {CondCount} conditions, {ActionCount} actions",
                                   rung.Id, conditions.Length, actions.Length)

                    return Some logicFlow
            with
            | ex ->
                logger.LogError(ex, "Error analyzing rung {RungId}", rung.Id)
                return None
        }

        member this.ExtractConditionsAsync(rung: RawLogic) = task {
            let (conditions, _) = extractConditionsAndActions rung.Content
            return conditions
        }

        member this.ExtractActionsAsync(rung: RawLogic) = task {
            let (_, actions) = extractConditionsAndActions rung.Content
            return actions
        }

        member this.DetectLogicFlowTypeAsync(rung: RawLogic) = task {
            let (conditions, actions) = extractConditionsAndActions rung.Content
            return detectLogicFlowType conditions actions
        }

        member this.ExtractIOVariablesAsync(rung: RawLogic) = task {
            let (conditions, actions) = extractConditionsAndActions rung.Content
            return extractIOVariables conditions actions
        }

        member this.AnalyzeRungsBatchAsync(rungs: RawLogic list) = task {
            let! results =
                rungs
                |> List.map (fun rung -> (this :> ILSLogicAnalyzer).AnalyzeRungAsync(rung))
                |> Task.WhenAll

            let logicFlows =
                results
                |> Array.choose id
                |> Array.mapi (fun i flow -> { flow with Sequence = i })
                |> Array.toList

            logger.LogInformation("Batch analyzed {Count} rungs, produced {FlowCount} logic flows",
                                rungs.Length, logicFlows.Length)

            return logicFlows
        }

        member this.AnalyzeSequenceAsync(rungs: RawLogic list) = task {
            let! logicFlows = (this :> ILSLogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

            // 안전 시퀀스 감지
            let safetySequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Safety)

            // 전역 시퀀스 (모든 로직 흐름)
            let globalSequence = logicFlows

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

            logger.LogInformation("Sequence analysis complete: {TotalRungs} rungs, {TotalConditions} conditions, {TotalActions} actions",
                                statistics.TotalRungs, statistics.TotalConditions, statistics.TotalActions)

            return sequenceAnalysis
        }

        member this.ExtractApiDependenciesAsync(rungs: RawLogic list, devices: Device list) = task {
            // API 의존성 추출은 고급 분석기에서 수행
            // 여기서는 기본 구현만 제공
            return []
        }

/// LS Electric 로직 분석기 팩토리
module LSLogicAnalyzerFactory =
    let create (logger: ILogger<LSLogicAnalyzer>) : ILSLogicAnalyzer =
        LSLogicAnalyzer(logger) :> ILSLogicAnalyzer
