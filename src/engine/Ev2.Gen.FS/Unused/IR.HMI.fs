namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRHMI =

    /// HMI tag read/write mode
    type HmiRwMode =
        | Read
        | Write
        | ReadWrite

    /// HMI tag
    type HmiTag = {
        Name: string
        Var: string  // Reference to variable
        Rw: HmiRwMode
        Format: string option  // e.g., "0.00" for float formatting
    }

    /// HMI screen
    type HmiScreen = {
        Name: string
        Bindings: string list  // References to HmiTag names
    }

    /// HMI configuration
    type HMI = {
        Tags: HmiTag list
        Screens: HmiScreen list
    }

    /// Graphics editor configuration
    type GraphicsEditor = {
        Pou: string  // Reference to POU name
        Language: string  // "FBD", "LD", etc.
        Canvas: Canvas
    }

    /// Graphics configuration
    type Graphics = {
        Editors: GraphicsEditor list
    }
