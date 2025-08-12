namespace Ev2.Core.FS.Extension

open System

/// 확장 속성을 표시하는 Attribute
/// Third Party에서 확장 타입의 속성을 표시할 때 사용
[<AttributeUsage(AttributeTargets.Property)>]
type ExtensionPropertyAttribute() =
    inherit Attribute()
    
    /// 소스 객체의 속성 이름 (다른 이름으로 매핑할 경우)
    member val SourceName : string = null with get, set
    
    /// 필수 속성 여부
    member val IsRequired : bool = false with get, set
    
    /// 기본값
    member val DefaultValue : obj = null with get, set
    
    /// JSON 직렬화에 포함할지 여부
    member val IncludeInJson : bool = true with get, set
    
    /// 데이터베이스 컬럼 이름 (null이면 속성 이름 사용)
    member val ColumnName : string = null with get, set
    
    /// 데이터베이스 컬럼이 Nullable인지 여부
    member val IsNullable : bool = true with get, set

/// 확장 타입을 표시하는 Attribute
/// Third Party에서 확장 타입을 정의할 때 사용
[<AttributeUsage(AttributeTargets.Class, Inherited = false)>]
type ExtensionTypeAttribute(baseType: Type) =
    inherit Attribute()
    
    /// 확장하는 기본 타입
    member val BaseType = baseType
    
    /// 대응하는 NJ (JSON) 타입
    member val NjType : Type = null with get, set
    
    /// 대응하는 ORM 타입
    member val OrmType : Type = null with get, set
    
    /// 자동 등록 여부
    member val AutoRegister = true with get, set
    
    /// 타입 이름 (디버깅용)
    member val TypeName : string = null with get, set

/// 데이터베이스 인덱스가 필요한 속성을 표시하는 Attribute
[<AttributeUsage(AttributeTargets.Property)>]
type IndexedAttribute() =
    inherit Attribute()
    
    /// 인덱스 이름 (null이면 자동 생성)
    member val IndexName : string = null with get, set
    
    /// Unique 인덱스 여부
    member val IsUnique : bool = false with get, set

/// JSON 직렬화에서 제외할 속성을 표시하는 Attribute
[<AttributeUsage(AttributeTargets.Property)>]
type JsonIgnoreExtensionAttribute() =
    inherit Attribute()

/// 타입 매핑 정보를 표시하는 Attribute
/// Runtime, NJ, ORM 타입 간의 매핑 관계를 명시
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = true)>]
type TypeMappingAttribute(sourceType: Type, targetType: Type) =
    inherit Attribute()
    
    /// 소스 타입
    member val SourceType = sourceType
    
    /// 타겟 타입
    member val TargetType = targetType
    
    /// 매핑 방향 (Bidirectional, SourceToTarget, TargetToSource)
    member val Direction : string = "Bidirectional" with get, set