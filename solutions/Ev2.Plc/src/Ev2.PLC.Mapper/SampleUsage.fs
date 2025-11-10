namespace Ev2.PLC.Mapper.Samples

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Ev2.PLC.Mapper
open Ev2.PLC.Mapper.Core.Configuration

/// 외부 JSON 설정을 사용한 매핑 예제
module ConfigurationSamples =
    
    /// 기본 설정 파일을 사용한 매핑 예제
    let processWithDefaultConfigAsync (plcFilePath: string) = task {
        let configPath = "/path/to/mapper-config.json"
        let! result = MapperApi.processFileWithConfigFileAsync plcFilePath configPath
        
        match result.Success with
        | true -> 
            printfn "매핑 성공: %d개의 변수가 %d개의 영역, %d개의 장치로 매핑되었습니다." 
                result.Statistics.MappedVariables result.Statistics.TotalAreas result.Statistics.TotalDevices
        | false ->
            printfn "매핑 실패: %s" (String.Join("; ", result.Errors))
        
        return result
    }
    
    /// 커스텀 설정으로 팩토리 생성 및 사용 예제
    let createCustomFactoryExample() =
        let loggerFactory = LoggerFactory.Create(fun builder -> 
            builder.AddConsole() |> ignore)
        
        let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
        let configProvider = ConfigurationFactory.createProvider configLogger
        
        // 설정 로드
        let config = configProvider.LoadConfiguration(Some "/path/to/custom-config.json")
        printfn "설정 로드 완료: %d개의 장치 타입 힌트, %d개의 명명 규칙" 
            config.MappingConfiguration.DeviceTypeHints.Count config.MappingConfiguration.NamingConventions.Length
        
        // 팩토리 생성
        let factory = MapperFactory(loggerFactory, configProvider)
        factory
    
    /// 실행 중 설정 변경 예제
    let runtimeConfigurationChangeExample (factory: MapperFactory) = task {
        // 변수 분석기 생성
        let analyzer = factory.CreateVariableAnalyzer()
        
        // 초기 설정으로 분석
        let! result1 = factory.ProcessPlcProgramAsync("/path/to/program1.xml")
        printfn "초기 설정으로 매핑된 변수: %d개" result1.Statistics.MappedVariables
        
        // 설정 변경 (analyzer가 VariableAnalyzer 타입인 경우)
        match analyzer with
        | :? Ev2.PLC.Mapper.Core.Engine.VariableAnalyzer as va ->
            va.ReloadConfiguration(Some "/path/to/new-config.json")
            printfn "새로운 설정으로 변경됨"
        | _ -> ()
        
        // 새로운 설정으로 분석
        let! result2 = factory.ProcessPlcProgramAsync("/path/to/program2.xml")
        printfn "새로운 설정으로 매핑된 변수: %d개" result2.Statistics.MappedVariables
        
        return result2
    }
    
    /// 설정 생성 및 저장 예제
    let createAndSaveConfigExample() =
        let loggerFactory = LoggerFactory.Create(fun builder -> 
            builder.AddConsole() |> ignore)
        
        let configLogger = loggerFactory.CreateLogger<ConfigurationProvider>()
        let configProvider = ConfigurationFactory.createProvider configLogger
        
        // 기본 설정 가져오기
        let config = configProvider.LoadConfiguration(None)
        
        // 설정 수정 (예: 새로운 장치 타입 힌트 추가)
        config.MappingConfiguration.DeviceTypeHints.["ROBOT"] <- "Custom"
        config.MappingConfiguration.DeviceTypeHints.["AGV"] <- "Conveyor"
        
        // 설정 저장
        match configProvider.SaveConfiguration(config, "/path/to/modified-config.json") with
        | Ok () -> printfn "설정이 성공적으로 저장되었습니다."
        | Error msg -> printfn "설정 저장 실패: %s" msg
    
    /// JSON 설정 파일 검증 예제
    let validateConfigurationExample() =
        let loggerFactory = LoggerFactory.Create(fun builder -> 
            builder.AddConsole() |> ignore)
        
        let configLogger = loggerFactory.CreateLogger<ConfigurationLoader>()
        let loader = ConfigurationFactory.createLoader configLogger
        
        let configPath = "/path/to/test-config.json"
        
        match loader.LoadFromFile(configPath) with
        | Ok config ->
            printfn "설정 파일이 유효합니다:"
            printfn "- 장치 타입 힌트: %d개" config.MappingConfiguration.DeviceTypeHints.Count
            printfn "- 명명 규칙: %d개" config.MappingConfiguration.NamingConventions.Length
            printfn "- 커스텀 패턴: %d개" config.CustomPatterns.Length
        | Error msg ->
            printfn "설정 파일이 유효하지 않습니다: %s" msg

/// 사용법 예제 실행
module UsageExamples =
    
    let runAllExamplesAsync() = task {
        Console.WriteLine("=== Ev2.PLC.Mapper 외부 설정 사용 예제 ===")
        
        // 1. 기본 설정 파일 사용
        Console.WriteLine("\n1. 기본 설정 파일 사용 예제")
        let! _ = ConfigurationSamples.processWithDefaultConfigAsync "/path/to/sample.xml"
        
        // 2. 커스텀 팩토리 생성
        Console.WriteLine("\n2. 커스텀 팩토리 생성 예제")
        let factory = ConfigurationSamples.createCustomFactoryExample()
        
        // 3. 실행 중 설정 변경
        Console.WriteLine("\n3. 실행 중 설정 변경 예제")
        let! _ = ConfigurationSamples.runtimeConfigurationChangeExample factory
        
        // 4. 설정 생성 및 저장
        Console.WriteLine("\n4. 설정 생성 및 저장 예제")
        ConfigurationSamples.createAndSaveConfigExample()
        
        // 5. 설정 파일 검증
        Console.WriteLine("\n5. 설정 파일 검증 예제")
        ConfigurationSamples.validateConfigurationExample()
        
        Console.WriteLine("\n=== 모든 예제 완료 ===")
    }