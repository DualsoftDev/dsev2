namespace Dual.EV2.JsonIO

open System
open System.IO
open Newtonsoft.Json
open Dual.EV2.Core
open System.Collections.Generic

/// 프로젝트 루트 타입 (기존 도메인 타입용)
type Root = {
    Projects: Project[]
}

// --------- Raw JSON 타입 정의 (순수 데이터 구조) -----------
[<CLIMutable>]
type RawApiDefRef = {
    name: string
    system: string
}

[<CLIMutable>]
type RawApiCall = {
    name: string
    targetApiDef: RawApiDefRef
}

[<CLIMutable>]
type RawCall = {
    name: string
    apiCalls: RawApiCall[]
}

[<CLIMutable>]
type RawWork = {
    name: string
    calls: RawCall[]
    callGraph: string[][]
}

[<CLIMutable>]
type RawFlow = {
    name: string
    works: RawWork[]
    workGraph: string[][]
}

[<CLIMutable>]
type RawSystem = {
    name: string
    flows: RawFlow[]
    apiDefs: RawApiDefRef[]
}

module JsonIO =

    // JSON 직렬화 설정 (기존 도메인 객체용) - 재귀 참조 방지 강화
    let private settings = JsonSerializerSettings(
        TypeNameHandling = TypeNameHandling.None,
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,  // 순환 참조 무시
        PreserveReferencesHandling = PreserveReferencesHandling.None,  // 참조 보존 비활성화
        MaxDepth = 10,  // 최대 깊이 제한
        NullValueHandling = NullValueHandling.Ignore,  // null 값 무시
        DefaultValueHandling = DefaultValueHandling.Ignore  // 기본값 무시
    )

    // 단순한 JSON 설정 (Raw 타입용)
    let private simpleSettings = JsonSerializerSettings(
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore
    )

    // 커스텀 ContractResolver로 순환 참조 필드 제외
    type SafeContractResolver() =
        inherit Newtonsoft.Json.Serialization.DefaultContractResolver()
        
        override this.CreateProperty(memberInfo, memberSerialization) =
            let property = base.CreateProperty(memberInfo, memberSerialization)
            
            // 순환 참조를 일으킬 수 있는 속성들 제외
            let excludeProperties = [
                "Parent"; "Owner"; "System"; "Flow"; "Work"; "Call"  // 부모 참조
                "Children"; "Items"; "Collection"  // 컬렉션 참조
            ]
            
            if excludeProperties |> List.contains property.PropertyName then
                property.ShouldSerialize <- fun _ -> false
            
            property

    // 안전한 직렬화 설정
    let private safeSettings = JsonSerializerSettings(
        TypeNameHandling = TypeNameHandling.None,
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        PreserveReferencesHandling = PreserveReferencesHandling.None,
        MaxDepth = 8,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        ContractResolver = SafeContractResolver()
    )

    // JSON 저장 (기존 도메인 객체) - 재귀 저장 방지
    let saveToFile path (data: Root) =
        try
            let json = JsonConvert.SerializeObject(data, safeSettings)
            File.WriteAllText(path, json)
            printfn $"Successfully saved to: {path}"
        with
        | :? JsonSerializationException as ex ->
            // 재귀 문제가 발생하면 더 안전한 설정으로 재시도
            let ultraSafeSettings = JsonSerializerSettings(
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                MaxDepth = 3,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            )
            
            try
                let json = JsonConvert.SerializeObject(data, ultraSafeSettings)
                File.WriteAllText(path, json)
                printfn $"Saved with simplified structure to: {path}"
                printfn $"Warning: Some nested references may have been omitted due to complexity"
            with
            | ex2 -> 
                failwith $"JSON serialization failed even with safe settings: {ex2.Message}\nOriginal error: {ex.Message}"
        | ex ->
            failwith $"File write error: {ex.Message}"

    // JSON 로드 (기존 도메인 객체)
    let loadFromFile path : Root =
        let json = File.ReadAllText(path)
        JsonConvert.DeserializeObject<Root>(json, settings)

    // --------- 파일에서 시스템 로딩 (수정된 버전) -----------
    let loadSystemFromJson (filePath: string) (instanceName: string) (project: Project) (systemDic: Dictionary<string, System>) : System =
        try
            let json = File.ReadAllText(filePath)
            let raw: RawSystem = JsonConvert.DeserializeObject<RawSystem>(json, simpleSettings)

            let system = System(instanceName, project)

            for rawApiDef in raw.apiDefs do
                let apiDef = ApiDef(rawApiDef.name, system)
                system.ApiDefs.Add(apiDef)

            for rawFlow in raw.flows do
                let flow = Flow(rawFlow.name, system)
                system.Flows.Add(flow)

                // Work 이름 → Work 객체 맵핑
                let workMap = System.Collections.Generic.Dictionary<string, Work>()

                for rawWork in rawFlow.works do
                    let work = Work(rawWork.name, system, flow)
                    system.Works.Add(work)
                    workMap[work.Name] <- work

                    let callMap = System.Collections.Generic.Dictionary<string, Call>()

                    // Call 정의 처리
                    if not (isNull rawWork.calls) then
                        for rc in rawWork.calls do
                            let call = Call(rc.name, work)
                            work.Calls.Add(call)
                            callMap[call.Name] <- call

                            if not (isNull rc.apiCalls) then
                                for api in rc.apiCalls do
                                    match systemDic.TryGetValue(api.targetApiDef.system) with
                                    | true, targetSys -> 
                                        let apiDef = ApiDef(api.targetApiDef.name, targetSys)
                                        let apiCall = ApiCall(api.name, call, apiDef)
                                        call.ApiCalls.Add(apiCall)
                                    | _ -> 
                                        printfn $"Warning: System '{api.targetApiDef.system}' not found for ApiCall '{api.name}'"

                    // CallGraph 처리 - 누락된 Call들을 자동으로 생성
                    if not (isNull rawWork.callGraph) then
                        for edge in rawWork.callGraph do
                            if edge.Length >= 2 then
                                let src, tgt = edge.[0], edge.[1]
                                
                                // 소스 Call이 없으면 생성
                                if not (callMap.ContainsKey(src)) then
                                    let missingCall = Call(src, work)
                                    work.Calls.Add(missingCall)
                                    callMap[src] <- missingCall
                                    printfn $"Info: Created missing Call '{src}' in Work '{rawWork.name}'"
                                
                                // 타겟 Call이 없으면 생성
                                if not (callMap.ContainsKey(tgt)) then
                                    let missingCall = Call(tgt, work)
                                    work.Calls.Add(missingCall)
                                    callMap[tgt] <- missingCall
                                    printfn $"Info: Created missing Call '{tgt}' in Work '{rawWork.name}'"
                                
                                work.CallGraph.Add((callMap[src], callMap[tgt]))

                // WorkGraph 처리 - 누락된 Work들을 자동으로 생성
                if not (isNull rawFlow.workGraph) then
                    for edge in rawFlow.workGraph do
                        if edge.Length >= 2 then
                            let src, tgt = edge.[0], edge.[1]
                            
                            // 소스 Work가 없으면 생성
                            if not (workMap.ContainsKey(src)) then
                                let missingWork = Work(src, system, flow)
                                system.Works.Add(missingWork)
                                workMap[src] <- missingWork
                                printfn $"Info: Created missing Work '{src}' in Flow '{rawFlow.name}'"
                            
                            // 타겟 Work가 없으면 생성
                            if not (workMap.ContainsKey(tgt)) then
                                let missingWork = Work(tgt, system, flow)
                                system.Works.Add(missingWork)
                                workMap[tgt] <- missingWork
                                printfn $"Info: Created missing Work '{tgt}' in Flow '{rawFlow.name}'"
                            
                            system.WorkArrows.Add((workMap[src], workMap[tgt]))

            systemDic.Add(system.Name, system)
            system

        with
        | :? JsonSerializationException as ex ->
            failwith $"JSON deserialization failed: {ex.Message}\nPath: {ex.Path}"
        | :? FileNotFoundException ->
            failwith $"File not found: {filePath}"
        | :? UnauthorizedAccessException ->
            failwith $"Access denied to file: {filePath}"
        | ex ->
            failwith $"Unexpected error loading system from JSON: {ex.Message}"

    // Raw JSON 데이터를 파일에 저장 (테스트/디버깅용)
    let saveRawSystemToJson (filePath: string) (rawSystem: RawSystem) =
        let json = JsonConvert.SerializeObject(rawSystem, simpleSettings)
        File.WriteAllText(filePath, json)

    // Raw JSON을 직접 로드 (테스트/디버깅용)
    let loadRawSystemFromJson (filePath: string) : RawSystem =
        let json = File.ReadAllText(filePath)
        JsonConvert.DeserializeObject<RawSystem>(json, simpleSettings)

    