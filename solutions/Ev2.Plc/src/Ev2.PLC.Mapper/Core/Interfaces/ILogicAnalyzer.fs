namespace Ev2.PLC.Mapper.Core.Interfaces

open System.Threading.Tasks
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

/// 로직 분석기 인터페이스
type ILogicAnalyzer =
    /// 단일 Rung 분석
    abstract member AnalyzeRungAsync: rung: RawLogic -> Task<LogicFlow option>

    /// Rung에서 조건 추출
    abstract member ExtractConditionsAsync: rung: RawLogic -> Task<Condition list>

    /// Rung에서 액션 추출
    abstract member ExtractActionsAsync: rung: RawLogic -> Task<Action list>

    /// 로직 흐름 타입 감지
    abstract member DetectLogicFlowTypeAsync: rung: RawLogic -> Task<LogicFlowType>

    /// 입력/출력 변수 추출
    abstract member ExtractIOVariablesAsync: rung: RawLogic -> Task<string list * string list>

    /// 배치 로직 분석 (여러 Rung을 한 번에 처리)
    abstract member AnalyzeRungsBatchAsync: rungs: RawLogic list -> Task<LogicFlow list>

    /// 시퀀스 분석 (여러 Rung의 실행 순서 및 의존성 분석)
    abstract member AnalyzeSequenceAsync: rungs: RawLogic list -> Task<SequenceAnalysis>

    /// API 의존성 추출
    abstract member ExtractApiDependenciesAsync: rungs: RawLogic list * devices: Device list -> Task<ApiDependency list>

/// 제조사별 로직 분석기 인터페이스
type IVendorLogicAnalyzer =
    inherit ILogicAnalyzer

    /// 지원하는 제조사
    abstract member SupportedVendor: PlcVendor

    /// 특정 로직 타입 지원 여부
    abstract member CanAnalyze: logicType: LogicType -> bool

    /// 제조사별 특수 명령어 파싱
    abstract member ParseSpecialInstructionAsync: content: string -> Task<(Condition list * Action list) option>

/// LS Electric 로직 분석기 인터페이스
type ILSLogicAnalyzer =
    inherit IVendorLogicAnalyzer

    /// Contact 요소 파싱 (XIC, XIO)
    abstract member ParseContactAsync: contactElement: string -> Task<Condition option>

    /// Coil 요소 파싱 (OTE, OTL, OTU)
    abstract member ParseCoilAsync: coilElement: string -> Task<Action option>

    /// Timer 요소 파싱 (TON, TOF, TP)
    abstract member ParseTimerAsync: timerElement: string -> Task<(Condition option * Action option)>

    /// Counter 요소 파싱 (CTU, CTD, CTUD)
    abstract member ParseCounterAsync: counterElement: string -> Task<(Condition option * Action option)>

    /// Compare 요소 파싱 (EQU, NEQ, GRT, LES, GEQ, LEQ)
    abstract member ParseCompareAsync: compareElement: string -> Task<Condition option>

/// Allen-Bradley 로직 분석기 인터페이스
type IABLogicAnalyzer =
    inherit IVendorLogicAnalyzer

    /// XIC (Examine If Closed) 명령어 파싱
    abstract member ParseXICAsync: instruction: string -> Task<Condition option>

    /// XIO (Examine If Open) 명령어 파싱
    abstract member ParseXIOAsync: instruction: string -> Task<Condition option>

    /// OTE (Output Energize) 명령어 파싱
    abstract member ParseOTEAsync: instruction: string -> Task<Action option>

    /// OTL (Output Latch) 명령어 파싱
    abstract member ParseOTLAsync: instruction: string -> Task<Action option>

    /// OTU (Output Unlatch) 명령어 파싱
    abstract member ParseOTUAsync: instruction: string -> Task<Action option>

    /// MOV (Move) 명령어 파싱
    abstract member ParseMOVAsync: instruction: string -> Task<Action option>

    /// 산술 연산 명령어 파싱 (ADD, SUB, MUL, DIV)
    abstract member ParseArithmeticAsync: instruction: string -> Task<Action option>

    /// 비교 명령어 파싱 (EQU, NEQ, GRT, LES, GEQ, LEQ)
    abstract member ParseComparisonAsync: instruction: string -> Task<Condition option>

/// 로직 분석 엔진 인터페이스 (고급 분석)
type ILogicAnalysisEngine =
    /// 크리티컬 패스 감지
    abstract member DetectCriticalPathsAsync: sequences: DeviceSequence list -> Task<LogicFlow list list>

    /// 안전 인터록 감지
    abstract member DetectSafetyInterlocksAsync: rungs: RawLogic list -> Task<LogicFlow list>

    /// 디바이스 시퀀스 그룹화
    abstract member GroupDeviceSequencesAsync: logicFlows: LogicFlow list * devices: Device list -> Task<DeviceSequence list>

    /// 순환 의존성 감지
    abstract member DetectCircularDependenciesAsync: apiDependencies: ApiDependency list -> Task<ValidationResult list>

    /// 타이밍 제약 분석
    abstract member AnalyzeTimingConstraintsAsync: rungs: RawLogic list -> Task<TimingConstraint list>

    /// 복잡도 분석
    abstract member CalculateComplexityAsync: rungs: RawLogic list -> Task<SequenceStatistics>

    /// 병렬 실행 가능성 분석
    abstract member AnalyzeParallelismAsync: sequences: DeviceSequence list -> Task<(DeviceSequence * DeviceSequence) list>
