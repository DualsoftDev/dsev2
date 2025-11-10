namespace Ev2.Cpu.Core

open System
open System.Collections.Concurrent

/// Descriptor describing a logical tag in the runtime.
[<StructuralEquality; NoComparison>]
type TagDescriptor =
    { Name: string
      DataType: Type
      Description: string option
      Category: string option }
    static member Create(name, dataType: Type, ?description, ?category) =
        { Name = name
          DataType = dataType
          Description = description
          Category = category }

/// Tag representation shared between AST/Struct layers.
[<StructuralEquality; NoComparison>]
type DsTag =
    { Name: string
      StructType: Type }
    member this.DataType = this.StructType
    override this.ToString() = sprintf "%s:%s" this.Name (TypeHelpers.getTypeName this.StructType)

module internal TagRegistryStore =
    let registry =
        ConcurrentDictionary<string, DsTag>(StringComparer.Ordinal)

module internal TagRegistryHelpers =
    let normalizeName (name: string) =
        if String.IsNullOrWhiteSpace name then
            invalidArg "name" "Tag name cannot be null or whitespace"
        name.Trim()

    let ensureTypeConsistency name (dtype: Type) (existing: DsTag) =
        if existing.StructType <> dtype then
            raise (InvalidOperationException($"Tag '{name}' already registered as {existing.StructType} but requested {dtype}"))
        existing
    let getOrAdd (name: string) (dtype: Type) =
        let key = normalizeName name
        TagRegistryStore.registry.AddOrUpdate(
            key,
            (fun _ -> { Name = key; StructType = dtype }),
            (fun _ existing -> ensureTypeConsistency key dtype existing))

    let register (tag: DsTag) =
        if isNull (box tag) then invalidArg "tag" "Tag cannot be null"
        getOrAdd tag.Name tag.StructType |> ignore
        tag

    let registerDescriptor (descriptor: TagDescriptor) =
        if isNull (box descriptor) then invalidArg "descriptor" "Descriptor cannot be null"
        getOrAdd descriptor.Name descriptor.DataType

    let tryFind name =
        let key = normalizeName name
        match TagRegistryStore.registry.TryGetValue(key) with
        | true, tag -> Some tag
        | _ -> None

    let getAll () =
        TagRegistryStore.registry.Values |> Seq.toList

    let clear () = TagRegistryStore.registry.Clear()


module TagBuilders =
    let create name (dtype: Type) = TagRegistryHelpers.getOrAdd name dtype
    let bool name = create name typeof<bool>
    let int name = create name typeof<int>
    let double name = create name typeof<double>
    let string name = create name typeof<string>

type DsTag with
    static member Create(name, dtype: Type) = TagBuilders.create name dtype
    static member Bool(name) = TagBuilders.bool name
    static member Int(name) = TagBuilders.int name
    static member Double(name) = TagBuilders.double name
    static member String(name) = TagBuilders.string name
    
