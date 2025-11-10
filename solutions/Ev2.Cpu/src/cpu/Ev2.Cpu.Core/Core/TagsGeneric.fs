namespace Ev2.Cpu.Core

open System
open System.Collections.Concurrent

// ─────────────────────────────────────────────────────────────────────
// 제네릭 Tag 시스템 (Generic Tag System)
// ─────────────────────────────────────────────────────────────────────
// 기존 DsTag 시스템과 함께 사용 가능한 제네릭 Tag
// F# .NET 네이티브 타입을 사용한 타입 안전 태그 관리
// ─────────────────────────────────────────────────────────────────────

/// <summary>제네릭 Tag (타입 안전 버전)</summary>
/// <remarks>
/// DsTag와 달리 컴파일 타임에 타입이 결정됨
/// 예: Tag&lt;bool&gt;("Enable"), Tag&lt;int&gt;("Counter")
/// </remarks>
[<StructuralEquality; NoComparison>]
type Tag<'T> =
    { Name: string
      Description: string option
      Category: string option }

    member this.DataType = typeof<'T>

    override this.ToString() =
        sprintf "%s:%s" this.Name (typeof<'T>.Name)

    static member Create(name, ?description, ?category) =
        { Name = name
          Description = description
          Category = category }

/// 제네릭 Tag 레지스트리 (Thread-safe)
module internal GenericTagRegistryStore =
    let registry = ConcurrentDictionary<string, obj>(StringComparer.Ordinal)

/// 제네릭 Tag 레지스트리 헬퍼
module internal GenericTagRegistryHelpers =
    let normalizeName (name: string) =
        if String.IsNullOrWhiteSpace name then
            invalidArg "name" "Tag name cannot be null or whitespace"
        name.Trim()

    let ensureTypeConsistency<'T> name (existing: obj) =
        match existing with
        | :? Tag<'T> as tag -> tag
        | _ ->
            let existingType = existing.GetType().GetGenericArguments().[0]
            raise (InvalidOperationException(
                sprintf "Tag '%s' already registered as %s but requested as %s"
                    name existingType.Name typeof<'T>.Name))

    let getOrAdd<'T> (name: string) (description: string option) (category: string option) : Tag<'T> =
        let key = normalizeName name
        let tag = Tag<'T>.Create(key, ?description=description, ?category=category)

        GenericTagRegistryStore.registry.AddOrUpdate(
            key,
            (fun _ -> box tag),
            (fun _ existing -> box (ensureTypeConsistency<'T> key existing))
        ) :?> Tag<'T>

    let register<'T> (tag: Tag<'T>) =
        getOrAdd<'T> tag.Name tag.Description tag.Category

    let tryFind<'T> name : Tag<'T> option =
        let key = normalizeName name
        match GenericTagRegistryStore.registry.TryGetValue(key) with
        | true, tag ->
            match tag with
            | :? Tag<'T> as t -> Some t
            | _ -> None
        | _ -> None

    let getAll() =
        GenericTagRegistryStore.registry.Values |> Seq.toList

    let clear() =
        GenericTagRegistryStore.registry.Clear()

/// Generic Tag 빌더 헬퍼
module GenericTagBuilders =
    /// 제네릭 Tag 생성
    let create<'T> name =
        GenericTagRegistryHelpers.getOrAdd<'T> name None None

    /// bool Tag 생성
    let bool name = create<bool> name

    /// int Tag 생성
    let int name = create<int> name

    /// double Tag 생성
    let double name = create<double> name

    /// string Tag 생성
    let string name = create<string> name

    /// 설명과 함께 Tag 생성
    let createWith<'T> name description category =
        GenericTagRegistryHelpers.getOrAdd<'T> name (Some description) (Some category)

/// Tag Extension Methods
type Tag<'T> with
    /// bool Tag 생성
    static member Bool(name) = GenericTagBuilders.bool name

    /// int Tag 생성
    static member Int(name) = GenericTagBuilders.int name

    /// double Tag 생성
    static member Double(name) = GenericTagBuilders.double name

    /// string Tag 생성
    static member String(name) = GenericTagBuilders.string name

    /// 설명과 함께 생성
    static member CreateWith(name, description, category) =
        GenericTagBuilders.createWith<'T> name description category

/// DsTag와 Tag<'T> 간 변환
module TagConversion =
    /// DsTag를 Tag<'T>로 변환 시도
    let tryFromDsTag<'T> (dsTag: DsTag) : Tag<'T> option =
        let expectedType = typeof<'T>
        let actualType = dsTag.DataType

        if expectedType = actualType then
            Some (Tag<'T>.Create(dsTag.Name))
        else
            None

    /// Tag<'T>를 DsTag로 변환
    let toDsTag<'T> (tag: Tag<'T>) : DsTag =
        DsTag.Create(tag.Name, typeof<'T>)
