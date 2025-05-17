namespace PLC.CodeGen.Common

open System.Linq

open Dual.Common.Core.FS
open Dual.Ev2

[<AutoOpen>]

module Compatibility =
    /// 기존 코드와의 호환을 위해서 임시로..
    type IExpression with
        member x.CollectStorages() : IStorage list =
            x.EnumerateValueObjects().OfType<IStorage>().ToFSharpList()

    type INamedExpressionizableTerminal = ITerminal
