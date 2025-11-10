namespace Ev2.PLC.Mapper.Core.Types

open System
open Ev2.PLC.Common.Types

/// PLC 프로그램 파일 형식
type PlcProgramFormat =
    | LSElectricXML of filePath: string
    | AllenBradleyL5K of filePath: string
    | MitsubishiCSV of filePath: string
    | SiemensXML of filePath: string
    | CustomFormat of filePath: string * parserName: string

    member this.FileExtension =
        match this with
        | LSElectricXML _ -> ".xml"
        | AllenBradleyL5K _ -> ".L5K"
        | MitsubishiCSV _ -> ".csv"
        | SiemensXML _ -> ".xml"
        | CustomFormat _ -> ".txt"

    member this.Vendor =
        match this with
        | LSElectricXML _ -> PlcVendor.CreateLSElectric()
        | AllenBradleyL5K _ -> PlcVendor.CreateAllenBradley()
        | MitsubishiCSV _ -> PlcVendor.CreateMitsubishi()
        | SiemensXML _ -> PlcVendor.CreateSiemens()
        | CustomFormat (_, parserName) -> PlcVendor.CreateCustom(parserName)

/// 로직 타입
type LogicType =
    | LadderRung
    | StructuredText
    | FunctionBlock
    | InstructionList
    | SequentialFunctionChart
    | Custom of string

/// 원본 변수 정보
type RawVariable = {
    Name: string
    Address: string
    DataType: string
    Comment: string option
    InitialValue: string option
    Scope: string option
    AccessLevel: string option
    Properties: Map<string, string>
} with
    static member Create(name: string, address: string, dataType: string) = {
        Name = name
        Address = address
        DataType = dataType
        Comment = None
        InitialValue = None
        Scope = None
        AccessLevel = None
        Properties = Map.empty
    }

/// 원본 로직 정보
type RawLogic = {
    Id: string
    Type: LogicType
    Content: string
    Variables: string list
    Comments: string list
    LineNumber: int option
    Properties: Map<string, string>
}

/// 원본 주석 정보
type RawComment = {
    Target: string
    Content: string
    Language: string option
    Author: string option
    CreatedDate: DateTime option
}

/// 프로젝트 정보
type ProjectInfo = {
    Name: string
    Version: string
    Vendor: PlcVendor
    Format: PlcProgramFormat
    CreatedDate: DateTime
    ModifiedDate: DateTime
    Description: string option
    Author: string option
    FilePath: string
    FileSize: int64
} with
    static member Create(name: string, vendor: PlcVendor, format: PlcProgramFormat, filePath: string) = {
        Name = name
        Version = "1.0.0"
        Vendor = vendor
        Format = format
        CreatedDate = DateTime.UtcNow
        ModifiedDate = DateTime.UtcNow
        Description = None
        Author = None
        FilePath = filePath
        FileSize = 0L
    }

/// 파싱된 원본 PLC 프로그램
type RawPlcProgram = {
    ProjectInfo: ProjectInfo
    Variables: RawVariable list
    Logic: RawLogic list
    Comments: RawComment list
    Metadata: Map<string, string>
} with
    static member Empty(projectInfo: ProjectInfo) = {
        ProjectInfo = projectInfo
        Variables = []
        Logic = []
        Comments = []
        Metadata = Map.empty
    }
