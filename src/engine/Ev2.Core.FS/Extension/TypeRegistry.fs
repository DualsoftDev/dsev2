namespace Ev2.Core.FS.Extension

open System
open System.Collections.Concurrent
open System.Reflection
open Dual.Common.Base

/// 타입 등록 정보
type TypeRegistration = {
    BaseType: Type
    ExtensionType: Type
    NjType: Type option
    OrmType: Type option
    RuntimeFactory: unit -> obj
    NjFactory: obj -> obj
    OrmFactory: unit -> obj
}

/// 타입 레지스트리 인터페이스
type ITypeRegistry =
    /// 타입 등록
    abstract RegisterType: baseType:Type * extensionType:Type * njType:Type option * ormType:Type option -> unit
    /// Assembly 스캔 및 등록
    abstract RegisterAssembly: assembly:Assembly -> unit
    /// 기본 타입으로 등록 정보 조회
    abstract GetRegistration: baseType:Type -> TypeRegistration option
    /// 모든 등록 정보 조회
    abstract GetAllRegistrations: unit -> TypeRegistration seq
    /// 인스턴스 생성
    abstract CreateInstance: baseType:Type -> obj option
    /// NJ 타입 해결
    abstract ResolveNjType: runtimeType:Type -> Type option
    /// ORM 타입 해결
    abstract ResolveOrmType: runtimeType:Type -> Type option
    /// 타입 이름으로 찾기
    abstract FindTypeByName: typeName:string -> Type option
    /// 등록 정보 삭제
    abstract UnregisterType: baseType:Type -> bool
    /// 캐시 초기화
    abstract Clear: unit -> unit

/// 타입 레지스트리 구현
type TypeRegistry() =
    let registrations = ConcurrentDictionary<Type, TypeRegistration>()
    let typeNameMap = ConcurrentDictionary<string, Type>()

    /// 안전한 인스턴스 생성
    let createSafeInstance (targetType: Type) (args: obj[]) =
        if Array.isEmpty args then
            Activator.CreateInstance(targetType)
        else
            Activator.CreateInstance(targetType, args)

    interface ITypeRegistry with
        member _.RegisterType(baseType, extensionType, njType, ormType) =
            let registration = {
                BaseType = baseType
                ExtensionType = extensionType
                NjType = njType
                OrmType = ormType
                RuntimeFactory = fun () -> createSafeInstance extensionType [||]
                NjFactory = fun runtime ->
                    match njType with
                    | Some t ->
                        // 먼저 runtime 매개변수를 받는 생성자 시도
                        let result = createSafeInstance t [| runtime |]
                        if isItNull result then
                            // 실패하면 기본 생성자 시도
                            createSafeInstance t [||]
                        else result
                    | None -> null
                OrmFactory = fun () ->
                    match ormType with
                    | Some t -> createSafeInstance t [||]
                    | None -> null
            }

            // 기본 타입으로 등록
            registrations.[baseType] <- registration
            // 확장 타입으로도 등록 (조회 편의성)
            registrations.[extensionType] <- registration

            // 타입 이름으로도 등록
            typeNameMap.[baseType.FullName] <- baseType
            typeNameMap.[baseType.Name] <- baseType
            typeNameMap.[extensionType.FullName] <- extensionType
            typeNameMap.[extensionType.Name] <- extensionType

            // NJ 타입 이름 등록
            njType |> Option.iter (fun t ->
                typeNameMap.[t.FullName] <- t
                typeNameMap.[t.Name] <- t)

            // ORM 타입 이름 등록
            ormType |> Option.iter (fun t ->
                typeNameMap.[t.FullName] <- t
                typeNameMap.[t.Name] <- t)

            logDebug $"Registered extension: {baseType.Name} -> {extensionType.Name}"

        member this.RegisterAssembly(assembly) =
            // TypeScanner를 사용하여 assembly 스캔
            let registrations = TypeScanner.scanAssembly(assembly)

            registrations
            |> Seq.iter (fun info ->
                (this :> ITypeRegistry).RegisterType(
                    info.BaseType,
                    info.ExtensionType,
                    info.NjType,
                    info.OrmType))

            logDebug $"Scanned assembly {assembly.GetName().Name}: {registrations |> Seq.length} types registered"

        member _.GetRegistration(baseType) =
            match registrations.TryGetValue(baseType) with
            | true, reg -> Some reg
            | _ -> None

        member _.GetAllRegistrations() =
            registrations.Values
            |> Seq.distinctBy (fun r -> r.ExtensionType)

        member this.CreateInstance(baseType) =
            (this :> ITypeRegistry).GetRegistration(baseType)
            |> Option.map (fun reg -> reg.RuntimeFactory())

        member this.ResolveNjType(runtimeType) =
            (this :> ITypeRegistry).GetRegistration(runtimeType)
            |> Option.bind (fun reg -> reg.NjType)

        member this.ResolveOrmType(runtimeType) =
            (this :> ITypeRegistry).GetRegistration(runtimeType)
            |> Option.bind (fun reg -> reg.OrmType)

        member _.FindTypeByName(typeName) =
            match typeNameMap.TryGetValue(typeName) with
            | true, t -> Some t
            | _ -> None

        member _.UnregisterType(baseType) =
            match registrations.TryRemove(baseType) with
            | true, reg ->
                // 관련 타입들도 제거
                registrations.TryRemove(reg.ExtensionType) |> ignore
                typeNameMap.TryRemove(baseType.FullName) |> ignore
                typeNameMap.TryRemove(baseType.Name) |> ignore
                typeNameMap.TryRemove(reg.ExtensionType.FullName) |> ignore
                typeNameMap.TryRemove(reg.ExtensionType.Name) |> ignore

                reg.NjType |> Option.iter (fun t ->
                    typeNameMap.TryRemove(t.FullName) |> ignore
                    typeNameMap.TryRemove(t.Name) |> ignore)

                reg.OrmType |> Option.iter (fun t ->
                    typeNameMap.TryRemove(t.FullName) |> ignore
                    typeNameMap.TryRemove(t.Name) |> ignore)

                true
            | _ -> false

        member _.Clear() =
            registrations.Clear()
            typeNameMap.Clear()
            logDebug "TypeRegistry cleared"

// TypeScanner와 TypeRegistrationInfo는 TypeScanner.fs에 정의됨

/// Global registry 모듈
module TypeRegistryModule =
    let mutable private globalRegistry : ITypeRegistry option = None

    /// Global registry 가져오기 (없으면 생성)
    let getRegistry() =
        match globalRegistry with
        | Some r -> r
        | None ->
            let r = TypeRegistry() :> ITypeRegistry
            globalRegistry <- Some r
            logDebug "TypeRegistry initialized"
            r

    /// Global registry 설정
    let setRegistry (registry: ITypeRegistry) =
        globalRegistry <- Some registry
        logDebug "TypeRegistry replaced"

    /// Registry 초기화
    let reset() =
        match globalRegistry with
        | Some r -> r.Clear()
        | None -> ()
        globalRegistry <- None
        logDebug "TypeRegistry reset"

    /// 통계 정보
    let getStats() =
        match globalRegistry with
        | Some r ->
            let registrations = r.GetAllRegistrations() |> Seq.toList
            {|
                RegisteredTypes = registrations.Length
                BaseTypes = registrations |> List.map (fun r -> r.BaseType.Name) |> List.distinct
                ExtensionTypes = registrations |> List.map (fun r -> r.ExtensionType.Name)
            |}
        | None ->
            {| RegisteredTypes = 0; BaseTypes = []; ExtensionTypes = [] |}