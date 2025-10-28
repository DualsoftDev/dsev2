namespace Ev2.Gen

open System

[<AutoOpen>]
module IRCommon =

    /// MetaValue - 타입 안전한 메타데이터 값
    type MetaValue =
        | String of string
        | Number of float
        | Boolean of bool
        | Object of Map<string, MetaValue>
        | Array of MetaValue list
        | Null

    /// Position in 2D space (for graphical editors)
    type Position = {
        X: float
        Y: float
    }

    /// Canvas configuration for graphical editors
    type Canvas = {
        Width: float
        Height: float
        Snap: float
        Zoom: float
    }

    /// Network configuration
    type Network = {
        Address: string
        Protocol: string
    }

    /// Reference to a node and port for wiring
    type PortRef = {
        Node: string
        Port: string
    }

    /// Wire connection between two ports
    type Wire = {
        From: PortRef
        To: PortRef
    }

    /// Initial value for variables
    type InitValue =
        | Simple of obj  // 단순 값 (primitive types)
        | Structured of Map<string, InitValue>  // 구조화된 초기값 (UDT 등)
        | ArrayInit of InitValue list
