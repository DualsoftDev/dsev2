namespace Ev2.Core.FS

open System
open System.Data

/// Third Party 확장을 위한 SQL 스키마 확장 인터페이스 (C# 친화적)
[<AllowNullLiteral>]
type ISchemaExtension =
    /// 테이블별 추가 컬럼 정의 (테이블명을 받아서 추가 컬럼 SQL 반환, null 가능)
    abstract GetAdditionalColumns : tableName:string -> string
    /// 테이블 생성 SQL 확장 (기본 테이블 SQL을 받아서 확장된 SQL 반환)
    abstract ExtendTableSchema : tableName:string * baseSchema:string -> string
    /// 확장된 테이블 목록 반환 (C# 친화적 - IEnumerable)
    abstract GetExtendedTables : unit -> System.Collections.Generic.IEnumerable<string>
    /// 전체 스키마를 받아서 수정된 스키마 반환 (추가 테이블, 인덱스 등)
    abstract ModifySchema : baseSchema:string -> string
    /// DB 생성 후 추가 작업 수행 (초기 데이터 삽입, 추가 설정 등)
    abstract PostCreateDatabase : conn:IDbConnection * tr:IDbTransaction -> unit

/// Third Party 확장을 위한 C# 호환 타입 팩토리 인터페이스
type ITypeFactory =
    /// 지정된 런타임 타입의 인스턴스 생성
    abstract CreateRuntime : runtimeType:Type -> obj
    /// 복제 전용 런타임 인스턴스 생성 (확장 속성 초기화 건너뜀)
    abstract CreateRuntimeForReplication : runtimeType:Type -> obj
    /// 런타임 객체로부터 JSON 직렬화 객체 생성
    abstract CreateJson : runtimeType:Type * runtimeObj:obj -> obj
    /// 지정된 런타임 타입에 해당하는 ORM 객체 생성
    abstract CreateOrm : runtimeType:Type -> obj
    /// 런타임 타입에 매핑되는 JSON 타입 해결
    abstract GetJsonType : runtimeType:Type -> Type
    /// 런타임 타입에 매핑되는 ORM 타입 해결
    abstract GetOrmType : runtimeType:Type -> Type
    /// SQL 스키마 확장 제공자 반환 (C# 친화적 - null 가능)
    abstract GetSchemaExtension : unit -> ISchemaExtension

/// Third Party 확장을 위한 Database CRUD 훅 인터페이스
type IExtensionDbHandler =
    /// Insert 완료 후 확장 처리
    abstract HandleAfterInsert : obj * IDbConnection * IDbTransaction -> unit
    /// Update 완료 후 확장 처리
    abstract HandleAfterUpdate : obj * IDbConnection * IDbTransaction -> unit
    /// Delete 완료 후 확장 처리
    abstract HandleAfterDelete : obj * IDbConnection * IDbTransaction -> unit
    /// Select 완료 후 확장 복원
    abstract HandleAfterSelect : baseObj:obj * IDbConnection * IDbTransaction -> obj

[<AutoOpen>]
module TypeFactoryModule =
    /// Global factory instance (외부에서 구현체 주입)
    let mutable TypeFactory : ITypeFactory option = None
    /// Global extension db handler instance (외부에서 구현체 주입)
    let mutable ExtensionDbHandler : IExtensionDbHandler option = None

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


    /// 새로운 제네릭 버전 - 매개변수 없는 직접 생성
    let inline createExtended<'T when 'T : (new : unit -> 'T) and 'T :> Unique>() : 'T =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateRuntime(typeof<'T>)
            if obj <> null then obj :?> 'T else new 'T()
        | None -> new 'T()

    /// 복제 전용 생성 - 확장 속성 초기화 건너뜀
    let inline createExtendedForReplication<'T when 'T : (new : unit -> 'T) and 'T :> Unique>() : 'T =
        match TypeFactory with
        | Some factory ->
            let obj = factory.CreateRuntimeForReplication(typeof<'T>)
            if obj <> null then
                obj :?> 'T
            else
                // 확장 타입이 등록되지 않았으므로 기본 타입 사용
                new 'T()
        | None ->
            // 팩토리가 없으므로 기본 타입 사용
            new 'T()

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