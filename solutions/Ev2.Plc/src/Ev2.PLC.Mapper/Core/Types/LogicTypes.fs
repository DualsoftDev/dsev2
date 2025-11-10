namespace Ev2.PLC.Mapper.Core.Types

open System

/// 로직 흐름 타입
type LogicFlowType =
    | Sequential
    | Parallel
    | Conditional
    | Loop
    | Interrupt
    | Safety
    | Timer
    | Counter
    | Simple
    | Math
    | Sequence
    | Custom of string

/// 조건 연산자
type ConditionOperator =
    | Equal
    | NotEqual
    | Greater
    | GreaterThan
    | Less
    | LessThan
    | GreaterOrEqual
    | LessOrEqual
    | And
    | Or
    | Not
    | Rising
    | Falling

    member this.Symbol =
        match this with
        | Equal -> "="
        | NotEqual -> "<>"
        | Greater | GreaterThan -> ">"
        | Less | LessThan -> "<"
        | GreaterOrEqual -> ">="
        | LessOrEqual -> "<="
        | And -> "AND"
        | Or -> "OR"
        | Not -> "NOT"
        | Rising -> "R_TRIG"
        | Falling -> "F_TRIG"

/// 액션 연산
type ActionOperation =
    | Set
    | Reset
    | Toggle
    | Assign
    | Increment
    | Decrement
    | Call
    | Jump

    member this.Symbol =
        match this with
        | Set -> "SET"
        | Reset -> "RESET"
        | Toggle -> "TOG"
        | Assign -> ":="
        | Increment -> "++"
        | Decrement -> "--"
        | Call -> "CALL"
        | Jump -> "JMP"

/// 타이밍 타입
type TimingType =
    | MinimumDelay
    | MaximumDelay
    | ExactDelay
    | Pulse
    | Debounce

/// 타이밍 제약
type TimingConstraint = {
    Type: TimingType
    MinDelay: TimeSpan option
    MaxDelay: TimeSpan option
    Description: string
}

/// 조건
type Condition = {
    Variable: string
    Operator: ConditionOperator
    Value: string
    Description: string
} with
    static member Create(variable: string, operator: ConditionOperator, value: string) = {
        Variable = variable
        Operator = operator
        Value = value
        Description = ""
    }

/// 액션
type Action = {
    Variable: string
    Operation: ActionOperation
    Value: string option
    Delay: TimeSpan option
    Description: string
} with
    static member Create(variable: string, operation: ActionOperation) = {
        Variable = variable
        Operation = operation
        Value = None
        Delay = None
        Description = ""
    }

/// 로직 흐름 분석 결과
type LogicFlow = {
    Id: string
    Type: LogicFlowType
    InputVariables: string list
    OutputVariables: string list
    Conditions: Condition list
    Actions: Action list
    Sequence: int
    Description: string
} with
    static member Create(id: string, flowType: LogicFlowType) = {
        Id = id
        Type = flowType
        InputVariables = []
        OutputVariables = []
        Conditions = []
        Actions = []
        Sequence = 0
        Description = ""
    }

/// API 의존성
type ApiDependency = {
    Api: string
    Device: string
    SourceDevice: string  // 추가
    TargetApi: string     // 추가
    DependencyType: string // 추가
    Parameters: string list // 추가
    IsRequired: bool      // 추가
    PrecedingApis: string list
    InterlockApis: string list
    SafetyInterlocks: string list
    TimingConstraints: TimingConstraint list
    Description: string
} with
    static member Create(api: string, device: string) = {
        Api = api
        Device = device
        SourceDevice = ""
        TargetApi = ""
        DependencyType = ""
        Parameters = []
        IsRequired = false
        PrecedingApis = []
        InterlockApis = []
        SafetyInterlocks = []
        TimingConstraints = []
        Description = ""
    }

/// 로직 타입
type LogicType =
    | LadderRung
    | Ladder
    | StructuredText
    | ST
    | FunctionBlock
    | FBD
    | InstructionList
    | IL
    | SequentialFunctionChart
    | SCL
    | STL
    | Custom of string

/// 원본 로직 정보
type RawLogic = {
    Id: string option
    Name: string option
    Number: int
    Content: string
    RawContent: string option
    LogicType: LogicType
    Type: LogicFlowType option
    Variables: string list
    Comments: string list
    LineNumber: int option
    Properties: Map<string, string>
    Comment: string option
}

/// 시퀀스 통계
type SequenceStatistics = {
    TotalRungs: int
    TotalConditions: int
    TotalActions: int
    AverageConditionsPerRung: float
    AverageActionsPerRung: float
    CyclomaticComplexity: int
} with
    static member Empty = {
        TotalRungs = 0
        TotalConditions = 0
        TotalActions = 0
        AverageConditionsPerRung = 0.0
        AverageActionsPerRung = 0.0
        CyclomaticComplexity = 0
    }

/// 시퀀스 분석 결과
type SequenceAnalysis = {
    LogicFlows: LogicFlow list
    ExecutionOrder: string list
    Dependencies: Map<string, string list>
    ParallelGroups: string list list
    Statistics: SequenceStatistics
} with
    static member Empty = {
        LogicFlows = []
        ExecutionOrder = []
        Dependencies = Map.empty
        ParallelGroups = []
        Statistics = SequenceStatistics.Empty
    }

/// 디바이스 시퀀스 통계
type DeviceSequenceStatistics = {
    TotalSequences: int
    ComplexSequences: int
    SafetySequences: int
    AverageComplexity: float
    MaxDepth: int
    CriticalPathCount: int
} with
    static member Empty = {
        TotalSequences = 0
        ComplexSequences = 0
        SafetySequences = 0
        AverageComplexity = 0.0
        MaxDepth = 0
        CriticalPathCount = 0
    }

/// 디바이스 시퀀스
type DeviceSequence = {
    Device: string
    Area: string
    Sequence: LogicFlow list
    ApiDependencies: ApiDependency list
    SafetyInterlocks: string list
    EstimatedCycleTime: TimeSpan option
} with
    static member Create(device: string, area: string) = {
        Device = device
        Area = area
        Sequence = []
        ApiDependencies = []
        SafetyInterlocks = []
        EstimatedCycleTime = None
    }

/// 고급 시퀀스 분석 결과
type AdvancedSequenceAnalysis = {
    DeviceSequences: DeviceSequence list
    GlobalSequence: LogicFlow list
    CriticalPaths: LogicFlow list list
    SafetySequences: LogicFlow list
    Statistics: SequenceStatistics
} with
    static member Empty = {
        DeviceSequences = []
        GlobalSequence = []
        CriticalPaths = []
        SafetySequences = []
        Statistics = SequenceStatistics.Empty
    }

/// 로직 분석 성능
type LogicAnalysisPerformance = {
    ParseTime: TimeSpan
    AnalysisTime: TimeSpan
    TotalTime: TimeSpan
    MemoryUsed: int64
    RungsProcessed: int
    VariablesAnalyzed: int
} with
    static member Empty = {
        ParseTime = TimeSpan.Zero
        AnalysisTime = TimeSpan.Zero
        TotalTime = TimeSpan.Zero
        MemoryUsed = 0L
        RungsProcessed = 0
        VariablesAnalyzed = 0
    }

/// 로직 분석 결과
type LogicAnalysisResult = {
    Success: bool
    SequenceAnalysis: SequenceAnalysis
    ApiDependencies: ApiDependency list
    ValidationResults: ValidationResult list
    Performance: LogicAnalysisPerformance
    Errors: string list
    Warnings: string list
} with
    static member CreateSuccess(sequenceAnalysis: SequenceAnalysis) = {
        Success = true
        SequenceAnalysis = sequenceAnalysis
        ApiDependencies = []
        ValidationResults = []
        Performance = LogicAnalysisPerformance.Empty
        Errors = []
        Warnings = []
    }

    static member CreateError(errors: string list) = {
        Success = false
        SequenceAnalysis = SequenceAnalysis.Empty
        ApiDependencies = []
        ValidationResults = []
        Performance = LogicAnalysisPerformance.Empty
        Errors = errors
        Warnings = []
    }

