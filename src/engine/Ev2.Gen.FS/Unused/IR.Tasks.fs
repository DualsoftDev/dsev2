namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRTasks =

    /// Program call in a task
    type ProgramCall = {
        PouRef: string  // Reference to POU name
        Order: int
    }

    /// Task definition
    type Task = {
        Name: string
        Interval: int  // milliseconds
        Priority: int
        ProgramCalls: ProgramCall list
    }

    /// Watchdog configuration
    type Watchdog = {
        Enabled: bool
        TimeoutMs: int
    }

    /// Resource (IEC 61131-3 resource)
    type Resource = {
        Name: string
        Tasks: string list  // References to Task names
        Watchdog: Watchdog option
    }
