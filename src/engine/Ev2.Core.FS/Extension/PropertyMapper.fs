namespace Ev2.Core.FS.Extension

open System
open System.Reflection
open System.Collections.Concurrent
open FSharp.Reflection
open Dual.Common.Base

/// 속성 매핑 정보
type PropertyMapping = {
    SourceProperty: PropertyInfo
    TargetProperty: PropertyInfo
    RequiresConversion: bool
    DefaultValue: obj option
    IsExtensionProperty: bool
}

/// 리플렉션 기반 속성 복사 모듈
module PropertyMapper =

    // 캐시: (소스타입, 타겟타입) -> 매핑 배열
    let private mappingCache = ConcurrentDictionary<Type * Type, PropertyMapping[]>()

    /// 속성이 확장 속성인지 확인
    let private isExtensionProperty (prop: PropertyInfo) (baseType: Type option) =
        match baseType with
        | None -> false
        | Some bt ->
            // 기본 타입에 없는 속성이면 확장 속성
            bt.GetProperty(prop.Name) |> isItNull

    /// 두 타입 간 속성 매핑 생성
    let private createMapping (sourceType: Type) (targetType: Type) =
        let sourceProps = sourceType.GetProperties() |> Array.map (fun p -> p.Name, p) |> Map.ofArray
        let targetProps = targetType.GetProperties()

        targetProps
        |> Array.choose (fun targetProp ->
            // 쓰기 가능한 속성만 대상
            if not targetProp.CanWrite then None
            else
                // ExtensionPropertyAttribute 확인
                let attr = targetProp.GetCustomAttribute<ExtensionPropertyAttribute>()
                let sourceName =
                    if isItNull attr || String.IsNullOrEmpty(attr.SourceName)
                    then targetProp.Name
                    else attr.SourceName

                sourceProps.TryFind sourceName
                |> Option.map (fun sourceProp ->
                    {
                        SourceProperty = sourceProp
                        TargetProperty = targetProp
                        RequiresConversion = sourceProp.PropertyType <> targetProp.PropertyType
                        DefaultValue = if isItNull attr then None else Option.ofObj attr.DefaultValue
                        IsExtensionProperty = not (isItNull attr)
                    }))

    /// 속성값 타입 변환
    let private convertValue (value: obj) (targetType: Type) =
        match value with
        | null -> null
        | _ when value.GetType() = targetType -> value
        | _ when targetType = typeof<string> -> value.ToString() :> obj
        | _ when targetType.IsEnum ->
            try Enum.Parse(targetType, value.ToString())
            with _ -> Enum.ToObject(targetType, 0)
        | _ ->
            Convert.ChangeType(value, targetType)
            //try Convert.ChangeType(value, targetType)
            //with _ ->
            //    // 변환 실패 시 기본값 반환
            //    if targetType.IsValueType then
            //        Activator.CreateInstance(targetType)
            //    else
            //        null

    /// 확장 속성 복사 (캐싱 사용)
    let copyExtensionProperties (source: obj) (target: obj) =
        if isItNull source || isItNull target then ()
        else
            let sourceType = source.GetType()
            let targetType = target.GetType()

            let mapping =
                mappingCache.GetOrAdd(
                    (sourceType, targetType),
                    fun (s, t) -> createMapping s t)

            mapping
            |> Array.iter (fun map ->
                let value = map.SourceProperty.GetValue(source)
                let finalValue =
                    if map.RequiresConversion && isItNotNull value then
                        convertValue value map.TargetProperty.PropertyType
                    else
                        value

                let valueToSet =
                    match finalValue, map.DefaultValue with
                    | null, Some def -> def
                    | v, _ -> v

                map.TargetProperty.SetValue(target, valueToSet))

    /// 기본 타입과 확장 타입 간 차이 속성만 복사
    let copyOnlyExtensionProperties (source: obj) (target: obj) (baseType: Type) =
        if isItNull source || isItNull target then ()
        else
            let baseProps = baseType.GetProperties() |> Array.map (fun p -> p.Name) |> Set.ofArray
            let targetType = target.GetType()
            let sourceType = source.GetType()

            let extensionProps =
                targetType.GetProperties()
                |> Array.filter (fun p ->
                    p.CanWrite && not (baseProps.Contains p.Name))

            extensionProps
            |> Array.iter (fun targetProp ->
                match sourceType.GetProperty(targetProp.Name) with
                | null -> ()
                | sourceProp when sourceProp.CanRead ->
                    try
                        let value = sourceProp.GetValue(source)
                        let finalValue =
                            if sourceProp.PropertyType <> targetProp.PropertyType && isItNotNull value then
                                convertValue value targetProp.PropertyType
                            else
                                value
                        targetProp.SetValue(target, finalValue)
                    with ex ->
                        logWarn $"Failed to copy extension property {targetProp.Name}: {ex.Message}"
                | _ -> ())

    /// 모든 속성 복사 (기본 + 확장)
    [<Obsolete("삭제 예정")>]
    let copyAllProperties (source: obj) (target: obj) =
        if isItNull source || isItNull target then ()
        else
            let sourceType = source.GetType()
            let targetType = target.GetType()

            sourceType.GetProperties()
            |> Array.filter (fun p -> p.CanRead)
            |> Array.iter (fun sourceProp ->
                match targetType.GetProperty(sourceProp.Name) with
                | null -> ()
                | targetProp when targetProp.CanWrite ->
                    let value = sourceProp.GetValue(source)
                    let finalValue =
                        if sourceProp.PropertyType <> targetProp.PropertyType && isItNotNull value then
                            convertValue value targetProp.PropertyType
                        else
                            value
                    targetProp.SetValue(target, finalValue)
                | _ -> ())

    /// 특정 속성만 복사
    let copySpecificProperties (source: obj) (target: obj) (propertyNames: string[]) =
        if isItNull source || isItNull target then ()
        else
            let sourceType = source.GetType()
            let targetType = target.GetType()

            propertyNames
            |> Array.iter (fun propName ->
                match sourceType.GetProperty(propName), targetType.GetProperty(propName) with
                | null, _ | _, null -> ()
                | sourceProp, targetProp when sourceProp.CanRead && targetProp.CanWrite ->
                    let value = sourceProp.GetValue(source)
                    let finalValue =
                        if sourceProp.PropertyType <> targetProp.PropertyType && isItNotNull value then
                            convertValue value targetProp.PropertyType
                        else
                            value
                    targetProp.SetValue(target, finalValue)
                | _ -> ())

    /// 캐시 초기화
    let clearCache() =
        mappingCache.Clear()

    /// 캐시 통계 정보
    let getCacheStats() =
        {|
            CachedMappings = mappingCache.Count
            Keys = mappingCache.Keys |> Seq.map (fun (s, t) -> $"{s.Name} -> {t.Name}") |> Seq.toList
        |}