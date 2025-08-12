namespace Ev2.Core.FS.Extension

open System
open System.Reflection
open Dual.Common.Base

/// 타입 등록 정보
type TypeRegistrationInfo = {
    BaseType: Type
    ExtensionType: Type
    NjType: Type option
    OrmType: Type option
}

/// Assembly에서 확장 타입을 스캔하는 모듈
module TypeScanner =
    
    /// 네이밍 규칙으로 NJ 타입 찾기
    let private findNjTypeByConvention (extensionType: Type) =
        // 규칙 1: CustomProject -> CustomNjProject
        // 규칙 2: ExtProject -> ExtNjProject
        let njTypeName = 
            if extensionType.Name.StartsWith("Custom") then
                extensionType.Name.Replace("Custom", "CustomNj")
            elif extensionType.Name.StartsWith("Ext") then
                extensionType.Name.Replace("Ext", "ExtNj")
            else
                // 기본 규칙: TypeName -> NjTypeName
                "Nj" + extensionType.Name
        
        // 같은 namespace에서 찾기
        let njType = extensionType.Assembly.GetType(
            extensionType.Namespace + "." + njTypeName)
        
        // 못 찾으면 다른 namespace에서도 시도
        if isItNull njType then
            // .Extensions namespace에서 찾기
            let altNamespace = 
                if extensionType.Namespace.EndsWith(".Extensions") then
                    extensionType.Namespace
                else
                    extensionType.Namespace + ".Extensions"
            
            extensionType.Assembly.GetType(altNamespace + "." + njTypeName)
            |> Option.ofObj
        else
            Some njType
    
    /// 네이밍 규칙으로 ORM 타입 찾기
    let private findOrmTypeByConvention (extensionType: Type) =
        // 규칙 1: CustomProject -> CustomORMProject
        // 규칙 2: ExtProject -> ExtORMProject
        let ormTypeName = 
            if extensionType.Name.StartsWith("Custom") then
                extensionType.Name.Replace("Custom", "CustomORM")
            elif extensionType.Name.StartsWith("Ext") then
                extensionType.Name.Replace("Ext", "ExtORM")
            else
                // 기본 규칙: TypeName -> ORMTypeName
                "ORM" + extensionType.Name
        
        extensionType.Assembly.GetType(
            extensionType.Namespace + "." + ormTypeName)
        |> Option.ofObj
    
    /// 타입이 확장 가능한 기본 타입인지 확인
    let private isExtensibleBaseType (t: Type) =
        // IRtUnique를 구현하거나 알려진 기본 타입
        let knownBaseTypes = [|
            "Project"; "DsSystem"; "Flow"; "Work"; "Call";
            "ApiDef"; "ApiCall"; "DsButton"; "Lamp"; 
            "DsCondition"; "DsAction"; "ArrowBetweenWorks"; "ArrowBetweenCalls"
        |]
        
        knownBaseTypes |> Array.exists (fun name -> t.Name = name)
    
    /// 타입의 기본 타입 찾기
    let private findBaseType (extensionType: Type) =
        // ExtensionTypeAttribute에서 BaseType 확인
        let attr = extensionType.GetCustomAttribute<ExtensionTypeAttribute>()
        if isItNotNull attr then
            Some attr.BaseType
        else
            // 부모 클래스 체인을 따라가며 기본 타입 찾기
            let rec findBase (t: Type) =
                if isItNull t || t = typeof<obj> then None
                elif isExtensibleBaseType t then Some t
                else findBase t.BaseType
            
            findBase extensionType.BaseType
    
    /// Assembly에서 확장 타입 스캔
    let scanAssembly (assembly: Assembly) =
        try
            assembly.GetTypes()
            |> Array.filter (fun t -> 
                // 추상 클래스나 인터페이스 제외
                not t.IsAbstract && 
                not t.IsInterface &&
                // ExtensionTypeAttribute가 있거나 네이밍 규칙 따르는 타입
                (t.GetCustomAttribute<ExtensionTypeAttribute>() |> isItNull |> not ||
                 t.Name.StartsWith("Custom") ||
                 t.Name.StartsWith("Ext")))
            |> Array.choose (fun t ->
                let attr = t.GetCustomAttribute<ExtensionTypeAttribute>()
                
                // BaseType 결정
                let baseType =
                    if isItNotNull attr && isItNotNull attr.BaseType then
                        Some attr.BaseType
                    else
                        findBaseType t
                
                match baseType with
                | None -> 
                    // 기본 타입을 찾을 수 없으면 스킵
                    None
                | Some bt ->
                    // NJ 타입 결정
                    let njType =
                        if isItNotNull attr && isItNotNull attr.NjType then
                            Some attr.NjType
                        else
                            findNjTypeByConvention t
                    
                    // ORM 타입 결정
                    let ormType =
                        if isItNotNull attr && isItNotNull attr.OrmType then
                            Some attr.OrmType
                        else
                            findOrmTypeByConvention t
                    
                    // AutoRegister 확인 (기본값 true)
                    let shouldRegister =
                        if isItNull attr then true
                        else attr.AutoRegister
                    
                    if shouldRegister then
                        Some {
                            BaseType = bt
                            ExtensionType = t
                            NjType = njType
                            OrmType = ormType
                        }
                    else
                        None)
            |> Array.toSeq
        with ex ->
            logWarn $"Failed to scan assembly {assembly.GetName().Name}: {ex.Message}"
            Seq.empty
    
    /// 현재 로드된 모든 어셈블리에서 확장 타입 스캔
    let scanAllAssemblies() =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.filter (fun a -> 
            // 시스템 어셈블리 제외
            not (a.FullName.StartsWith("System")) &&
            not (a.FullName.StartsWith("Microsoft")) &&
            not (a.FullName.StartsWith("FSharp")) &&
            not (a.FullName.StartsWith("mscorlib")) &&
            not (a.FullName.StartsWith("netstandard")) &&
            not (a.IsDynamic))
        |> Array.collect (fun a -> 
            try
                scanAssembly a |> Seq.toArray
            with ex ->
                logWarn $"Failed to scan assembly {a.GetName().Name}: {ex.Message}"
                [||])
        |> Array.toSeq
    
    /// 특정 namespace의 어셈블리만 스캔
    let scanAssembliesInNamespace (namespacePrefix: string) =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Array.filter (fun a -> 
            a.FullName.StartsWith(namespacePrefix) && not a.IsDynamic)
        |> Array.collect (fun a -> scanAssembly a |> Seq.toArray)
        |> Array.toSeq
    
    /// 통계 정보 수집
    let getStatistics (assemblies: Assembly[]) =
        let results = 
            assemblies
            |> Array.collect (fun a -> scanAssembly a |> Seq.toArray)
        
        {| 
            TotalAssemblies = assemblies.Length
            TotalTypes = results.Length
            TypesWithNj = results |> Array.filter (fun r -> r.NjType.IsSome) |> Array.length
            TypesWithOrm = results |> Array.filter (fun r -> r.OrmType.IsSome) |> Array.length
            BaseTypes = results |> Array.map (fun r -> r.BaseType.Name) |> Array.distinct
            ExtensionTypes = results |> Array.map (fun r -> r.ExtensionType.Name)
        |}