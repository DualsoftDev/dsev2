namespace Ev2.PLC.Mapper.Core.Interfaces

open System.Threading.Tasks
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types

/// PLC 프로그램 파서 인터페이스
type IPlcProgramParser =
    /// 지원하는 PLC 제조사
    abstract member SupportedVendor: PlcVendor
    
    /// 지원하는 파일 형식들
    abstract member SupportedFormats: PlcProgramFormat list
    
    /// 파일 형식 지원 여부 확인
    abstract member CanParse: format: PlcProgramFormat -> bool
    
    /// PLC 프로그램 파일 파싱
    abstract member ParseAsync: filePath: string -> Task<RawPlcProgram>
    
    /// 메모리 내 컨텐츠 파싱
    abstract member ParseContentAsync: content: string * format: PlcProgramFormat -> Task<RawPlcProgram>
    
    /// 파싱 유효성 검사
    abstract member ValidateFileAsync: filePath: string -> Task<ValidationResult>

/// LS Electric XML 파서 인터페이스
type ILSElectricParser =
    inherit IPlcProgramParser
    
    /// XG5000 프로젝트 파일 파싱
    abstract member ParseXG5000ProjectAsync: projectPath: string -> Task<RawPlcProgram>
    
    /// 심볼 테이블 추출
    abstract member ExtractSymbolTableAsync: xmlContent: string -> Task<RawVariable list>
    
    /// 래더 로직 추출  
    abstract member ExtractLadderLogicAsync: xmlContent: string -> Task<RawLogic list>
    
    /// 프로그램 정보 추출
    abstract member ExtractProjectInfoAsync: xmlContent: string -> Task<ProjectInfo>

/// Allen-Bradley L5K 파서 인터페이스
type IAllenBradleyParser =
    inherit IPlcProgramParser
    
    /// RSLogix 5000 L5K 파일 파싱
    abstract member ParseL5KFileAsync: filePath: string -> Task<RawPlcProgram>
    
    /// 태그 섹션 파싱
    abstract member ParseTagSectionAsync: content: string -> Task<RawVariable list>
    
    /// 루틴 섹션 파싱
    abstract member ParseRoutineSectionAsync: content: string -> Task<RawLogic list>
    
    /// 프로그램 구조 분석
    abstract member AnalyzeProgramStructureAsync: content: string -> Task<Map<string, string>>

/// Mitsubishi CSV 파서 인터페이스
type IMitsubishiParser =
    inherit IPlcProgramParser
    
    /// GX Works CSV 파일 파싱
    abstract member ParseGXWorksCSVAsync: filePath: string -> Task<RawPlcProgram>
    
    /// 디바이스 목록 CSV 파싱
    abstract member ParseDeviceListAsync: csvContent: string -> Task<RawVariable list>
    
    /// 코멘트 CSV 파싱
    abstract member ParseCommentCSVAsync: csvContent: string -> Task<RawComment list>
    
    /// 프로그램 텍스트 분석
    abstract member AnalyzeProgramTextAsync: programText: string -> Task<RawLogic list>

/// Siemens XML 파서 인터페이스
type ISiemensParser =
    inherit IPlcProgramParser
    
    /// TIA Portal XML 파일 파싱
    abstract member ParseTIAPortalXMLAsync: filePath: string -> Task<RawPlcProgram>
    
    /// 데이터 블록 파싱
    abstract member ParseDataBlocksAsync: xmlContent: string -> Task<RawVariable list>
    
    /// 함수 블록 파싱
    abstract member ParseFunctionBlocksAsync: xmlContent: string -> Task<RawLogic list>
    
    /// 하드웨어 설정 파싱
    abstract member ParseHardwareConfigAsync: xmlContent: string -> Task<Map<string, string>>

/// 파서 팩토리 인터페이스
type IParserFactory =
    /// 지원하는 모든 파서 목록
    abstract member AvailableParsers: IPlcProgramParser list
    
    /// 제조사별 파서 생성
    abstract member CreateParser: vendor: PlcVendor -> IPlcProgramParser option
    
    /// 파일 형식에 따른 파서 선택
    abstract member GetParserForFormat: format: PlcProgramFormat -> IPlcProgramParser option
    
    /// 파일 확장자로 파서 추론
    abstract member GetParserForFile: filePath: string -> IPlcProgramParser option
    
    /// 사용자 정의 파서 등록
    abstract member RegisterCustomParser: parser: IPlcProgramParser -> unit
