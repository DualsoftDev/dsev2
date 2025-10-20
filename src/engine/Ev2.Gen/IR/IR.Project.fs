namespace Ev2.Gen

open System

[<AutoOpen>]
module IRProject =

    /// Target platform profile
    type TargetProfile = {
        Vendor: string
        Ide: string
        Profile: string
    }

    /// Project metadata
    type Project = {
        Name: string
        Description: string option
        Version: string
        CreatedAt: DateTime
        ModifiedAt: DateTime
        TimeBase: string  // "ms", "us", etc.
        TargetProfiles: TargetProfile list
    }
