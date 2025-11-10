namespace Ev2.PLC.Mapper.Core.Types

open System

/// 매핑 통계
type MappingStatistics = {
    TotalVariables: int
    MappedVariables: int
    TotalAreas: int
    TotalDevices: int
    TotalApis: int
    ProcessingTime: TimeSpan
    FileSize: int64
    ParsingTime: TimeSpan
    AnalysisTime: TimeSpan
} with
    static member Empty = {
        TotalVariables = 0
        MappedVariables = 0
        TotalAreas = 0
        TotalDevices = 0
        TotalApis = 0
        ProcessingTime = TimeSpan.Zero
        FileSize = 0L
        ParsingTime = TimeSpan.Zero
        AnalysisTime = TimeSpan.Zero
    }

    member this.MappingRate =
        if this.TotalVariables = 0 then 0.0
        else (float this.MappedVariables) / (float this.TotalVariables) * 100.0

    member this.VariablesPerSecond =
        if this.ProcessingTime.TotalSeconds > 0.0 then
            float this.TotalVariables / this.ProcessingTime.TotalSeconds
        else 0.0

/// 매핑 결과
type MappingResult = {
    Success: bool
    ProjectInfo: ProjectInfo
    Areas: Area list
    Devices: Device list
    ApiDefinitions: ApiDefinition list
    IOMapping: IOMapping
    LogicFlow: LogicFlow list
    Statistics: MappingStatistics
    Warnings: string list
    Errors: string list
} with
    static member CreateSuccess(projectInfo: ProjectInfo, areas: Area list, devices: Device list) = {
        Success = true
        ProjectInfo = projectInfo
        Areas = areas
        Devices = devices
        ApiDefinitions = []
        IOMapping = IOMapping.Empty
        LogicFlow = []
        Statistics = MappingStatistics.Empty
        Warnings = []
        Errors = []
    }

    static member CreateError(projectInfo: ProjectInfo, errors: string list) = {
        Success = false
        ProjectInfo = projectInfo
        Areas = []
        Devices = []
        ApiDefinitions = []
        IOMapping = IOMapping.Empty
        LogicFlow = []
        Statistics = MappingStatistics.Empty
        Warnings = []
        Errors = errors
    }

