namespace Ev2.PLC.Mapper.Core.Engine

open System
open System.Xml.Linq
open System.Threading.Tasks
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Interfaces

/// LS Electric 로직 분석기 구현 (ElementType 기반 + 개선된 parseRung)
type LSLogicAnalyzer(logger: ILogger<LSLogicAnalyzer>) =

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

    /// 사용자 제공 parseRung 함수 - XElement에서 RawLogic으로 변환
    let parseRung (rungElement: XElement) : RawLogic =
        let id = getAttributeValue rungElement "Number" |> Option.defaultValue (Guid.NewGuid().ToString())
        let content = rungElement.ToString()

        // Extract variables using regex
        let variablePattern = @"Variable=""([^""]+)"""
        let variables =
            Regex.Matches(content, variablePattern)
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups.[1].Value)
            |> Seq.distinct
            |> Seq.toList

        // Extract comments
        let commentPattern = @"Comment=""([^""]+)"""
        let comments =
            Regex.Matches(content, commentPattern)
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups.[1].Value)
            |> Seq.toList

        // Get rung name/description
        let name = getAttributeValue rungElement "Name"
        let desc = getAttributeValue rungElement "Description"

        // Detect logic type
        let logicType =
            if content.Contains("<Element") then LogicType.LadderRung
            elif content.Contains("IF") || content.Contains("THEN") then LogicType.StructuredText
            else LogicType.LadderRung

        // Detect flow type based on content
        let detectFlowType() =
            if content.ToLower().Contains("safety") || content.ToLower().Contains("emergency") then
                Some LogicFlowType.Safety
            elif content.ToLower().Contains("timer") || content.Contains("TON") || content.Contains("TOF") then
                Some LogicFlowType.Timer
            elif content.ToLower().Contains("counter") || content.Contains("CTU") || content.Contains("CTD") then
                Some LogicFlowType.Counter
            elif content.Contains("JMP") || content.Contains("CALL") then
                Some LogicFlowType.Conditional
            elif content.Contains("ADD") || content.Contains("SUB") || content.Contains("MUL") || content.Contains("DIV") then
                Some LogicFlowType.Math
            elif content.Contains("SEQ") then
                Some LogicFlowType.Sequence
            else
                Some LogicFlowType.Simple

        {
            Id = Some id
            Name = name |> Option.orElse desc
            Number = match Int32.TryParse(id) with | true, n -> n | _ -> 0
            Content = content
            RawContent = Some content
            LogicType = logicType
            Type = detectFlowType()
            Variables = variables
            Comments = comments
            LineNumber = getAttributeValue rungElement "Line" |> Option.bind (fun v -> match Int32.TryParse(v) with | true, n -> Some n | _ -> None)
            Properties = Map.empty
            Comment = comments |> List.tryHead
        }

    /// Contact 요소를 Condition으로 변환 (ElementType 기반)
    let contactToCondition (element: LSLadderElement) : Condition option =
        match element.Variable with
        | Some variable ->
            let operator = LSElementType.toConditionOperator element.ElementType
            match operator with
            | Some op ->
                let value =
                    match element.ElementType with
                    | LSElementType.ClosedContactMode
                    | LSElementType.ClosedPulseContactMode
                    | LSElementType.ClosedNPulseContactMode -> "False"
                    | _ -> "True"

                Some {
                    Variable = variable
                    Operator = op
                    Value = value
                    Description = element.Description |> Option.defaultValue (sprintf "Contact %A: %s" element.ElementType variable)
                }
            | None -> None
        | None -> None

    /// Coil 요소를 Action으로 변환 (ElementType 기반)
    let coilToAction (element: LSLadderElement) : Action option =
        match element.Variable with
        | Some variable ->
            let operation = LSElementType.toActionOperation element.ElementType
            match operation with
            | Some op ->
                Some {
                    Variable = variable
                    Operation = op
                    Value =
                        match element.ElementType with
                        | LSElementType.ClosedCoilMode -> Some "False"
                        | _ -> Some "True"
                    Delay = None
                    Description = element.Description |> Option.defaultValue (sprintf "Coil %A: %s" element.ElementType variable)
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
                Description = element.Description |> Option.defaultValue (sprintf "Function: %s" funcName)
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
                Description = element.Description |> Option.defaultValue (sprintf "Branch %A: %s" element.ElementType target)
            }
        | _ -> None

    /// Timer 요소 처리 (조건과 액션 생성)
    let processTimer (element: LSLadderElement) : (Condition option * Action option) =
        match element.Variable with
        | Some timerVar ->
            // Timer의 .Q 비트를 조건으로
            let condition = Some {
                Variable = sprintf "%s.Q" timerVar
                Operator = ConditionOperator.Equal
                Value = "True"
                Description = sprintf "Timer done: %s" timerVar
            }
            // Timer 시작 액션
            let action = Some {
                Variable = timerVar
                Operation = ActionOperation.Call
                Value = element.Value
                Delay =
                    element.Value
                    |> Option.bind (fun v ->
                        match Int32.TryParse(v) with
                        | true, ms -> Some (TimeSpan.FromMilliseconds(float ms))
                        | _ -> None)
                Description = sprintf "Start timer: %s" timerVar
            }
            (condition, action)
        | None -> (None, None)

    /// Counter 요소 처리 (조건과 액션 생성)
    let processCounter (element: LSLadderElement) : (Condition option * Action option) =
        match element.Variable with
        | Some counterVar ->
            // Counter의 .Q 비트를 조건으로
            let condition = Some {
                Variable = sprintf "%s.Q" counterVar
                Operator = ConditionOperator.GreaterOrEqual
                Value = element.Value |> Option.defaultValue "0"
                Description = sprintf "Counter done: %s" counterVar
            }
            // Counter 증가/감소 액션
            let action = Some {
                Variable = counterVar
                Operation =
                    if element.Variable.IsSome && element.Variable.Value.Contains("CTD") then
                        ActionOperation.Decrement
                    else
                        ActionOperation.Increment
                Value = None
                Delay = None
                Description = sprintf "Counter: %s" counterVar
            }
            (condition, action)
        | None -> (None, None)

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
                | None ->
                    logger.LogWarning("Unknown ElementType: {Type}", t)
                    None
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
            let doc =
                try
                    XDocument.Parse(sprintf "<Rung>%s</Rung>" rungContent)
                with
                | _ -> XDocument.Parse(sprintf "<root>%s</root>" rungContent)

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
                        // Timer/Counter 판별
                        match lsElement.Variable with
                        | Some var when var.StartsWith("T") || var.Contains("TON") || var.Contains("TOF") || var.Contains("TP") ->
                            let (cond, act) = processTimer lsElement
                            Option.iter (fun c -> conditions <- c :: conditions) cond
                            Option.iter (fun a -> actions <- a :: actions) act
                        | Some var when var.StartsWith("C") || var.Contains("CTU") || var.Contains("CTD") || var.Contains("CTUD") ->
                            let (cond, act) = processCounter lsElement
                            Option.iter (fun c -> conditions <- c :: conditions) cond
                            Option.iter (fun a -> actions <- a :: actions) act
                        | _ ->
                            match functionToAction lsElement with
                            | Some act -> actions <- act :: actions
                            | None -> ()
                    elif LSElementType.isBranch lsElement.ElementType then
                        match branchToAction lsElement with
                        | Some act -> actions <- act :: actions
                        | None -> ()
                | None -> ()

            // 기존 방식의 요소 추출도 유지 (하위 호환성)
            for contactNode in doc.Descendants(XName.Get "Contact") do
                let variable = getAttributeValue contactNode "Variable" |> Option.defaultValue ""
                let contactType = getAttributeValue contactNode "Type" |> Option.defaultValue "NO"
                let operator = if contactType = "NC" then ConditionOperator.Not else ConditionOperator.Equal
                conditions <- {
                    Variable = variable
                    Operator = operator
                    Value = if operator = ConditionOperator.Not then "False" else "True"
                    Description = sprintf "Contact %s: %s" contactType variable
                } :: conditions

            for coilNode in doc.Descendants(XName.Get "Coil") do
                let variable = getAttributeValue coilNode "Variable" |> Option.defaultValue ""
                let coilType = getAttributeValue coilNode "Type" |> Option.defaultValue "OTE"
                let operation =
                    match coilType with
                    | "OTL" -> ActionOperation.Set
                    | "OTU" -> ActionOperation.Reset
                    | _ -> ActionOperation.Assign
                actions <- {
                    Variable = variable
                    Operation = operation
                    Value = Some "True"
                    Delay = None
                    Description = sprintf "Coil %s: %s" coilType variable
                } :: actions

        with
        | ex ->
            logger.LogWarning(ex, "Failed to extract conditions and actions from rung content")

        (List.rev conditions, List.rev actions)

    /// 로직 흐름 타입 감지 (개선된 버전)
    let detectLogicFlowType (conditions: Condition list) (actions: Action list) (rungType: LogicFlowType option) : LogicFlowType =
        // 먼저 rungType이 이미 설정되어 있으면 사용
        match rungType with
        | Some flowType -> flowType
        | None ->
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
                    lower.Contains("err") ||
                    lower.Contains("alarm"))

            // Rising/Falling edge 감지
            let hasEdgeTrigger =
                conditions |> List.exists (fun c ->
                    c.Operator = ConditionOperator.Rising ||
                    c.Operator = ConditionOperator.Falling)

            // Timer/Counter 감지
            let hasTimer =
                actions |> List.exists (fun a ->
                    a.Variable.StartsWith("T") || a.Variable.Contains("TON") || a.Variable.Contains("TOF"))
                ||
                conditions |> List.exists (fun c ->
                    c.Variable.Contains(".Q") && c.Variable.StartsWith("T"))

            let hasCounter =
                actions |> List.exists (fun a ->
                    a.Variable.StartsWith("C") || a.Variable.Contains("CTU") || a.Variable.Contains("CTD"))
                ||
                conditions |> List.exists (fun c ->
                    c.Variable.Contains(".Q") && c.Variable.StartsWith("C"))

            // Math operations 감지
            let hasMath =
                actions |> List.exists (fun a ->
                    match a.Value with
                    | Some v -> v.Contains("+") || v.Contains("-") || v.Contains("*") || v.Contains("/")
                    | None -> false)

            // 플로우 타입 결정 (우선순위 순)
            if hasSafetyKeywords then
                LogicFlowType.Safety
            elif hasTimer then
                LogicFlowType.Timer
            elif hasCounter then
                LogicFlowType.Counter
            elif hasMath then
                LogicFlowType.Math
            elif hasEdgeTrigger then
                LogicFlowType.Interrupt
            elif actions |> List.exists (fun a -> a.Operation = Jump || a.Operation = Call) then
                LogicFlowType.Conditional
            elif actions |> List.exists (fun a -> a.Delay.IsSome) then
                LogicFlowType.Sequential
            elif conditions.Length > 3 then
                LogicFlowType.Conditional
            else
                LogicFlowType.Simple

    /// 입력/출력 변수 추출
    let extractIOVariables (conditions: Condition list) (actions: Action list) : (string list * string list) =
        let inputVars =
            conditions
            |> List.map (fun c -> c.Variable)
            |> List.filter (fun v -> not (String.IsNullOrEmpty(v)))
            |> List.distinct

        let outputVars =
            actions
            |> List.map (fun a -> a.Variable)
            |> List.filter (fun v -> not (String.IsNullOrEmpty(v)))
            |> List.distinct

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
                let doc = XDocument.Parse(sprintf "<root>%s</root>" contactElement)
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
                let doc = XDocument.Parse(sprintf "<root>%s</root>" coilElement)
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
            try
                let doc = XDocument.Parse(sprintf "<root>%s</root>" timerElement)
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return (None, None)
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement -> return processTimer lsElement
                    | None -> return (None, None)
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse timer element")
                return (None, None)
        }

        member this.ParseCounterAsync(counterElement: string) = task {
            try
                let doc = XDocument.Parse(sprintf "<root>%s</root>" counterElement)
                let element = doc.Root.Element(XName.Get "Element")
                match element with
                | null -> return (None, None)
                | elem ->
                    match parseLadderElement elem with
                    | Some lsElement -> return processCounter lsElement
                    | None -> return (None, None)
            with
            | ex ->
                logger.LogWarning(ex, "Failed to parse counter element")
                return (None, None)
        }

        member this.ParseCompareAsync(compareElement: string) = task {
            try
                let doc = XDocument.Parse(sprintf "<root>%s</root>" compareElement)
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
                                Description = sprintf "Compare: %s" funcName
                            })
                        | Some funcName when funcName.ToUpper().StartsWith("NEQ") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("NEQ", "").Trim()
                                Operator = ConditionOperator.NotEqual
                                Value = v
                                Description = sprintf "Compare: %s" funcName
                            })
                        | Some funcName when funcName.ToUpper().StartsWith("GRT") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("GRT", "").Trim()
                                Operator = ConditionOperator.GreaterThan
                                Value = v
                                Description = sprintf "Compare: %s" funcName
                            })
                        | Some funcName when funcName.ToUpper().StartsWith("LES") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("LES", "").Trim()
                                Operator = ConditionOperator.LessThan
                                Value = v
                                Description = sprintf "Compare: %s" funcName
                            })
                        | Some funcName when funcName.ToUpper().StartsWith("GEQ") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("GEQ", "").Trim()
                                Operator = ConditionOperator.GreaterOrEqual
                                Value = v
                                Description = sprintf "Compare: %s" funcName
                            })
                        | Some funcName when funcName.ToUpper().StartsWith("LEQ") ->
                            return lsElement.Value |> Option.map (fun v -> {
                                Variable = funcName.Replace("LEQ", "").Trim()
                                Operator = ConditionOperator.LessOrEqual
                                Value = v
                                Description = sprintf "Compare: %s" funcName
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
                    let flowType = detectLogicFlowType conditions actions rung.Type
                    let (inputVars, outputVars) = extractIOVariables conditions actions

                    let logicFlow = {
                        Id = rung.Id |> Option.defaultValue ""
                        Type = flowType
                        InputVariables = inputVars
                        OutputVariables = outputVars
                        Conditions = conditions
                        Actions = actions
                        Sequence = rung.Number
                        Description =
                            rung.Name
                            |> Option.defaultValue (sprintf "Rung %s: %d conditions, %d actions"
                                (rung.Id |> Option.defaultValue "") conditions.Length actions.Length)
                    }

                    logger.LogDebug("Analyzed LS rung {RungId}: Type={Type}, {CondCount} conditions, {ActionCount} actions",
                                   rung.Id, flowType, conditions.Length, actions.Length)

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
            return detectLogicFlowType conditions actions rung.Type
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

            // 플로우 타입별 분류
            let safetySequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Safety)
            let interruptSequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Interrupt)
            let timerSequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Timer)
            let counterSequences = logicFlows |> List.filter (fun f -> f.Type = LogicFlowType.Counter)

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
                CyclomaticComplexity = totalConditions + 1  // McCabe complexity
            }

            // 실행 순서 추출
            let executionOrder = logicFlows |> List.map (fun f -> f.Id)

            // 의존성 맵 구성 (입력/출력 변수 기반)
            let buildDependencies() =
                let mutable deps = Map.empty<string, string list>

                for i in 0 .. logicFlows.Length - 1 do
                    let current = logicFlows.[i]
                    let dependencies =
                        logicFlows
                        |> List.take i  // 이전 플로우들만 확인
                        |> List.filter (fun prev ->
                            // 현재 플로우의 입력이 이전 플로우의 출력과 겹치는지 확인
                            current.InputVariables |> List.exists (fun input ->
                                prev.OutputVariables |> List.contains input))
                        |> List.map (fun f -> f.Id)

                    if not dependencies.IsEmpty then
                        deps <- deps.Add(current.Id, dependencies)

                deps

            let dependencies = buildDependencies()

            // 병렬 그룹 식별 (의존성이 없는 플로우들)
            let parallelGroups =
                let independentFlows =
                    logicFlows
                    |> List.filter (fun f -> not (dependencies.ContainsKey(f.Id)))
                    |> List.map (fun f -> f.Id)
                if independentFlows.IsEmpty then []
                else [independentFlows]

            let sequenceAnalysis = {
                LogicFlows = logicFlows
                ExecutionOrder = executionOrder
                Dependencies = dependencies
                ParallelGroups = parallelGroups
                Statistics = statistics
            }

            logger.LogInformation("LS Sequence analysis complete: {TotalRungs} rungs, Safety={SafetySeq}, Interrupt={IntSeq}, Timer={TimerSeq}, Counter={CounterSeq}",
                                statistics.TotalRungs, safetySequences.Length, interruptSequences.Length, timerSequences.Length, counterSequences.Length)

            return sequenceAnalysis
        }

        member this.ExtractApiDependenciesAsync(rungs: RawLogic list, devices: Device list) = task {
            // API 의존성 추출 - Function Block 호출 기반
            let apiDeps =
                rungs
                |> List.collect (fun rung ->
                    let (_, actions) = extractConditionsAndActions rung.Content
                    actions
                    |> List.filter (fun a -> a.Operation = ActionOperation.Call)
                    |> List.map (fun a -> {
                        Api = a.Variable
                        Device =
                            devices
                            |> List.tryFind (fun d ->
                                rung.Variables |> List.exists (fun v -> v.Contains(d.Name)))
                            |> Option.map (fun d -> d.Name)
                            |> Option.defaultValue ""
                        SourceDevice = ""  // 기본값
                        TargetApi = ""     // 기본값
                        DependencyType = "FunctionCall"
                        Parameters = []    // 기본값
                        IsRequired = true  // 기본값
                        PrecedingApis = [] // 기본값
                        InterlockApis = [] // 기본값
                        SafetyInterlocks = [] // 기본값
                        TimingConstraints = [] // 기본값
                        Description = a.Description
                    }))
                |> List.distinct

            return apiDeps
        }

/// LS Electric 로직 분석기 팩토리 (Refactored)
module LSLogicAnalyzerFactory =
    /// parseRung 함수를 포함한 분석기 생성
    let createWithParseRung (logger: ILogger<LSLogicAnalyzer>) : ILSLogicAnalyzer =
        LSLogicAnalyzer(logger) :> ILSLogicAnalyzer

    /// 기본 분석기 생성 (기존 호환성 유지)
    let create (logger: ILogger<LSLogicAnalyzer>) : ILSLogicAnalyzer =
        createWithParseRung logger