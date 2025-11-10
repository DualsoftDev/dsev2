namespace Ev2.Cpu.Core

open System

/// Relay hierarchy aligned with RuntimeSpec.md
[<RequireQualifiedAccess>]
type RelayGroup =
    | System
    | Flow
    | Work
    | Call
    | Api
    | Error
    | Timing

/// Primitive relay behaviour classification
[<RequireQualifiedAccess>]
type RelayPrimitive =
    | SrLatch
    | Pulse
    | TimerTon
    | TimerTof
    | CounterCtu
    | CounterCtd
    | State
    | Flag

/// Relay definition distilled from the specification tables
type RelayDefinition =
    {
        Group: RelayGroup
        Key: string
        Tag: DsTag
        Primitive: RelayPrimitive
        Description: string
        DefaultValue: obj
    }

/// Condition descriptor used for documentation and tooling
type ConditionSpec =
    {
        Name: string
        Description: string
    }

/// Formalised relay logic description (SET/RST semantics)
type RelayLogicSpec =
    {
        Relay: RelayDefinition
        SetConditions: ConditionSpec list
        ResetConditions: ConditionSpec list
        Notes: string list
    }

module private Builders =

    let cond name desc = { Name = name; Description = desc }

    let boolRelay group key tag primitive description =
        {
            Group = group
            Key = key
            Tag = DsTag.Create(tag, DsDataType.TBool)
            Primitive = primitive
            Description = description
            DefaultValue = box false
        }

/// Work relay catalogue – mirrors RuntimeSpec.md §10.1
module WorkRelays =

    open Builders

    let startWork =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.SW"
                "work_start"
                RelayPrimitive.SrLatch
                "Start Work relay – triggers the Ready→Going transition."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "∀PrevWork.EW" "All predecessor works have finished."
                    cond "∃ApiDef.PS" "At least one external API provided a start pulse."
                    cond "HMI.ForceStart" "Manual force start command."
                    cond "Work.Aux" "Auxiliary and interlock checks satisfied."
                    cond "Work.OG" "Origin sensor confirmed."
                ]
            ResetConditions =
                [
                    cond "Work.RW" "Work reset request."
                    cond "!Flow.Drive" "Flow control left drive mode."
                ]
            Notes =
                [
                    "Implements SR latch with RST priority."
                ]
        }

    let endWork =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.EW"
                "work_end"
                RelayPrimitive.SrLatch
                "End Work relay – asserted when all calls complete."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "∀Call.EC" "Every call under the work reports completion."
                    cond "Work.Going" "Work is in Going state."
                    cond "!Work.Error" "No work-level error latched."
                ]
            ResetConditions =
                [
                    cond "Work.RW" "Work reset clears completion."
                ]
            Notes =
                [
                    "RST priority ensures reset clears completion even if conditions remain true."
                ]
        }

    let resetWork =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.RW"
                "work_reset"
                RelayPrimitive.Pulse
                "Reset Work relay – drives homing/cleanup cycle."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "Work.ET" "Work reached end-of-task guard."
                    cond "ResetCausal" "Reset request from downstream or manual command."
                ]
            ResetConditions =
                [
                    cond "Work.R" "Reset clears when Ready state regained."
                ]
            Notes =
                [
                    "Runtime implements as pulse (one scan)."
                ]
        }

    let readyState =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.R"
                "work_ready"
                RelayPrimitive.State
                "Ready state – entry condition for new work cycle."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "Work.OG" "Origin verified."
                    cond "!Work.G" "Not currently in Going."
                    cond "!Work.Error" "No error latched."
                ]
            ResetConditions =
                [
                    cond "Work.SW" "Start transition to Going clears Ready."
                ]
            Notes =
                [
                    "Member of one-hot {R,G,F,H} state machine."
                ]
        }

    let goingState =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.G"
                "work_going"
                RelayPrimitive.State
                "Going state – work actively executing."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "Work.SW" "Start trigger."
                    cond "Work.R" "Must depart from Ready."
                ]
            ResetConditions =
                [
                    cond "Work.EW" "Normal completion."
                    cond "Work.RW" "Reset request."
                    cond "Work.Error" "Work-level fault."
                ]
            Notes =
                [
                    "Part of one-hot FSM with Ready/Finish/Homing."
                ]
        }

    let finishState =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.F"
                "work_finish"
                RelayPrimitive.State
                "Finish state – completion acknowledgement before reset."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "Work.EW" "End relay latched."
                    cond "Work.G" "Came from Going."
                ]
            ResetConditions =
                [
                    cond "Work.RW↑" "Reset pulse rising edge."
                ]
            Notes =
                [
                    "One-hot FSM member; cleared by reset edge."
                ]
        }

    let homingState =
        let relay =
            boolRelay
                RelayGroup.Work
                "Work.H"
                "work_homing"
                RelayPrimitive.State
                "Homing state – machine returning to origin."
        relay,
        {
            Relay = relay
            SetConditions =
                [
                    cond "Work.RW" "Reset command initiates homing."
                    cond "Work.F" "Must have completed current work."
                ]
            ResetConditions =
                [
                    cond "Work.OG" "Origin reached."
                    cond "System.Reset" "System-wide reset."
                ]
            Notes =
                [
                    "Completes cycle before re-entering Ready."
                ]
        }

    let originSensor =
        boolRelay
            RelayGroup.Work
            "Work.OG"
            "work_origin"
            RelayPrimitive.Flag
            "Origin guard – indicates homing complete."

    let errorFlag =
        boolRelay
            RelayGroup.Work
            "Work.Error"
            "work_error"
            RelayPrimitive.Flag
            "Work-level error latch."

    let goingGuard =
        boolRelay
            RelayGroup.Work
            "Work.GG"
            "work_g_guard"
            RelayPrimitive.Pulse
            "One-scan delayed Going guard."

    /// Aggregated relay records
    let definitions : RelayDefinition list =
        [
            fst startWork
            fst endWork
            fst resetWork
            fst readyState
            fst goingState
            fst finishState
            fst homingState
            originSensor
            errorFlag
            goingGuard
        ]

    /// Aggregated logic details used for documentation or tooling
    let logic : RelayLogicSpec list =
        [
            snd startWork
            snd endWork
            snd resetWork
            snd readyState
            snd goingState
            snd finishState
            snd homingState
        ]

/// Root catalogue for all relay groups (currently Work populated)
module RuntimeCatalogue =

    let allDefinitions : RelayDefinition list =
        WorkRelays.definitions

    let allLogic : RelayLogicSpec list =
        WorkRelays.logic

    let tryFindByKey key =
        allDefinitions |> List.tryFind (fun d -> StringComparer.Ordinal.Equals(d.Key, key))

    let tryFindByTagName tagName =
        allDefinitions |> List.tryFind (fun d -> StringComparer.Ordinal.Equals(d.Tag.Name, tagName))
