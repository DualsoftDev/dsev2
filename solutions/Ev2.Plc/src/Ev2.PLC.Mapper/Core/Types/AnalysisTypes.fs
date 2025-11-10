namespace Ev2.PLC.Mapper.Core.Types

open System

/// 패턴 매칭 결과
type PatternMatchResult = {
    Pattern: VariablePattern
    Confidence: float
    ExtractedFields: Map<string, string>
    Suggestions: string list
} with
    static member Create(pattern: VariablePattern, confidence: float) = {
        Pattern = pattern
        Confidence = confidence
        ExtractedFields = Map.empty
        Suggestions = []
    }

/// 접근 타입
type AccessType =
    | ReadOnly
    | WriteOnly
    | ReadWrite
    | Intermittent
    | Continuous

/// 사용 패턴
type UsagePattern = {
    Variable: string
    AccessFrequency: int
    AccessType: AccessType
    RelatedVariables: string list
    TimingConstraints: TimingConstraint list
}

/// 관계 타입
type RelationshipType =
    | Sequential    // A → B 순차 실행
    | Parallel      // A || B 병렬 실행
    | Conditional   // A → B (조건부)
    | Interlocked   // A ⊕ B (상호 배타)
    | Dependent     // A depends on B

/// 디바이스 관계
type DeviceRelationship = {
    PrimaryDevice: string
    RelatedDevice: string
    RelationshipType: RelationshipType
    Strength: float
    Description: string
}

/// 이상 타입
type AnomalyType =
    | UnusedVariable
    | NamingInconsistency
    | AddressGap
    | LogicError
    | PerformanceIssue

/// 이상 패턴
type Anomaly = {
    Type: AnomalyType
    Variable: string option
    Device: string option
    Severity: ValidationSeverity
    Description: string
    Suggestion: string option
}

/// 최적화 타입
type OptimizationType =
    | AddressCompaction
    | NamingStandardization
    | LogicSimplification
    | PerformanceImprovement
    | MemoryOptimization

/// 최적화 영향도
type OptimizationImpact =
    | Low
    | Medium
    | High
    | Critical

/// 최적화 제안
type OptimizationSuggestion = {
    Type: OptimizationType
    Target: string
    Impact: OptimizationImpact
    Description: string
    Implementation: string
    EstimatedGain: float
}

