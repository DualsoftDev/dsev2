namespace Ev2.PLC.Mapper.Test.TestData

open System
open System.Collections.Generic
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper.Core.Configuration

/// 테스트용 데이터 타입들
module TestDataTypes =

    /// 테스트 시나리오 타입
    type TestScenario = {
        Name: string
        Description: string
        InputFile: string
        ExpectedResults: ExpectedMappingResult
        Config: MapperConfigRoot option
    }

    /// 기대되는 매핑 결과
    and ExpectedMappingResult = {
        ShouldSucceed: bool
        ExpectedVariableCount: int option
        ExpectedAreaCount: int option
        ExpectedDeviceCount: int option
        ExpectedApiCount: int option
        ExpectedErrors: string list
        ExpectedWarnings: string list
    }

    /// 테스트 변수 생성기
    type TestVariableBuilder() =
        let mutable name = ""
        let mutable address = ""
        let mutable dataType = "BOOL"
        let mutable comment = None
        let mutable initialValue = None
        let mutable scope = None
        
        member this.WithName(n: string) = 
            name <- n
            this
            
        member this.WithAddress(addr: string) = 
            address <- addr
            this
            
        member this.WithDataType(dt: string) = 
            dataType <- dt
            this
            
        member this.WithComment(c: string) = 
            comment <- Some c
            this
            
        member this.WithInitialValue(iv: string) = 
            initialValue <- Some iv
            this
            
        member this.WithScope(s: string) = 
            scope <- Some s
            this
            
        member this.Build() : RawVariable = {
            Name = name
            Address = address
            DataType = dataType
            Comment = comment
            InitialValue = initialValue
            Scope = scope
            AccessLevel = None
            Properties = Map.empty
        }

    /// 테스트 프로젝트 정보 생성기
    type TestProjectInfoBuilder() =
        let mutable name = "TestProject"
        let mutable version = "1.0.0"
        let mutable vendor = PlcVendor.CreateLSElectric()
        let mutable format = LSElectricXML ""
        let mutable filePath = "test.xml"
        
        member this.WithName(n: string) = 
            name <- n
            this
            
        member this.WithVendor(v: PlcVendor) = 
            vendor <- v
            this
            
        member this.WithFormat(f: PlcProgramFormat) = 
            format <- f
            this
            
        member this.WithFilePath(fp: string) = 
            filePath <- fp
            this
            
        member this.Build() : ProjectInfo = {
            Name = name
            Version = version
            Vendor = vendor
            Format = format
            CreatedDate = DateTime.UtcNow
            ModifiedDate = DateTime.UtcNow
            Description = Some "Test project"
            Author = Some "Test Author"
            FilePath = filePath
            FileSize = 1024L
        }

    /// 테스트용 설정 생성기
    type TestConfigBuilder() =
        let mutable deviceHints = Dictionary<string, string>()
        let mutable apiCommands = Dictionary<string, string>()
        let mutable apiStatus = Dictionary<string, string>()
        let mutable apiParameters = Dictionary<string, string>()
        let mutable apiFeedback = Dictionary<string, string>()
        
        member this.AddDeviceHint(key: string, value: string) =
            deviceHints.[key] <- value
            this
            
        member this.AddApiCommand(key: string) =
            apiCommands.[key] <- "Command"
            this
            
        member this.AddApiStatus(key: string) =
            apiStatus.[key] <- "Status"
            this
            
        member this.Build() : MapperConfigRoot = {
            MappingConfiguration = {
                DeviceTypeHints = deviceHints
                ApiTypeHints = {
                    Commands = apiCommands
                    Status = apiStatus
                    Parameters = apiParameters
                    Feedback = apiFeedback
                }
                NamingConventions = [|
                    {
                        Name = "Test"
                        Pattern = @"^(?<area>[A-Z0-9]+)_(?<device>[A-Z0-9_]+)_(?<api>[A-Z]+)$"
                        Description = "Test pattern"
                        DeviceTypeHints = Dictionary<string, string>()
                        ApiTypeHints = Dictionary<string, string>()
                        Priority = 1
                    }
                |]
                DeviceInferencePatterns = {
                    FallbackPatterns = [||]
                }
                ApiInferenceRules = {
                    DeviceSpecificRules = [||]
                    GeneralRules = [||]
                }
                ConfidenceBoosts = {
                    DeviceHintBoost = 0.1
                    ApiHintBoost = 0.1
                    PatternMatchBoost = 0.05
                }
                DefaultConfidenceLevels = {
                    FullMatch = 0.9
                    DeviceAndApi = 0.8
                    AreaAndDevice = 0.6
                    DeviceOnly = 0.5
                    Fallback = 0.3
                }
            }
            MappingOptions = {
                AnalyzeLogicFlow = true
                GenerateApiDependencies = true
                OptimizeAddressAllocation = true
                ValidateNaming = true
                GenerateDocumentation = false
                IncludeStatistics = true
                ParallelProcessing = false  // 테스트에서는 단일 스레드
                MaxConcurrency = 1
            }
            ValidationRules = {
                VariableNaming = {
                    MaxLength = 100
                    AllowedCharacters = @"^[A-Za-z0-9_]+$"
                    ReservedWords = Some [| "TEST" |]
                }
                DeviceNaming = {
                    MaxLength = 50
                    AllowedCharacters = @"^[A-Z0-9_]+$"
                    ReservedWords = None
                }
                ApiNaming = {
                    MaxLength = 30
                    AllowedCharacters = @"^[A-Z0-9_]+$"
                    ReservedWords = None
                }
            }
            AddressRanges = {
                LSElectric = {|
                    InputAreas = [||]
                    OutputAreas = [||]
                    MemoryAreas = [||]
                |}
            }
            CustomPatterns = [||]
        }

    /// 테스트 어설션 도우미
    module TestAssertions =
        
        let assertMappingResult (expected: ExpectedMappingResult) (actual: MappingResult) =
            // 성공 여부 확인
            if expected.ShouldSucceed && not actual.Success then
                failwithf "Expected success but got failure. Errors: %A" actual.Errors
            elif not expected.ShouldSucceed && actual.Success then
                failwithf "Expected failure but got success"
            
            // 변수 수 확인
            match expected.ExpectedVariableCount with
            | Some count when actual.Statistics.TotalVariables <> count ->
                failwithf "Expected %d variables but got %d" count actual.Statistics.TotalVariables
            | _ -> ()
            
            // 영역 수 확인
            match expected.ExpectedAreaCount with
            | Some count when actual.Statistics.TotalAreas <> count ->
                failwithf "Expected %d areas but got %d" count actual.Statistics.TotalAreas
            | _ -> ()
            
            // 장치 수 확인
            match expected.ExpectedDeviceCount with
            | Some count when actual.Statistics.TotalDevices <> count ->
                failwithf "Expected %d devices but got %d" count actual.Statistics.TotalDevices
            | _ -> ()
            
            // API 수 확인
            match expected.ExpectedApiCount with
            | Some count when actual.Statistics.TotalApis <> count ->
                failwithf "Expected %d APIs but got %d" count actual.Statistics.TotalApis
            | _ -> ()
        
        let assertContainsError (expectedError: string) (actual: MappingResult) =
            if not (actual.Errors |> List.exists (fun e -> e.Contains(expectedError))) then
                failwithf "Expected error containing '%s' but got errors: %A" expectedError actual.Errors
        
        let assertContainsWarning (expectedWarning: string) (actual: MappingResult) =
            if not (actual.Warnings |> List.exists (fun w -> w.Contains(expectedWarning))) then
                failwithf "Expected warning containing '%s' but got warnings: %A" expectedWarning actual.Warnings