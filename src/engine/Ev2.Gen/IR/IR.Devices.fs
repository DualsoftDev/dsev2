namespace Ev2.Gen

open System

[<AutoOpen>]
module IRDevices =

    /// Slot/Module in a device
    type Slot = {
        Id: string
        SlotType: string  // "IOModule", "CommunicationModule", etc. (Type은 F# 예약어이므로 SlotType 사용)
        Model: string
        Role: string  // "DriveIF", "DI", "DO", "AI", "AO", etc.
    }

    /// Device (Controller/PLC/Remote IO)
    type Device = {
        Id: string
        DeviceType: string  // "PLC", "RemoteIO", "Drive", etc.
        Vendor: string
        Model: string
        Firmware: string option
        Network: Network option
        Slots: Slot list
    }
