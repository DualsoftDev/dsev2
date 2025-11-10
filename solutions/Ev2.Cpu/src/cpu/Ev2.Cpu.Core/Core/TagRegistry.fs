namespace Ev2.Cpu.Core

open System
open System.Collections.Concurrent

module DsTagRegistry =
    /// Remove every tag from the registry.
    let clear () = TagRegistryHelpers.clear ()

    /// Register a descriptor (creates or validates existing entry).
    let registerDescriptor descriptor = TagRegistryHelpers.registerDescriptor descriptor

    /// Ensure the provided tag is present.
    let register tag = TagRegistryHelpers.register tag

    /// Try find a tag by name.
    let tryFind name = TagRegistryHelpers.tryFind name

    /// Get all registered tags.
    let all () = TagRegistryHelpers.getAll ()

    /// Ensure many descriptors are registered.
    let registerDescriptors descriptors =
        descriptors |> Seq.iter (registerDescriptor >> ignore)

