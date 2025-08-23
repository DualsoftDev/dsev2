namespace Ev2.Core.FS

open System
open System.Data
open Dual.Common.Base
open Dual.Common.Core.FS
open Newtonsoft.Json

/// Third Party 확장을 위한 C# 호환 타입 팩토리 인터페이스
type ITypeFactory =
    /// 전체 스키마를 받아서 수정된 스키마 반환 (추가 테이블, 인덱스 등)
    abstract ModifySchema : baseSchema:string * vendorName:string -> string
    /// DB 생성 후 추가 작업 수행 (초기 데이터 삽입, 추가 설정 등)
    abstract PostCreateDatabase : conn:IDbConnection * tr:IDbTransaction -> unit

    /// 지정된 런타임 타입의 인스턴스 생성
    abstract CreateRuntime : runtimeType:Type -> IRtUnique
    abstract CreateNj : njType:Type -> INjUnique

    /// 확장 속성 복사
    abstract CopyProperties: source:IUnique * target:IUnique -> unit
    abstract DeserializeJson: typeName:string * jsonString:string * settings:JsonSerializerSettings -> INjUnique

    /// Insert 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterInsert : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Update 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterUpdate : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Delete 완료 후 확장 처리 (런타임 타입 전달)
    abstract HandleAfterDelete : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// Select 완료 후 확장 복원 (런타임 타입 전달, 확장 타입 반환)
    abstract HandleAfterSelect : IRtUnique * IDbConnection * IDbTransaction -> unit
    /// 확장 속성의 diff 계산 (두 객체의 확장 속성 비교)
    abstract ComputeExtensionDiff : obj1:IRtUnique * obj2:IRtUnique -> seq<ICompareResult>

    abstract GetSemanticId : semanticKey:string -> string
    abstract WriteAasExtensionProperties : njObj:INjUnique -> seq<System.Text.Json.Nodes.JsonObject>
    abstract ReadAasExtensionProperties : njObj:INjUnique * smc: obj -> unit        // smc: SubmodelElementCollection type

[<AutoOpen>]
module TypeFactoryModule =
    /// Global factory instance (외부에서 구현체 주입) - C# 친화적으로 null 사용.  설정은 C# 에서 수행
    let mutable TypeFactory : ITypeFactory = getNull<ITypeFactory>()

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
    let createExtended<'T when 'T : (new : unit -> 'T) and 'T :> IUnique and 'T : not struct>() : 'T =
        getTypeFactory()
        |-> (fun factory ->
                let isRuntimeType = typeof<IRtUnique>.IsAssignableFrom(typeof<'T>)
                let isNjType = typeof<INjUnique>.IsAssignableFrom(typeof<'T>)
                let obj =
                    if isRuntimeType then
                        factory.CreateRuntime(typeof<'T>) :> IUnique
                    elif isNjType then
                        factory.CreateNj(typeof<'T>) :> IUnique
                    else
                        failwith "ERROR"
                obj :?> 'T)
        |?? (fun () -> new 'T())

