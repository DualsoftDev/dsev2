namespace Ev2.PLC.Mapper.Tests

open System
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Ev2.PLC.Common.Types
open Ev2.PLC.Mapper.Core.Types
open Ev2.PLC.Mapper

/// 간단한 테스트 및 예제 사용법
module SampleUsage =
    
    /// 예제 변수들
    let sampleVariables = [
        RawVariable.Create("AREA1_MOTOR01_FWD", "Q0.1", Bool, "모터1 전진")
        RawVariable.Create("AREA1_MOTOR01_BACK", "Q0.2", Bool, "모터1 후진")
        RawVariable.Create("AREA1_MOTOR01_RUNNING", "I0.1", Bool, "모터1 운전중")
        RawVariable.Create("AREA1_CYL01_UP", "Q0.3", Bool, "실린더1 상승")
        RawVariable.Create("AREA1_CYL01_DOWN", "Q0.4", Bool, "실린더1 하강")
        RawVariable.Create("AREA1_CYL01_UP_SENSOR", "I0.2", Bool, "실린더1 상한센서")
        RawVariable.Create("AREA1_CYL01_DOWN_SENSOR", "I0.3", Bool, "실린더1 하한센서")
        RawVariable.Create("AREA2_CONV01_START", "Q1.0", Bool, "컨베이어1 시작")
        RawVariable.Create("AREA2_CONV01_STOP", "Q1.1", Bool, "컨베이어1 정지")
        RawVariable.Create("AREA2_CONV01_SPEED", "MW100", Int32, "컨베이어1 속도")
    ]
    
    /// 예제 원시 프로그램
    let sampleProgram = {
        ProjectInfo = ProjectInfo.Create("TestProject", PlcVendor.CreateLSElectric(), LSElectricXML("test.xml"), "test.xml")
        Variables = sampleVariables
        Logic = [] // 로직은 나중에 구현
        Imports = []
        Exports = []
        Configuration = Map.empty
    }
    
    /// 매퍼 테스트 실행
    let runMapperTest () = async {
        try
            printfn "=== Ev2.PLC.Mapper 테스트 시작 ==="
            
            // 1. MapperFactory 생성
            let loggerFactory = NullLoggerFactory.Instance :> ILoggerFactory
            
            let factory = MapperFactory(loggerFactory)
            
            // 2. 변수 분석기 생성
            let analyzer = factory.CreateVariableAnalyzer()
            
            // 3. 매핑 설정 구성
            let config = MappingConfiguration.Default(PlcVendor.CreateLSElectric())
            
            printfn "분석할 변수 수: %d" sampleVariables.Length
            
            // 4. 변수 배치 분석
            let! analysisResults = analyzer.AnalyzeVariablesBatchAsync(sampleVariables, config)
            
            printfn "분석 완료 - 성공: %d, 실패: %d" 
                (analysisResults |> List.filter (fun r -> r.IsValid) |> List.length)
                (analysisResults |> List.filter (fun r -> not r.IsValid) |> List.length)
            
            // 5. 영역 추출
            let! areas = analyzer.ExtractAreasAsync(sampleVariables)
            printfn "추출된 영역: %A" (areas |> List.map (fun a -> a.Name))
            
            // 6. 디바이스 추출
            let! devices = analyzer.ExtractDevicesAsync(sampleVariables, areas)
            printfn "추출된 디바이스: %A" (devices |> List.map (fun d -> $"{d.Name} ({d.Type})"))
            
            // 7. API 정의 생성
            let! apiDefs = analyzer.GenerateApiDefinitionsAsync(devices)
            printfn "생성된 API: %d개" apiDefs.Length
            
            // 8. 결과 상세 출력
            printfn "\n=== 분석 결과 상세 ==="
            for result in analysisResults do
                if result.IsValid then
                    let deviceName = result.Device |> Option.map (fun d -> d.Name) |> Option.defaultValue "Unknown"
                    let apiName = result.Api |> Option.map (fun api -> api.Name) |> Option.defaultValue "Unknown"
                    printfn "✓ %s -> %s.%s (신뢰도: %.1f%%)" 
                        result.Variable.Name deviceName apiName (result.Confidence * 100.0)
                else
                    printfn "✗ %s - %s" result.Variable.Name (String.Join("; ", result.Issues))
            
            printfn "\n=== 디바이스별 API 구성 ==="
            for device in devices do
                printfn "📱 %s (%A)" device.Name device.Type
                let deviceApis = 
                    analysisResults 
                    |> List.choose (fun r -> 
                        match r.Device, r.Api with
                        | Some d, Some api when d.Name = device.Name -> Some api
                        | _ -> None)
                    |> List.distinctBy (fun api -> api.Name)
                
                for api in deviceApis do
                    let direction = if api.Direction = Input then "⬅️" else "➡️"
                    printfn "  %s %s (%A)" direction api.Name api.Type
            
            printfn "\n=== 테스트 완료 ==="
            return true
            
        with
        | ex ->
            printfn "❌ 테스트 실패: %s" ex.Message
            return false
    }
    
    /// 특정 제조사 파서 테스트
    let testParser (vendor: PlcVendor) = async {
        try
            printfn $"=== {vendor} 파서 테스트 ==="
            
            let loggerFactory = NullLoggerFactory.Instance :> ILoggerFactory
            
            let factory = MapperFactory(loggerFactory)
            
            match factory.CreateParser(vendor) with
            | Some parser ->
                printfn "✓ 파서 생성 성공"
                
                // 가상의 파일로 검증 테스트
                let! validationResult = parser.ValidateFileAsync("test.xml")
                printfn "검증 결과: %A" validationResult
                
                return true
            | None ->
                printfn "❌ 파서를 생성할 수 없습니다"
                return false
        with
        | ex ->
            printfn "❌ 파서 테스트 실패: %s" ex.Message
            return false
    }
    
    /// 명명 규칙 테스트
    let testNamingConventions () =
        printfn "=== 명명 규칙 테스트 ==="
        
        let testVariables = [
            "AREA1_MOTOR01_FWD"      // Standard pattern
            "CYL01_UP"               // Simple pattern  
            "SENSOR_VALUE"           // Simple pattern
            "InvalidName123"         // Should not match
        ]
        
        let conventions = NamingConvention.GetDefaults()
        
        for varName in testVariables do
            let matched = 
                conventions 
                |> List.tryPick (fun conv ->
                    try
                        let regex = System.Text.RegularExpressions.Regex(conv.Pattern)
                        let regexMatch = regex.Match(varName)
                        if regexMatch.Success then Some conv.Name else None
                    with _ -> None)
            
            match matched with
            | Some convention -> printfn "✓ %s -> %s 규칙" varName convention
            | None -> printfn "❌ %s -> 매칭 규칙 없음" varName
    
    /// 전체 테스트 실행
    let runAllTests () = async {
        printfn "🚀 Ev2.PLC.Mapper 통합 테스트 시작\n"
        
        // 명명 규칙 테스트
        testNamingConventions()
        printfn ""
        
        // 파서 테스트
        let! lsResult = testParser (PlcVendor.CreateLSElectric())
        let! abResult = testParser (PlcVendor.CreateAllenBradley())
        let! mitResult = testParser (PlcVendor.CreateMitsubishi())
        let! siemensResult = testParser (PlcVendor.CreateSiemens())
        printfn ""
        
        // 메인 매퍼 테스트
        let! mapperResult = runMapperTest()
        
        printfn "\n📊 테스트 결과 요약:"
        printfn $"  LS Electric 파서: {if lsResult then "✅" else "❌"}"
        printfn $"  Allen-Bradley 파서: {if abResult then "✅" else "❌"}"
        printfn $"  Mitsubishi 파서: {if mitResult then "⚠️ 미구현" else "❌"}"
        printfn $"  Siemens 파서: {if siemensResult then "⚠️ 미구현" else "❌"}"
        printfn $"  매퍼 엔진: {if mapperResult then "✅" else "❌"}"
        
        return mapperResult && lsResult && abResult
    }

/// 콘솔 앱 진입점 (테스트용)
module Program =
    [<EntryPoint>]
    let main args =
        try
            let result = SampleUsage.runAllTests() |> Async.RunSynchronously
            if result then 0 else 1
        with
        | ex ->
            printfn "💥 테스트 실행 중 오류 발생: %s" ex.Message
            1
