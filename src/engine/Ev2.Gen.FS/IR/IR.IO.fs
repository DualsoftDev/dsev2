namespace Ev2.Gen

open System

[<AutoOpen>]
module IRIO =

    /// IO Direction
    type IODirection =
        | In
        | Out
        | InOut

    /// IO Channel
    type IOChannel = {
        Id: string
        Direction: IODirection
        DataType: string  // "BOOL", "INT", "REAL", etc.
        DeviceRef: string  // Reference to Device.Id or Slot.Id
        ChannelIndex: int
        Label: string option
    }

    /// IO Mapping (Variable to Channel)
    type IOMapping = {
        Variable: string  // e.g., "GVL.StartPB"
        ChannelRef: string  // Reference to IOChannel.Id
    }

    /// IO Configuration
    type IOConfig = {
        Channels: IOChannel list
        Mappings: IOMapping list
    }
