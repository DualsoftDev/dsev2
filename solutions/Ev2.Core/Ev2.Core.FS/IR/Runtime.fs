namespace Ev2.Core.FS.IR

open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.CompilerServices
open Dual.Common.Base

[<AutoOpen>]
module private RuntimeHelpers =
    //let isInputVar  (var:IVariable) = var.VarType.IsOneOf(VarType.VarInput, VarType.VarInOut)
    //let isOutputVar (var:IVariable) = var.VarType.IsOneOf(VarType.VarOutput, VarType.VarInOut)
    //let isInOutVar  (var:IVariable) = var.VarType = VarType.VarInOut

    //let isLocalVariable (var:IVariable) = var.VarType.IsOneOf(VarType.Var, VarType.VarConstant)
    //let isParameterVariable (var:IVariable) = var.VarType.IsOneOf(VarType.VarInput, VarType.VarOutput, VarType.VarInOut)

    //let isFBInput (var:IVariable) = var.VarType.IsOneOf(VarType.VarInput, VarType.VarInOut)
    //let isFBOutput (var:IVariable) = var.VarType.IsOneOf(VarType.VarOutput, VarType.VarInOut)
    //let isFBInternal (var:IVariable) = var.VarType.IsOneOf(VarType.Var, VarType.VarConstant)

    //let tryGetInitValue (variable: IVariable) =
    //    match variable with
    //    | :? VarBase<_> as varBase -> varBase.InitValue |> Option.map box
    //    | _ -> None
    ()

