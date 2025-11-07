namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRMotion =

    /// Homing mode
    type HomingMode =
        | LimitSwitch
        | Marker
        | AbsOffset

    /// Homing configuration
    type HomingConfig = {
        Mode: HomingMode
        Offset: float
    }

    /// Axis limits
    type AxisLimits = {
        PosMin: float option
        PosMax: float option
        VelMax: float option
    }

    /// Motion axis
    type MotionAxis = {
        Name: string
        DeviceRef: string option  // Reference to Device or Slot
        Limits: AxisLimits option
        Homing: HomingConfig option
    }

    /// Motion axis group (coordinated motion)
    type MotionGroup = {
        Name: string
        Axes: string list  // References to MotionAxis names
    }

    /// FB target mapping for a specific profile
    type FbTarget = {
        Profile: string  // e.g., "TwinCAT3", "CODESYS_Default"
        Fb: string  // Target FB name
    }

    /// Done edge behavior
    type EdgeBehavior =
        | Rising
        | Falling
        | Level

    /// Busy behavior
    type BusyBehavior =
        | Level
        | Pulse

    /// FB semantics
    type FbSemantics = {
        DoneEdge: EdgeBehavior option
        BusyBehavior: BusyBehavior option
    }

    /// FB parameter mapping (standard -> target)
    type FbMapping = {
        Standard: string  // Standard FB name (e.g., "MC_MoveAbsolute")
        Targets: FbTarget list
        ParameterMap: Map<string, string>  // key에 '?'가 붙으면 optional parameter
        Semantics: FbSemantics option
    }

    /// Motion configuration
    type Motion = {
        Axes: MotionAxis list
        Groups: MotionGroup list
        FbMapping: FbMapping list
    }
