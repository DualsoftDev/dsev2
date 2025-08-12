namespace Ev2.Core.FS.Extension

open System
open System.Reflection
open Dual.Common.Base

/// 확장 시스템 헬퍼 함수들
[<AutoOpen>]
module ExtensionHelpers =
    
    /// Option 타입 변환 헬퍼
    let inline optionOfObj (obj: obj) : 'T option =
        if isItNull obj then None
        else Some (obj :?> 'T)
    
    /// 안전한 타입 캐스팅
    let inline tryCast<'T> (obj: obj) : 'T option =
        match obj with
        | :? 'T as t -> Some t
        | _ -> None
    
    /// 타입이 특정 인터페이스를 구현하는지 확인
    let implementsInterface (interfaceType: Type) (targetType: Type) =
        targetType.GetInterfaces() |> Array.exists (fun i -> i = interfaceType)
    
    /// 타입이 특정 기본 클래스를 상속하는지 확인
    let inheritsFrom (baseType: Type) (targetType: Type) =
        let rec checkBase (t: Type) =
            if isItNull t || t = typeof<obj> then false
            elif t = baseType then true
            else checkBase t.BaseType
        
        checkBase targetType
    
    /// 속성이 읽기/쓰기 가능한지 확인
    let isReadWriteProperty (prop: PropertyInfo) =
        prop.CanRead && prop.CanWrite
    
    /// 기본값 생성
    let getDefaultValue (targetType: Type) =
        if targetType.IsValueType then
            Activator.CreateInstance(targetType)
        else
            null
    
    /// 타입 이름 포맷팅
    let rec formatTypeName (t: Type) =
        if t.IsGenericType then
            let name = t.Name.Substring(0, t.Name.IndexOf('`'))
            let args = t.GetGenericArguments() |> Array.map formatTypeName |> String.concat ", "
            sprintf "%s<%s>" name args
        else
            t.Name
    
    /// 확장 타입인지 확인
    let isExtensionType (t: Type) =
        t.GetCustomAttribute<ExtensionTypeAttribute>() |> isItNull |> not ||
        t.Name.StartsWith("Custom") ||
        t.Name.StartsWith("Ext")
    
    /// 기본 타입과 확장 타입 간 속성 차이 가져오기
    let getExtensionProperties (extensionType: Type) (baseType: Type) =
        let baseProps = baseType.GetProperties() |> Array.map (fun p -> p.Name) |> Set.ofArray
        
        extensionType.GetProperties()
        |> Array.filter (fun p -> not (baseProps.Contains p.Name))
    
    /// 속성값 비교
    let arePropertiesEqual (obj1: obj) (obj2: obj) (propertyNames: string[]) =
        if isItNull obj1 || isItNull obj2 then false
        else
            let type1 = obj1.GetType()
            let type2 = obj2.GetType()
            
            propertyNames
            |> Array.forall (fun propName ->
                match type1.GetProperty(propName), type2.GetProperty(propName) with
                | null, _ | _, null -> false
                | prop1, prop2 ->
                    let val1 = prop1.GetValue(obj1)
                    let val2 = prop2.GetValue(obj2)
                    obj.Equals(val1, val2))
    
    /// 디버그 정보 생성
    let getDebugInfo (obj: obj) =
        if isItNull obj then "null"
        else
            let t = obj.GetType()
            let props = 
                t.GetProperties()
                |> Array.filter (fun p -> p.CanRead)
                |> Array.map (fun p ->
                    try
                        let value = p.GetValue(obj)
                        sprintf "%s=%A" p.Name value
                    with _ ->
                        sprintf "%s=<error>" p.Name)
                |> String.concat ", "
            
            sprintf "%s { %s }" (formatTypeName t) props

/// 확장 시스템 초기화 모듈
module ExtensionSystem =
    
    let mutable private isInitialized = false
    
    /// 확장 시스템 초기화
    let initialize() =
        if isInitialized then
            logDebug "Extension system already initialized"
        else
            try
                // TypeRegistry 초기화
                let registry = TypeRegistryModule.getRegistry()
                
                // 현재 로드된 어셈블리 스캔
                let registrations = TypeScanner.scanAllAssemblies() |> Seq.toArray
                
                // 발견된 타입 등록
                registrations
                |> Array.iter (fun info ->
                    registry.RegisterType(
                        info.BaseType,
                        info.ExtensionType,
                        info.NjType,
                        info.OrmType))
                
                isInitialized <- true
                
                logInfo $"Extension system initialized: {registrations.Length} types registered"
                
                // 등록된 타입 정보 출력 (디버그용)
                if registrations.Length > 0 then
                    registrations
                    |> Array.iter (fun info ->
                        let njInfo = info.NjType |> Option.map (fun t -> t.Name) |> Option.defaultValue "None"
                        let ormInfo = info.OrmType |> Option.map (fun t -> t.Name) |> Option.defaultValue "None"
                        logDebug $"  {info.BaseType.Name} -> {info.ExtensionType.Name} (NJ: {njInfo}, ORM: {ormInfo})")
            with ex ->
                logError $"Failed to initialize extension system: {ex.Message}"
                raise ex
    
    /// 특정 어셈블리만 스캔하여 등록
    let registerAssembly (assembly: Assembly) =
        try
            let registry = TypeRegistryModule.getRegistry()
            registry.RegisterAssembly(assembly)
            logDebug $"Registered assembly: {assembly.GetName().Name}"
        with ex ->
            logWarn $"Failed to register assembly {assembly.GetName().Name}: {ex.Message}"
    
    /// 확장 시스템 리셋
    let reset() =
        TypeRegistryModule.reset()
        PropertyMapper.clearCache()
        isInitialized <- false
        logDebug "Extension system reset"
    
    /// 시스템 상태 가져오기
    let getStatus() =
        {| 
            IsInitialized = isInitialized
            Registry = TypeRegistryModule.getStats()
            PropertyMapperCache = PropertyMapper.getCacheStats()
        |}