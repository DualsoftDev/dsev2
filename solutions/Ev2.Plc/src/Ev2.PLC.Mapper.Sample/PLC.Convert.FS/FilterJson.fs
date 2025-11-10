namespace PLC.Convert.FS

open System
open System.Text
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open System.Collections.Generic
open PLC.Convert.LSCore.Expression

module FilterJsonModule =

    
    /// **Step 1: JSON 설정 파일 로드**
    /// `filters.json` 파일을 자동으로 불러와 설정을 적용합니다.
    let loadFilterConfig () =
        let filePath = Path.Combine(__SOURCE_DIRECTORY__, "filters.json")
        if File.Exists(filePath) then
            let json = File.ReadAllText(filePath)
            JsonSerializer.Deserialize<Map<string, obj>>(json)
        else
            failwithf "filters.json 파일을 찾을 수 없습니다: %s" filePath
        /// JSON 데이터를 `Map<string, string>`으로 변환하는 함수**
    let loadJsonMap (jsonElement: obj) =
        match jsonElement with
        | :? JsonElement as v -> 
            v.EnumerateObject()
            |> Seq.map (fun kvp -> kvp.Name, kvp.Value.GetString())
            |> Map.ofSeq
        | _ -> failwithf "JSON 변환 오류: %s" (jsonElement.ToString())

    /// **JSON에서 특정 키를 `List<string>`으로 변환하는 함수**
    let loadJsonList (jsonElement: obj) =
        match jsonElement with
        | :? JsonElement as v -> 
            v.EnumerateArray()
            |> Seq.map (fun elem -> elem.GetString())
            |> List.ofSeq
        | _ -> failwithf "JSON 변환 오류: %s" (jsonElement.ToString())

    /// **JSON 설정 로드**
    let config = loadFilterConfig ()
    let skipKeywords = loadJsonList config.["skipKeywords"]
    let autoKeywords = loadJsonList config.["autoKeywords"]
    let safetyKeywords = loadJsonList config.["safetyKeywords"]
    let areaExtenstionKeywords = loadJsonList config.["areaExtenstionKeywords"]
    let devicePostKeywords = loadJsonList config.["devicePostKeywords"]
    let splitAreaKeywords = loadJsonList config.["splitAreaKeywords"]
    let targetReplacements = loadJsonMap config.["targetReplacements"]
    let sourceReplacements = loadJsonMap config.["sourceReplacements"]

    