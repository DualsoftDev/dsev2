namespace Ev2.PLC.Mapper.Core.Interfaces

open System
open Ev2.PLC.Mapper.Core.Types

/// PLC 파일 파서 인터페이스
type IPlcParser =
    /// 파일을 파싱하여 RawLogic 리스트 반환
    abstract member ParseFileAsync : filePath:string -> Async<RawLogic list>

    /// 디렉토리 내 모든 파일을 파싱
    abstract member ParseDirectoryAsync : directoryPath:string -> Async<RawLogic list>

    /// 문자열 내용을 직접 파싱
    abstract member ParseContentAsync : content:string -> Async<RawLogic list>

    /// 지원하는 파일 확장자 목록
    abstract member SupportedFileExtensions : string list

    /// 파일을 파싱할 수 있는지 확인
    abstract member CanParse : filePath:string -> bool