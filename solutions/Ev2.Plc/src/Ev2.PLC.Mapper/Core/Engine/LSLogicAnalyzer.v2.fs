namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Xml.Linq
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// LS Electric 로직 분석기 구현 (ElementType 기반)
type LSLogicAnalyzerV2(logger: ILogger<LSLogicAnalyzerV2>) =

    /// XML Element에서 속성 값 추출
    let getAttributeValue (element: XElement) (name: string) =
        element.Attribute(XName.Get name)
        |> Option.ofObj
        |> Option.map (fun attr -> attr.Value)

    /// ElementType 파싱
    let parseElementType (typeValue: string) : LSElementType option =
        match Int32.TryParse(typeValue) with
        | true, value ->
            if Enum.IsDefined(typeof<LSElementType>, value) then
                Some (enum<LSElementType> value)
            else
                None
        | _ -> None

    /// Contact 요소를 Condition으로 변환
    let contactToCondition (element: LSLadderElement) : Condition option =
        match element.Variable with
        | Some variable ->
            let operator = LSElementType.toConditionOperator element.ElementType
            match operator with
            | Some op ->
                let value =
                    match element.ElementType with
                    | LSElementType.ClosedContactMode -> "False"
                    | _ -> "True"

                Some {
                    Variable = variable
                    Operator = op
                    Value = value
                    Description = element.Description |> Option.defaultValue $"Contact: {variable}"
                }
            | None -> None
        | None -> None

    /// Coil 요소를 Action으로 변환
    let coilToAction (element: LSLadderElement) : Action option =
        match element.Variable with
        | Some variable ->
            let operation = LSElementType.toActionOperation element.ElementType
            match operation with
            | Some op ->
                Some {
                    Variable = variable
                    Operation = op
                    Value = Some "True"
                    Delay = None
                    Description = element.Description |> Option.defaultValue $"Coil: {variable}"
                }
            | None -> None
        | None -> None

    /// Function/FB 요소를 Action으로 변환
    let functionToAction (element: LSLadderElement) : Action option =
        match element.Variable with
        | Some funcName ->
            Some {
                Variable = funcName
                Operation = ActionOperation.Call
                Value = element.Value
                Delay = None
                Description = element.Description |> Option.defaultValue $"Function: {funcName}"
            }
        | None -> None

    /// Branch 요소를 Action으로 변환
    let branchToAction (element: LSLadderElement) : Action option =
        let operation = LSElementType.toActionOperation element.ElementType
        match operation, element.Variable with
        | Some op, Some target ->
            Some {
                Variable = target
                Operation = op
                Value = None
                Delay = None
                Description = element.Description |> Option.defaultValue $"Branch: {target}"
            }
        | _ -> None

    /// XML에서 ladder element 파싱
    let parseLadderElement (xmlElement: XElement) : LSLadderElement option =
        try
            let typeAttr = getAttributeValue xmlElement "Type"
            let variable = getAttributeValue xmlElement "Variable"
            let value = getAttributeValue xmlElement "Value"
            let row = getAttributeValue xmlElement "Row" |> Option.bind (fun v -> match Int32.TryParse(v) with | true, n -> Some n | _ -> None) |> Option.defaultValue 0
            let column = getAttributeValue xmlElement "Column" |> Option.bind (fun v -> match Int32.TryParse(v) with | true, n -> Some n | _ -> None) |> Option.defaultValue 0
            let description = getAttributeValue xmlElement "Description"

            match typeAttr with
            | Some t ->
                match parseElementType t with
                | Some elementType ->
                    Some {
                        ElementType = elementType
                        Variable = variable
                        Value = value
                        Row = row
                        Column = column
                        Description = description
                    }
                | None -> None
            | None -> None
        with
        | ex ->
            logger.LogWarning(ex, "Failed to parse ladder element")
            None

    /// Rung의 모든 요소를 조건과 액션으로 변환
    let extractConditionsAndActions (rungContent: string) : (Condition list * Action list) =
        let mutable conditions = []
        let mutable actions = []

        try
            // Rung content를 XML로 파싱
            let doc = XDocument.Parse($"<Rung>{rungContent}</Rung>")

            // 모든 Element 요소 추출
            for element in doc.Descendants(XName.Get "Element") do
                match parseLadderElement element with
                | Some lsElement ->
                    // Element type에 따라 처리
                    if LSElementType.isContact lsElement.ElementType then
                        match contactToCondition lsElement with
                        | Some cond -> conditions <- cond :: conditions
                        | None -> ()
                    elif LSElementType.isCoil lsElement.ElementType then
                        match coilToAction lsElement with
                        | Some act -> actions <- act :: actions
                        | None -> ()
                    elif LSElementType.isFunction lsElement.ElementType then
                        match functionToAction lsElement with
                        | Some act -> actions <- act :: actions
                        | None -> ()
                    elif LSElementType.isBranch lsElement.ElementType then
                        match branchToAction lsElement with
                        | Some act -> actions <- act :: actions
                        | None -> ()
                | None -> ()

        with
        | ex ->
            logger.LogWarning(ex, "Failed to extract conditions and actions from rung content")

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
                lower.Contains("err"))

        // Rising/Falling edge 감지
        let hasEdgeTrigger =
            conditions |> List.exists (fun c ->
                c.Operator = ConditionOperator.Rising ||
                c.Operator = ConditionOperator.Falling)

        if hasSafetyKeywords then
            LogicFlowType.Safety
        elif actions |> List.exists (fun a -> a.Operation = Jump || a.Operation = Call) then
            LogicFlowType.Conditional
        elif hasEdgeTrigger then
            LogicFlowType.Interrupt
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
            try
                let doc = XDocument.Parse($"<root>{contactElement}</root>")
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return None
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement when LSElementType.isContact lsElement.ElementType ->
                        return contactToCondition lsElement
                    | _ -> return None
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse contact element")
                return None
        }

        member this.ParseCoilAsync(coilElement: string) = task {
            try
                let doc = XDocument.Parse($"<root>{coilElement}</root>")
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return None
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement when LSElementType.isCoil lsElement.ElementType ->
                        return coilToAction lsElement
                    | _ -> return None
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse coil element")
                return None
        }

        member this.ParseTimerAsync(timerElement: string) = task {
            // Timer는 Function으로 처리
            try
                let doc = XDocument.Parse($"<root>{timerElement}</root>")
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return (None, None)
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement ->
                        // Timer의 .Q 비트를 조건으로
                        let condition = lsElement.Variable |> Option.map (fun v -> {
                            Variable = $"{v}.Q"
                            Operator = ConditionOperator.Equal
                            Value = "True"
                            Description = $"Timer done: {v}"
                        })
                        // Timer 자체를 액션으로
                        let action = functionToAction lsElement
                        return (condition, action)
                    | None -> return (None, None)
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse timer element")
                return (None, None)
        }

        member this.ParseCounterAsync(counterElement: string) = task {
            // Counter는 Function으로 처리
            try
                let doc = XDocument.Parse($"<root>{counterElement}</root>")
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return (None, None)
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement ->
                        // Counter의 .Q 비트를 조건으로
                        let condition = lsElement.Variable |> Option.map (fun v -> {
                            Variable = $"{v}.Q"
                            Operator = ConditionOperator.GreaterOrEqual
                            Value = lsElement.Value |> Option.defaultValue "0"
                            Description = $"Counter done: {v}"
                        })
                        // Counter 자체를 액션으로
                        let action = functionToAction lsElement
                        return (condition, action)
                    | None -> return (None, None)
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse counter element")
                return (None, None)
        }

        member this.ParseCompareAsync(compareElement: string) = task {
            // Compare는 Function으로 처리되며 조건 생성
            try
                let doc = XDocument.Parse($"<root>{compareElement}</root>")
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return None
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement ->
                        // Function name에서 비교 연산자 추론
                        match lsElement.Variable with
                        | Some funcName when funcName.ToUpper().StartsWith("EQU") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("EQU", "").Trim()
                                Operator = ConditionOperator.Equal
                                Value = v
                                Description = $"Compare: {funcName}"
                            })
                        | _ -> return None
                    | None -> return None
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse compare element")
                return None
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
                        Sequence = 0
                        Description = sprintf "Rung %s: %d conditions, %d actions" (rung.Id |> Option.defaultValue "") conditions.Length actions.Length
                    }

                    logger.LogDebug("Analyzed LS rung {RungId}: {CondCount} conditions, {ActionCount} actions",
                                   rung.Id, conditions.Length, actions.Length)

                    return Some logicFlow
            with
            | ex ->
                logger.LogError(ex, "Error analyzing LS rung {RungId}", rung.Id)
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

            logger.LogInformation("Batch analyzed {Count} LS rungs, produced {FlowCount} logic flows",
                                rungs.Length, logicFlows.Length)

            return logicFlows
        }

        member this.AnalyzeSequenceAsync(rungs: RawLogic list) = task {
            let! logicFlows = (this :> ILSLogicAnalyzer).AnalyzeRungsBatchAsync(rungs)

            // 안전 시퀀스 감지
            let safetySequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Safety)

            // 인터럽트 시퀀스 감지 (Rising/Falling edge)
            let interruptSequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Interrupt)

            // 전역 시퀀스
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
                CyclomaticComplexity = totalConditions + 1
            }

            // 실행 순서 추출
            let executionOrder = logicFlows |> List.map (fun f -> f.Id)

            // 의존성 맵 구성 (간단한 구현)
            let dependencies = Map.empty<string, string list>

            // 병렬 그룹 (간단한 구현)
            let parallelGroups = []

            let sequenceAnalysis = {
                LogicFlows = logicFlows
                ExecutionOrder = executionOrder
                Dependencies = dependencies
                ParallelGroups = parallelGroups
                Statistics = statistics
            }

            logger.LogInformation("LS Sequence analysis complete: {TotalRungs} rungs, {SafetySeq} safety, {IntSeq} interrupt sequences",
                                statistics.TotalRungs, safetySequences.Length, interruptSequences.Length)

            return sequenceAnalysis
        }

        member this.ExtractApiDependenciesAsync(rungs: RawLogic list, devices: Device list) = task {
            return []
        }

/// LS Electric 로직 분석기 팩토리 V2
module LSLogicAnalyzerV2Factory =
    let create (logger: ILogger<LSLogicAnalyzerV2>) : ILSLogicAnalyzer =
        LSLogicAnalyzerV2(logger) :> ILSLogicAnalyzer
