namespace Ev2.PLC.Mapper.Core.Interfaces

open System.Threading.Tasks
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

/// 변수 분석기 인터페이스
type IVariableAnalyzer =
    /// 변수명 패턴 분석
    abstract member AnalyzeVariableNameAsync: variable: RawVariable * conventions: NamingConvention list -> Task<VariableNamingPattern option>
    
    /// 디바이스 타입 추론
    abstract member InferDeviceTypeAsync: variableName: string -> Task<DeviceType>
    
    /// API 타입 추론
    abstract member InferApiTypeAsync: apiName: string * deviceType: DeviceType -> Task<ApiType>
    
    /// 배치 변수 분석
    abstract member AnalyzeVariablesBatchAsync: variables: RawVariable list * config: MappingConfiguration -> Task<VariableAnalysisResult list>
    
    /// 영역(Area) 추출
    abstract member ExtractAreasAsync: variables: RawVariable list -> Task<Area list>
    
    /// 디바이스 추출
    abstract member ExtractDevicesAsync: variables: RawVariable list * areas: Area list -> Task<Device list>
    
    /// API 정의 생성
    abstract member GenerateApiDefinitionsAsync: devices: Device list -> Task<ApiDefinition list>

/// 명명 규칙 분석기 인터페이스
type INamingAnalyzer =
    /// 변수명에서 구성 요소 추출
    abstract member ParseVariableName: variableName: string * pattern: string -> Map<string, string> option
    
    /// 디바이스명 정규화
    abstract member NormalizeDeviceName: deviceName: string -> string
    
    /// API명 정규화
    abstract member NormalizeApiName: apiName: string -> string
    
    /// 영역명 추출
    abstract member ExtractAreaName: variableName: string -> string option
    
    /// 변수명 유효성 검사
    abstract member ValidateVariableName: variableName: string * conventions: NamingConvention list -> ValidationResult

/// 주소 분석기 인터페이스
type IAddressAnalyzer =
    /// PLC 주소 파싱
    abstract member ParseAddressAsync: address: string * vendor: PlcVendor -> Task<PlcAddress option>
    
    /// 주소 유효성 검증
    abstract member ValidateAddressAsync: address: PlcAddress * dataType: PlcDataType * vendor: PlcVendor -> Task<ValidationResult>
    
    /// 최적 주소 할당
    abstract member OptimizeAddressAllocationAsync: variables: IOVariable list * vendor: PlcVendor -> Task<IOVariable list>
    
    /// 주소 충돌 검사
    abstract member CheckAddressConflictsAsync: variables: IOVariable list -> Task<ValidationResult list>

/// 패턴 매칭 엔진 인터페이스
type IPatternMatchingEngine =
    /// 패턴 매칭 실행
    abstract member MatchPattern: input: string * pattern: VariablePattern -> PatternMatchResult option
    
    /// 여러 패턴 중 최적 매칭
    abstract member FindBestMatch: input: string * patterns: VariablePattern list -> PatternMatchResult option
    
    /// 패턴 학습 (머신러닝 기반)
    abstract member LearnPatternsAsync: examples: (string * VariableNamingPattern) list -> Task<VariablePattern list>
    
    /// 패턴 정확도 측정
    abstract member EvaluatePatternAccuracy: pattern: VariablePattern * testCases: (string * bool) list -> float

/// 스마트 분석기 인터페이스 (AI/ML 기반)
type ISmartAnalyzer =
    /// 변수 사용 패턴 분석
    abstract member AnalyzeUsagePatternsAsync: variables: RawVariable list * logic: RawLogic list -> Task<UsagePattern list>
    
    /// 디바이스 관계 분석
    abstract member AnalyzeDeviceRelationshipsAsync: devices: Device list * logic: RawLogic list -> Task<DeviceRelationship list>
    
    /// 이상 패턴 감지
    abstract member DetectAnomaliesAsync: variables: RawVariable list -> Task<Anomaly list>
    
    /// 최적화 제안
    abstract member SuggestOptimizationsAsync: mapping: MappingResult -> Task<OptimizationSuggestion list>
