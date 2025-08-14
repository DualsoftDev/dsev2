namespace Ev2.Core.FS.Extension

open System

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