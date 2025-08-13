namespace Ev2.Core.FS

open Newtonsoft.Json.Linq
open System
open System.Reflection
open Ev2.Core.FS.Extension
open Dual.Common.Base

/// 확장 속성 수집 모듈
module ExtensionPropertyCollector =

    /// 기본값인지 확인
    let private isDefaultValue (value: obj) (propertyType: Type) =
        match value with
        | null -> true
        | _ when propertyType.IsValueType ->
            let defaultValue = Activator.CreateInstance(propertyType)
            obj.Equals(value, defaultValue)
        | _ -> false

    /// 확장 속성 자동 수집
    let collectExtensionProperties (obj: obj) : JToken[] =
        if isItNull obj then [||]
        else
            let objType = obj.GetType()
            let baseType = objType.BaseType

            // 기본 타입의 속성 이름들
            let baseProps =
                if isItNull baseType then Set.empty
                else
                    baseType.GetProperties()
                    |> Array.map (fun p -> p.Name)
                    |> Set.ofArray

            // 확장 속성만 필터링
            let extensionProps =
                objType.GetProperties()
                |> Array.filter (fun p ->
                    p.CanRead &&
                    not (baseProps.Contains p.Name) &&
                    // JsonIgnoreExtensionAttribute 확인
                    p.GetCustomAttribute<JsonIgnoreExtensionAttribute>() |> isItNull &&
                    // Newtonsoft.Json.JsonIgnoreAttribute 확인
                    p.GetCustomAttribute<Newtonsoft.Json.JsonIgnoreAttribute>() |> isItNull)

            let tokens = ResizeArray<JToken>()

            // 확장 속성들을 JToken으로 변환
            for prop in extensionProps do
                let value = prop.GetValue(obj)

                // ExtensionPropertyAttribute 확인
                let attr = prop.GetCustomAttribute<ExtensionPropertyAttribute>()
                let includeInJson =
                    if isItNull attr then true
                    else attr.IncludeInJson

                if includeInJson && not (isDefaultValue value prop.PropertyType) then
                    tokens.Add(JProperty(prop.Name, JToken.FromObject(value)))
                    logDebug $"Collected extension property: {prop.Name} = {value}"

            // 타입 정보 추가 (역직렬화용)
            tokens.Add(JProperty("ExtensionType", objType.FullName))

            logDebug $"Collected {tokens.Count - 1} extension properties from {objType.Name}"

            tokens.ToArray()

    /// NjObject의 CollectExtensionProperties 구현을 위한 헬퍼
    let createCollector (njType: Type) =
        fun (njObj: obj) ->
            collectExtensionProperties njObj

    /// 확장 속성을 JSON 객체에 적용
    let applyExtensionProperties (jsonObject: JObject) (extensionProperties: JToken[]) =
        if isItNotNull jsonObject && not (Array.isEmpty extensionProperties) then
            for prop in extensionProperties do
                match prop with
                | :? JProperty as jprop ->
                    jsonObject.[jprop.Name] <- jprop.Value
                | _ -> ()

    /// JSON에서 확장 속성 추출
    let extractExtensionProperties (jsonObject: JObject) (baseType: Type) =
        if isItNull jsonObject || isItNull baseType then [||]
        else
            // 기본 타입의 속성 이름들
            let baseProps =
                baseType.GetProperties()
                |> Array.map (fun p -> p.Name)
                |> Set.ofArray

            // 기본 타입에 없는 속성들만 추출
            jsonObject.Properties()
            |> Seq.filter (fun prop -> not (baseProps.Contains prop.Name))
            |> Seq.map (fun prop -> prop :> JToken)
            |> Seq.toArray

    /// 확장 타입 정보 추출
    let getExtensionTypeFromJson (jsonObject: JObject) =
        if isItNull jsonObject then None
        else
            match jsonObject.["ExtensionType"] with
            | null -> None
            | token ->
                let typeName = token.ToString()
                // TypeRegistry에서 타입 찾기
                TypeRegistryModule.getRegistry().FindTypeByName(typeName)

    /// 디버그용 확장 속성 정보 출력
    let debugPrintExtensionProperties (obj: obj) =
        if isItNull obj then
            printfn "Object is null"
        else
            let properties = collectExtensionProperties obj
            printfn "Extension properties for %s:" (obj.GetType().Name)
            for prop in properties do
                match prop with
                | :? JProperty as jprop ->
                    printfn "  %s = %A" jprop.Name jprop.Value
                | _ ->
                    printfn "  %A" prop