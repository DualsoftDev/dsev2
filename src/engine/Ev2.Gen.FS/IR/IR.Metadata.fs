namespace Ev2.Gen

open System

[<AutoOpen>]
module IRMetadata =

    /// Memory constraints
    type MemoryConstraints = {
        RamKB: int option
        NvKB: int option
    }

    /// Task watchdog
    type TaskWatchdog = {
        Task: string  // Reference to Task name
        TimeoutMs: int
    }

    /// System constraints
    type Constraints = {
        CycleBudgetMs: float option
        Memory: MemoryConstraints option
        Watchdogs: TaskWatchdog list
    }

    /// Localization strings
    type Localization = {
        Languages: string list  // e.g., ["ko-KR", "en-US"]
        Strings: Map<string, Map<string, string>>  // Language -> (Key -> Value)
    }

    /// Engineering units configuration
    type Units = {
        Default: string  // e.g., "SI", "Imperial"
        Overrides: Map<string, string>  // Variable -> Unit (e.g., "AxisX.Position" -> "mm")
    }

    /// Vendor-specific extensions (for lossless round-trip)
    type VendorExtensions = {
        TwinCAT3: Map<string, MetaValue> option
        CODESYS: Map<string, MetaValue> option
        Custom: Map<string, Map<string, MetaValue>> option  // Vendor name -> extensions
    }
