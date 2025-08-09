namespace Ev2.Core.FS

open System
open System.Data
open Dual.Common.Base
open Dual.Common.Core.FS

/// Third Party 확장을 위한 SQL 스키마 확장 인터페이스 (C# 친화적)
[<AllowNullLiteral>]
type ISchemaExtension =
    /// 전체 스키마를 받아서 수정된 스키마 반환 (추가 테이블, 인덱스 등)
    abstract ModifySchema : baseSchema:string -> string
    /// DB 생성 후 추가 작업 수행 (초기 데이터 삽입, 추가 설정 등)
    abstract PostCreateDatabase : conn:IDbConnection * tr:IDbTransaction -> unit

/// Third Party 확장을 위한 C# 호환 타입 팩토리 인터페이스
type ITypeFactory =
    /// 지정된 런타임 타입의 인스턴스 생성
    abstract CreateRuntime : runtimeType:Type -> obj
    /// 런타임 객체로부터 JSON 직렬화 객체 생성
    abstract CreateJson : runtimeType:Type * runtimeObj:obj -> obj
    /// 지정된 런타임 타입에 해당하는 ORM 객체 생성
    abstract CreateOrm : runtimeType:Type -> obj
    /// SQL 스키마 확장 제공자 반환 (C# 친화적 - null 가능)
    abstract GetSchemaExtension : unit -> ISchemaExtension
    /// RuntimeType 문자열로 JSON 타입 찾기 (역직렬화용)
    abstract FindJsonTypeByName : typeName:string -> Type
    /// JSON 객체에서 런타임 객체로 확장 속성 복사
    abstract CopyExtensionProperties : njObj:obj * rtObj:obj -> unit

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
    /// Global factory instance (외부에서 구현체 주입) - C# 친화적으로 null 사용.  설정은 C# 에서 수행
    let mutable TypeFactory : ITypeFactory = getNull<ITypeFactory>()
    /// Global extension db handler instance (외부에서 구현체 주입) - C# 친화적으로 null 사용
    let mutable ExtensionDbHandler : IExtensionDbHandler = getNull<IExtensionDbHandler>()

    let getTypeFactory() : ITypeFactory option = TypeFactory |> Option.ofObj


/// Third Party 확장 지원을 위한 Generic Helper 함수들
[<AutoOpen>]
module TypeFactoryHelper =

    /// 확장 타입 생성을 지원하는 helper 함수 (fallback 포함)
    let createWithFallback<'T when 'T : not struct> (fallbackFactory: unit -> 'T) : 'T =
       getTypeFactory()
       >>= (fun factory ->
            factory.CreateRuntime(typeof<'T>)
            |> tee(fun z ->
                if isNull z then
                    failwith $"[TypeFactory] No extension type registered for {typeof<'T>.Name}. Using default." )
            |> Option.ofObj)
       >>= tryCast<'T> // (fun obj -> obj :?> 'T)
       |? fallbackFactory()


    /// 새로운 제네릭 버전 - 매개변수 없는 직접 생성
    let inline createExtended<'T when 'T : (new : unit -> 'T) and 'T :> Unique and 'T : not struct>() : 'T =
        createWithFallback<'T>(fun () -> new 'T())

