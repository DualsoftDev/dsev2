namespace Ev2.Cpu.Core

open System
open System.Collections.Concurrent

/// Descriptor describing a logical tag in the runtime.
[<StructuralEquality; NoComparison>]
type TagDescriptor =
    { Name: string
      DsDataType: DsDataType
      Description: string option
      Category: string option }
    static member Create(name, dsDataType, ?description, ?category) =
        { Name = name
          DsDataType = dsDataType
          Description = description
          Category = category }

/// Tag representation shared between AST/Struct layers.
[<StructuralEquality; NoComparison>]
type DsTag =
    { Name: string
      StructType: DsDataType }
    member this.DsDataType = this.StructType
    override this.ToString() = sprintf "%s:%s" this.Name (this.StructType.ToString())

module internal TagRegistryStore =
    let registry =
        ConcurrentDictionary<string, DsTag>(StringComparer.Ordinal)

module internal TagRegistryHelpers =
    let normalizeName (name: string) =
        if String.IsNullOrWhiteSpace name then
            invalidArg "name" "Tag name cannot be null or whitespace"
        name.Trim()

    let ensureTypeConsistency name dtype (existing: DsTag) =
        if existing.StructType <> dtype then
            raise (InvalidOperationException($"Tag '{name}' already registered as {existing.StructType} but requested {dtype}"))
        existing
    let getOrAdd (name: string) (dtype: DsDataType) =
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
        getOrAdd descriptor.Name descriptor.DsDataType

    let tryFind name =
        let key = normalizeName name
        match TagRegistryStore.registry.TryGetValue(key) with
        | true, tag -> Some tag
        | _ -> None

    let getAll () =
        TagRegistryStore.registry.Values |> Seq.toList

    let clear () = TagRegistryStore.registry.Clear()


module TagBuilders =
    let create name dtype = TagRegistryHelpers.getOrAdd name dtype
    let bool name = create name TBool
    let int name = create name TInt
    let double name = create name TDouble
    let string name = create name TString

type DsTag with
    static member Create(name, dtype: DsDataType) = TagBuilders.create name dtype
    static member Bool(name) = TagBuilders.bool name
    static member Int(name) = TagBuilders.int name
    static member Double(name) = TagBuilders.double name
    static member String(name) = TagBuilders.string name
    
