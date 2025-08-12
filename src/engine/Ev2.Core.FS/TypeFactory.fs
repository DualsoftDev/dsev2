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
    abstract CreateRuntime : runtimeType:Type -> IRtUnique
    /// 런타임 객체로부터 JSON 직렬화 객체 생성
    abstract CreateJson : runtimeType:Type * runtimeObj:IRtUnique -> INjUnique
    /// 지정된 런타임 타입에 해당하는 ORM 객체 생성
    abstract CreateOrm : runtimeType:Type -> IORMUnique
    /// 런타임 객체로부터 AAS JSON 직렬화 객체 생성 (AASX serialize용)
    abstract CreateNjFromRuntime : runtimeObj:IRtUnique -> INjUnique
    /// JSON 문자열로부터 적절한 확장 NjXXX 타입 생성
    abstract CreateNjFromJson : jsonString:string * baseType:Type -> INjUnique
    /// RuntimeType 문자열로 NjXXX 타입 찾기 (AASX 역직렬화용)
    abstract FindNjTypeByName : typeName:string -> Type
    /// SQL 스키마 확장 제공자 반환 (C# 친화적 - null 가능)
    abstract GetSchemaExtension : unit -> ISchemaExtension
    /// JSON 객체에서 런타임 객체로 확장 속성 복사
    abstract CopyExtensionProperties : njObj:INjUnique * rtObj:IRtUnique -> unit

/// Third Party 확장을 위한 Database CRUD 훅 인터페이스
type IExtensionDbHandler =
    /// Insert 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterInsert : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Update 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterUpdate : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Delete 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterDelete : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Select 완료 후 확장 복원 (런타임 타입 전달, 확장 타입 반환)
    abstract HandleAfterSelect : baseObj:IRtUnique * IDbConnection * IDbTransaction -> IRtUnique
    /// 확장 속성의 diff 계산 (두 객체의 확장 속성 비교)
    abstract ComputeExtensionDiff : obj1:IRtUnique * obj2:IRtUnique -> seq<obj>

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
    let createWithFallback<'T when 'T : not struct and 'T :> IRtUnique> (fallbackFactory: unit -> 'T) : 'T =
       getTypeFactory()
       >>= (fun factory ->
            factory.CreateRuntime(typeof<'T>)
            |> Option.ofObj
            |> Option.iter (fun _ -> ())  // 타입 검증만 수행, 오류 출력 제거
            |> fun _ -> factory.CreateRuntime(typeof<'T>) |> Option.ofObj)
       >>= tryCast<'T> // (fun obj -> obj :?> 'T)
       |? fallbackFactory()


    /// 새로운 제네릭 버전 - 매개변수 없는 직접 생성
    /// TypeRegistry 통합 버전
    let inline createExtended<'T when 'T : (new : unit -> 'T) and 'T :> IRtUnique and 'T : not struct>() : 'T =
        // 먼저 TypeFactory 확인
        if isItNull TypeFactory then
            // TypeFactory 없으면 Registry 확인
            match Extension.TypeRegistryModule.getRegistry().CreateInstance(typeof<'T>) with
            | Some obj -> obj :?> 'T
            | None -> new 'T()
        else
            let result = TypeFactory.CreateRuntime(typeof<'T>)
            if isItNull result then
                // Factory에서 못 찾으면 Registry 확인
                match Extension.TypeRegistryModule.getRegistry().CreateInstance(typeof<'T>) with
                | Some obj -> obj :?> 'T
                | None -> new 'T()
            else
                result :?> 'T
    
    /// 확장 시스템 초기화 (자동 스캔 포함)
    let initializeExtensionSystem() =
        Extension.ExtensionSystem.initialize()
    
    /// ITypeFactory 인터페이스에 자동 속성 복사 메서드 추가
    type ITypeFactory with
        /// 리플렉션 기반 자동 속성 복사
        member x.AutoCopyProperties(source: obj, target: obj) =
            Extension.PropertyMapper.copyExtensionProperties source target

