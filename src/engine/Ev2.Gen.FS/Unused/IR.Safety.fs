namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRSafety =

    /// Safety circuit logic type
    type SafetyLogic =
        | Latching
        | NonLatching
        | Custom of string

    /// Safety circuit
    type SafetyCircuit = {
        Name: string
        Inputs: string list  // References to IO channel IDs
        Logic: SafetyLogic
    }

    /// Safety FB IO mapping
    type SafetyIoMap = Map<string, string>  // FB port -> IO channel ID

    /// Safety FB instance
    type SafetyFbInstance = {
        Name: string
        FbType: string  // e.g., "SF_EmergencyStop"
        Params: Map<string, obj> option
        IoMap: SafetyIoMap option
    }

    /// Safety configuration
    type Safety = {
        PL: string option  // Performance Level (e.g., "PL-d")
        SIL: string option  // Safety Integrity Level (e.g., "SIL2")
        Circuits: SafetyCircuit list
        FbInstances: SafetyFbInstance list
    }
