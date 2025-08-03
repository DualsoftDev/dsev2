namespace Ev2.Core.FS

open System

/// Third Party 확장을 위한 C# 호환 타입 팩토리 인터페이스
type ITypeFactory =
    /// 지정된 런타임 타입의 인스턴스 생성
    abstract CreateRuntime : runtimeType:Type -> obj
    /// 런타임 객체로부터 JSON 직렬화 객체 생성
    abstract CreateJson : runtimeType:Type * runtimeObj:obj -> obj
    /// 지정된 런타임 타입에 해당하는 ORM 객체 생성
    abstract CreateOrm : runtimeType:Type -> obj
    /// 런타임 타입에 매핑되는 JSON 타입 해결
    abstract GetJsonType : runtimeType:Type -> Type
    /// 런타임 타입에 매핑되는 ORM 타입 해결
    abstract GetOrmType : runtimeType:Type -> Type

[<AutoOpen>]
module TypeFactoryModule =
    /// Global factory instance (외부에서 구현체 주입)
    let mutable TypeFactory : ITypeFactory option = None

/// Third Party 확장 지원을 위한 Generic Helper 함수들
[<AutoOpen>]
module TypeFactoryHelper =

    /// 확장 타입 생성을 지원하는 helper 함수 (fallback 포함)
    let createWithFallback<'T> (fallbackFactory: unit -> 'T) : 'T =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateRuntime(typeof<'T>)
            if obj <> null then obj :?> 'T
            else fallbackFactory()
        | None -> fallbackFactory()

    /// inline 최적화 버전 - 성능 최적화
    let inline createExtensible<'T> (defaultFactory: unit -> 'T) =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateRuntime(typeof<'T>)
            if obj <> null then obj :?> 'T else defaultFactory()
        | None -> defaultFactory()

    /// JSON 객체 생성을 위한 helper 함수
    let createJsonFromRuntime<'TRuntime, 'TJson when 'TRuntime :> Unique> (runtime: 'TRuntime) (defaultFactory: 'TRuntime -> 'TJson) : 'TJson =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateJson(typeof<'TRuntime>, runtime)
            if obj <> null then obj :?> 'TJson
            else defaultFactory runtime
        | None -> defaultFactory runtime

    /// ORM 객체 생성을 위한 helper 함수
    let createOrmFromRuntime<'TRuntime, 'TOrm when 'TRuntime :> Unique> (runtime: 'TRuntime) (defaultFactory: 'TRuntime -> 'TOrm) : 'TOrm =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateOrm(typeof<'TRuntime>)
            if obj <> null then obj :?> 'TOrm
            else defaultFactory runtime
        | None -> defaultFactory runtime