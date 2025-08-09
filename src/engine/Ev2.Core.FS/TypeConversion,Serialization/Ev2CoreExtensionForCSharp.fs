namespace Ev2.Core.FS

open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json
open System
open System.Runtime.CompilerServices
open System.IO

[<Extension>]
type Ev2CoreExtensionForCSharp =

    // Project 확장 메서드 - C# 전용
    [<Extension>]
    static member CsToJson(project:Project): string =
        // EmJson을 사용하여 $type 정보 포함
        let njProject = project.ToNjObj() :?> NjProject
        EmJson.ToJson(njProject)
    
    [<Extension>]
    static member CsToJson(project:Project, filePath:string): string =
        let njProject = project.ToNjObj() :?> NjProject
        let json = EmJson.ToJson(njProject)
        File.WriteAllText(filePath, json)
        json
    
    // DsSystem 확장 메서드 - C# 전용
    [<Extension>]
    static member CsExportToJson(system:DsSystem): string =
        let njSystem = system.ToNj<NjSystem>()
        njSystem.ExportToJson()
    
    [<Extension>]
    static member CsExportToJson(system:DsSystem, filePath:string): string =
        let njSystem = system.ToNj<NjSystem>()
        njSystem.ExportToJsonFile(filePath)
    
    // 범용 RtUnique 확장 메서드
    [<Extension>]
    static member CsToJson(rtObj:RtUnique): string =
        match rtObj with
        | :? Project as p -> 
            let njProject = p.ToNjObj() :?> NjProject
            njProject.ToJson()
        | :? DsSystem as s -> 
            let njSystem = s.ToNj<NjSystem>()
            njSystem.ExportToJson()
        | _ -> 
            // 다른 타입들에 대한 기본 JSON 직렬화
            let njObj = rtObj.ToNjObj()
            EmJson.ToJson(njObj)

// Project 타입에 대한 정적 메서드 (C#에서 ProjectExtensions.CsFromJson() 형태로 사용)
type ProjectExtensions =
    static member CsFromJson(json:string): Project =
        // JSON을 JObject로 파싱하여 RuntimeType 확인
        let jObj = Newtonsoft.Json.Linq.JObject.Parse(json)
        let runtimeTypeName = 
            match jObj.["RuntimeType"] with
            | null -> "NjProject"
            | token -> token.ToString()
        
        // TypeFactory를 통해 RuntimeType에 맞는 JSON 타입 찾기
        let njProject = 
            match getTypeFactory() with
            | Some factory ->
                // RuntimeType 문자열로 JSON 타입 찾기
                match factory.FindJsonTypeByName(runtimeTypeName) with
                | null -> 
                    // 타입을 찾지 못하면 기본 NjProject로 역직렬화
                    EmJson.FromJson<NjProject>(json)
                | jsonType ->
                    // 찾은 타입으로 동적 역직렬화
                    let genericMethod = typeof<EmJson>.GetMethod("FromJson", [| typeof<string> |]).MakeGenericMethod([|jsonType|])
                    genericMethod.Invoke(null, [|json|]) :?> NjProject
            | None ->
                // TypeFactory가 없으면 기본 NjProject로 역직렬화
                EmJson.FromJson<NjProject>(json)
        
        njProject
        |> NewtonsoftJsonModules.getRuntimeObject<Project>
        |> validateRuntime

// DsSystem 타입에 대한 정적 메서드
type DsSystemExtensions =
    static member CsImportFromJson(json:string): DsSystem =
        json
        |> NjSystem.ImportFromJson
        |> NewtonsoftJsonModules.getRuntimeObject<DsSystem>
        |> validateRuntime
    
    static member CsFromJson(json:string): DsSystem =
        DsSystemExtensions.CsImportFromJson(json)
