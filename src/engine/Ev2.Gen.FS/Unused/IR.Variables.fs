namespace Ev2.Gen.IR.Unused

open System

[<AutoOpen>]
module IRVariables =

    /// Library dependency
    type Library = {
        Name: string
        Version: string
        Optional: bool
    }

    /// Variable declaration
    type VarDecl = {
        Name: string
        VarType: string  // Type은 F# 예약어이므로 VarType 사용
        Retain: bool
        Init: InitValue option
    }

    /// Constant declaration
    type ConstDecl = {
        Name: string
        ConstType: string
        Value: obj
    }

    /// Variables collection
    type Variables = {
        Global: VarDecl list
        Constants: ConstDecl list
        Retain: VarDecl list
    }
